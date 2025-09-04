using UnityEngine;

public class WeaponPickup : Interactable
{
    [Header("Attach target (optional)")]
    [SerializeField] private Transform attachPoint; // If null, uses WeaponCamera or Camera.main

    [Header("Offsets when attached")]
    [SerializeField] private Vector3 localPosition; //= new Vector3(0.3f, -0.25f, 0.6f)
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;

    [Header("Layers")]
    [SerializeField] private bool moveToIgnoreRaycastLayer = true;
    [SerializeField] private string droppedLayerName = "Interactable"; // layer to use when dropped

    private bool equipped;
    private int originalLayer;
    private Rigidbody rb;
    private Collider[] cachedColliders;

    private void Awake()
    {
        originalLayer = gameObject.layer;
        rb = GetComponent<Rigidbody>();
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    protected override void Interact()
    {
        Transform root = GetAttachRoot();
        if (root == null)
        {
            Debug.LogWarning("WeaponPickup: No attach target found. Ensure a 'WeaponCamera' exists, assign Attach Point, or ensure a MainCamera exists.");
            return;
        }

        var newWeapon = GetComponent<Weapon>();
        if (newWeapon == null)
        {
            Debug.LogWarning("WeaponPickup: No Weapon component found on pickup.");
            return;
        }

        var current = GetCurrentlyEquippedWeapon(root);
        if (current != null && newWeapon.Slot != current.Slot)
        {
            bool otherSlotAlreadyOwned = HasWeaponInSlot(root, newWeapon.Slot);
            if (otherSlotAlreadyOwned)
            {
                Debug.Log($"WeaponPickup: Cannot pick up a {newWeapon.Slot} while holding a {current.Slot} (both slots present).");
                return;
            }
        }

        var heldSameSlot = FindHeldWeaponPickupInSlot(root, newWeapon.Slot);
        if (heldSameSlot != null && heldSameSlot != this)
        {
            Vector3 dropPos = transform.position;
            Quaternion dropRot = transform.rotation;
            heldSameSlot.DropTo(dropPos, dropRot);
        }

        EquipThis(root);

        var shooter = Object.FindFirstObjectByType<PlayerShoot>();
        if (shooter != null)
        {
            shooter.SetCurrentWeapon(newWeapon);
        }

        // Notify mission system generically
        MissionEventBus.RaiseWeaponEquipped(newWeapon.Slot);
    }

    private void EquipThis(Transform target)
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedColliders != null)
        {
            foreach (var col in cachedColliders) col.enabled = false;
        }

        if (moveToIgnoreRaycastLayer)
        {
            int weaponLayer = LayerMask.NameToLayer("Weapon");
            if (weaponLayer >= 0)
            {
                SetLayerRecursively(gameObject, weaponLayer);
            }
            else
            {
                Debug.LogWarning("WeaponPickup: 'Weapon' layer not found. Keeping current layer.");
            }
        }

        transform.SetParent(target, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);

        var weaponComp = GetComponent<Weapon>();
        if (weaponComp != null) weaponComp.OnEquip();

        equipped = true;
        promptMessage = string.Empty;

        enabled = false;
    }

    public void DropTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        var weaponComp = GetComponent<Weapon>();
        if (weaponComp != null) weaponComp.OnUnequip();

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (cachedColliders != null)
        {
            foreach (var col in cachedColliders) col.enabled = true;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        int targetLayer = LayerMask.NameToLayer(droppedLayerName);
        if (targetLayer < 0) targetLayer = originalLayer;
        SetLayerRecursively(gameObject, targetLayer);

        equipped = false;
        promptMessage = "Pick up " + gameObject.name;
        enabled = true;
    }

    private Transform GetAttachRoot()
    {
        if (attachPoint != null) return attachPoint;

        var weaponCam = GameObject.Find("WeaponCamera");
        if (weaponCam != null) return weaponCam.transform;

        return Camera.main != null ? Camera.main.transform : null;
    }

    private WeaponPickup FindHeldWeaponPickupInSlot(Transform root, WeaponSlot slot)
    {
        var weapons = root.GetComponentsInChildren<Weapon>(true);
        foreach (var w in weapons)
        {
            if (w.Slot == slot)
            {
                var pickup = w.GetComponent<WeaponPickup>();
                if (pickup != null) return pickup;
            }
        }
        return null;
    }

    private Weapon GetCurrentlyEquippedWeapon(Transform root)
    {
        var weapons = root.GetComponentsInChildren<Weapon>(true);
        foreach (var w in weapons)
        {
            if (w.IsEquipped) return w;
        }
        return null;
    }

    private bool HasWeaponInSlot(Transform root, WeaponSlot slot)
    {
        var weapons = root.GetComponentsInChildren<Weapon>(true);
        foreach (var w in weapons)
        {
            if (w.Slot == slot) return true;
        }
        return false;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
