using UnityEngine;

namespace DuvalDrive
{
    public enum LookTarget { LeftMirror, RearMirror, RightMirror, LeftBlindSpot, RightBlindSpot }

    /// <summary>
    /// Driver's head: mouse look, quick glances at the mirrors, and a record of
    /// when the driver last looked at each mirror / over each shoulder.
    /// Sits on a pivot at the driver's eye position; the camera is its child.
    /// </summary>
    public class DriverHeadLook : MonoBehaviour
    {
        public Transform leftMirror;
        public Transform rearMirror;
        public Transform rightMirror;

        public float sensitivity = 2.2f;
        public float glanceSpeed = 10f;
        public float recenterDelay = 1.5f;
        public float recenterSpeed = 2.5f;
        public float minYaw = -150f, maxYaw = 150f;
        public float minPitch = -40f, maxPitch = 35f;
        public float mirrorLookAngle = 13f;
        public float blindSpotYaw = 100f;

        public static DriverHeadLook Instance { get; private set; }

        readonly float[] lastLook = { -999f, -999f, -999f, -999f, -999f };
        float yaw, pitch, idle;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            SetCursorLocked(true);
        }

        public bool LookedAt(LookTarget t, float withinSeconds)
        {
            return Time.time - lastLook[(int)t] <= withinSeconds;
        }

        public bool CursorLocked { get { return Cursor.lockState == CursorLockMode.Locked; } }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (DriverInput.EscapePressed()) SetCursorLocked(false);
            else if (DriverInput.ClickPressed()) SetCursorLocked(true);

            Transform glance = null;
            if (DriverInput.Held(Control.LookLeftMirror)) glance = leftMirror;
            else if (DriverInput.Held(Control.LookRearMirror)) glance = rearMirror;
            else if (DriverInput.Held(Control.LookRightMirror)) glance = rightMirror;

            float dt = Time.deltaTime;
            if (glance != null)
            {
                float ty, tp;
                AnglesTo(glance.position, out ty, out tp);
                yaw = Mathf.LerpAngle(yaw, ty, dt * glanceSpeed);
                pitch = Mathf.LerpAngle(pitch, tp, dt * glanceSpeed);
                idle = 0f;
            }
            else
            {
                Vector2 md = CursorLocked ? DriverInput.MouseDelta() * sensitivity : Vector2.zero;
                if (md.sqrMagnitude > 0.0001f)
                {
                    yaw = Mathf.Clamp(yaw + md.x, minYaw, maxYaw);
                    pitch = Mathf.Clamp(pitch - md.y, minPitch, maxPitch);
                    idle = 0f;
                }
                else
                {
                    idle += dt;
                    if (idle > recenterDelay)
                    {
                        yaw = Mathf.Lerp(yaw, 0f, dt * recenterSpeed);
                        pitch = Mathf.Lerp(pitch, 0f, dt * recenterSpeed);
                    }
                }
            }

            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            TrackObservation();
        }

        void AnglesTo(Vector3 worldPoint, out float targetYaw, out float targetPitch)
        {
            Transform frame = transform.parent != null ? transform.parent : transform;
            Vector3 d = frame.InverseTransformDirection(worldPoint - transform.position);
            targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            targetPitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }

        void TrackObservation()
        {
            Vector3 fwd = transform.forward;
            Mark(LookTarget.LeftMirror, leftMirror, fwd);
            Mark(LookTarget.RearMirror, rearMirror, fwd);
            Mark(LookTarget.RightMirror, rightMirror, fwd);
            if (yaw < -blindSpotYaw) lastLook[(int)LookTarget.LeftBlindSpot] = Time.time;
            if (yaw > blindSpotYaw) lastLook[(int)LookTarget.RightBlindSpot] = Time.time;
        }

        void Mark(LookTarget t, Transform mirror, Vector3 fwd)
        {
            if (mirror == null) return;
            if (Vector3.Angle(fwd, mirror.position - transform.position) < mirrorLookAngle)
                lastLook[(int)t] = Time.time;
        }
    }
}
