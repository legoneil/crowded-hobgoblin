using UnityEngine;

namespace DuvalDrive
{
    /// <summary>
    /// Prototype heads-up display drawn with IMGUI (no UI setup needed):
    /// speed, gear, signals, speed-limit sign, street name, faults, instructor line, help.
    /// </summary>
    public class DrivingHUD : MonoBehaviour
    {
        public bool showHelp = true;

        CarController car;
        CarLights lights;
        DriverHeadLook head;
        GUIStyle label, big, small, center, sign, signBig;
        float scale;

        static readonly Color Panel = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color Amber = new Color(1f, 0.75f, 0.2f);
        static readonly Color Green = new Color(0.3f, 1f, 0.4f);
        static readonly Color Red = new Color(1f, 0.3f, 0.25f);

        void Start()
        {
            car = CarController.Player;
            lights = CarLights.Instance;
            head = DriverHeadLook.Instance;
        }

        void Update()
        {
            if (DriverInput.Pressed(Control.ToggleHelp)) showHelp = !showHelp;
        }

        void BuildStyles()
        {
            scale = Mathf.Max(0.6f, Screen.height / 1080f);
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * scale), richText = true, wordWrap = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(18 * scale) };
            big = new GUIStyle(label) { fontSize = Mathf.RoundToInt(64 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            sign = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(17 * scale) };
            sign.normal.textColor = Color.black;
            signBig = new GUIStyle(sign) { fontSize = Mathf.RoundToInt(46 * scale) };
        }

        void OnGUI()
        {
            if (car == null) return;
            if (label == null || !Mathf.Approximately(scale, Mathf.Max(0.6f, Screen.height / 1080f))) BuildStyles();

            DrawSpeedLimit();
            DrawDash();
            DrawScore();
            DrawInstructor();
            if (showHelp) DrawHelp();
            else Shadow(new Rect(12 * scale, Screen.height - 34 * scale, 400 * scale, 30 * scale), "TAB: show controls", small);

            if (head != null && !head.CursorLocked)
                Shadow(new Rect(0, Screen.height * 0.45f, Screen.width, 40 * scale), "Click the game view to look around with the mouse", center);
        }

        void DrawSpeedLimit()
        {
            var rules = RulesEngine.Instance;
            if (rules == null) return;
            float s = scale;
            float x = 20 * s, y = 20 * s, w = 110 * s, h = 140 * s;

            if (rules.InActiveSchoolZone)
            {
                Fill(new Rect(x - 4 * s, y - 4 * s, w + 8 * s, h + 8 * s), Color.black);
                Fill(new Rect(x, y, w, h), new Color(0.75f, 1f, 0.15f));
                bool flash = Mathf.Repeat(Time.time, 1f) < 0.5f;
                GUI.Label(new Rect(x, y + 4 * s, w, 22 * s), flash ? "* SCHOOL *" : "SCHOOL", sign);
                GUI.Label(new Rect(x, y + 26 * s, w, 70 * s), rules.CurrentLimitMph.ToString("0"), signBig);
                GUI.Label(new Rect(x, y + 92 * s, w, 44 * s), "WHEN\nFLASHING", sign);
            }
            else
            {
                Fill(new Rect(x - 4 * s, y - 4 * s, w + 8 * s, h + 8 * s), Color.black);
                Fill(new Rect(x, y, w, h), Color.white);
                GUI.Label(new Rect(x, y + 6 * s, w, 46 * s), "SPEED\nLIMIT", sign);
                GUI.Label(new Rect(x, y + 56 * s, w, 74 * s), rules.CurrentLimitMph.ToString("0"), signBig);
            }

            Fill(new Rect(x - 4 * s, y + h + 14 * s, 300 * s, 34 * s), new Color(0.05f, 0.35f, 0.15f, 0.9f));
            GUI.Label(new Rect(x + 6 * s, y + h + 16 * s, 300 * s, 30 * s), rules.CurrentStreet, label);
        }

