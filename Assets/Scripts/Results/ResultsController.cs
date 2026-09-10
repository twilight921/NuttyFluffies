using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Reveals the post-ride results overlay once the lead cart's RunEndTrigger
// fires: deposits this run's RunStats.Coins into the persisted GarageSave
// balance -- closing the loop GarageSave's own header comment flagged as
// unwired -- then shows a Score/Coins summary with a Continue button back to
// the Garage. Lives as a normally-hidden overlay Canvas inside SampleScene
// itself (not a separate scene) because RunStats is scene-scoped: loading
// another scene before reading it would lose the run's tally.
//
// Named handler method (not a lambda) for the RunEndTrigger subscription, per
// project convention -- see CoasterCart/PowerUpCart/CreaturePassenger.
public class ResultsController : MonoBehaviour
{
    [SerializeField] private RunEndTrigger runEndTrigger;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private TMP_Text coinsLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private string garageSceneName = "Garage";

    private bool _isSubscribed;

    private void Awake()
    {
        if (resultsPanel != null) resultsPanel.SetActive(false);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void OnEnable()
    {
        if (runEndTrigger != null && !_isSubscribed)
        {
            runEndTrigger.OnRunEnd += HandleRunEnd;
            _isSubscribed = true;
        }
    }

    private void OnDisable()
    {
        if (_isSubscribed && runEndTrigger != null)
        {
            runEndTrigger.OnRunEnd -= HandleRunEnd;
            _isSubscribed = false;
        }
    }

    private void HandleRunEnd()
    {
        int score = 0;
        int coins = 0;
        if (RunStats.Instance != null)
        {
            score = RunStats.Instance.Score;
            coins = RunStats.Instance.Coins;
        }

        GarageSave.AddCoins(coins);

        if (scoreLabel != null) scoreLabel.text = $"Score: {score}";
        if (coinsLabel != null) coinsLabel.text = $"Coins: {coins}";
        if (resultsPanel != null) resultsPanel.SetActive(true);
    }

    private void OnContinueClicked() => SceneManager.LoadScene(garageSceneName);
}
