using UnityEngine;

namespace DuvalDrive
{
    /// <summary>
    /// Trigger over an intersection box. Works out whether the car turned and
    /// checks it signalled early enough and checked its mirrors first.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class IntersectionZone : MonoBehaviour
    {
        public string intersectionName = "Intersection";
        public float minSignalSeconds = 1.5f;
        public float mirrorCheckWindow = 8f;
        public float turnAngle = 45f;

        bool inside;
        Vector3 entryForward;
        int entrySignal;
        float entrySignalDuration;
        bool checkedLeft, checkedRight;

        static CarController PlayerBody(Collider other)
        {
            var car = CarController.Player;
            return car != null && other == car.BodyCollider ? car : null;
        }

        void OnTriggerEnter(Collider other)
        {
            var car = PlayerBody(other);
            if (car == null || car.CurrentGear != Gear.Drive) return;

            inside = true;
            entryForward = Flat(car.transform.forward);
            var lights = CarLights.Instance;
            entrySignal = lights != null ? lights.SignalDirection : 0;
            entrySignalDuration = lights != null ? Time.time - lights.SignalOnSince : 0f;

            var head = DriverHeadLook.Instance;
            float w = mirrorCheckWindow;
            bool rear = head != null && head.LookedAt(LookTarget.RearMirror, w);
            checkedLeft = rear || (head != null && (head.LookedAt(LookTarget.LeftMirror, w) || head.LookedAt(LookTarget.LeftBlindSpot, w)));
            checkedRight = rear || (head != null && (head.LookedAt(LookTarget.RightMirror, w) || head.LookedAt(LookTarget.RightBlindSpot, w)));
        }

        void OnTriggerExit(Collider other)
        {
            var car = PlayerBody(other);
            if (car == null || !inside) return;
            inside = false;
            var rules = RulesEngine.Instance;
            if (rules == null || car.CurrentGear != Gear.Drive) return;

            float angle = Vector3.SignedAngle(entryForward, Flat(car.transform.forward), Vector3.up);
            int turn = angle > turnAngle ? 1 : angle < -turnAngle ? -1 : 0;
            string side = turn < 0 ? "left" : "right";

            if (turn == 0)
            {
                if (entrySignal != 0)
                    rules.Report("MISLEADING_SIGNAL", "Signal on while going straight at " + intersectionName, Severity.Minor, 5f,
                        "Your signal was on but you went straight. That confuses other drivers.");
                return;
            }

            bool good = true;
            if (entrySignal == 0)
            {
                good = false;
                rules.Report("NO_SIGNAL", "Turned " + side + " without signaling at " + intersectionName, Severity.Minor, 3f,
                    "You didn't signal that turn. Use Q for left, E for right.");
            }
            else if (entrySignal != turn)
            {
                good = false;
                rules.Report("WRONG_SIGNAL", "Signaled the wrong way at " + intersectionName, Severity.Minor, 3f,
                    "Your signal was pointing the other way!");
            }
            else if (entrySignalDuration < minSignalSeconds)
            {
                good = false;
                rules.Report("LATE_SIGNAL", "Signaled too late at " + intersectionName, Severity.Minor, 3f,
                    "Signal earlier. Florida wants at least 100 feet of warning before a turn.");
            }

            if (!(turn < 0 ? checkedLeft : checkedRight))
            {
                good = false;
                rules.Report("NO_MIRROR", "No mirror check before turning " + side, Severity.Minor, 3f,
                    "Check your mirrors before you turn. I didn't see your eyes move.");
            }

            if (good) Instructor.Say("Good turn: signal and mirror check. That's how it's done.", 3f);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }
    }
}
