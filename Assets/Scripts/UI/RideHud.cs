using UnityEngine;
using TMPro;

// Live ride HUD: mirrors RunStats' Score (displayed as "Hearts") and Coins
// onto two TMP labels while the coaster is running. RunStats.Instance may not
// be assigned yet when this component's own Awake runs (scene object
// creation order isn't guaranteed), so resolving it -- and subscribing to its
// change events -- is deferred to Start and retried from Update until it
// succeeds. Named handler methods only, per project convention (no lambdas).
public class RideHud : MonoBehaviour
{
    [SerializeField] private TMP_Text heartsLabel;
    [SerializeField] private TMP_Text coinsLabel;

    private RunStats _runStats;

    private void Start()
    {
        TryResolveRunStats();
    }

    private void Update()
    {
        if (_runStats == null) TryResolveRunStats();
    }

    private void TryResolveRunStats()
    {
        _runStats = RunStats.Instance;
        if (_runStats == null) return; // not ready yet -- Update will retry

        _runStats.OnScoreChanged += HandleScoreChanged;
        _runStats.OnCoinsChanged += HandleCoinsChanged;

        // Show current values immediately rather than waiting for the first
        // change event.
        HandleScoreChanged(_runStats.Score);
        HandleCoinsChanged(_runStats.Coins);
    }

    private void HandleScoreChanged(int newScore)
    {
        if (heartsLabel != null) heartsLabel.text = $"Hearts: {newScore}";
    }

    private void HandleCoinsChanged(int newCoins)
    {
        if (coinsLabel != null) coinsLabel.text = $"Coins: {newCoins}";
    }

    private void OnDestroy()
    {
        if (_runStats == null) return;
        _runStats.OnScoreChanged -= HandleScoreChanged;
        _runStats.OnCoinsChanged -= HandleCoinsChanged;
    }
}
