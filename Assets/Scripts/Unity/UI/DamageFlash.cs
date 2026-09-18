using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>
    /// Full-screen red tint that appears when the player takes damage and fades out.
    /// Has no Update(); the game flow calls <see cref="Tick"/> every frame.
    /// </summary>
    public sealed class DamageFlash : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.35f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;

        private float _alpha;

        public void Flash()
        {
            _alpha = peakAlpha;
            Apply();
        }

        public void Tick(float deltaTime)
        {
            if (_alpha <= 0f)
            {
                return;
            }

            _alpha = Mathf.Max(0f, _alpha - peakAlpha * deltaTime / fadeSeconds);
            Apply();
        }

        public void Clear()
        {
            _alpha = 0f;
            Apply();
        }

        private void Apply()
        {
            Color color = image.color;
            color.a = _alpha;
            image.color = color;
            image.enabled = _alpha > 0f; // A fully transparent full-screen image still costs fill rate.
        }
    }
}
