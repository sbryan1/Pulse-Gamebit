using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a settings panel with a Music toggle and a Move Sounds toggle.
/// Use the same prefab in the Main Menu scene and the Board scene.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;          // the settings window (starts hidden)
    [SerializeField] private Button openButton;         // the gear / "Settings" button
    [SerializeField] private Button closeButton;        // "X" or "Back" inside the panel
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle moveSoundsToggle;

    [Header("Behaviour")]
    [Tooltip("Close the panel with the Escape key / Android back button.")]
    [SerializeField] private bool closeOnEscape = true;

    /// <summary>True while the panel is open — check this in your board input
    /// code so clicks on the panel don't also select pieces.</summary>
    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        if (openButton) openButton.onClick.AddListener(Open);
        if (closeButton) closeButton.onClick.AddListener(Close);

        musicToggle.onValueChanged.AddListener(on => GameAudioSettings.MusicEnabled = on);
        moveSoundsToggle.onValueChanged.AddListener(on => GameAudioSettings.MoveSoundsEnabled = on);

        SetPanel(false);
    }

    private void Update()
    {
        if (closeOnEscape && IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public void Open()
    {
        // sync toggles with saved values without firing their callbacks
        musicToggle.SetIsOnWithoutNotify(GameAudioSettings.MusicEnabled);
        moveSoundsToggle.SetIsOnWithoutNotify(GameAudioSettings.MoveSoundsEnabled);
        SetPanel(true);
    }

    public void Close() => SetPanel(false);

    private void SetPanel(bool open)
    {
        panel.SetActive(open);
        IsOpen = open;
    }

    private void OnDestroy()
    {
        IsOpen = false; // reset when leaving the scene
    }
}
