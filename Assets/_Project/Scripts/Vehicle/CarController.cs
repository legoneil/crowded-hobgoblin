using UnityEngine;

namespace DuvalDrive
{
    public enum Gear { Drive, Reverse }

    /// <summary>
    /// Front-wheel-drive automatic car built on Unity's WheelColliders.
    /// Exposes the state the rules engine needs (speed, gear, brake, steering).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public const float MpsToMph = 2.23694f;

        [Header("Wheels")]
        public WheelCollider frontLeft;
        public WheelCollider frontRight;
        public WheelCollider rearLeft;
        public WheelCollider rearRight;
        public Transform frontLeftVisual;
        public Transform frontRightVisual;
        public Transform rearLeftVisual;
        public Transform rearRightVisual;

        [Header("Parts")]
        public BoxCollider bodyCollider;
        public Transform steeringWheel;

        [Header("Engine & brakes")]
        public float maxMotorTorque = 850f;      // per driven (front) wheel
        public float creepTorque = 110f;         // automatic transmission idle creep
        public float creepSpeedMph = 5f;
        public float maxBrakeTorque = 2600f;
        public float parkingBrakeTorque = 5000f;
        public float coastBrakeTorque = 40f;
        public float topSpeedMph = 85f;
        public float reverseTopSpeedMph = 12f;

        [Header("Steering")]
        public float maxSteerAngle = 35f;
        public float highSpeedSteerAngle = 9f;
        public float steerRate = 1.8f;           // input units per second
        public float steerReturnRate = 3f;
        public float steeringWheelDegrees = 450f; // wheel rotation at full lock
        public Vector3 centerOfMass = new Vector3(0f, 0.45f, 0.1f);

        public static CarController Player { get; private set; }

        public Gear CurrentGear { get; private set; }
        public bool ParkingBrake { get; private set; }
        /// <summary>Signed speed along the car's forward axis (m/s).</summary>
        public float ForwardSpeed { get; private set; }
        public float SpeedMph { get { return Mathf.Abs(ForwardSpeed) * MpsToMph; } }
        public float Steer { get; private set; }
        public float Throttle { get; private set; }
        public float BrakeInput { get; private set; }
        public BoxCollider BodyCollider { get { return bodyCollider; } }
        public bool IsBraking { get { return BrakeInput > 0.05f; } }

        Rigidbody rb;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        bool resetRequested;
        Quaternion steeringWheelBase;
        float parkingBrakeNagAt;

        public event System.Action OnReset;

        void Awake()
        {
            Player = this;
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMass;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            CurrentGear = Gear.Drive;
            ParkingBrake = true;
            if (steeringWheel != null) steeringWheelBase = steeringWheel.localRotation;
        }

        void Update()
        {
            Throttle = DriverInput.Held(Control.Throttle) ? 1f : 0f;
            BrakeInput = DriverInput.Held(Control.Brake) ? 1f : 0f;

            float target = 0f;
            if (DriverInput.Held(Control.SteerLeft)) target -= 1f;
            if (DriverInput.Held(Control.SteerRight)) target += 1f;
            float rate = Mathf.Abs(target) > 0.01f ? steerRate : steerReturnRate;
            Steer = Mathf.MoveTowards(Steer, target, rate * Time.deltaTime);

            if (DriverInput.Pressed(Control.ParkingBrake)) ParkingBrake = !ParkingBrake;
            if (DriverInput.Pressed(Control.ShiftDrive)) TryShift(Gear.Drive);
            if (DriverInput.Pressed(Control.ShiftReverse)) TryShift(Gear.Reverse);
            if (DriverInput.Pressed(Control.ResetCar)) resetRequested = true;

            if (ParkingBrake && Throttle > 0f && Time.time > parkingBrakeNagAt)
            {
                Instructor.Say("The parking brake is still on. Press SPACE to release it.", 4f);
                parkingBrakeNagAt = Time.time + 6f;
            }

            if (steeringWheel != null)
                steeringWheel.localRotation = steeringWheelBase * Quaternion.Euler(0f, 0f, -Steer * steeringWheelDegrees);
        }

