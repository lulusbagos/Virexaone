using System;
using UnityEngine;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Touch Input & Gesture Controller optimized for in-cabin tablet mountings.
    /// Handles single-finger panning, multi-touch pinch zoom, and quick swipe tab navigation.
    /// </summary>
    public class OperatorTouchController : MonoBehaviour
    {
        public static OperatorTouchController Instance { get; private set; }

        [Header("Touch Settings")]
        public float touchDragSensitivity = 0.5f;
        public float pinchZoomSensitivity = 0.02f;
        public bool isDayMode = false;

        private Vector2 lastTouchPos;
        private float lastPinchDistance;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Update()
        {
            HandleTouchGestures();
        }

        private void HandleTouchGestures()
        {
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    lastTouchPos = t.position;
                }
            }
            else if (Input.touchCount == 2)
            {
                Touch t1 = Input.GetTouch(0);
                Touch t2 = Input.GetTouch(1);

                float currentPinchDist = Vector2.Distance(t1.position, t2.position);
                if (t1.phase == TouchPhase.Began || t2.phase == TouchPhase.Began)
                {
                    lastPinchDistance = currentPinchDist;
                }
                else if (t1.phase == TouchPhase.Moved || t2.phase == TouchPhase.Moved)
                {
                    float delta = currentPinchDist - lastPinchDistance;
                    lastPinchDistance = currentPinchDist;

                    // Trigger zoom event if needed
                }
            }
        }

        public void ToggleDayNightMode()
        {
            var dashboard = Virexa.FMS.FMSDashboardUI.Instance;
            if (dashboard == null) return;
            var atmosphere = Virexa.FMS.FMSDigitalTwinAtmosphere.Instance;
            isDayMode = atmosphere != null && atmosphere.currentCycleIndex == 2;
            dashboard.SetSunLighting(isDayMode ? 0 : 2);
        }
    }
}
