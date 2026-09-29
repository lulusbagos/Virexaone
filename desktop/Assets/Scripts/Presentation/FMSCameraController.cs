using UnityEngine;

namespace Virexa.FMS
{
    public enum CameraFollowMode
    {
        FreeOrbit,          // Standard free orbital pan/zoom/rotate
        OrbitTarget,        // Smooth orbital tracking centered on selected unit
        ChaseBehind,        // Dynamic 3rd-person chase camera behind the unit
        CockpitPOV,         // 1st-person cabin driver cockpit POV
        FrontBumper,        // Action camera on the front bumper / shovel boom
        BirdEyeFollow       // Top-down overhead 90-degree tracking
    }

    public class FMSCameraController : MonoBehaviour
    {
        public static FMSCameraController Instance { get; private set; }

        [Header("Camera Mode & Target")]
        public CameraFollowMode currentFollowMode = CameraFollowMode.FreeOrbit;
        public Transform followTarget;
        public Vector3 pivotPoint = new Vector3(0f, 150f, 0f);

        [Header("Orbit & Rotation Settings")]
        public float orbitSensitivity = 3.5f;
        public float minPitch = 5f;
        public float maxPitch = 85f;
        private float currentYaw = -45f;
        private float currentPitch = 45f;

        [Header("Pan & Movement Settings")]
        public float panSensitivity = 0.8f;
        public float keyboardPanSpeed = 350f;
        public float fastMultiplier = 2.5f;

        [Header("Zoom Settings")]
        public float zoomSensitivity = 150f;
        public float minDistance = 15f;
        public float maxDistance = 8000f;
        private float currentDistance = 2800f;

        [Header("Smooth Damping")]
        public float smoothSpeed = 12f;

        [Header("POV Camera Offsets")]
        public Vector3 haulTruckCockpitOffset = new Vector3(-0.95f, 4.40f, 2.90f);
        public Vector3 excavatorCockpitOffset = new Vector3(-1.15f, 4.60f, 2.20f);
        public Vector3 genericCockpitOffset = new Vector3(0f, 3.80f, 2.50f);
        public Vector3 bumperOffset = new Vector3(0f, 1.8f, 5.2f);
        public Vector3 chaseOffset = new Vector3(0f, 7.5f, -22f);

        private Vector3 targetPivot;
        private float targetYaw;
        private float targetPitch;
        private float targetDistance;

        private Vector3 lastMousePos;
        private bool isPanningWithLeftClick = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            targetPivot = new Vector3(0f, 150f, 0f);
            targetYaw = -45f;
            targetPitch = 45f;
            targetDistance = 2800f;

            currentYaw = targetYaw;
            currentPitch = targetPitch;
            currentDistance = targetDistance;
            pivotPoint = targetPivot;
        }

        private void Start()
        {
            UpdateCameraTransform(true);
        }

        private void LateUpdate()
        {
            // Escape key exits unit-locked POV modes
            if (Input.GetKeyDown(KeyCode.Escape) && currentFollowMode != CameraFollowMode.FreeOrbit)
            {
                ExitUnitCamera();
            }

            if (followTarget != null)
            {
                switch (currentFollowMode)
                {
                    case CameraFollowMode.CockpitPOV:
                        UpdateCockpitCamera();
                        return;

                    case CameraFollowMode.FrontBumper:
                        UpdateFrontBumperCamera();
                        return;

                    case CameraFollowMode.ChaseBehind:
                        UpdateChaseCamera();
                        return;

                    case CameraFollowMode.BirdEyeFollow:
                        targetPivot = followTarget.position + Vector3.up * 2f;
                        targetPitch = 88f;
                        targetDistance = Mathf.Clamp(targetDistance, 100f, 600f);
                        break;

                    case CameraFollowMode.OrbitTarget:
                    default:
                        targetPivot = followTarget.position + Vector3.up * 2.5f;
                        break;
                }
            }

            HandleKeyboardInput();
            HandleMouseInput();
            UpdateCameraTransform(false);
        }

