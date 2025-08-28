using UnityEngine;

public class WeaponPickup : Interactable
{
    [Header("Attach target (optional)")]
    [SerializeField] private Transform attachPoint; // If null, uses Camera.main

    [Header("Offsets when attached")]
    [SerializeField] private Vector3 localPosition = new Vector3(0.3f, -0.25f, 0.6f);
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
            Debug.LogWarning("WeaponPickup: No attach target found. Assign Attach Point or ensure a MainCamera exists.");
            return;
        }

        var newWeapon = GetComponent<Weapon>();
        if (newWeapon == null)
        {
            Debug.LogWarning("WeaponPickup: No Weapon component found on pickup.");
            return;
        }

        // If we already have a weapon in this slot under the attach root, drop it (swap same-slot).
        var heldSameSlot = FindHeldWeaponPickupInSlot(root, newWeapon.Slot);
        if (heldSameSlot != null && heldSameSlot != this)
        {
            Vector3 dropPos = transform.position;
            Quaternion dropRot = transform.rotation;
            heldSameSlot.DropTo(dropPos, dropRot);
        }

        // Equip this weapon (parent to camera/hold point)
        EquipThis(root);

        // Prefer making the newly picked weapon the active one so player can switch back if desired.
        var shooter = FindObjectOfType<PlayerShoot>();
        if (shooter != null)
        {
            shooter.SetCurrentWeapon(newWeapon);
        }
    }

    private void EquipThis(Transform target)
    {
        // Stop physics from fighting the camera
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

        // Disable all colliders so it won't collide or block interaction rays
        if (cachedColliders != null)
        {
            foreach (var col in cachedColliders) col.enabled = false;
        }

        // Optionally move to Ignore Raycast layer
        if (moveToIgnoreRaycastLayer)
        {
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            if (ignore >= 0) SetLayerRecursively(gameObject, ignore);
        }

        // Parent to camera/attach point and set offsets
        transform.SetParent(target, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);

        // Notify weapon it’s now in hands (PlayerShoot may also call OnEquip, which is harmless)
        var weaponComp = GetComponent<Weapon>();
        if (weaponComp != null) weaponComp.OnEquip();

        equipped = true;
        promptMessage = string.Empty;

        // Prevent interacting with the held weapon object
        enabled = false;
    }

    public void DropTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        // Restore colliders
        if (cachedColliders != null)
        {
            foreach (var col in cachedColliders) col.enabled = true;
        }

        // Restore physics
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Restore to the Interactable layer (or original if missing)
        int targetLayer = LayerMask.NameToLayer(droppedLayerName);
        if (targetLayer < 0) targetLayer = originalLayer;
        SetLayerRecursively(gameObject, targetLayer);

        var weaponComp = GetComponent<Weapon>();
        if (weaponComp != null) weaponComp.OnUnequip();

        equipped = false;
        promptMessage = "Pick up " + gameObject.name;
        enabled = true;
    }

    private Transform GetAttachRoot()
    {
        if (attachPoint != null) return attachPoint;
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

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
