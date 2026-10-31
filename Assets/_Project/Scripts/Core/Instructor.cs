using System.Collections;
using UnityEngine;

namespace DuvalDrive
{
    /// <summary>
    /// The driving instructor riding in the passenger seat. Shows one line of
    /// dialogue at a time; the HUD reads <see cref="CurrentLine"/>.
    /// </summary>
    public class Instructor : MonoBehaviour
    {
        public string instructorName = "Ms. Delgado";

        public static Instructor Instance { get; private set; }
        public static string CurrentLine { get; private set; }

        static float lineUntil;
        static float lastSpokeAt = -999f;

        static readonly string[] Tips =
        {
            "Check your mirrors with Z, X and C. I'm watching where you look.",
            "Signal before every turn, and give it a couple of seconds before you get there.",
            "Park St has a school zone: 20 mph when the yellow lights are flashing.",
            "Riverside Ave is 35. The side streets here are 30, Florida's default for residential.",
            "At the 4-way stop on Park and Stockton, whoever stops first goes first.",
            "In Florida, if your wipers are on, your headlights must be on too.",
            "Keep right. Stay in your lane, not over the yellow line.",
        };

        CarController car;

        public static string SpeakerName
        {
            get { return Instance != null ? Instance.instructorName : "Instructor"; }
        }

        public static void Say(string line, float seconds = 5f)
        {
            CurrentLine = line;
            lineUntil = Time.time + seconds;
            lastSpokeAt = Time.time;
        }

        void Awake()
        {
            Instance = this;
            CurrentLine = null;
        }

        void Start()
        {
            car = CarController.Player;
            StartCoroutine(Intro());
            StartCoroutine(TipLoop());
        }

        void Update()
        {
            if (CurrentLine != null && Time.time > lineUntil) CurrentLine = null;
        }

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(0.5f);
            Say("Hi, I'm " + instructorName + ". Welcome to your first lesson in Riverside!", 5f);
            yield return new WaitForSeconds(5.5f);
            Say("The parking brake is on. Press SPACE to release it, then hold W to drive.", 7f);

            while (car != null && car.ParkingBrake) yield return null;
            yield return new WaitForSeconds(0.5f);
            Say("Good. Drive out of the lot to Post St, and come to a full stop at the stop sign.", 7f);
        }

        IEnumerator TipLoop()
        {
            int i = 0;
            while (true)
            {
                yield return new WaitForSeconds(5f);
                if (Time.time - lastSpokeAt < 35f) continue;
                Say(Tips[i % Tips.Length], 7f);
                i++;
            }
        }
    }
}
