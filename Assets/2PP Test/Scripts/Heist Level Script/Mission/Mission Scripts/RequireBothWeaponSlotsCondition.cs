using UnityEngine;

[CreateAssetMenu(menuName = "Mission/Conditions/Require Both Weapon Slots")]
public class RequireBothWeaponSlotsCondition : ObjectiveCondition
{
    private bool _gotPrimary, _gotSecondary;

    public override void ResetProgress()
    {
        base.ResetProgress();
        _gotPrimary = false;
        _gotSecondary = false;
    }

    public override void Activate()
    {
        MissionEventBus.WeaponEquipped += OnEquipped;
    }

    public override void Deactivate()
    {
        MissionEventBus.WeaponEquipped -= OnEquipped;
    }

    private void OnEquipped(WeaponSlot slot)
    {
        if (IsCompleted) return;

        if (slot == WeaponSlot.Primary) _gotPrimary = true;
        else if (slot == WeaponSlot.Secondary) _gotSecondary = true;

        if (_gotPrimary && _gotSecondary) Complete();
    }
}
