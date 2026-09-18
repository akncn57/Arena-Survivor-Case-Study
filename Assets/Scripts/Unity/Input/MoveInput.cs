using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaSurvivor.Unity.Input
{
    /// <summary>
    /// Combines the on-screen joystick with keyboard (WASD / arrows) and gamepad input,
    /// so the game can be played and tested in the Editor without touch.
    /// The joystick wins while it is being touched.
    /// </summary>
    public sealed class MoveInput
    {
        private readonly VirtualJoystick _joystick;

        public MoveInput(VirtualJoystick joystick)
        {
            _joystick = joystick;
        }

        public Vector2 Read()
        {
            if (_joystick != null && _joystick.IsPressed)
            {
                return _joystick.Value;
            }

            Vector2 value = ReadKeyboard();

            Gamepad gamepad = Gamepad.current;
            if (value == Vector2.zero && gamepad != null)
            {
                value = gamepad.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(value, 1f);
        }

        private static Vector2 ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
    }
}
