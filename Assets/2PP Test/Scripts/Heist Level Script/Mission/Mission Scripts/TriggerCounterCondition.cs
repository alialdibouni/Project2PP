using UnityEngine;

[CreateAssetMenu(menuName = "Mission/Conditions/Trigger Counter")]
public class TriggerCounterCondition : ObjectiveCondition
{
    [SerializeField] public string triggerId;
    [SerializeField] public int requiredCount = 1;

    private int _count;

    public override void ResetProgress()
    {
        base.ResetProgress();
        _count = 0;
    }

    public override void Activate()
    {
        MissionEventBus.TriggerFired += OnTrigger;
    }

    public override void Deactivate()
    {
        MissionEventBus.TriggerFired -= OnTrigger;
    }

    private void OnTrigger(string id)
    {
        if (IsCompleted) return;
        if (string.IsNullOrEmpty(triggerId) || id != triggerId) return;

        _count++;
        if (_count >= Mathf.Max(1, requiredCount))
        {
            Complete();
        }
    }
}