using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionUI : MonoBehaviour
{
    public TextMeshProUGUI missionText;

    private void Start()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionUpdated += UpdateMission;
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionUpdated -= UpdateMission;
    }

    private void UpdateMission(string newMission)
    {
        missionText.text = newMission;
    }
}
