using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DuvalDrive
{
    public enum Control
    {
        Throttle,
        Brake,
        SteerLeft,
        SteerRight,
        ParkingBrake,
        ShiftDrive,
        ShiftReverse,
        SignalLeft,
        SignalRight,
        Hazards,
        Headlights,
        LookLeftMirror,
        LookRearMirror,
        LookRightMirror,
        ResetCar,
        ToggleHelp,
    }

    /// <summary>
    /// Keyboard + mouse input that works with either Unity input backend
    /// (old Input Manager or the new Input System package).
    /// </summary>
    public static class DriverInput
    {
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        static Key[] Keys(Control c)
        {
            switch (c)
            {
                case Control.Throttle: return new[] { Key.W, Key.UpArrow };
                case Control.Brake: return new[] { Key.S, Key.DownArrow };
                case Control.SteerLeft: return new[] { Key.A, Key.LeftArrow };
                case Control.SteerRight: return new[] { Key.D, Key.RightArrow };
                case Control.ParkingBrake: return new[] { Key.Space };
                case Control.ShiftDrive: return new[] { Key.F };
                case Control.ShiftReverse: return new[] { Key.R };
                case Control.SignalLeft: return new[] { Key.Q };
                case Control.SignalRight: return new[] { Key.E };
                case Control.Hazards: return new[] { Key.H };
                case Control.Headlights: return new[] { Key.L };
                case Control.LookLeftMirror: return new[] { Key.Z };
                case Control.LookRearMirror: return new[] { Key.X };
                case Control.LookRightMirror: return new[] { Key.C };
                case Control.ResetCar: return new[] { Key.Backspace };
                case Control.ToggleHelp: return new[] { Key.Tab, Key.F1 };
            }
            return new Key[0];
        }

        public static bool Held(Control c)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var k in Keys(c)) if (kb[k].isPressed) return true;
            return false;
        }

        public static bool Pressed(Control c)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var k in Keys(c)) if (kb[k].wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>Mouse movement this frame, scaled to match the old Input Manager's "Mouse X/Y" axes.</summary>
        public static Vector2 MouseDelta()
        {
            var m = Mouse.current;
            return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
        }

        public static bool ClickPressed()
        {
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        public static bool EscapePressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
#else
        static KeyCode[] Keys(Control c)
        {
            switch (c)
            {
                case Control.Throttle: return new[] { KeyCode.W, KeyCode.UpArrow };
                case Control.Brake: return new[] { KeyCode.S, KeyCode.DownArrow };
                case Control.SteerLeft: return new[] { KeyCode.A, KeyCode.LeftArrow };
                case Control.SteerRight: return new[] { KeyCode.D, KeyCode.RightArrow };
                case Control.ParkingBrake: return new[] { KeyCode.Space };
                case Control.ShiftDrive: return new[] { KeyCode.F };
                case Control.ShiftReverse: return new[] { KeyCode.R };
                case Control.SignalLeft: return new[] { KeyCode.Q };
                case Control.SignalRight: return new[] { KeyCode.E };
                case Control.Hazards: return new[] { KeyCode.H };
                case Control.Headlights: return new[] { KeyCode.L };
                case Control.LookLeftMirror: return new[] { KeyCode.Z };
                case Control.LookRearMirror: return new[] { KeyCode.X };
                case Control.LookRightMirror: return new[] { KeyCode.C };
                case Control.ResetCar: return new[] { KeyCode.Backspace };
                case Control.ToggleHelp: return new[] { KeyCode.Tab, KeyCode.F1 };
            }
            return new KeyCode[0];
        }

        public static bool Held(Control c)
        {
            foreach (var k in Keys(c)) if (Input.GetKey(k)) return true;
            return false;
        }

        public static bool Pressed(Control c)
        {
            foreach (var k in Keys(c)) if (Input.GetKeyDown(k)) return true;
            return false;
        }

        public static Vector2 MouseDelta()
        {
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }

        public static bool ClickPressed()
        {
            return Input.GetMouseButtonDown(0);
        }

        public static bool EscapePressed()
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }
#endif
    }
}
