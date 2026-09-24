using System;
using ArenaSurvivor.Core.Session;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Session
{
    public class GameSessionTests
    {
        private const float Duration = 180f;

        private GameSession _session;
        private RunResult? _result;
        private int _endedCount;

        [SetUp]
        public void SetUp()
        {
            _session = new GameSession();
            _result = null;
            _endedCount = 0;
            _session.Ended += r =>
            {
                _result = r;
                _endedCount++;
            };
        }

        [Test]
        public void NewSession_IsIdle()
        {
            Assert.That(_session.State, Is.EqualTo(GameState.Idle));
            Assert.That(_session.IsPlaying, Is.False);
        }

        [Test]
        public void Start_SetsPlayingAndRaisesStarted()
        {
            bool started = false;
            _session.Started += () => started = true;

            _session.Start(Duration);

            Assert.That(_session.State, Is.EqualTo(GameState.Playing));
            Assert.That(_session.Remaining, Is.EqualTo(Duration));
            Assert.That(started, Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Start_WithNonPositiveDuration_Throws(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _session.Start(duration));
        }

        [Test]
        public void Tick_AdvancesTimeAndProgress()
        {
            _session.Start(Duration);

            _session.Tick(45f);

            Assert.That(_session.Elapsed, Is.EqualTo(45f));
            Assert.That(_session.Remaining, Is.EqualTo(135f));
            Assert.That(_session.Progress, Is.EqualTo(0.25f));
        }

        [Test]
        public void Tick_BeforeStart_DoesNothing()
        {
            _session.Tick(10f);

            Assert.That(_session.Elapsed, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_WithNegativeDelta_DoesNothing()
        {
            _session.Start(Duration);

            _session.Tick(-5f);

            Assert.That(_session.Elapsed, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_ReachingDuration_WinsOnce()
        {
            _session.Start(Duration);

            _session.Tick(Duration + 10f);
            _session.Tick(1f);

            Assert.That(_session.State, Is.EqualTo(GameState.Won));
            Assert.That(_session.Elapsed, Is.EqualTo(Duration), "Elapsed should not overshoot the duration.");
            Assert.That(_endedCount, Is.EqualTo(1));
            Assert.That(_result.Value.Outcome, Is.EqualTo(GameState.Won));
        }

        [Test]
        public void SimulatedFrames_WinAfterFullDuration()
        {
            _session.Start(Duration);

            // 60 FPS for 3 minutes (plus a few frames of slack for float rounding).
            for (int i = 0; i < 180 * 60 + 10 && _session.IsPlaying; i++)
            {
                _session.Tick(1f / 60f);
            }

            Assert.That(_session.State, Is.EqualTo(GameState.Won));
        }

        [Test]
        public void RegisterKill_CountsOnlyWhilePlaying()
        {
            _session.RegisterKill();
            _session.Start(Duration);
            _session.RegisterKill();
            _session.RegisterKill();
            _session.NotifyPlayerDied();
            _session.RegisterKill();

            Assert.That(_session.Kills, Is.EqualTo(2));
        }

        [Test]
        public void NotifyPlayerDied_LosesWithKillsAndSurvivedTime()
        {
            _session.Start(Duration);
            _session.Tick(30f);
            _session.RegisterKill();

            _session.NotifyPlayerDied();

            Assert.That(_session.State, Is.EqualTo(GameState.Lost));
            Assert.That(_result.Value.Outcome, Is.EqualTo(GameState.Lost));
            Assert.That(_result.Value.Kills, Is.EqualTo(1));
            Assert.That(_result.Value.SurvivedSeconds, Is.EqualTo(30f));
        }

        [Test]
        public void NotifyPlayerDied_AfterWin_IsIgnored()
        {
            _session.Start(Duration);
            _session.Tick(Duration);

            _session.NotifyPlayerDied();

            Assert.That(_session.State, Is.EqualTo(GameState.Won));
            Assert.That(_endedCount, Is.EqualTo(1));
        }

        [Test]
        public void Start_AfterRunEnded_ResetsForReplay()
        {
            _session.Start(Duration);
            _session.Tick(50f);
            _session.RegisterKill();
            _session.NotifyPlayerDied();

            _session.Start(Duration);

            Assert.That(_session.State, Is.EqualTo(GameState.Playing));
            Assert.That(_session.Elapsed, Is.EqualTo(0f));
            Assert.That(_session.Kills, Is.EqualTo(0));
        }

        [Test]
        public void ReturnToIdle_StopsTheRun()
        {
            _session.Start(Duration);

            _session.ReturnToIdle();
            _session.Tick(Duration);

            Assert.That(_session.State, Is.EqualTo(GameState.Idle));
            Assert.That(_endedCount, Is.EqualTo(0));
        }

        [Test]
        public void StartEndless_NeverWinsAndReportsNoProgress()
        {
            _session.StartEndless();

            for (int i = 0; i < 60 * 60; i++)
            {
                _session.Tick(1f);
            }

            Assert.That(_session.IsEndless, Is.True);
            Assert.That(_session.IsPlaying, Is.True);
            Assert.That(_session.Elapsed, Is.EqualTo(3600f).Within(1e-2f));
            Assert.That(_session.Progress, Is.EqualTo(0f));
            Assert.That(_endedCount, Is.EqualTo(0));
        }

        [Test]
        public void StartEndless_EndsOnlyByDeath()
        {
            _session.StartEndless();
            _session.Tick(42f);

            _session.NotifyPlayerDied();

            Assert.That(_session.State, Is.EqualTo(GameState.Lost));
            Assert.That(_result.Value.SurvivedSeconds, Is.EqualTo(42f));
        }

        [Test]
        public void TimedRunAfterEndless_IsNotEndless()
        {
            _session.StartEndless();

            _session.Start(Duration);

            Assert.That(_session.IsEndless, Is.False);
            Assert.That(_session.Remaining, Is.EqualTo(Duration));
        }
    }
}
