using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Upgrades;
using TMPro;
using UnityEngine;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>
    /// "LEVEL UP!" overlay with the offered cards. The game is paused behind it; tapping a card picks it.
    /// Cards ignore taps for a short moment after appearing: the screen pops up in the middle of play, and a
    /// finger that happens to tap at that moment must not pick a card the player has not even read.
    /// </summary>
    public sealed class LevelUpScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Card slots, left to right. Unused slots are hidden.")]
        [SerializeField] private UpgradeCard[] cards;

        [Tooltip("Seconds (real time) before the cards accept taps.")]
        [SerializeField, Min(0f)] private float inputDelay = 0.4f;

        private float _inputDelayLeft;

        /// <summary>Raised with the index of the chosen card.</summary>
        public event Action<int> CardChosen;

        private void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i; // Captured per card; the loop variable would be shared by all lambdas.
                cards[i].Clicked += () => OnCardClicked(index);
            }
        }

        /// <param name="levelOf">How many times each card was already taken this run.</param>
        public void Show(int level, IReadOnlyList<UpgradeEntry> offer, Func<UpgradeEntry, int> levelOf)
        {
            titleText.text = $"LEVEL {level}!";
            for (int i = 0; i < cards.Length; i++)
            {
                if (i < offer.Count)
                {
                    cards[i].Show(offer[i], levelOf(offer[i]));
                }
                else
                {
                    cards[i].Hide();
                }
            }

            SetCardsInteractable(false);
            _inputDelayLeft = inputDelay;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Called every frame by the game flow with unscaled time (the game is paused while this is shown).</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_inputDelayLeft <= 0f || !gameObject.activeSelf)
            {
                return;
            }

            _inputDelayLeft -= unscaledDeltaTime;
            if (_inputDelayLeft <= 0f)
            {
                SetCardsInteractable(true);
            }
        }

        private void OnCardClicked(int index)
        {
            if (_inputDelayLeft > 0f)
            {
                return;
            }

            CardChosen?.Invoke(index);
        }

        private void SetCardsInteractable(bool interactable)
        {
            foreach (UpgradeCard card in cards)
            {
                card.Interactable = interactable;
            }
        }
    }
}
