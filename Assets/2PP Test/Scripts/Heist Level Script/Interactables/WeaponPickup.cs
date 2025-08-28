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

    private static WeaponPickup currentlyEquipped;

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
        Transform target = attachPoint != null
            ? attachPoint
            : (Camera.main != null ? Camera.main.transform : null);

        if (target == null)
        {
            Debug.LogWarning("WeaponPickup: No attach target found. Assign Attach Point or ensure a MainCamera exists.");
            return;
        }

        if (currentlyEquipped == null)
        {
            EquipThis(target);
        }
        else
        {
            Vector3 dropPos = transform.position;
            Quaternion dropRot = transform.rotation;

            // Drop the currently held weapon at this pickup's spot
            currentlyEquipped.DropTo(dropPos, dropRot);

            // Equip this one
            EquipThis(target);
        }
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
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            if (ignore >= 0) SetLayerRecursively(gameObject, ignore);
        }

        transform.SetParent(target, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);

        var weaponComp = GetComponent<Weapon>();
        if (weaponComp != null) weaponComp.OnEquip();

        equipped = true;
        promptMessage = string.Empty;
        currentlyEquipped = this;
        enabled = false; // prevent interacting with the held weapon
    }

    private void DropTo(Vector3 worldPosition, Quaternion worldRotation)
    {
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

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
