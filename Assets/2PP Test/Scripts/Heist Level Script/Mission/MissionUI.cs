using TMPro;
using UnityEngine;

public class MissionUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI missionText;

    private void OnEnable()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionUpdated += UpdateMission;
            // Force-initialize the text immediately
            UpdateMission(MissionManager.Instance.CurrentStepDescription);
        }
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionUpdated -= UpdateMission;
    }

    private void UpdateMission(string newMission)
    {
        if (missionText != null)
            missionText.text = newMission;
    }
}
