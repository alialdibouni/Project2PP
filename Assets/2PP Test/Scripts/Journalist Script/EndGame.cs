using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class EndGame : MonoBehaviour
{
    [Header("UI")]
    [TextArea] [SerializeField] private string completedPrompt = "Press E to hand in reports";
    [TextArea] [SerializeField] private string incompletePrompt = "You still have reports to complete!";
    [SerializeField] private bool showIncompletePrompt = false;

    [Header("End Action")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float fadeOutDuration = 1.0f;
    [Tooltip("If set, this scene will be loaded when handing in reports; otherwise quits the app.")]
    [SerializeField] private string loadSceneOnEnd = "";

    private PlayerUI _playerUI;
    private bool _playerInside;
    private bool _ending;
    private Component _enteredMarker; // used to identify the same entering object

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnEnable()
    {
        ReporterTriggerBox.ReportProgressChanged += RefreshPrompt;
    }

    private void OnDisable()
    {
        ReporterTriggerBox.ReportProgressChanged -= RefreshPrompt;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Use presence of InputReader to identify the player (consistent with ReporterTriggerBox)
        var reader = other.GetComponentInParent<Synty.AnimationBaseLocomotion.Samples.InputSystem.InputReader>();
        if (reader == null) return;

        _enteredMarker = reader;
        _playerUI = other.GetComponentInParent<PlayerUI>();
        _playerInside = true;

        RefreshPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        var reader = other.GetComponentInParent<Synty.AnimationBaseLocomotion.Samples.InputSystem.InputReader>();
        if (reader == null || _enteredMarker == null || reader != _enteredMarker) return;

        _playerUI?.UpdateText(string.Empty);
        _playerInside = false;
        _playerUI = null;
        _enteredMarker = null;
    }

    private void Update()
    {
        if (!_playerInside || _ending) return;
        if (!AllReportsComplete()) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCoroutine(EndRoutine());
        }
    }

    private void RefreshPrompt()
    {
        if (!_playerInside || _playerUI == null) return;

        if (AllReportsComplete())
        {
            _playerUI.UpdateText(completedPrompt);
        }
        else
        {
            if (showIncompletePrompt)
                _playerUI.UpdateText(incompletePrompt);
            else
                _playerUI.UpdateText(string.Empty);
        }
    }

    private static bool AllReportsComplete()
    {
        int total = ReporterTriggerBox.TotalReports;
        int done = ReporterTriggerBox.CompletedReports;
        return total > 0 && done >= total;
    }

    private IEnumerator EndRoutine()
    {
        _ending = true;
        _playerUI?.UpdateText(string.Empty);

        if (screenFader != null && fadeOutDuration > 0f)
        {
            yield return screenFader.FadeOut(fadeOutDuration);
        }

        if (!string.IsNullOrEmpty(loadSceneOnEnd))
        {
            SceneManager.LoadScene(loadSceneOnEnd);
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
