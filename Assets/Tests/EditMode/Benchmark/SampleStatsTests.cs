using System;
using ArenaSurvivor.Core.Benchmark;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Benchmark
{
    public class SampleStatsTests
    {
        [Test]
        public void Empty_ReturnsZeros()
        {
            var stats = new SampleStats(10);

            Assert.That(stats.Count, Is.EqualTo(0));
            Assert.That(stats.Average, Is.EqualTo(0f));
            Assert.That(stats.Max, Is.EqualTo(0f));
            Assert.That(stats.Percentile(0.99f), Is.EqualTo(0f));
        }

        [Test]
        public void AverageAndMax()
        {
            var stats = new SampleStats(10);
            stats.Add(10f);
            stats.Add(20f);
            stats.Add(30f);

            Assert.That(stats.Average, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(stats.Max, Is.EqualTo(30f));
        }

        [Test]
        public void Percentile_UsesNearestRank()
        {
            var stats = new SampleStats(200);
            for (int i = 1; i <= 100; i++)
            {
                stats.Add(i); // 1..100, added in order
            }

            Assert.That(stats.Percentile(0.99f), Is.EqualTo(99f));
            Assert.That(stats.Percentile(0.5f), Is.EqualTo(50f));
            Assert.That(stats.Percentile(1f), Is.EqualTo(100f));
        }

        [Test]
        public void Percentile_IsIndependentOfInsertionOrder()
        {
            var stats = new SampleStats(10);
            foreach (float v in new[] { 5f, 1f, 4f, 2f, 3f })
            {
                stats.Add(v);
            }

            Assert.That(stats.Percentile(0.6f), Is.EqualTo(3f));
        }

        [Test]
        public void OneSlowFrame_ShowsInHighPercentileNotAverage()
        {
            // 99 smooth frames and one 100 ms hitch: the average barely moves, the 99th+ percentile catches it.
            var stats = new SampleStats(100);
            for (int i = 0; i < 99; i++)
            {
                stats.Add(16f);
            }

            stats.Add(100f);

            Assert.That(stats.Average, Is.LessThan(17f));
            Assert.That(stats.Percentile(1f), Is.EqualTo(100f));
            Assert.That(stats.Max, Is.EqualTo(100f));
        }

        [Test]
        public void BeyondCapacity_DropsAndFlags()
        {
            var stats = new SampleStats(2);
            stats.Add(1f);
            stats.Add(2f);
            stats.Add(100f);

            Assert.That(stats.Count, Is.EqualTo(2));
            Assert.That(stats.Overflowed, Is.True);
            Assert.That(stats.Max, Is.EqualTo(2f));
        }

        [Test]
        public void Clear_Resets()
        {
            var stats = new SampleStats(5);
            stats.Add(3f);

            stats.Clear();

            Assert.That(stats.Count, Is.EqualTo(0));
            Assert.That(stats.Average, Is.EqualTo(0f));
        }

        [Test]
        public void Constructor_WithZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SampleStats(0));
        }
    }
}
