using UnityEngine;
using UnityEngine.UI;

public class PointerCursorController : MonoBehaviour
{
    [Header("References")]
    public UDPFingerReceiver fingerReceiver;
    public TexturePainter texturePainter;

    [Header("Settings")]
    public float smoothSpeed = 12f;
    public float eraserScaleMultiplier = 2.4f;   // How big the eraser pointer is

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Image pointerImage;

    private Vector2 smoothPos;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        pointerImage = GetComponent<Image>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void Update()
    {
        if (fingerReceiver == null) return;

        Vector2 finger = fingerReceiver.FingerPos;
        bool fingerDetected = finger != Vector2.zero;
        bool isErasing = texturePainter != null ? texturePainter.IsErasing : false;

        // Update Position smoothly
        if (fingerDetected)
        {
            Vector2 target = new Vector2(
                finger.x * Screen.width,
                (1f - finger.y) * Screen.height
            );

            smoothPos = Vector2.Lerp(smoothPos, target, Time.deltaTime * smoothSpeed);
            rectTransform.position = smoothPos;
        }

        // === Show Pointer Logic ===
        if (fingerDetected)
        {
            // Always show pointer when finger is moving
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, Time.deltaTime * 12f);

            if (isErasing && pointerImage != null)
            {
                // ERASER MODE - Very visible
                pointerImage.color = new Color(1f, 0.15f, 0.15f, 0.95f);   // Bright Red
                rectTransform.localScale = Vector3.one * eraserScaleMultiplier;
            }
            else if (pointerImage != null)
            {
                // Normal Drawing Mode
                pointerImage.color = new Color(1f, 1f, 1f, 0.85f);
                rectTransform.localScale = Vector3.one * 1.1f;
            }
        }
        else
        {
            // Hide when no finger detected
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, Time.deltaTime * 8f);
        }
    }
}