        void TryShift(Gear gear)
        {
            if (gear == CurrentGear) return;
            if (SpeedMph > 1.5f)
            {
                Instructor.Say("Come to a complete stop before you shift gears.", 4f);
                return;
            }
            CurrentGear = gear;
        }

        void FixedUpdate()
        {
            if (resetRequested)
            {
                resetRequested = false;
                ResetToSpawn();
                return;
            }

            Vector3 v = rb.GetPointVelocity(rb.worldCenterOfMass);
            ForwardSpeed = Vector3.Dot(v, transform.forward);

            // Less steering lock at speed, like a real car feels.
            float speedFactor = Mathf.InverseLerp(4f, 30f, Mathf.Abs(ForwardSpeed));
            float steerAngle = Steer * Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedFactor);
            frontLeft.steerAngle = steerAngle;
            frontRight.steerAngle = steerAngle;

            float dir = CurrentGear == Gear.Reverse ? -1f : 1f;
            float topSpeed = (CurrentGear == Gear.Reverse ? reverseTopSpeedMph : topSpeedMph) / MpsToMph;
            float speedInGear = ForwardSpeed * dir;

            float motor = 0f;
            if (Throttle > 0f)
                motor = Throttle * maxMotorTorque * (1f - Mathf.Clamp01(speedInGear / topSpeed));
            else if (BrakeInput <= 0f && speedInGear < creepSpeedMph / MpsToMph)
                motor = creepTorque;
            motor *= dir;

            float brake = BrakeInput * maxBrakeTorque;
            if (Throttle <= 0f && BrakeInput <= 0f) brake = coastBrakeTorque;
            // Moving against the selected gear (e.g. rolling forward in reverse): throttle acts as a brake.
            if (Throttle > 0f && speedInGear < -0.5f) { brake = maxBrakeTorque * 0.6f; motor = 0f; }

            frontLeft.motorTorque = motor;
            frontRight.motorTorque = motor;
            frontLeft.brakeTorque = brake;
            frontRight.brakeTorque = brake;
            float rearBrake = brake + (ParkingBrake ? parkingBrakeTorque : 0f);
            rearLeft.brakeTorque = rearBrake;
            rearRight.brakeTorque = rearBrake;

            CheckSurfaces();
        }

        void LateUpdate()
        {
            SyncWheel(frontLeft, frontLeftVisual);
            SyncWheel(frontRight, frontRightVisual);
            SyncWheel(rearLeft, rearLeftVisual);
            SyncWheel(rearRight, rearRightVisual);
        }

        static void SyncWheel(WheelCollider wc, Transform visual)
        {
            if (wc == null || visual == null) return;
            Vector3 pos;
            Quaternion rot;
            wc.GetWorldPose(out pos, out rot);
            visual.SetPositionAndRotation(pos, rot);
        }

        void CheckSurfaces()
        {
            if (SpeedMph < 2f || RulesEngine.Instance == null) return;
            WheelCollider[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
            foreach (var wc in wheels)
            {
                WheelHit hit;
                if (!wc.GetGroundHit(out hit) || hit.collider == null) continue;
                var s = hit.collider.GetComponent<Surface>();
                if (s == null) continue;
                if (s.type == SurfaceType.Sidewalk)
                    RulesEngine.Instance.Report("SIDEWALK", "Drove onto the sidewalk", Severity.Minor, 5f,
                        "Whoa, you're up on the curb! Stay on the road.");
                else if (s.type == SurfaceType.Grass)
                    RulesEngine.Instance.Report("OFFROAD", "Left the roadway", Severity.Minor, 5f,
                        "You've run off the road. Ease back onto the pavement.");
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.GetComponent<Surface>() != null) return;
            if (collision.relativeVelocity.magnitude < 1f || RulesEngine.Instance == null) return;
            RulesEngine.Instance.Report("COLLISION", "Collision with " + collision.collider.name, Severity.Critical, 3f,
                "We hit something! In a real test, that's an automatic fail.");
        }

        public void ResetToSpawn()
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            rb.position = spawnPosition;
            rb.rotation = spawnRotation;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Steer = 0f;
            CurrentGear = Gear.Drive;
            ParkingBrake = true;
            if (OnReset != null) OnReset();
        }
    }
}
