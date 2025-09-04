using System;
using UnityEngine;

public static class MissionEventBus
{
    public static event Action<string> TriggerFired;
    public static void RaiseTrigger(string id) => TriggerFired?.Invoke(id);

    public static event Action<WeaponSlot> WeaponEquipped;
    public static void RaiseWeaponEquipped(WeaponSlot slot) => WeaponEquipped?.Invoke(slot);
}
