using UnityEngine;
using System;

public class MissionManager : MonoBehaviour
{
    [Serializable]
    public class MissionStep
    {
        public string id; // optional for manual completion
        [TextArea] public string description;

        // Conditions to complete this step (all must complete). If empty, step is manual.
        public ObjectiveCondition[] conditions;

        // If true and there are conditions, advance automatically when all are done.
        public bool autoAdvanceWhenAllConditionsMet = true;
    }

    [Header("Steps (order matters)")]
    public MissionStep[] steps;

    private int currentStep = 0;
    private int completedConditions = 0;

    public static MissionManager Instance;

    public event Action<string> OnMissionUpdated;

    public bool IsComplete => steps == null || steps.Length == 0 || currentStep >= steps.Length;
    public string CurrentStepId => IsComplete ? null : steps[currentStep].id;
    public string CurrentStepDescription => IsComplete ? "All missions complete!" : steps[currentStep].description;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ActivateCurrentStep();
        ShowCurrentMission();
    }

    private void OnDestroy()
    {
        DeactivateCurrentStep();
    }

    // Manual completion (only valid when step has no conditions)
    public bool TryCompleteMissionStep(string id)
    {
        if (IsComplete) return false;

        var step = steps[currentStep];
        bool isManual = step.conditions == null || step.conditions.Length == 0;
        if (!isManual) return false;

        if (!string.Equals(id, step.id, StringComparison.Ordinal))
        {
            return false;
        }

        Advance();
        return true;
    }

    public void CompleteStepById(string id) => TryCompleteMissionStep(id);

    private void Advance()
    {
        DeactivateCurrentStep();

        currentStep++;
        if (currentStep < (steps?.Length ?? 0))
        {
            ActivateCurrentStep();
            ShowCurrentMission();
        }
        else
        {
            Debug.Log("All missions complete!");
            OnMissionUpdated?.Invoke("All missions complete!");
        }
    }

    private void ActivateCurrentStep()
    {
        if (IsComplete) return;

        completedConditions = 0;

        var step = steps[currentStep];
        if (step.conditions != null)
        {
            foreach (var cond in step.conditions)
            {
                if (cond == null) continue;
                cond.ResetProgress();
                cond.Completed += OnConditionCompleted;
                cond.Activate();
            }
        }
    }

    private void DeactivateCurrentStep()
    {
        if (IsComplete) return;

        var step = steps[currentStep];
        if (step.conditions != null)
        {
            foreach (var cond in step.conditions)
            {
                if (cond == null) continue;
                cond.Completed -= OnConditionCompleted;
                cond.Deactivate();
            }
        }
    }

    private void OnConditionCompleted(ObjectiveCondition _)
    {
        if (IsComplete) return;

        completedConditions++;
        var step = steps[currentStep];
        int total = step.conditions != null ? step.conditions.Length : 0;

        if (step.autoAdvanceWhenAllConditionsMet && total > 0 && completedConditions >= total)
        {
            Advance();
        }
    }

    private void ShowCurrentMission()
    {
        if (IsComplete)
        {
            OnMissionUpdated?.Invoke("All missions complete!");
            return;
        }

        string mission = steps[currentStep].description;
        Debug.Log("Current Mission: " + mission);
        OnMissionUpdated?.Invoke(mission);
    }
}
