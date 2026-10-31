using UnityEngine;

namespace DuvalDrive
{
    /// <summary>
    /// Trigger volume covering one block of a two-way street (between intersections).
    /// Supplies the street name and speed limit, and checks the car keeps right.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RoadSegment : MonoBehaviour
    {
        public string streetName = "Street";
        public float limitMph = 30f;
        [Tooltip("Direction the road runs (either way). Traffic keeps to the right of the center line.")]
        public Vector3 axis = Vector3.forward;

        [Header("School zone")]
        public bool schoolZone;
        public float schoolLimitMph = 20f;
        public bool beaconsFlashing = true;
        public Renderer[] beacons;
        public Color beaconOn = new Color(1f, 0.85f, 0.1f);
        public Color beaconOff = new Color(0.3f, 0.25f, 0.05f);

        public bool SchoolZoneActive { get { return schoolZone && beaconsFlashing; } }
        public float EffectiveLimitMph { get { return SchoolZoneActive ? schoolLimitMph : limitMph; } }

        float wrongSideSince = -1f;

        void Update()
        {
            if (beacons == null || beacons.Length == 0) return;
            bool phase = SchoolZoneActive && Mathf.Repeat(Time.time, 1f) < 0.5f;
            for (int i = 0; i < beacons.Length; i++)
            {
                // Alternate the two lamps on each sign, like real school-zone flashers.
                bool on = SchoolZoneActive && (i % 2 == 0 ? phase : !phase);
                if (beacons[i] != null) beacons[i].material.color = on ? beaconOn : beaconOff;
            }
        }

        static CarController PlayerBody(Collider other)
        {
            var car = CarController.Player;
            return car != null && other == car.BodyCollider ? car : null;
        }

        void OnTriggerEnter(Collider other)
        {
            if (PlayerBody(other) != null && RulesEngine.Instance != null) RulesEngine.Instance.EnterSegment(this);
        }

        void OnTriggerExit(Collider other)
        {
            if (PlayerBody(other) == null) return;
            wrongSideSince = -1f;
            if (RulesEngine.Instance != null) RulesEngine.Instance.ExitSegment(this);
        }

        void OnTriggerStay(Collider other)
        {
            var car = PlayerBody(other);
            if (car == null || RulesEngine.Instance == null) return;
            if (car.CurrentGear == Gear.Reverse || car.SpeedMph < 3f) { wrongSideSince = -1f; return; }

            Vector3 a = axis.normalized;
            Vector3 travel = Vector3.Dot(car.transform.forward, a) >= 0f ? a : -a;
            Vector3 right = Vector3.Cross(Vector3.up, travel);
            float lateral = Vector3.Dot(car.transform.position - transform.position, right);

            if (lateral < -0.3f)
            {
                if (wrongSideSince < 0f) wrongSideSince = Time.time;
                if (Time.time - wrongSideSince > 1.5f)
                    RulesEngine.Instance.Report("WRONG_SIDE", "Drove on the wrong side of " + streetName, Severity.Minor, 8f,
                        "Get back to the right side of the road, over the yellow line!");
            }
            else
            {
                wrongSideSince = -1f;
            }
        }
    }
}
