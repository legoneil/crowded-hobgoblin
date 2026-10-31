using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuvalDrive
{
    public enum Severity { Minor, Critical }

    [Serializable]
    public class Violation
    {
        public string code;
        public string message;
        public Severity severity;
        public float time;
        public Vector3 position;
    }

    /// <summary>
    /// Single source of truth for faults and scoring. Zones and the car call
    /// <see cref="Report"/>; the HUD and instructor read from here.
    /// Also owns the speed-limit check, using the road segments the car is in.
    /// </summary>
    public class RulesEngine : MonoBehaviour
    {
        public int startingScore = 100;
        public int minorFaultPoints = 5;
        public float defaultLimitMph = 30f; // Florida default for residential/business districts

        public static RulesEngine Instance { get; private set; }

        public readonly List<Violation> Violations = new List<Violation>();
        public event Action<Violation> ViolationRaised;

        public bool Failed { get; private set; }
        public int MinorCount { get; private set; }
        public int Score { get { return Mathf.Max(0, startingScore - MinorCount * minorFaultPoints); } }

        public float CurrentLimitMph { get; private set; }
        public string CurrentStreet { get; private set; }
        public bool InActiveSchoolZone { get; private set; }

        readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();
        readonly List<RoadSegment> activeSegments = new List<RoadSegment>();
        float overLimitSince = -1f;
        CarController car;

        void Awake()
        {
            Instance = this;
            CurrentLimitMph = defaultLimitMph;
            CurrentStreet = "Parking lot";
        }

        void Start()
        {
            car = CarController.Player;
            if (car != null) car.OnReset += ResetSession;
        }

        public void ResetSession()
        {
            Violations.Clear();
            cooldownUntil.Clear();
            MinorCount = 0;
            Failed = false;
            overLimitSince = -1f;
            activeSegments.Clear();
            CurrentLimitMph = defaultLimitMph;
            CurrentStreet = "Parking lot";
            InActiveSchoolZone = false;
            Instructor.Say("Okay, let's start over from the parking lot.", 4f);
        }

        /// <summary>Record a fault. Repeats of the same code within <paramref name="cooldown"/> seconds are ignored.</summary>
        public void Report(string code, string message, Severity severity, float cooldown = 5f, string instructorLine = null)
        {
            float until;
            if (cooldownUntil.TryGetValue(code, out until) && Time.time < until) return;
            cooldownUntil[code] = Time.time + cooldown;

            var v = new Violation
            {
                code = code,
                message = message,
                severity = severity,
                time = Time.time,
                position = car != null ? car.transform.position : Vector3.zero,
            };
            Violations.Add(v);
            if (severity == Severity.Critical) Failed = true;
            else MinorCount++;

            if (!string.IsNullOrEmpty(instructorLine)) Instructor.Say(instructorLine, 5f);
            if (ViolationRaised != null) ViolationRaised(v);
        }

        public void EnterSegment(RoadSegment s)
        {
            activeSegments.Remove(s);
            activeSegments.Add(s);
            CurrentStreet = s.streetName;
        }

        public void ExitSegment(RoadSegment s)
        {
            activeSegments.Remove(s);
            if (activeSegments.Count > 0) CurrentStreet = activeSegments[activeSegments.Count - 1].streetName;
        }

        void Update()
        {
            if (car == null) return;

            // Speed limit: the strictest road we're on. In an intersection, keep the last limit.
            if (activeSegments.Count > 0)
            {
                float limit = float.MaxValue;
                bool school = false;
                foreach (var s in activeSegments)
                {
                    limit = Mathf.Min(limit, s.EffectiveLimitMph);
                    school |= s.SchoolZoneActive;
                }
                CurrentLimitMph = limit;
                InActiveSchoolZone = school;
            }

            float over = car.SpeedMph - CurrentLimitMph;
            float tolerance = InActiveSchoolZone ? 2f : 5f;
            if (over > 15f || (InActiveSchoolZone && over > 8f))
            {
                Report("SPEED_CRIT", string.Format("Excessive speed: {0:0} in a {1:0}", car.SpeedMph, CurrentLimitMph),
                    Severity.Critical, 15f, "Way too fast! Slow down right now.");
            }
            else if (over > tolerance)
            {
                if (overLimitSince < 0f) overLimitSince = Time.time;
                if (Time.time - overLimitSince > 2f)
                {
                    string where = InActiveSchoolZone ? " (school zone)" : "";
                    Report("SPEED", string.Format("Speeding: {0:0} in a {1:0}{2}", car.SpeedMph, CurrentLimitMph, where),
                        Severity.Minor, 10f,
                        InActiveSchoolZone ? "School zone! 20 mph while the lights are flashing."
                                           : "Watch your speed. The limit here is " + CurrentLimitMph.ToString("0") + ".");
                }
            }
            else
            {
                overLimitSince = -1f;
            }
        }
    }
}
