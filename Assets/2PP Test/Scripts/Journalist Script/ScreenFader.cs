using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Screen Fader (Image)")]
public class ScreenFader : MonoBehaviour
{
    [Tooltip("Fullscreen Image whose color alpha will be animated (0 = clear, 1 = black).")]
    [SerializeField] private Image fadeImage;

    [Tooltip("Automatically disable raycast target while transparent.")]
    [SerializeField] private bool manageRaycastTarget = true;

    private Coroutine currentFade;

    private void Awake()
    {
        if (fadeImage == null)
            fadeImage = GetComponentInChildren<Image>();

        if (fadeImage == null)
        {
            Debug.LogWarning("[ScreenFader] No Image found. Creating one.");
            var go = new GameObject("FadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            fadeImage = go.GetComponent<Image>();
            fadeImage.color = Color.black.WithAlpha(0f);
        }
        else
        {
            // Ensure starts fully transparent (preserve RGB)
            var c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }

        if (manageRaycastTarget)
            fadeImage.raycastTarget = false;
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return StartFade(1f, duration);
    }

    public IEnumerator FadeIn(float duration)
    {
        yield return StartFade(0f, duration);
    }

    private IEnumerator StartFade(float targetAlpha, float duration)
    {
        if (currentFade != null)
            StopCoroutine(currentFade);
        currentFade = StartCoroutine(FadeRoutine(targetAlpha, duration));
        yield return currentFade;
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (fadeImage == null) yield break;

        float startAlpha = fadeImage.color.a;
        float t = 0f;
        if (manageRaycastTarget && targetAlpha > startAlpha)
            fadeImage.raycastTarget = true;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? t / duration : 1f;
            float a = Mathf.Lerp(startAlpha, targetAlpha, k);
            var c = fadeImage.color;
            c.a = a;
            fadeImage.color = c;
            yield return null;
        }

        var final = fadeImage.color;
        final.a = targetAlpha;
        fadeImage.color = final;

        if (manageRaycastTarget && Mathf.Approximately(targetAlpha, 0f))
            fadeImage.raycastTarget = false;

        currentFade = null;
    }
}

internal static class ColorExtensions
{
    public static Color WithAlpha(this Color c, float a)
    {
        c.a = a;
        return c;
    }
}