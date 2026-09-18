using System;
using ArenaSurvivor.Core.Combat;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Combat
{
    public class HealthTests
    {
        [Test]
        public void NewHealth_IsFull()
        {
            var health = new Health(100);

            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(health.Normalized, Is.EqualTo(1f));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void TakeDamage_ReducesAndRaisesDamaged()
        {
            var health = new Health(100);
            int reported = 0;
            health.Damaged += amount => reported = amount;

            health.TakeDamage(30);

            Assert.That(health.Current, Is.EqualTo(70));
            Assert.That(reported, Is.EqualTo(30));
        }

        [Test]
        public void TakeDamage_BeyondZero_ClampsAndReportsAppliedAmount()
        {
            var health = new Health(10);
            int reported = 0;
            health.Damaged += amount => reported = amount;

            health.TakeDamage(25);

            Assert.That(health.Current, Is.EqualTo(0));
            Assert.That(reported, Is.EqualTo(10));
        }

        [Test]
        public void Died_FiresExactlyOnce()
        {
            var health = new Health(10);
            int diedCount = 0;
            health.Died += () => diedCount++;

            health.TakeDamage(10);
            health.TakeDamage(10);

            Assert.That(health.IsDead, Is.True);
            Assert.That(diedCount, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void TakeDamage_NonPositive_IsIgnored(int amount)
        {
            var health = new Health(10);
            bool damaged = false;
            health.Damaged += _ => damaged = true;

            health.TakeDamage(amount);

            Assert.That(health.Current, Is.EqualTo(10));
            Assert.That(damaged, Is.False);
        }

        [Test]
        public void Invulnerable_IgnoresDamage()
        {
            var health = new Health(10) { IsInvulnerable = true };
            bool damaged = false;
            health.Damaged += _ => damaged = true;

            health.TakeDamage(50);

            Assert.That(health.Current, Is.EqualTo(10));
            Assert.That(damaged, Is.False);
        }

        [Test]
        public void Reset_RefillsAfterDeath()
        {
            var health = new Health(10);
            health.TakeDamage(10);

            health.Reset();

            Assert.That(health.Current, Is.EqualTo(10));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void ResetWithMax_ChangesMax()
        {
            var health = new Health(10);

            health.Reset(50);

            Assert.That(health.Max, Is.EqualTo(50));
            Assert.That(health.Current, Is.EqualTo(50));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_WithNonPositiveMax_Throws(int max)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Health(max));
        }
    }
}
