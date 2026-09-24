using System;
using ArenaSurvivor.Core.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>One level up card: title, effect text and "Lv 2 -> 3". The whole card is the button.</summary>
    public sealed class UpgradeCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text levelText;

        /// <summary>Raised when the card is tapped.</summary>
        public event Action Clicked;

        public bool Interactable
        {
            set => button.interactable = value;
        }

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke());
        }

        /// <param name="currentLevel">How many times the card was already taken this run.</param>
        public void Show(UpgradeEntry entry, int currentLevel)
        {
            titleText.text = entry.Title;
            descriptionText.text = entry.Description;
            levelText.text = entry.IsFiller
                ? ""
                : currentLevel == 0 ? "NEW" : $"Lv {currentLevel} > {currentLevel + 1}";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