        void DrawDash()
        {
            float s = scale;
            float w = 420 * s, h = 130 * s;
            float x = (Screen.width - w) / 2f, y = Screen.height - h - 12 * s;
            Fill(new Rect(x, y, w, h), Panel);

            // Signals
            bool phase = lights != null && lights.LampPhase;
            bool l = phase && lights.State != SignalState.Off && lights.State != SignalState.Right;
            bool r = phase && lights.State != SignalState.Off && lights.State != SignalState.Left;
            Colored(new Rect(x + 10 * s, y + 20 * s, 70 * s, 70 * s), "<<", big, l ? Green : new Color(1, 1, 1, 0.15f));
            Colored(new Rect(x + w - 80 * s, y + 20 * s, 70 * s, 70 * s), ">>", big, r ? Green : new Color(1, 1, 1, 0.15f));

            // Speed
            GUI.Label(new Rect(x, y + 4 * s, w, 80 * s), car.SpeedMph.ToString("0"), big);
            GUI.Label(new Rect(x, y + 76 * s, w, 26 * s), "MPH", center);

            // Gear & status line
            string gear = car.CurrentGear == Gear.Drive ? "D" : "<color=#ffcc44>R</color>";
            string status = "Gear " + gear;
            if (car.ParkingBrake) status += "   <color=#ff5544>PARKING BRAKE</color>";
            if (lights != null && lights.Headlights) status += "   <color=#88ccff>HEADLIGHTS</color>";
            if (lights != null && lights.State == SignalState.Hazards) status += "   <color=#ffaa33>HAZARDS</color>";
            GUI.Label(new Rect(x, y + 100 * s, w, 28 * s), status, center);

            // Mirror-check lights: so you can see what the instructor sees.
            if (head != null)
            {
                string m = "Eyes:  " + Dot(head.LookedAt(LookTarget.LeftBlindSpot, 3f)) + " shoulder  "
                         + Dot(head.LookedAt(LookTarget.LeftMirror, 3f)) + " L  "
                         + Dot(head.LookedAt(LookTarget.RearMirror, 3f)) + " rear  "
                         + Dot(head.LookedAt(LookTarget.RightMirror, 3f)) + " R  "
                         + Dot(head.LookedAt(LookTarget.RightBlindSpot, 3f)) + " shoulder";
                Shadow(new Rect(x - 60 * s, y - 30 * s, w + 120 * s, 28 * s), m, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
        }

        static string Dot(bool on)
        {
            return on ? "<color=#55ff66>●</color>" : "<color=#666666>●</color>";
        }

        void DrawScore()
        {
            var rules = RulesEngine.Instance;
            if (rules == null) return;
            float s = scale;
            float w = 420 * s;
            float x = Screen.width - w - 20 * s, y = 20 * s;
            int shown = Mathf.Min(6, rules.Violations.Count);
            float h = (70 + shown * 28) * s;
            Fill(new Rect(x, y, w, h), Panel);

            string header = "Score  <b>" + rules.Score + "</b>/100";
            if (rules.Failed) header += "   <color=#ff4433><b>CRITICAL FAULT: TEST FAILED</b></color>";
            GUI.Label(new Rect(x + 10 * s, y + 6 * s, w - 20 * s, 30 * s), header, label);
            GUI.Label(new Rect(x + 10 * s, y + 36 * s, w - 20 * s, 26 * s),
                rules.Violations.Count == 0 ? "No faults yet" : "Faults (" + rules.Violations.Count + "):", small);

            for (int i = 0; i < shown; i++)
            {
                var v = rules.Violations[rules.Violations.Count - 1 - i];
                Color c = v.severity == Severity.Critical ? Red : Amber;
                Colored(new Rect(x + 16 * s, y + (64 + i * 28) * s, w - 26 * s, 28 * s),
                    (v.severity == Severity.Critical ? "X  " : "-  ") + v.message, small, c);
            }
        }

        void DrawInstructor()
        {
            string line = Instructor.CurrentLine;
            if (string.IsNullOrEmpty(line)) return;
            float s = scale;
            float w = Mathf.Min(Screen.width - 40 * s, 900 * s), h = 70 * s;
            float x = (Screen.width - w) / 2f, y = Screen.height - 130 * s - h - 50 * s;
            Fill(new Rect(x, y, w, h), new Color(0.05f, 0.1f, 0.2f, 0.8f));
            GUI.Label(new Rect(x + 14 * s, y + 6 * s, w - 28 * s, h - 12 * s),
                "<color=#9fd0ff><b>" + Instructor.SpeakerName + ":</b></color> " + line, label);
        }

        void DrawHelp()
        {
            float s = scale;
            float w = 360 * s, h = 470 * s;
            float x = 20 * s, y = 230 * s;
            Fill(new Rect(x, y, w, h), Panel);
            GUI.Label(new Rect(x + 12 * s, y + 8 * s, w - 24 * s, h - 16 * s),
                "<b>CONTROLS</b>  (TAB to hide)\n\n" +
                "W / Up: accelerate\n" +
                "S / Down: brake\n" +
                "A D / Left Right: steer\n" +
                "SPACE: parking brake\n" +
                "F: Drive    R: Reverse (when stopped)\n" +
                "Q / E: left / right signal\n" +
                "H: hazards     L: headlights\n\n" +
                "Mouse: look around\n" +
                "Z / X / C: glance left mirror / rear-view / right mirror\n" +
                "Look far left/right: shoulder check\n\n" +
                "BACKSPACE: restart lesson\n" +
                "ESC: free the mouse", small);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Colored(Rect r, string text, GUIStyle style, Color c)
        {
            var old = style.normal.textColor;
            style.normal.textColor = c;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        static void Shadow(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), StripColor(text), style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        static string StripColor(string s)
        {
            return System.Text.RegularExpressions.Regex.Replace(s, "<color=[^>]+>|</color>", "");
        }
    }
}
