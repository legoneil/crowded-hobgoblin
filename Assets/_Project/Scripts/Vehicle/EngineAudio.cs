using UnityEngine;

namespace DuvalDrive
{
    /// <summary>Simple synthesized engine hum whose pitch follows speed and throttle.</summary>
    [RequireComponent(typeof(CarController))]
    public class EngineAudio : MonoBehaviour
    {
        public float volume = 0.18f;

        CarController car;
        AudioSource source;

        void Awake()
        {
            car = GetComponent<CarController>();
            source = gameObject.AddComponent<AudioSource>();
            source.clip = MakeEngineLoop();
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.Play();
        }

        void Update()
        {
            // Fake automatic gearbox: pitch climbs then drops at each "shift".
            float mph = car.SpeedMph;
            float gearBand = 18f;
            float inGear = (mph % gearBand) / gearBand;
            float gearIndex = Mathf.Floor(mph / gearBand);
            float pitch = 0.75f + inGear * 0.55f + gearIndex * 0.06f + car.Throttle * 0.1f;
            source.pitch = Mathf.Lerp(source.pitch, pitch, Time.deltaTime * 6f);
            source.volume = volume * (0.7f + car.Throttle * 0.3f);
        }

        static AudioClip MakeEngineLoop()
        {
            const int rate = 22050;
            int n = rate; // 1 second; all frequencies are whole Hz so it loops cleanly
            var data = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float s = 0.55f * Mathf.Sin(2f * Mathf.PI * 60f * t)
                        + 0.25f * Mathf.Sin(2f * Mathf.PI * 120f * t)
                        + 0.12f * Mathf.Sin(2f * Mathf.PI * 180f * t)
                        + 0.06f * Mathf.Sin(2f * Mathf.PI * 30f * t);
                s += ((float)rng.NextDouble() - 0.5f) * 0.05f;
                data[i] = s * 0.5f;
            }
            var clip = AudioClip.Create("engine", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
