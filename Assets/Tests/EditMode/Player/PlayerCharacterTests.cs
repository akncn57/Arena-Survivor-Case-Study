using System;
using ArenaSurvivor.Core.Player;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Player
{
    public class PlayerCharacterTests
    {
        private const float Speed = 5f;
        private const float ArenaHalfSize = 20f;

        private PlayerCharacter _player;

        [SetUp]
        public void SetUp()
        {
            _player = new PlayerCharacter(new PlayerConfig(100, Speed), ArenaHalfSize);
        }

        [Test]
        public void Move_UpInput_MovesAlongWorldForward()
        {
            _player.Move(1f, Vector2.up);

            Assert.That(_player.Position.z, Is.EqualTo(Speed).Within(1e-4f));
            Assert.That(_player.Position.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(_player.Forward, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void Move_RightInput_MovesAlongWorldRight()
        {
            _player.Move(1f, Vector2.right);

            Assert.That(_player.Position.x, Is.EqualTo(Speed).Within(1e-4f));
        }

        [Test]
        public void Move_PartialTilt_MovesSlower()
        {
            _player.Move(1f, new Vector2(0f, 0.5f));

            Assert.That(_player.Position.z, Is.EqualTo(Speed * 0.5f).Within(1e-4f));
        }

        [Test]
        public void Move_OversizedInput_IsClampedToFullSpeed()
        {
            _player.Move(1f, new Vector2(1f, 1f));

            Assert.That(_player.Velocity.magnitude, Is.EqualTo(Speed).Within(1e-4f));
        }

        [Test]
        public void Move_NoInput_KeepsPositionAndFacing()
        {
            _player.Move(1f, Vector2.right);
            Vector3 position = _player.Position;

            _player.Move(1f, Vector2.zero);

            Assert.That(_player.Position, Is.EqualTo(position));
            Assert.That(_player.Forward, Is.EqualTo(Vector3.right));
            Assert.That(_player.IsMoving, Is.False);
        }

        [Test]
        public void Move_StaysInsideArena()
        {
            _player.Move(100f, new Vector2(1f, -1f));

            Assert.That(_player.Position.x, Is.EqualTo(ArenaHalfSize));
            Assert.That(_player.Position.z, Is.EqualTo(-ArenaHalfSize));
        }

        [Test]
        public void Move_WhenDead_DoesNothing()
        {
            _player.Health.TakeDamage(100);

            _player.Move(1f, Vector2.up);

            Assert.That(_player.Position, Is.EqualTo(Vector3.zero));
            Assert.That(_player.IsMoving, Is.False);
        }

        [Test]
        public void AimAt_OverridesMovementFacing()
        {
            _player.Move(1f, Vector2.up);

            _player.AimAt(_player.Position + new Vector3(-3f, 2f, 0f));

            Assert.That(_player.Forward, Is.EqualTo(Vector3.left));
        }

        [Test]
        public void AimAt_OwnPosition_KeepsFacing()
        {
            _player.AimAt(Vector3.zero);

            Assert.That(_player.Forward, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void Reset_RestoresPositionAndHealth()
        {
            _player.Move(1f, Vector2.right);
            _player.Health.TakeDamage(60);

            _player.Reset(new Vector3(1f, 5f, 2f));

            Assert.That(_player.Position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(_player.Health.Current, Is.EqualTo(100));
            Assert.That(_player.Forward, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void Constructor_WithNonPositiveArena_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerCharacter(new PlayerConfig(), 0f));
        }
    }
}
