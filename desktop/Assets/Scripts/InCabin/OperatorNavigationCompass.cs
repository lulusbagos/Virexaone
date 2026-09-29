using System;
using System.Collections.Generic;
using UnityEngine;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Computes real-time 3D directional vector, compass bearing angle, distance, 
    /// slope grade, and ETA from the Dump Truck's current position to its assigned Excavator / Disposal.
    /// </summary>
    public class OperatorNavigationCompass : MonoBehaviour
    {
        public static OperatorNavigationCompass Instance { get; private set; }

        [Header("Target Location Telemetry")]
        public string targetName = "EX-201 (Pit North Face)";
        public Vector3 targetPosition = Vector3.zero;
        public bool hasValidTarget = false;

        [Header("Real-Time Navigation Output")]
        public float relativeBearingDegrees = 0f;  // Angle (-180 to +180) relative to truck forward heading
        public float absoluteCompassBearing = 0f;   // 0 = North, 90 = East, 180 = South, 270 = West
        public float distanceToTargetMeters = 0f;   // Distance in meters
        public float elevationDeltaMeters = 0f;    // Elevation difference (+ = uphill, - = downhill)
        public float roadGradePercent = 0f;        // Slope percentage (%)
        public float estimatedTimeArrivalSeconds = 0f; // ETA

        [Header("Compass Smoothing")]
        [Range(1f, 20f)] public float smoothSpeed = 8.0f;
        private float smoothedBearing = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Update()
        {
            UpdateNavigationVectors();
        }

        /// <summary>
        /// Updates bearing, distance, and slope to the active target
        /// </summary>
        public void UpdateNavigationVectors()
        {
            if (!hasValidTarget) return;

            Vector3 currentPos = transform.position;
            Vector3 diff = targetPosition - currentPos;
            
            // Horizontal 2D distance and elevation delta
            Vector3 diffHorizontal = new Vector3(diff.x, 0f, diff.z);
            distanceToTargetMeters = diffHorizontal.magnitude;
            elevationDeltaMeters = diff.y;

            // Slope / Grade calculation: (deltaY / distanceHorizontal) * 100%
            if (distanceToTargetMeters > 5f)
            {
                roadGradePercent = Mathf.Clamp((elevationDeltaMeters / distanceToTargetMeters) * 100f, -25f, 25f);
            }
            else
            {
                roadGradePercent = 0f;
            }

            // Absolute compass bearing (0 = North/Vector3.forward)
            absoluteCompassBearing = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
            if (absoluteCompassBearing < 0) absoluteCompassBearing += 360f;

            // Relative bearing to current truck heading (Forward = 0 deg, Right = +90 deg, Left = -90 deg)
            Vector3 forwardH = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            if (forwardH.sqrMagnitude > 0.001f && diffHorizontal.sqrMagnitude > 0.001f)
            {
                Vector3 targetDir = diffHorizontal.normalized;
                float angle = Vector3.SignedAngle(forwardH, targetDir, Vector3.up);
                smoothedBearing = Mathf.LerpAngle(smoothedBearing, angle, Time.deltaTime * smoothSpeed);
                relativeBearingDegrees = smoothedBearing;
            }

            // Estimated Time of Arrival (ETA) calculation
            float currentSpeedMs = 8.33f; // Default 30 km/h = 8.33 m/s
            if (OperatorInCabinUI.Instance != null && OperatorInCabinUI.Instance.currentSpeedKmh > 3f)
            {
                currentSpeedMs = OperatorInCabinUI.Instance.currentSpeedKmh / 3.6f;
            }
            estimatedTimeArrivalSeconds = currentSpeedMs > 0.5f ? (distanceToTargetMeters / currentSpeedMs) : 0f;
        }

        /// <summary>
        /// Sets navigation target based on destination category (Excavator / Pit vs Disposal / Hopper)
        /// </summary>
        public void SetTarget(string name, Vector3 worldPos)
        {
            targetName = name;
            targetPosition = worldPos;
            hasValidTarget = true;
        }

        /// <summary>
        /// Clear target
        /// </summary>
        public void ClearTarget()
        {
            hasValidTarget = false;
            targetName = "Menunggu Penugasan Dispatch...";
            relativeBearingDegrees = 0f;
            distanceToTargetMeters = 0f;
        }
    }
}
