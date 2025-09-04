using UnityEngine;

[CreateAssetMenu(menuName = "Mission/Conditions/Require Trigger Id")]
public class RequireTriggerIdCondition : ObjectiveCondition
{
    [SerializeField] public string triggerId;

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
        if (!string.IsNullOrEmpty(triggerId) && id == triggerId)
        {
            Complete();
        }
    }
}
