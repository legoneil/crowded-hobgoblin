using UnityEngine;

namespace DuvalDrive
{
    /// <summary>
    /// The stretch of lane leading up to a stop line. The zone's forward (blue)
    /// axis points toward the intersection. A car travelling that way must reach
    /// a full stop before it leaves the zone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class StopZone : MonoBehaviour
    {
        public float fullStopMph = 1f;
        public float rollingStopMph = 6f;
        public string crossStreet = "the intersection";

        bool tracking;
        float minSpeed;

        static CarController PlayerBody(Collider other)
        {
            var car = CarController.Player;
            return car != null && other == car.BodyCollider ? car : null;
        }

        bool Approaching(CarController car)
        {
            return car.CurrentGear == Gear.Drive && Vector3.Dot(car.transform.forward, transform.forward) > 0.5f;
        }

        void OnTriggerEnter(Collider other)
        {
            var car = PlayerBody(other);
            if (car == null) return;
            tracking = Approaching(car);
            minSpeed = car.SpeedMph;
        }

        void OnTriggerStay(Collider other)
        {
            var car = PlayerBody(other);
            if (car == null) return;
            if (!tracking && Approaching(car)) { tracking = true; minSpeed = car.SpeedMph; }
            if (tracking) minSpeed = Mathf.Min(minSpeed, car.SpeedMph);
        }

        void OnTriggerExit(Collider other)
        {
            var car = PlayerBody(other);
            if (car == null || !tracking) return;
            tracking = false;

            // Only judge cars leaving through the stop line, not backing out or turning away.
            Vector3 toCar = car.transform.position - transform.position;
            if (Vector3.Dot(toCar, transform.forward) < 0f || !Approaching(car) || RulesEngine.Instance == null) return;

            if (minSpeed <= fullStopMph)
            {
                if (Random.value < 0.4f) Instructor.Say("Nice full stop.", 2.5f);
            }
            else if (minSpeed <= rollingStopMph)
            {
                RulesEngine.Instance.Report("ROLLING_STOP", "Rolling stop at " + crossStreet, Severity.Minor, 3f,
                    "That was a rolling stop. In Florida you need a complete stop: wheels not turning.");
            }
            else
            {
                RulesEngine.Instance.Report("RAN_STOP", "Ran the stop sign at " + crossStreet, Severity.Critical, 3f,
                    "You ran that stop sign! That's an automatic fail on the test.");
            }
        }
    }
}
