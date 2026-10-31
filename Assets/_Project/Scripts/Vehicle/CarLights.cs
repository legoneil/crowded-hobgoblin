using UnityEngine;

namespace DuvalDrive
{
    public enum SignalState { Off, Left, Right, Hazards }

    /// <summary>
    /// Turn signals (with self-cancel), hazards, headlights and brake lights.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarLights : MonoBehaviour
    {
        [Header("Turn signal lamps")]
        public Renderer[] leftLamps;
        public Renderer[] rightLamps;
        public Color lampOn = new Color(1f, 0.65f, 0.1f);
        public Color lampOff = new Color(0.25f, 0.17f, 0.05f);
        public Color dashLampOn = new Color(0.2f, 1f, 0.3f);
        public Color dashLampOff = new Color(0.05f, 0.15f, 0.07f);
        public Renderer leftDashLamp;
        public Renderer rightDashLamp;
        public float blinksPerMinute = 90f;

        [Header("Headlights & brake lights")]
        public Light[] headlights;
        public Renderer[] brakeLamps;
        public Color brakeOn = new Color(1f, 0.05f, 0.05f);
        public Color brakeOff = new Color(0.35f, 0.02f, 0.02f);

        public static CarLights Instance { get; private set; }

        public SignalState State { get; private set; }
        public bool Headlights { get; private set; }
        public bool LampPhase { get; private set; }
        /// <summary>Time the current signal was switched on.</summary>
        public float SignalOnSince { get; private set; }

        CarController car;
        AudioSource audioSource;
        AudioClip tick, tock;
        float blinkTimer;
        bool cancelArmed;

        void Awake()
        {
            Instance = this;
            car = GetComponent<CarController>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            tick = MakeClick("tick", 2400f);
            tock = MakeClick("tock", 1600f);
            car.OnReset += () => { SetSignal(SignalState.Off); SetHeadlights(false); };
            SetHeadlights(false);
            ApplyLamps(false);
        }

        /// <summary>-1 = left, 1 = right, 0 = none (hazards don't count as a turn signal).</summary>
        public int SignalDirection
        {
            get { return State == SignalState.Left ? -1 : State == SignalState.Right ? 1 : 0; }
        }

        void Update()
        {
            if (DriverInput.Pressed(Control.SignalLeft))
                SetSignal(State == SignalState.Left ? SignalState.Off : SignalState.Left);
            if (DriverInput.Pressed(Control.SignalRight))
                SetSignal(State == SignalState.Right ? SignalState.Off : SignalState.Right);
            if (DriverInput.Pressed(Control.Hazards))
                SetSignal(State == SignalState.Hazards ? SignalState.Off : SignalState.Hazards);
            if (DriverInput.Pressed(Control.Headlights)) SetHeadlights(!Headlights);

            // Self-cancel: once the wheel has turned in the signal's direction and comes back to center.
            int dir = SignalDirection;
            if (dir != 0)
            {
                if (car.Steer * dir > 0.45f) cancelArmed = true;
                else if (cancelArmed && Mathf.Abs(car.Steer) < 0.1f) SetSignal(SignalState.Off);
            }

            if (State != SignalState.Off)
            {
                blinkTimer += Time.deltaTime;
                float half = 30f / blinksPerMinute;
                if (blinkTimer >= half)
                {
                    blinkTimer -= half;
                    LampPhase = !LampPhase;
                    audioSource.PlayOneShot(LampPhase ? tick : tock, 0.5f);
                    ApplyLamps(LampPhase);
                }
            }

            Color b = car.IsBraking ? brakeOn : brakeOff;
            if (brakeLamps != null) foreach (var r in brakeLamps) if (r != null) r.material.color = b;
        }

        public void SetSignal(SignalState s)
        {
            if (s == State) return;
            State = s;
            cancelArmed = false;
            SignalOnSince = Time.time;
            blinkTimer = 0f;
            LampPhase = s != SignalState.Off;
            if (s != SignalState.Off) audioSource.PlayOneShot(tick, 0.5f);
            ApplyLamps(LampPhase);
        }

        public void SetHeadlights(bool on)
        {
            Headlights = on;
            if (headlights != null) foreach (var l in headlights) if (l != null) l.enabled = on;
        }

        void ApplyLamps(bool phase)
        {
            bool left = phase && (State == SignalState.Left || State == SignalState.Hazards);
            bool right = phase && (State == SignalState.Right || State == SignalState.Hazards);
            if (leftLamps != null) foreach (var r in leftLamps) if (r != null) r.material.color = left ? lampOn : lampOff;
            if (rightLamps != null) foreach (var r in rightLamps) if (r != null) r.material.color = right ? lampOn : lampOff;
            if (leftDashLamp != null) leftDashLamp.material.color = left ? dashLampOn : dashLampOff;
            if (rightDashLamp != null) rightDashLamp.material.color = right ? dashLampOn : dashLampOff;
        }

        static AudioClip MakeClick(string name, float freq)
        {
            const int rate = 44100;
            int n = rate / 40;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 180f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
