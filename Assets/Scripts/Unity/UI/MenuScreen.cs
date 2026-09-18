using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Difficulty;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>Start screen: one button per difficulty and the lifetime kill count.</summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        [Tooltip("One button per difficulty, in the same order as the bootstrap's difficulty list.")]
        [SerializeField] private Button[] difficultyButtons;
        [SerializeField] private TMP_Text totalKillsText;

        /// <summary>Raised with the index of the chosen difficulty.</summary>
        public event Action<int> DifficultySelected;

        public void Bind(IReadOnlyList<DifficultySettings> difficulties)
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                Button button = difficultyButtons[i];
                bool hasDifficulty = i < difficulties.Count;
                button.gameObject.SetActive(hasDifficulty);
                if (!hasDifficulty)
                {
                    continue;
                }

                button.GetComponentInChildren<TMP_Text>().text = difficulties[i].DisplayName;

                int index = i; // Captured per button; the loop variable would be shared by all lambdas.
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => DifficultySelected?.Invoke(index));
            }
        }

        public void Show(int totalKills)
        {
            totalKillsText.text = $"Total kills: {totalKills}";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
