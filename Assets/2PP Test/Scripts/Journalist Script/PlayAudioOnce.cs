using UnityEngine;

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

    private bool _played;
    private AudioSource _audioSource;
    private Collider _col;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;

        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;

        if (_clip != null)
        {
            // Assign clip so we can optionally use Play() (but we still use PlayOneShot for safety).
            _audioSource.clip = _clip;
        }

        if (!string.IsNullOrEmpty(_playerPrefsKey))
        {
            _played = PlayerPrefs.GetInt(_playerPrefsKey, 0) == 1;
            if (_played && _disableColliderAfterPlay) _col.enabled = false;
            // Optionally destroy if already consumed in a previous session
            if (_played && _destroyAfterPlay) Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_played) return;
        if (!other.CompareTag(_playerTag)) return;

        _played = true;

        if (_clip != null)
        {
            _audioSource.PlayOneShot(_clip, _volume);
            if (_destroyAfterPlay)
            {
                // Destroy after clip duration (fallback small delay if length == 0)
                float t = _clip.length > 0f ? _clip.length : 0.1f;
                Destroy(gameObject, t + 0.05f);
            }
        }
        else
        {
            Debug.LogWarning("[PlayAudioOnce] No AudioClip assigned.", this);
        }

        if (_disableColliderAfterPlay) _col.enabled = false;

        if (!string.IsNullOrEmpty(_playerPrefsKey))
        {
            PlayerPrefs.SetInt(_playerPrefsKey, 1);
            PlayerPrefs.Save();
        }
    }
}
