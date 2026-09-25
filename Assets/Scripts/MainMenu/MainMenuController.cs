using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Top-level controller for the Main Menu scene: wires the Play/Quit buttons.
// Play hands off to the Garage (shop/loadout) scene, same as GarageController
// hands off to the coaster scene -- keeps the MainMenu -> Garage -> coaster
// flow each scene's controller only knowing the single next scene name.
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private string playSceneName = "Garage";

    private void Awake()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        UIButtonFeedback.Ensure(playButton.gameObject);
        UIButtonFeedback.Ensure(quitButton.gameObject);

        // Browsers and iOS have no "quit" -- hide the button rather than leave a dead control.
#if UNITY_WEBGL || UNITY_IOS
        quitButton.gameObject.SetActive(false);
#endif
    }

    private void OnPlayClicked() => SceneManager.LoadScene(playSceneName);

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
