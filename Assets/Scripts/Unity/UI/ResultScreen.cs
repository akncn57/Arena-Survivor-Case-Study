using System;
using ArenaSurvivor.Core.Presentation;
using ArenaSurvivor.Core.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>Shown when a run ends: win/lose title, kills, survived time, lifetime kills, replay and menu.</summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text survivedText;
        [SerializeField] private TMP_Text totalKillsText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button menuButton;

        [SerializeField] private Color winColor = new Color(0.45f, 0.9f, 0.45f);
        [SerializeField] private Color loseColor = new Color(0.95f, 0.35f, 0.3f);

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
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
