using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>Shows the benchmark result after a benchmark run.</summary>
    public sealed class BenchmarkScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button menuButton;

        public event Action MenuClicked;

        private void Awake()
        {
            menuButton.onClick.AddListener(() => MenuClicked?.Invoke());
        }

        public void Show(string text)
        {
            resultText.text = text;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
