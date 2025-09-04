using UnityEngine;
using System;

public class MissionManager : MonoBehaviour
{
    public string[] missionSteps;   // Example: {"Touch the cube", "Enter the building", "Pick up weapon"}
    private int currentStep = 0;

    public static MissionManager Instance;

    public event Action<string> OnMissionUpdated;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ShowCurrentMission();
    }

    public void CompleteMissionStep()
    {
        currentStep++;

        if (currentStep < missionSteps.Length)
        {
            ShowCurrentMission();
        }
        else
        {
            Debug.Log("All missions complete!");
            OnMissionUpdated?.Invoke("All missions complete!");
        }
    }

    private void ShowCurrentMission()
    {
        string mission = missionSteps[currentStep];
        Debug.Log("Current Mission: " + mission);
        OnMissionUpdated?.Invoke(mission);
    }
}
