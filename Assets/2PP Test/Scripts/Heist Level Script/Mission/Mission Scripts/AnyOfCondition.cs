using UnityEngine;

[CreateAssetMenu(menuName = "Mission/Conditions/Any Of")]
public class AnyOfCondition : ObjectiveCondition
{
    [SerializeField] public ObjectiveCondition[] children;

    public override void ResetProgress()
    {
        base.ResetProgress();
        if (children == null) return;
        foreach (var c in children)
            c?.ResetProgress();
    }

    public override void Activate()
    {
        if (children == null) return;
        foreach (var c in children)
        {
            if (c == null) continue;
            c.Completed += OnChildCompleted;
            c.Activate();
        }
    }

    public override void Deactivate()
    {
        if (children == null) return;
        foreach (var c in children)
        {
            if (c == null) continue;
            c.Completed -= OnChildCompleted;
            c.Deactivate();
        }
    }

    private void OnChildCompleted(ObjectiveCondition _)
    {
        if (IsCompleted) return;
        // Complete this composite; MissionManager will deactivate children
        Complete();
    }
}