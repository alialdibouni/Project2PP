using UnityEngine;

public class ReplenishAmmo : Interactable
{
    [Header("Uses")]
    [SerializeField] private int uses = 3;

    protected override void Interact()
    {
        Transform root = GetWeaponRoot();
        if (root == null)
        {
            Debug.LogWarning("ReplenishAmmo: No WeaponCamera or MainCamera found to search for weapons.");
            return;
        }

        var weapons = root.GetComponentsInChildren<Weapon>(true);
        if (weapons.Length == 0)
        {
            Debug.Log("ReplenishAmmo: No weapons found to refill.");
            return;
        }

        bool anyRefilled = false;

        for (int i = 0; i < weapons.Length; i++)
        {
            var w = weapons[i];
            bool needsRefill = w.AmmoInMagazine < w.MagazineSize || w.ReserveAmmo < w.MaxReserveAmmo;
            if (needsRefill)
            {
                w.RefillAllAmmo();
                anyRefilled = true;
            }
        }

        // If nothing needed a refill, do not consume a use
        if (!anyRefilled)
        {
            Debug.Log("ReplenishAmmo: Ammo already full. No use consumed.");
            return;
        }

        uses = Mathf.Max(0, uses - 1);
        promptMessage = uses > 0 ? $"Replenish Ammo ({uses} left)" : string.Empty;

        if (uses <= 0)
        {
            Destroy(gameObject);
        }
    }

    private Transform GetWeaponRoot()
    {
        var wc = GameObject.Find("WeaponCamera");
        if (wc != null) return wc.transform;
        return Camera.main != null ? Camera.main.transform : null;
    }
}
