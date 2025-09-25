using System.Collections;
using UnityEngine;

public class BackgrondMusic : MonoBehaviour
{
    private static BackgrondMusic _instance;
    public static BackgrondMusic Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<BackgrondMusic>();
            return _instance;
        }
    }

    [Header("Audio Sources (assign in Inspector)")]
    public AudioSource backgroundSource;
    public AudioSource chaseSource;

    [Header("Volumes")]
    [Range(0f, 1f)] public float backgroundVolume = 0.3f;
    [Range(0f, 1f)] public float chaseVolume = 0.3f;
    [Range(0f, 1f)] public float backgroundVolumeDuringChase = 0f;

    [Header("Behavior")]
    public float fadeDuration = 1.5f;
    public bool playBackgroundOnStart = true;

    private Coroutine _fadeCoroutine;
    private int _chaseRefCount = 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (backgroundSource != null)
        {
            backgroundSource.loop = true;
            //backgroundSource.volume = backgroundVolume;
            if (playBackgroundOnStart && !backgroundSource.isPlaying)
                backgroundSource.Play();
        }

        if (chaseSource != null)
        {
            chaseSource.loop = true;
            // Start silent; we fade in when chase begins
            chaseSource.volume = 0f;
            if (chaseSource.isPlaying)
                chaseSource.Stop();
        }
    }

    public void BeginChase(float? duration = null)
    {
        _chaseRefCount++;
        if (_chaseRefCount == 1)
        {
            StartCrossfade(
                chaseTarget: chaseVolume,
                backgroundTarget: backgroundVolumeDuringChase,
                duration: duration ?? fadeDuration,
                ensurePlayChase: true,
                ensurePlayBackground: true
            );
        }
    }

    public void EndChase(float? duration = null)
    {
        _chaseRefCount = Mathf.Max(0, _chaseRefCount - 1);
        if (_chaseRefCount == 0)
        {
            StartCrossfade(
                chaseTarget: 0f,
                backgroundTarget: backgroundVolume,
                duration: duration ?? fadeDuration,
                ensurePlayChase: false,
                ensurePlayBackground: true
            );
        }
    }

    private void StartCrossfade(float chaseTarget, float backgroundTarget, float duration, bool ensurePlayChase, bool ensurePlayBackground)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(CrossfadeRoutine(chaseTarget, backgroundTarget, duration, ensurePlayChase, ensurePlayBackground));
    }

    private IEnumerator CrossfadeRoutine(float chaseTarget, float backgroundTarget, float duration, bool ensurePlayChase, bool ensurePlayBackground)
    {
        float startChase = chaseSource ? chaseSource.volume : 0f;
        float startBg = backgroundSource ? backgroundSource.volume : 0f;

        if (ensurePlayChase && chaseSource != null && !chaseSource.isPlaying)
            chaseSource.Play();

        if (ensurePlayBackground && backgroundSource != null && !backgroundSource.isPlaying)
            backgroundSource.Play();

        float t = 0f;
        float d = Mathf.Max(0.0001f, duration);

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);

            if (chaseSource) chaseSource.volume = Mathf.Lerp(startChase, chaseTarget, k);
            if (backgroundSource) backgroundSource.volume = Mathf.Lerp(startBg, backgroundTarget, k);

            yield return null;
        }

        if (chaseSource) chaseSource.volume = chaseTarget;
        if (backgroundSource) backgroundSource.volume = backgroundTarget;

        if (chaseSource && chaseTarget <= 0f)
            chaseSource.Stop();

        _fadeCoroutine = null;
    }
}
