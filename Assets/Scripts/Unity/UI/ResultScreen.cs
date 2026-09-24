using System;
using ArenaSurvivor.Core.Presentation;
using ArenaSurvivor.Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>
    /// Shown when a run ends: win/lose title, kills, survived time, lifetime kills, replay and menu.
    /// After an endless run: "GAME OVER", time and level reached, and the best endless run.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text survivedText;
        [SerializeField] private TMP_Text totalKillsText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button menuButton;

        [Tooltip("Endless only: best run, or \"NEW RECORD!\". Optional.")]
        [SerializeField] private TMP_Text recordText;

        [SerializeField] private Color winColor = new Color(0.45f, 0.9f, 0.45f);
        [SerializeField] private Color loseColor = new Color(0.95f, 0.35f, 0.3f);
        [SerializeField] private Color recordColor = new Color(1f, 0.82f, 0.25f);

        public event Action ReplayClicked;
        public event Action MenuClicked;

        private void Awake()
        {
            replayButton.onClick.AddListener(() => ReplayClicked?.Invoke());
            menuButton.onClick.AddListener(() => MenuClicked?.Invoke());
        }

        public void Show(RunResult result, int totalKills)
        {
            bool won = result.Outcome == GameState.Won;
            titleText.text = won ? "YOU SURVIVED" : "YOU DIED";
            titleText.color = won ? winColor : loseColor;
            killsText.text = $"Kills: {result.Kills}";
            survivedText.text = $"Survived: {TimeFormat.MinutesSeconds(TimeFormat.ElapsedSeconds(result.SurvivedSeconds))}";
            totalKillsText.text = $"Total kills: {totalKills}";
            if (recordText != null)
            {
                recordText.gameObject.SetActive(false);
            }

            gameObject.SetActive(true);
        }

        /// <summary>Result of an endless run. It always ends with the player's death.</summary>
        public void ShowEndless(RunResult result, int level, int totalKills, bool isRecord, float bestSeconds, int bestLevel)
        {
            titleText.text = "GAME OVER";
            titleText.color = loseColor;
            killsText.text = $"Kills: {result.Kills}";
            survivedText.text =
                $"Survived: {TimeFormat.MinutesSeconds(TimeFormat.ElapsedSeconds(result.SurvivedSeconds))}  |  Level {level}";
            totalKillsText.text = $"Total kills: {totalKills}";

            if (recordText != null)
            {
                recordText.text = isRecord
                    ? "NEW RECORD!"
                    : $"Best: {TimeFormat.MinutesSeconds(TimeFormat.ElapsedSeconds(bestSeconds))}  |  Lv {bestLevel}";
                recordText.color = isRecord ? recordColor : Color.white;
                recordText.gameObject.SetActive(true);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