        private void UpdateCockpitCamera()
        {
            if (followTarget == null) return;
            float dt = Time.unscaledDeltaTime;

            Vector3 offset = genericCockpitOffset;
            FMSUnitController unit = followTarget.GetComponent<FMSUnitController>();
            if (unit != null)
            {
                if (unit.unitType == UnitType.HaulTruck) offset = haulTruckCockpitOffset;
                else if (unit.unitType == UnitType.Excavator) offset = excavatorCockpitOffset;
            }

            Vector3 worldPos = followTarget.TransformPoint(offset);
            Quaternion targetRot = followTarget.rotation;

            // Allow subtle mouse look inside cockpit
            if (Input.GetMouseButton(1))
            {
                targetYaw += Input.GetAxis("Mouse X") * orbitSensitivity * 20f * dt;
                targetPitch -= Input.GetAxis("Mouse Y") * orbitSensitivity * 20f * dt;
                targetPitch = Mathf.Clamp(targetPitch, -30f, 40f);
                targetYaw = Mathf.Clamp(targetYaw, -85f, 85f);
            }
            else
            {
                targetYaw = Mathf.Lerp(targetYaw, 0f, dt * 4f);
                targetPitch = Mathf.Lerp(targetPitch, 4f, dt * 4f);
            }

            Quaternion lookRot = targetRot * Quaternion.Euler(targetPitch, targetYaw, 0f);

            transform.position = Vector3.Lerp(transform.position, worldPos, dt * 18f);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, dt * 18f);
        }

        private void UpdateFrontBumperCamera()
        {
            if (followTarget == null) return;
            float dt = Time.unscaledDeltaTime;

            Vector3 worldPos = followTarget.TransformPoint(bumperOffset);
            Quaternion targetRot = followTarget.rotation;

            transform.position = Vector3.Lerp(transform.position, worldPos, dt * 16f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 16f);
        }

        private void UpdateChaseCamera()
        {
            if (followTarget == null) return;
            float dt = Time.unscaledDeltaTime;

            Vector3 wantedPos = followTarget.position + followTarget.TransformDirection(chaseOffset);
            Vector3 lookTarget = followTarget.position + Vector3.up * 3.5f;

            transform.position = Vector3.Lerp(transform.position, wantedPos, dt * 10f);
            Quaternion targetRot = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 12f);
        }

        private void HandleKeyboardInput()
        {
            if (FMSDashboardUI.Instance != null && FMSDashboardUI.Instance.IsMapEditorOpen) return;
            float dt = Time.unscaledDeltaTime;
            float speed = keyboardPanSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);

            Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 rightFlat = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += forwardFlat;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move -= forwardFlat;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += rightFlat;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move -= rightFlat;

            if (move.sqrMagnitude > 0.001f)
            {
                if (currentFollowMode != CameraFollowMode.FreeOrbit)
                {
                    ExitUnitCamera();
                }
                followTarget = null;
                targetPivot += move.normalized * speed * dt;
                ClampPivotBounds();
            }

            // Reset view to center on 'F' key or 'Space'
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space))
            {
                ExitUnitCamera();
                targetPivot = new Vector3(0f, 150f, 0f);
                targetDistance = 2800f;
                targetPitch = 45f;
                targetYaw = -45f;
            }
        }

        private void HandleMouseInput()
        {
            // Block 3D camera pan, orbit, and zoom when mouse is over UI navigation bar or modals
            if (FMSDashboardUI.Instance != null && FMSDashboardUI.Instance.IsPointerOverUI())
            {
                isPanningWithLeftClick = false;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            lastMousePos = Input.mousePosition;

            // 1. RIGHT CLICK DRAG: Orbit / Rotate 3D Camera around Pivot
            if (Input.GetMouseButton(1))
            {
                targetYaw += Input.GetAxis("Mouse X") * orbitSensitivity * 40f * dt;
                targetPitch -= Input.GetAxis("Mouse Y") * orbitSensitivity * 40f * dt;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            }

            bool isMeasuring = FMSMeasureTool.Instance != null && FMSMeasureTool.Instance.isToolActive;

            // 2. MIDDLE CLICK OR LEFT CLICK DRAG: Pan / Move Map
            if (Input.GetMouseButton(2) || (Input.GetMouseButton(0) && isPanningWithLeftClick && !isMeasuring))
            {
                if (currentFollowMode != CameraFollowMode.FreeOrbit && currentFollowMode != CameraFollowMode.OrbitTarget)
                {
                    ExitUnitCamera();
                }

                followTarget = null;
                float distFactor = targetDistance / 600f;
                Vector3 rightFlat = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                Vector3 forwardFlat = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

                float moveX = -Input.GetAxis("Mouse X") * panSensitivity * 15f * distFactor;
                float moveZ = -Input.GetAxis("Mouse Y") * panSensitivity * 15f * distFactor;

                targetPivot += (rightFlat * moveX) + (forwardFlat * moveZ);
                ClampPivotBounds();
            }

            // Detect start of Left Click Drag
            if (Input.GetMouseButtonDown(0))
            {
                if (!isMeasuring)
                {
                    isPanningWithLeftClick = true;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                isPanningWithLeftClick = false;
            }

            // 3. MOUSE SCROLL WHEEL: Zoom In / Zoom Out
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                float zoomDelta = scroll * zoomSensitivity * (targetDistance * 0.015f);
                targetDistance = Mathf.Clamp(targetDistance - zoomDelta, minDistance, maxDistance);

                // Zoom slightly towards ground cursor point
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit groundHit, 15000f))
                {
                    targetPivot = Vector3.Lerp(targetPivot, groundHit.point, Mathf.Abs(scroll) * 0.35f);
                    ClampPivotBounds();
                }
            }
        }

        private void UpdateCameraTransform(bool instant)
        {
            float dt = Time.unscaledDeltaTime;

            if (instant)
            {
                currentYaw = targetYaw;
                currentPitch = targetPitch;
                currentDistance = targetDistance;
                pivotPoint = targetPivot;
            }
            else
            {
                currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, dt * smoothSpeed);
                currentPitch = Mathf.Lerp(currentPitch, targetPitch, dt * smoothSpeed);
                currentDistance = Mathf.Lerp(currentDistance, targetDistance, dt * smoothSpeed);
                pivotPoint = Vector3.Lerp(pivotPoint, targetPivot, dt * smoothSpeed);
            }

            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);

            transform.position = pivotPoint + offset;
            transform.rotation = rotation;
        }

        private void ClampPivotBounds()
        {
            targetPivot.x = Mathf.Clamp(targetPivot.x, -3500f, 3500f);
            targetPivot.z = Mathf.Clamp(targetPivot.z, -3000f, 3000f);
            targetPivot.y = Mathf.Clamp(targetPivot.y, 50f, 500f);
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            if (target != null)
            {
                currentFollowMode = CameraFollowMode.OrbitTarget;
                targetPivot = target.position;
                targetDistance = Mathf.Clamp(targetDistance, 45f, 220f);
            }
            else
            {
                currentFollowMode = CameraFollowMode.FreeOrbit;
            }
        }

        public void SetFollowMode(CameraFollowMode mode, Transform target = null)
        {
            if (target != null) followTarget = target;
            currentFollowMode = mode;
            targetYaw = 0f;
            targetPitch = 0f;

            if (mode == CameraFollowMode.OrbitTarget && followTarget != null)
            {
                targetPivot = followTarget.position;
                targetDistance = Mathf.Clamp(targetDistance, 45f, 220f);
                targetPitch = 35f;
            }
            else if (mode == CameraFollowMode.BirdEyeFollow && followTarget != null)
            {
                targetPivot = followTarget.position;
                targetPitch = 88f;
                targetDistance = 250f;
            }
        }

        public void ExitUnitCamera()
        {
            currentFollowMode = CameraFollowMode.FreeOrbit;
            followTarget = null;
            targetPitch = 45f;
            targetDistance = Mathf.Clamp(targetDistance, 200f, 2500f);
            if (FMSFleetManager.Instance != null)
            {
                FMSFleetManager.Instance.DeselectUnit();
            }
        }

        public bool IsInUnitCameraMode => currentFollowMode != CameraFollowMode.FreeOrbit && followTarget != null;
        public string ActiveCameraModeLabel => currentFollowMode switch
        {
            CameraFollowMode.CockpitPOV => "🪟 Kamera Kabin Supir (Cockpit POV)",
            CameraFollowMode.ChaseBehind => "🏎️ Kamera Chase (Belakang Unit)",
            CameraFollowMode.FrontBumper => "🎥 Kamera Bumper Depan (Action POV)",
            CameraFollowMode.BirdEyeFollow => "🦅 Kamera Bird-Eye (Atas Unit)",
            CameraFollowMode.OrbitTarget => "🎯 Kamera Orbit Fokus Unit",
            _ => "🌐 Kamera Bebas"
        };

        public float CurrentYaw => currentYaw;
        public float TargetYaw => targetYaw;

        public void ResetHeadingToNorth()
        {
            targetYaw = 0f;
        }

        public void SetHeading(float yawDeg)
        {
            targetYaw = yawDeg;
        }

        public void SetTopDownView()
        {
            followTarget = null;
            currentFollowMode = CameraFollowMode.FreeOrbit;
            targetPitch = 88f;
            targetYaw = 0f;
        }

        public void SetIsometricView()
        {
            followTarget = null;
            currentFollowMode = CameraFollowMode.FreeOrbit;
            targetPitch = 45f;
            targetYaw = -45f;
            targetDistance = Mathf.Clamp(targetDistance, 1500f, 3200f);
        }

        public void JumpTo(Vector3 worldPos, float distance = 600f)
        {
            followTarget = null;
            currentFollowMode = CameraFollowMode.FreeOrbit;
            targetPivot = worldPos;
            targetDistance = distance;
            ClampPivotBounds();
        }
    }
}
