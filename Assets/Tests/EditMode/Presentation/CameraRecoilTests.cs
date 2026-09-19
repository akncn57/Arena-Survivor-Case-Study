using System;
using ArenaSurvivor.Core.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Presentation
{
    public class CameraRecoilTests
    {
        private const float Distance = 0.2f;

        private CameraRecoil _recoil;

        [SetUp]
        public void SetUp()
        {
            _recoil = new CameraRecoil(Distance, returnSharpness: 20f);
        }

        [Test]
        public void Kick_PushesAwayFromShotOnGroundPlane()
        {
            _recoil.Kick(new Vector3(3f, 5f, 0f));

            Assert.That(_recoil.Offset.x, Is.EqualTo(-Distance).Within(1e-5f));
            Assert.That(_recoil.Offset.y, Is.EqualTo(0f));
            Assert.That(_recoil.Offset.z, Is.EqualTo(0f));
        }

        [Test]
        public void RepeatedKicks_DoNotStack()
        {
            for (int i = 0; i < 5; i++)
            {
                _recoil.Kick(Vector3.forward);
                _recoil.Tick(1f / 60f);
            }

            _recoil.Kick(Vector3.forward);

            Assert.That(_recoil.Offset.magnitude, Is.EqualTo(Distance).Within(1e-5f));
        }

        [Test]
        public void Tick_ReturnsToRestBeforeNextShot()
        {
            // At the default fire interval (0.35 s) the camera is back at rest before the next kick.
            _recoil.Kick(Vector3.forward);

            _recoil.Tick(0.15f);
            Assert.That(_recoil.Offset.magnitude, Is.LessThan(Distance * 0.06f));

            _recoil.Tick(0.2f);
            Assert.That(_recoil.Offset.magnitude, Is.LessThan(Distance * 0.001f));
        }

        [Test]
        public void Tick_IsFrameRateIndependent()
        {
            var fast = new CameraRecoil(Distance, 20f);
            var slow = new CameraRecoil(Distance, 20f);
            fast.Kick(Vector3.right);
            slow.Kick(Vector3.right);

            for (int i = 0; i < 12; i++) fast.Tick(1f / 120f);
            for (int i = 0; i < 3; i++) slow.Tick(1f / 30f);

            Assert.That(fast.Offset.x, Is.EqualTo(slow.Offset.x).Within(1e-5f));
        }

        [Test]
        public void ZeroDirection_IsIgnored()
        {
            _recoil.Kick(Vector3.up);

            Assert.That(_recoil.Offset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Reset_StopsImmediately()
        {
            _recoil.Kick(Vector3.forward);

            _recoil.Reset();

            Assert.That(_recoil.Offset, Is.EqualTo(Vector3.zero));
        }

        [TestCase(-1f, 1f)]
        [TestCase(1f, 0f)]
        public void Constructor_WithInvalidValues_Throws(float distance, float sharpness)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CameraRecoil(distance, sharpness));
        }
    }
}
