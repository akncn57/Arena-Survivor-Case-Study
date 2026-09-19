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

        [Tooltip("Fill image of the health bar, stretched over the bar. Its right edge follows the health fraction.")]
        [SerializeField] private Image healthFill;

        private int _shownSeconds = -1;
        private int _shownKills = -1;
        private float _shownHealth = -1f;

        public void Show()
        {
            _shownSeconds = -1;
            _shownKills = -1;
            _shownHealth = -1f;
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

            float health = playerHealth.Normalized;
            if (!Mathf.Approximately(health, _shownHealth))
            {
                _shownHealth = health;
                SetHealthFill(health);
            }
        }

        private void SetHealthFill(float fraction)
        {
            // The bar is shortened by moving the fill's right anchor instead of using Image.fillAmount:
            // fillAmount is silently ignored by an Image without a sprite, which is how the bar got stuck at full.
            // Moving the anchor works for any image and only rebuilds the layout when health changes.
            RectTransform fill = healthFill.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), fill.anchorMax.y);
            healthFill.enabled = fraction > 0f;
        }
    }
}
