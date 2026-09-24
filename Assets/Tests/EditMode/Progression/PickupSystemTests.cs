using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Progression
{
    public class PickupSystemTests
    {
        private const float CollectRadius = 0.5f;
        private const float MagnetRadius = 3f;
        private const float MagnetSpeed = 10f;

        private PickupSystem _system;
        private List<Pickup> _collected;

        [SetUp]
        public void SetUp()
        {
            _system = CreateSystem(maxActive: 100);
        }

        private PickupSystem CreateSystem(int maxActive)
        {
            var system = new PickupSystem(new PickupConfig(1, 0f, 20, CollectRadius, MagnetRadius, MagnetSpeed, maxActive));
            _collected = new List<Pickup>();
            system.Collected += p => _collected.Add(p);
            return system;
        }

        [Test]
        public void Spawn_AddsActivePickupOnGround()
        {
            Pickup spawnedEvent = null;
            _system.Spawned += p => spawnedEvent = p;

            Pickup pickup = _system.Spawn(PickupKind.Experience, new Vector3(1f, 2f, 3f), 5);

            Assert.That(pickup.IsActive, Is.True);
            Assert.That(pickup.Position, Is.EqualTo(new Vector3(1f, 0f, 3f)));
            Assert.That(pickup.Value, Is.EqualTo(5));
            Assert.That(spawnedEvent, Is.SameAs(pickup));
            Assert.That(_system.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void OutsideMagnetRadius_StaysPut()
        {
            Pickup pickup = _system.Spawn(PickupKind.Experience, new Vector3(5f, 0f, 0f), 1);

            _system.Tick(1f, Vector3.zero);

            Assert.That(pickup.Position, Is.EqualTo(new Vector3(5f, 0f, 0f)));
            Assert.That(pickup.IsAttracted, Is.False);
            Assert.That(_collected, Is.Empty);
        }

        [Test]
        public void InsideMagnetRadius_FliesToPlayerAndIsCollected()
        {
            Pickup pickup = _system.Spawn(PickupKind.Experience, new Vector3(2.5f, 0f, 0f), 1);

            _system.Tick(0.1f, Vector3.zero);
            Assert.That(pickup.IsAttracted, Is.True);
            Assert.That(pickup.Position.x, Is.EqualTo(1.5f).Within(1e-4f));

            _system.Tick(0.1f, Vector3.zero);

            Assert.That(_collected.Count, Is.EqualTo(1));
            Assert.That(_system.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void AttractedPickup_FollowsPlayerOutOfMagnetRange()
        {
            Pickup pickup = _system.Spawn(PickupKind.Experience, new Vector3(2.9f, 0f, 0f), 1);
            _system.Tick(0.01f, Vector3.zero);

            // The player runs away beyond the magnet radius; the pickup keeps chasing.
            _system.Tick(0.1f, new Vector3(-10f, 0f, 0f));

            Assert.That(pickup.IsAttracted, Is.True);
            Assert.That(pickup.Position.x, Is.LessThan(2.9f));
        }

        [Test]
        public void InsideCollectRadius_CollectedImmediately_CollectedBeforeDespawned()
        {
            var order = new List<string>();
            _system.Collected += _ => order.Add("collected");
            _system.Despawned += _ => order.Add("despawned");
            _system.Spawn(PickupKind.Health, new Vector3(0.3f, 0f, 0f), 20);

            _system.Tick(0.016f, Vector3.zero);

            Assert.That(order, Is.EqualTo(new[] { "collected", "despawned" }));
            Assert.That(_collected[0].Kind, Is.EqualTo(PickupKind.Health));
        }

        [Test]
        public void MagnetMultiplier_WidensRadius()
        {
            Pickup pickup = _system.Spawn(PickupKind.Experience, new Vector3(5f, 0f, 0f), 1);
            _system.MagnetMultiplier = 2f;

            _system.Tick(0.01f, Vector3.zero);

            Assert.That(_system.MagnetRadius, Is.EqualTo(MagnetRadius * 2f));
            Assert.That(pickup.IsAttracted, Is.True);

            _system.ResetModifiers();
            Assert.That(_system.MagnetRadius, Is.EqualTo(MagnetRadius));
        }

        [Test]
        public void AtCap_ExperienceMergesIntoExistingGemAndHealthIsSkipped()
        {
            PickupSystem system = CreateSystem(maxActive: 2);
            Pickup gem = system.Spawn(PickupKind.Experience, new Vector3(10f, 0f, 0f), 1);
            system.Spawn(PickupKind.Health, new Vector3(-10f, 0f, 0f), 20);

            Pickup merged = system.Spawn(PickupKind.Experience, new Vector3(0f, 0f, 10f), 3);
            Pickup skipped = system.Spawn(PickupKind.Health, new Vector3(0f, 0f, -10f), 20);

            Assert.That(system.ActiveCount, Is.EqualTo(2));
            Assert.That(merged, Is.SameAs(gem));
            Assert.That(gem.Value, Is.EqualTo(4), "No XP is lost at the cap.");
            Assert.That(skipped, Is.Null);
        }

        [Test]
        public void Clear_DespawnsAllWithoutCollecting()
        {
            int despawned = 0;
            _system.Despawned += _ => despawned++;
            _system.Spawn(PickupKind.Experience, new Vector3(0.1f, 0f, 0f), 1);
            _system.Spawn(PickupKind.Experience, new Vector3(9f, 0f, 0f), 1);

            _system.Clear();

            Assert.That(_system.ActiveCount, Is.EqualTo(0));
            Assert.That(despawned, Is.EqualTo(2));
            Assert.That(_collected, Is.Empty);
        }

        [Test]
        public void Pool_ReusesAndResetsPickups()
        {
            Pickup first = _system.Spawn(PickupKind.Experience, new Vector3(2f, 0f, 0f), 7);
            _system.Tick(1f, Vector3.zero);

            Pickup second = _system.Spawn(PickupKind.Health, new Vector3(9f, 0f, 0f), 20);

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(second.IsAttracted, Is.False);
            Assert.That(second.Value, Is.EqualTo(20));
        }

        [Test]
        public void ManyPickupsCollectedInOneTick_AllCounted()
        {
            for (int i = 0; i < 20; i++)
            {
                _system.Spawn(PickupKind.Experience, new Vector3(0.1f * (i % 3), 0f, 0f), 1);
            }

            _system.Tick(0.016f, Vector3.zero);

            Assert.That(_collected.Count, Is.EqualTo(20));
            Assert.That(_system.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _system.Spawn(PickupKind.Experience, Vector3.zero, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupConfig(1, 1.5f, 20, 0.5f, 2f, 10f, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupConfig(1, 0.1f, 20, 0f, 2f, 10f, 10));
        }
    }
}
