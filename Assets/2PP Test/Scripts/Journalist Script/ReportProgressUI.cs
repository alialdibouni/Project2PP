using TMPro;
using UnityEngine;

public class ReportProgressUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI reportsText;

    private void OnEnable()
    {
        ReporterTriggerBox.ReportProgressChanged += UpdateText;
        UpdateText(); // initial
    }

    private void OnDisable()
    {
        ReporterTriggerBox.ReportProgressChanged -= UpdateText;
    }

    private void UpdateText()
    {
        if (reportsText == null) return;
        int total = ReporterTriggerBox.TotalReports;
        int done = ReporterTriggerBox.CompletedReports;
        // If you want "remaining" instead, use: int remaining = total - done;
        reportsText.text = $"Reports remaining: {done}/{total}"; 
    }
}