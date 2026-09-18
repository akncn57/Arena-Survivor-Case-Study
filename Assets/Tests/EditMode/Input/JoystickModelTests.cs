using System;
using ArenaSurvivor.Core.Input;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Input
{
    public class JoystickModelTests
    {
        private const float Radius = 100f;
        private const float DeadZone = 0.1f;

        private JoystickModel _joystick;

        [SetUp]
        public void SetUp()
        {
            _joystick = new JoystickModel(Radius, DeadZone);
        }

        [Test]
        public void Press_SetsOriginWithZeroValue()
        {
            _joystick.Press(new Vector2(300f, 200f));

            Assert.That(_joystick.IsPressed, Is.True);
            Assert.That(_joystick.Origin, Is.EqualTo(new Vector2(300f, 200f)));
            Assert.That(_joystick.Value, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DragToRadius_GivesFullStrength()
        {
            _joystick.Press(Vector2.zero);

            _joystick.Drag(new Vector2(0f, Radius));

            Assert.That(_joystick.Value.y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(_joystick.Value.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void DragBeyondRadius_IsClamped()
        {
            _joystick.Press(Vector2.zero);

            _joystick.Drag(new Vector2(Radius * 5f, 0f));

            Assert.That(_joystick.HandleOffset.magnitude, Is.EqualTo(Radius).Within(1e-3f));
            Assert.That(_joystick.Value.magnitude, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void DragInsideDeadZone_GivesZero()
        {
            _joystick.Press(Vector2.zero);

            _joystick.Drag(new Vector2(Radius * DeadZone * 0.5f, 0f));

            Assert.That(_joystick.Value, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DragOutsideDeadZone_IsRescaledFromZero()
        {
            _joystick.Press(Vector2.zero);

            // Halfway between the dead zone edge (10) and the radius (100).
            _joystick.Drag(new Vector2(55f, 0f));

            Assert.That(_joystick.Value.x, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Drag_IsRelativeToOrigin()
        {
            _joystick.Press(new Vector2(500f, 500f));

            _joystick.Drag(new Vector2(500f - Radius, 500f));

            Assert.That(_joystick.Value.x, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void DragWithoutPress_IsIgnored()
        {
            _joystick.Drag(new Vector2(Radius, 0f));

            Assert.That(_joystick.Value, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Release_ResetsValue()
        {
            _joystick.Press(Vector2.zero);
            _joystick.Drag(new Vector2(Radius, 0f));

            _joystick.Release();

            Assert.That(_joystick.IsPressed, Is.False);
            Assert.That(_joystick.Value, Is.EqualTo(Vector2.zero));
            Assert.That(_joystick.HandleOffset, Is.EqualTo(Vector2.zero));
        }

        [TestCase(0f, 0.1f)]
        [TestCase(10f, -0.1f)]
        [TestCase(10f, 1f)]
        public void Constructor_WithInvalidValues_Throws(float radius, float deadZone)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new JoystickModel(radius, deadZone));
        }
    }
}
