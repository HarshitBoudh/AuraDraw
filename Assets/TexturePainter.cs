using UnityEngine;

public class TexturePainter : MonoBehaviour
{
    [Header("References")]
    public UDPFingerReceiver fingerReceiver;
    public ColorWheelUI colorWheelUI;
    public Renderer quadRenderer;

    [Header("Texture Settings")]
    public int textureWidth = 1024;
    public int textureHeight = 1024;
    public Color backgroundColor = Color.white;
    public Color brushColor = Color.black;

    [Header("Brush Settings")]
    public int baseBrushSize = 16;
    public int eraserMultiplier = 5;

    [Header("Depth Brush Scaling")]
    [Tooltip("Must match PRESSURE_MIN in Python")]
    public float pressureMin = 0.4f;
    [Tooltip("Must match PRESSURE_MAX in Python")]
    public float pressureMax = 3.0f;
    public int minBrushSize = 4;
    public int maxBrushSize = 90;
    [Tooltip("Higher = snappier. Lower = smoother/slower.")]
    public float brushSmoothing = 10f;

    [Header("Drawing Quality")]
    [Range(1, 8)]
    public int pointsPerFrame = 4;
    public float minDrawDistance = 0.8f;

    private Texture2D drawTexture;
    private Vector2 previousUV = Vector2.zero;
    private bool wasTwoFingersUpLastFrame = false;
    private bool isErasing = false;

    // Smoothed brush size — interpolates every frame for buttery transitions
    private float smoothedBrushSize = 16f;

    void Start()
    {
        drawTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        ClearTexture();

        if (quadRenderer != null)
            quadRenderer.material.mainTexture = drawTexture;

        smoothedBrushSize = baseBrushSize;
        Debug.Log("✅ TexturePainter Started");
    }

    void Update()
    {
        if (fingerReceiver == null) return;

        bool twoFingersUpNow = fingerReceiver.IsPinching;
        Vector2 currentUV = fingerReceiver.FingerPos;

        // Toggle Eraser
        if (twoFingersUpNow && !wasTwoFingersUpLastFrame)
        {
            if (!colorWheelUI || !colorWheelUI.PinchConsumedThisFrame)
            {
                isErasing = !isErasing;
                Debug.Log("Eraser Mode: " + isErasing);
            }
        }

        // === CONTINUOUS BRUSH SIZE UPDATE (runs every frame, even when not drawing) ===
        // This is what makes it feel "burst-like" — size updates live as hand moves
        float targetSize;
        if (isErasing)
        {
            targetSize = baseBrushSize * eraserMultiplier;
        }
        else
        {
            float pressure = fingerReceiver.Pressure;

            // Map Python pressure range [0.4 → 3.0] to brush size [4 → 90]
            float t = Mathf.InverseLerp(pressureMin, pressureMax, pressure);
            targetSize = Mathf.Lerp(minBrushSize, maxBrushSize, t);
        }

        // Smooth the brush size every frame — this is the key to smooth transitions
        smoothedBrushSize = Mathf.Lerp(smoothedBrushSize, targetSize, Time.deltaTime * brushSmoothing);

        // DRAW / ERASE
        if (currentUV != Vector2.zero)
        {
            Color paintColor = isErasing ? backgroundColor : brushColor;
            int currentSize = Mathf.RoundToInt(smoothedBrushSize);
            currentSize = Mathf.Clamp(currentSize, minBrushSize, maxBrushSize);

            DrawSmoothStroke(previousUV, currentUV, paintColor, currentSize);
        }

        wasTwoFingersUpLastFrame = twoFingersUpNow;
        previousUV = currentUV;
    }

    private void DrawSmoothStroke(Vector2 prevUV, Vector2 currUV, Color color, int brushSize)
    {
        if (prevUV == Vector2.zero)
        {
            DrawCircleStamp(currUV, color, brushSize);
            drawTexture.Apply();
            return;
        }

        Vector2 prevPixel = new Vector2(prevUV.x * textureWidth, (1f - prevUV.y) * textureHeight);
        Vector2 currPixel = new Vector2(currUV.x * textureWidth, (1f - currUV.y) * textureHeight);
        float distance = Vector2.Distance(prevPixel, currPixel);

        if (distance < minDrawDistance)
        {
            DrawCircleStamp(currUV, color, brushSize);
            drawTexture.Apply();
            return;
        }

        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / pointsPerFrame));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 interpolatedUV = Vector2.Lerp(prevUV, currUV, t);
            DrawCircleStamp(interpolatedUV, color, brushSize);
        }

        drawTexture.Apply();
    }

    private void DrawCircleStamp(Vector2 uv, Color color, int radius)
    {
        int centerX = (int)(uv.x * textureWidth);
        int centerY = (int)((1f - uv.y) * textureHeight);
        int rSquared = radius * radius;

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > rSquared) continue;
                int px = centerX + x;
                int py = centerY + y;
                if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    drawTexture.SetPixel(px, py, color);
            }
        }
    }

    public void ClearTexture()
    {
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;
        drawTexture.SetPixels(pixels);
        drawTexture.Apply();
    }

    public void SetBrushColor(Color c)
    {
        brushColor = c;
        isErasing = false;
    }

    public bool IsErasing => isErasing;
}