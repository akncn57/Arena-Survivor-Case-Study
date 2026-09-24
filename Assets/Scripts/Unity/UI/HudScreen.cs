using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Presentation;
using ArenaSurvivor.Core.Progression;
using ArenaSurvivor.Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>
    /// In-game overlay: remaining time, kill count and health bar; in endless mode also the XP bar and level,
    /// and the timer counts up instead of down. Also hosts the joystick, so the joystick is only active while
    /// this screen is shown.
    /// Texts are rebuilt only when the shown number changes, so the HUD creates no garbage per frame.
    /// </summary>
    public sealed class HudScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text killsText;

        [Tooltip("Fill image of the health bar, stretched over the bar. Its right edge follows the health fraction.")]
        [SerializeField] private Image healthFill;

        [Header("Endless mode (optional)")]
        [Tooltip("Root of the XP bar. Shown only in endless mode.")]
        [SerializeField] private GameObject experienceBar;

        [Tooltip("Fill image of the XP bar, stretched over the bar like the health fill.")]
        [SerializeField] private Image experienceFill;

        [SerializeField] private TMP_Text levelText;

        private int _shownSeconds = -1;
        private int _shownKills = -1;
        private float _shownHealth = -1f;
        private int _shownLevel = -1;
        private float _shownExperience = -1f;

        public void Show(bool endless)
        {
            _shownSeconds = -1;
            _shownKills = -1;
            _shownHealth = -1f;
            _shownLevel = -1;
            _shownExperience = -1f;

            if (experienceBar != null)
            {
                experienceBar.SetActive(endless);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <param name="experience">The run's XP in endless mode, null in a timed run.</param>
        public void Refresh(GameSession session, Health playerHealth, Experience experience)
        {
            // Endless runs have no end time: show the time survived instead of the time left.
            int seconds = session.IsEndless
                ? TimeFormat.ElapsedSeconds(session.Elapsed)
                : TimeFormat.CountdownSeconds(session.Remaining);
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

            float health = playerHealth.Normalized;
            if (!Mathf.Approximately(health, _shownHealth))
            {
                _shownHealth = health;
                SetFill(healthFill, health);
            }

            if (experience != null && experienceFill != null)
            {
                float fraction = experience.Normalized;
                if (!Mathf.Approximately(fraction, _shownExperience))
                {
                    _shownExperience = fraction;
                    SetFill(experienceFill, fraction);
                }

                if (levelText != null && experience.Level != _shownLevel)
                {
                    _shownLevel = experience.Level;
                    levelText.text = $"LV {_shownLevel}";
                }
            }
        }

        private static void SetFill(Image fill, float fraction)
        {
            // The bar is shortened by moving the fill's right anchor instead of using Image.fillAmount:
            // fillAmount is silently ignored by an Image without a sprite, which is how the bar got stuck at full.
            // Moving the anchor works for any image and only rebuilds the layout when the value changes.
            RectTransform rect = fill.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), rect.anchorMax.y);
            fill.enabled = fraction > 0f;
        }
    }
}
