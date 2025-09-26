using System.Collections.Generic;
using UnityEngine;

public class Radio : MonoBehaviour
{
    public List<AudioClip> radioSongs; // List of audio clips for radio stations
    public AudioSource audioSource;
    private int lastSongIndex = -1;

    [Header("Behavior")]
    [SerializeField] private bool autoStart = true; // allow disabling auto play
    [SerializeField] private bool resumeNextTrackWhileActive = true; // when active, keep picking new songs

    // ADD: fade-out support
    [Header("Fade")]
    [SerializeField] private float fadeOutDuration = 0.6f;
    private Coroutine _fadeRoutine;
    private float _originalVolume = 1f;

    // Runtime flag controlling whether the radio should be playing/advancing
    private bool _isActive;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            _originalVolume = audioSource.volume <= 0f ? 1f : audioSource.volume;
        }
    }

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        Random.InitState(System.DateTime.Now.Millisecond);

        _isActive = autoStart;
        if (_isActive)
            PlayRandomSong();
    }

    void Update()
    {
        if (!_isActive) return;

        if (resumeNextTrackWhileActive &&
            audioSource != null && audioSource.enabled &&
            !audioSource.isPlaying &&
            radioSongs != null && radioSongs.Count > 0)
        {
            PlayRandomSong();
        }
    }

    public void ActivateRadio()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // cancel any ongoing fade
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _isActive = true;
        audioSource.volume = _originalVolume;

        if (!audioSource.isPlaying)
            PlayRandomSong();
    }

    public void DeactivateRadio()
    {
        _isActive = false;

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // ADD: fade-out APIs for UnityEvents
    public void FadeOutAndStop()
    {
        FadeOutAndStopCustom(fadeOutDuration);
    }

    public void FadeOutAndStopCustom(float duration)
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) return;

        _isActive = false;

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(CoFadeOut(duration));
    }

    private System.Collections.IEnumerator CoFadeOut(float duration)
    {
        float startVol = audioSource.volume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? t / duration : 1f;
            audioSource.volume = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        audioSource.volume = _originalVolume; // restore for next activation
        _fadeRoutine = null;
    }

    // Play a random song, avoiding immediate repeats
    private void PlayRandomSong()
    {
        if (radioSongs == null || radioSongs.Count == 0 || audioSource == null)
            return;

        int randomIndex;
        do
        {
            randomIndex = Random.Range(0, radioSongs.Count);
        } while (radioSongs.Count > 1 && randomIndex == lastSongIndex);

        audioSource.clip = radioSongs[randomIndex];
        audioSource.Play();
        lastSongIndex = randomIndex;
    }
}
