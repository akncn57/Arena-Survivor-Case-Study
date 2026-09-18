using ArenaSurvivor.Core.Presentation;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Presentation
{
    public class TimeFormatTests
    {
        [TestCase(180f, 180)]
        [TestCase(179.2f, 180)]
        [TestCase(0.2f, 1)]
        [TestCase(0f, 0)]
        [TestCase(-3f, 0)]
        [TestCase(3.00002f, 3)]
        public void CountdownSeconds_RoundsUp(float seconds, int expected)
        {
            Assert.That(TimeFormat.CountdownSeconds(seconds), Is.EqualTo(expected));
        }

        [TestCase(59.9f, 59)]
        [TestCase(60f, 60)]
        [TestCase(59.99995f, 60)]
        [TestCase(-1f, 0)]
        public void ElapsedSeconds_RoundsDown(float seconds, int expected)
        {
            Assert.That(TimeFormat.ElapsedSeconds(seconds), Is.EqualTo(expected));
        }

        [TestCase(180, "3:00")]
        [TestCase(125, "2:05")]
        [TestCase(59, "0:59")]
        [TestCase(0, "0:00")]
        [TestCase(-5, "0:00")]
        public void MinutesSeconds_Formats(int seconds, string expected)
        {
            Assert.That(TimeFormat.MinutesSeconds(seconds), Is.EqualTo(expected));
        }
    }
}
