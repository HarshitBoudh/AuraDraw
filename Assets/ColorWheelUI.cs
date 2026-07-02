using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ColorWheelUI : MonoBehaviour
{
    [Header("References")]
    public UDPFingerReceiver fingerReceiver;  // For finger coords + pinch bool
    public TexturePainter texturePainter;     // So we can set the chosen color
    public Image colorWheelImage;             // The UI Image with the color wheel sprite
    public Camera uiCamera;                  // If using World Space or Screen Space - Camera

    [Header("Position & Fade Settings")]
    public float hoverRegionWidthRatio = 0.2f;   // e.g. bottom-right 20% of the screen width
    public float hoverRegionHeightRatio = 0.2f;  // e.g. bottom-right 20% of the screen height
    public float fadeSpeed = 5f;                 // how fast the wheel fades in/out

    // We store whether we consumed the pinch in this frame
    // so the TexturePainter can skip toggling eraser
    public bool PinchConsumedThisFrame { get; private set; }

    private CanvasGroup canvasGroup;
    private bool wheelVisible = false;
    private bool pickingColor = false;

    private bool wasPinchingLastFrame = false;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        wheelVisible = false;
    }

    void Update()
    {
        PinchConsumedThisFrame = false; // reset each frame
        if (fingerReceiver == null || colorWheelImage == null) return;

        // Convert to screen coords
        Vector2 fingerNorm = fingerReceiver.FingerPos;
        float screenX = fingerNorm.x * Screen.width;
        float screenY = (1f - fingerNorm.y) * Screen.height;
        Vector2 fingerScreenPos = new Vector2(screenX, screenY);

        bool isPinching = fingerReceiver.IsPinching;
        bool pinchBegan = (isPinching && !wasPinchingLastFrame);

        // 1) Check if finger is in the "bottom-right" region to fade in
        bool inBottomRightRegion = 
            (screenX > (1f - hoverRegionWidthRatio) * Screen.width) &&
            (screenY < hoverRegionHeightRatio * Screen.height);

        if (inBottomRightRegion)
        {
            wheelVisible = true;  // fade in
        }
        else
        {
            // hide if not inside wheel
            if (!pickingColor) 
                wheelVisible = false;
        }

        // 2) Fade in/out
        float targetAlpha = wheelVisible ? 1f : 0f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        // 3) If visible, see if fingertip is inside the color wheel
        if (canvasGroup.alpha > 0.01f)
        {
            RectTransform wheelRect = colorWheelImage.rectTransform;
            Vector2 localPoint;
            // IMPORTANT: pass uiCamera for WorldSpace or ScreenSpace-Camera
            bool insideWheel = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                wheelRect, fingerScreenPos, uiCamera, out localPoint);

            if (insideWheel)
            {
                float halfW = wheelRect.rect.width * 0.5f;
                float halfH = wheelRect.rect.height * 0.5f;
                float cx = localPoint.x + halfW;
                float cy = localPoint.y + halfH;

                // Check if inside sprite rect
                if (cx >= 0 && cx < wheelRect.rect.width && cy >= 0 && cy < wheelRect.rect.height)
                {
                    pickingColor = true;

                    // If pinch just began here, pick color
                    if (pinchBegan)
                    {
                        Color picked = GetColorFromWheel((int)cx, (int)cy);
                        // If alpha is significant => valid color
                        if (picked.a > 0.9f)
                        {
                            // Apply color to painter
                            if (texturePainter) 
                                texturePainter.SetBrushColor(picked);
                        }
                        PinchConsumedThisFrame = true;
                        // fade out after picking color
                        wheelVisible = false;
                        pickingColor = false;
                    }
                }
                else
                {
                    pickingColor = false;
                }
            }
            else
            {
                pickingColor = false;
            }
        }
        else
        {
            pickingColor = false;
        }

        wasPinchingLastFrame = isPinching;
    }

    private Color GetColorFromWheel(int px, int py)
    {
        Texture2D tex2D = colorWheelImage.sprite.texture;
        Rect spriteRect = colorWheelImage.sprite.rect;

        int texX = (int)(spriteRect.x + px);
        int texY = (int)(spriteRect.y + py);

        texX = Mathf.Clamp(texX, 0, tex2D.width - 1);
        texY = Mathf.Clamp(texY, 0, tex2D.height - 1);

        return tex2D.GetPixel(texX, texY);
    }
}
