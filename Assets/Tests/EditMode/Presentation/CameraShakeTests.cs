using System;
using ArenaSurvivor.Core.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Presentation
{
    public class CameraShakeTests
    {
        private const float MaxOffset = 0.5f;

        private CameraShake _shake;

        [SetUp]
        public void SetUp()
        {
            _shake = new CameraShake(MaxOffset, decayPerSecond: 1f, frequency: 8f);
        }

        [Test]
        public void NoTrauma_NoOffset()
        {
            _shake.Tick(0.1f);

            Assert.That(_shake.Offset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void AddTrauma_AccumulatesAndCapsAtOne()
        {
            _shake.AddTrauma(0.4f);
            _shake.AddTrauma(0.4f);
            Assert.That(_shake.Trauma, Is.EqualTo(0.8f).Within(1e-5f));

            _shake.AddTrauma(0.9f);
            Assert.That(_shake.Trauma, Is.EqualTo(1f));
        }

        [Test]
        public void AddTrauma_NonPositive_IsIgnored()
        {
            _shake.AddTrauma(-1f);

            Assert.That(_shake.Trauma, Is.EqualTo(0f));
        }

        [Test]
        public void RaiseTo_DoesNotStack()
        {
            // Repeated hits keep the shake at one level instead of piling up to full strength.
            _shake.RaiseTo(0.4f);
            _shake.RaiseTo(0.4f);
            _shake.RaiseTo(0.4f);
            Assert.That(_shake.Trauma, Is.EqualTo(0.4f));

            _shake.RaiseTo(0.2f);
            Assert.That(_shake.Trauma, Is.EqualTo(0.4f), "A lower level must not reduce the current trauma.");

            _shake.RaiseTo(1.5f);
            Assert.That(_shake.Trauma, Is.EqualTo(1f));
        }

        [Test]
        public void Trauma_DecaysLinearlyToZero()
        {
            _shake.AddTrauma(1f);

            _shake.Tick(0.25f);
            Assert.That(_shake.Trauma, Is.EqualTo(0.75f).Within(1e-5f));

            _shake.Tick(1f);
            Assert.That(_shake.Trauma, Is.EqualTo(0f));
            Assert.That(_shake.Offset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Offset_StaysOnGroundPlaneAndWithinTraumaSquaredLimit()
        {
            _shake.AddTrauma(0.6f);

            for (int i = 0; i < 30; i++)
            {
                _shake.Tick(1f / 60f);
                float limit = _shake.Trauma * _shake.Trauma * MaxOffset * Mathf.Sqrt(2f);
                Assert.That(_shake.Offset.y, Is.EqualTo(0f));
                Assert.That(_shake.Offset.magnitude, Is.LessThanOrEqualTo(limit + 1e-5f));
            }
        }

        [Test]
        public void SmallTrauma_ShakesMuchLessThanFullTrauma()
        {
            // Strength is trauma squared: a quarter of the trauma gives a sixteenth of the shake limit.
            var small = new CameraShake(MaxOffset, 0.001f, 8f);
            var full = new CameraShake(MaxOffset, 0.001f, 8f);
            small.AddTrauma(0.25f);
            full.AddTrauma(1f);
            float smallMax = 0f, fullMax = 0f;

            for (int i = 0; i < 120; i++)
            {
                small.Tick(1f / 60f);
                full.Tick(1f / 60f);
                smallMax = Mathf.Max(smallMax, small.Offset.magnitude);
                fullMax = Mathf.Max(fullMax, full.Offset.magnitude);
            }

            Assert.That(fullMax, Is.GreaterThan(0f));
            Assert.That(smallMax / fullMax, Is.EqualTo(1f / 16f).Within(0.01f));
        }

        [Test]
        public void Offset_ChangesSmoothlyBetweenFrames()
        {
            // Perlin noise instead of random numbers: consecutive frames are close, so the camera sways.
            _shake.AddTrauma(1f);
            _shake.Tick(1f / 60f);
            Vector3 previous = _shake.Offset;
            float largestStep = 0f;

            for (int i = 0; i < 20; i++)
            {
                _shake.Tick(1f / 60f);
                largestStep = Mathf.Max(largestStep, (_shake.Offset - previous).magnitude);
                previous = _shake.Offset;
            }

            Assert.That(largestStep, Is.LessThan(MaxOffset * 0.5f));
        }

        [Test]
        public void Reset_StopsImmediately()
        {
            _shake.AddTrauma(1f);
            _shake.Tick(0.05f);

            _shake.Reset();

            Assert.That(_shake.Trauma, Is.EqualTo(0f));
            Assert.That(_shake.Offset, Is.EqualTo(Vector3.zero));
        }

        [TestCase(-1f, 1f, 1f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(1f, 1f, 0f)]
        public void Constructor_WithInvalidValues_Throws(float maxOffset, float decay, float frequency)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CameraShake(maxOffset, decay, frequency));
        }
    }
}
