using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>Start screen: one button per difficulty, the endless mode button and the lifetime records.</summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        [Tooltip("One button per difficulty, in the same order as the bootstrap's difficulty list.")]
        [SerializeField] private Button[] difficultyButtons;
        [SerializeField] private TMP_Text totalKillsText;

        [Tooltip("Starts the fixed performance benchmark.")]
        [SerializeField] private Button benchmarkButton;

        [Header("Endless mode (optional)")]
        [Tooltip("Starts an endless run. Hidden if not assigned or no endless settings exist.")]
        [SerializeField] private Button endlessButton;

        [Tooltip("Best endless time and level.")]
        [SerializeField] private TMP_Text endlessRecordText;

        /// <summary>Raised with the index of the chosen difficulty.</summary>
        public event Action<int> DifficultySelected;

        public event Action BenchmarkSelected;

        public event Action EndlessSelected;

        public void Bind(IReadOnlyList<DifficultySettings> difficulties, bool endlessAvailable)
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

            benchmarkButton.onClick.RemoveAllListeners();
            benchmarkButton.onClick.AddListener(() => BenchmarkSelected?.Invoke());

            if (endlessButton != null)
            {
                endlessButton.gameObject.SetActive(endlessAvailable);
                endlessButton.onClick.RemoveAllListeners();
                endlessButton.onClick.AddListener(() => EndlessSelected?.Invoke());
            }

            if (endlessRecordText != null)
            {
                endlessRecordText.gameObject.SetActive(endlessAvailable);
            }
        }

        public void Show(int totalKills, float bestEndlessSeconds, int bestEndlessLevel)
        {
            totalKillsText.text = $"Total kills: {totalKills}";

            if (endlessRecordText != null)
            {
                endlessRecordText.text = bestEndlessSeconds > 0f
                    ? $"Best: {TimeFormat.MinutesSeconds(TimeFormat.ElapsedSeconds(bestEndlessSeconds))}  |  Lv {bestEndlessLevel}"
                    : "No timer. Level up and pick upgrades.";
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
