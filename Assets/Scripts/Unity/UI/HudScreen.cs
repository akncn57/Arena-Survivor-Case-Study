using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Presentation;
using ArenaSurvivor.Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>
    /// In-game overlay: remaining time, kill count and health bar. Also hosts the joystick,
    /// so the joystick is only active while this screen is shown.
    /// Texts are rebuilt only when the shown number changes, so the HUD creates no garbage per frame.
    /// </summary>
    public sealed class HudScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text killsText;

        [Tooltip("Image of type Filled; fillAmount shows the health fraction.")]
        [SerializeField] private Image healthFill;

        private int _shownSeconds = -1;
        private int _shownKills = -1;

        public void Show()
        {
            _shownSeconds = -1;
            _shownKills = -1;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Refresh(GameSession session, Health playerHealth)
        {
            int seconds = TimeFormat.CountdownSeconds(session.Remaining);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                timerText.text = TimeFormat.MinutesSeconds(seconds);
            }

            if (session.Kills != _shownKills)
            {
                _shownKills = session.Kills;
                killsText.text = $"Kills: {_shownKills}";
            }

            healthFill.fillAmount = playerHealth.Normalized;
        }
    }
}
