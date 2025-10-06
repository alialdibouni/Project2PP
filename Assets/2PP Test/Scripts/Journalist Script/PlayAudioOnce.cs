using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class PlayAudioOnce : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip _clip;
    [Range(0f, 1f)] [SerializeField] private float _volume = 1f;

    [Header("Trigger Filtering")]
    [SerializeField] private string _playerTag = "Player";

    [Header("Lifecycle")]
    [SerializeField] private bool _disableColliderAfterPlay = true;
    [SerializeField] private bool _destroyAfterPlay = false;

    [Header("Persistence (Optional)")]
    [Tooltip("If not empty, audio will only play once across sessions (uses PlayerPrefs).")]
    [SerializeField] private string _playerPrefsKey = "";

    [Header("UI Prompt")]
    [SerializeField] private bool _showPrompt = true;
    [TextArea]
    [SerializeField] private string _promptMessage = "To distract a guard, press 'H' to whistle";

    private bool _played;
    private AudioSource _audioSource;
    private Collider _col;

    // UI tracking
    private PlayerUI _playerUI;
    private string _previousPrompt;
    private bool _appliedPrompt;
    private Coroutine _promptClearRoutine;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;

        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        if (_clip != null)
        {
            _audioSource.clip = _clip;
        }

        if (!string.IsNullOrEmpty(_playerPrefsKey))
        {
            _played = PlayerPrefs.GetInt(_playerPrefsKey, 0) == 1;
            if (_played && _disableColliderAfterPlay) _col.enabled = false;
            if (_played && _destroyAfterPlay) Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(_playerTag)) return;

        // UI prompt
        if (_showPrompt)
        {
            _playerUI = other.GetComponentInParent<PlayerUI>();
            if (_playerUI != null && !_appliedPrompt)
            {
                _previousPrompt = null; // we clear when done
                _playerUI.UpdateText(_promptMessage);
                _appliedPrompt = true;
            }
        }

        if (_played) return;

        _played = true;

        if (_clip != null)
        {
            _audioSource.PlayOneShot(_clip, _volume);

            // Schedule clearing the prompt exactly when audio finishes (then optional destroy)
            if (_showPrompt && _playerUI != null)
            {
                if (_promptClearRoutine != null) StopCoroutine(_promptClearRoutine);
                float len = _clip.length > 0f ? _clip.length : 0.05f;
                _promptClearRoutine = StartCoroutine(ClearPromptAfter(len));
            }
            else if (_destroyAfterPlay)
            {
                // No prompt to manage; just destroy after clip
                float len = _clip.length > 0f ? _clip.length : 0.05f;
                Destroy(gameObject, len + 0.01f);
            }
        }
        else
        {
            Debug.LogWarning("[PlayAudioOnce] No AudioClip assigned.", this);
            // Still clear prompt quickly if no clip
            if (_showPrompt && _playerUI != null)
            {
                if (_promptClearRoutine != null) StopCoroutine(_promptClearRoutine);
                _promptClearRoutine = StartCoroutine(ClearPromptAfter(0.05f));
            }
            else if (_destroyAfterPlay)
            {
                Destroy(gameObject);
            }
        }

        if (_disableColliderAfterPlay) _col.enabled = false;

        if (!string.IsNullOrEmpty(_playerPrefsKey))
        {
            PlayerPrefs.SetInt(_playerPrefsKey, 1);
            PlayerPrefs.Save();
        }
    }

    private IEnumerator ClearPromptAfter(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);

        if (_playerUI != null)
        {
            _playerUI.UpdateText(string.Empty);
        }
        _appliedPrompt = false;
        _previousPrompt = null;
        _promptClearRoutine = null;

        if (_destroyAfterPlay)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(_playerTag)) return;

        // If audio already scheduled prompt clear, do nothing (let coroutine handle).
        // If audio not played (player left early), restore/clear immediately.
        if (_appliedPrompt && _playerUI != null && !_played)
        {
            _playerUI.UpdateText(_previousPrompt ?? string.Empty);
        }

        _playerUI = null;
        _appliedPrompt = false;
        _previousPrompt = null;
    }
}
