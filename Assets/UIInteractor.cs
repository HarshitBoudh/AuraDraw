using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIInteractor : MonoBehaviour
{
    public UDPFingerReceiver fingerReceiver;
    public Camera uiCamera;

    [Header("Wall Mode — Dwell Click")]
    [Tooltip("Hover over a button this long to click it (seconds). 0 = pinch only.")]
    public float dwellTime = 1.2f;
    [Tooltip("Show a dwell progress indicator on the cursor")]
    public Image dwellIndicator;

    private bool wasPinchingLastFrame = false;
    private GameObject dwellTarget    = null;
    private float dwellTimer          = 0f;

    void Update()
    {
        if (fingerReceiver == null) return;

        Vector2 fingerNorm   = fingerReceiver.FingerPos;
        float screenX        = fingerNorm.x * Screen.width;
        float screenY        = (1f - fingerNorm.y) * Screen.height;
        Vector2 fingerScreen = new Vector2(screenX, screenY);

        bool isPinchingNow = fingerReceiver.IsPinching;
        bool pinchBegan    = isPinchingNow && !wasPinchingLastFrame;

        GameObject uiHit = GetUIElementAtScreenPosition(fingerScreen);

        // ── Pinch click (instant) ─────────────────────────────────
        if (pinchBegan && uiHit != null)
        {
            TryClickButton(uiHit);
            ResetDwell();
        }

        // ── Dwell click (hover to click — great for wall projection) ─
        if (dwellTime > 0f && uiHit != null)
        {
            Button btn = uiHit.GetComponent<Button>();
            if (btn != null)
            {
                if (uiHit == dwellTarget)
                {
                    dwellTimer += Time.deltaTime;

                    if (dwellIndicator != null)
                        dwellIndicator.fillAmount = dwellTimer / dwellTime;

                    if (dwellTimer >= dwellTime)
                    {
                        TryClickButton(uiHit);
                        ResetDwell();
                    }
                }
                else
                {
                    // Moved to new button — restart dwell
                    dwellTarget = uiHit;
                    dwellTimer  = 0f;
                }
            }
            else
            {
                ResetDwell();
            }
        }
        else if (uiHit == null)
        {
            ResetDwell();
        }

        wasPinchingLastFrame = isPinchingNow;
    }

    void TryClickButton(GameObject obj)
    {
        Button btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.Invoke();
            Debug.Log("Clicked: " + obj.name);
        }
    }

    void ResetDwell()
    {
        dwellTarget = null;
        dwellTimer  = 0f;
        if (dwellIndicator != null)
            dwellIndicator.fillAmount = 0f;
    }

    GameObject GetUIElementAtScreenPosition(Vector2 screenPos)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        return results.Count > 0 ? results[0].gameObject : null;
    }
}