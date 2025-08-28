using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [Header("How we find the equipped weapon(s)")]
    [SerializeField] private Transform searchRoot;          // If null, defaults to camera transform
    [SerializeField] private float recheckInterval = 0.25f; // How often we scan for weapons under the camera

    private InputManager inputManager;
    private PlayerLook playerLook;

    private Weapon primaryWeapon;
    private Weapon secondaryWeapon;
    private Weapon currentWeapon;

    private float recheckTimer;

    void Start()
    {
        inputManager = GetComponent<InputManager>();
        playerLook = GetComponent<PlayerLook>();

        if (searchRoot == null && playerLook != null && playerLook.cam != null)
            searchRoot = playerLook.cam.transform;

        RefreshWeapons(force: true);
    }

    void Update()
    {
        // Periodically re-scan to detect newly picked up weapons
        recheckTimer += Time.deltaTime;
        if (recheckTimer >= recheckInterval)
        {
            recheckTimer = 0f;
            RefreshWeapons(force: false);
        }

        if (currentWeapon == null || !currentWeapon.IsEquipped)
        {
            // Nothing active yet; try ensure something is active
            EnsureActiveWeapon();
            return;
        }

        // Inputs
        bool shootHeld = inputManager != null && inputManager.onFoot.Shoot.IsPressed();
        bool shootPressed = inputManager != null && inputManager.onFoot.Shoot.triggered;
        bool reloadPressed = inputManager != null && inputManager.onFoot.Reload.triggered;
        bool switchPressed = inputManager != null && inputManager.onFoot.SwitchWeapon.triggered;

        if (reloadPressed)
        {
            currentWeapon.TryReload();
        }

        if (switchPressed)
        {
            SwitchWeapon();
        }

        // Fire logic: automatic = hold to sustain fire; semi-auto = only on press
        if (currentWeapon.IsAutomatic)
        {
            if (shootHeld) currentWeapon.TryFire();
        }
        else
        {
            if (shootPressed) currentWeapon.TryFire();
        }
    }

    // Toggle between primary and secondary if both exist; otherwise no-op
    private void SwitchWeapon()
    {
        if (currentWeapon == primaryWeapon && secondaryWeapon != null)
        {
            SetActiveWeapon(secondaryWeapon);
        }
        else if (currentWeapon == secondaryWeapon && primaryWeapon != null)
        {
            SetActiveWeapon(primaryWeapon);
        }
        else if (currentWeapon == null)
        {
            EnsureActiveWeapon();
        }
    }

    // Ensure we have one active weapon selected (prefer primary)
    private void EnsureActiveWeapon()
    {
        if (primaryWeapon != null)
        {
            SetActiveWeapon(primaryWeapon);
        }
        else if (secondaryWeapon != null)
        {
            SetActiveWeapon(secondaryWeapon);
        }
    }

    // Centralized activation/deactivation so IsEquipped stays correct
    private void SetActiveWeapon(Weapon target)
    {
        if (target == currentWeapon) return;

        // Deactivate previous
        if (currentWeapon != null)
        {
            currentWeapon.OnUnequip();
            currentWeapon.gameObject.SetActive(false);
        }

        // Activate new
        currentWeapon = target;
        if (currentWeapon != null)
        {
            currentWeapon.gameObject.SetActive(true);
            currentWeapon.OnEquip();
        }
    }

    // Rescan the searchRoot for weapons and assign slots
    private void RefreshWeapons(bool force)
    {
        if (searchRoot == null) return;

        Weapon foundPrimary = null;
        Weapon foundSecondary = null;

        var weapons = searchRoot.GetComponentsInChildren<Weapon>(true);
        foreach (var w in weapons)
        {
            if (w.Slot == WeaponSlot.Primary && foundPrimary == null) foundPrimary = w;
            else if (w.Slot == WeaponSlot.Secondary && foundSecondary == null) foundSecondary = w;
        }

        bool changed = force || foundPrimary != primaryWeapon || foundSecondary != secondaryWeapon;
        primaryWeapon = foundPrimary;
        secondaryWeapon = foundSecondary;

        if (!changed) return;

        // If current disappeared or changed, choose a sensible default
        if (currentWeapon != primaryWeapon && currentWeapon != secondaryWeapon)
        {
            currentWeapon = null;
        }

        // Hide the non-active weapon to prevent visual overlap
        if (primaryWeapon != null && currentWeapon != primaryWeapon)
        {
            primaryWeapon.gameObject.SetActive(false);
            primaryWeapon.OnUnequip();
        }
        if (secondaryWeapon != null && currentWeapon != secondaryWeapon)
        {
            secondaryWeapon.gameObject.SetActive(false);
            secondaryWeapon.OnUnequip();
        }

        EnsureActiveWeapon();
    }

    // Optional external setter
    public void SetCurrentWeapon(Weapon weapon)
    {
        RefreshWeapons(force: true); // make sure slots are up to date
        SetActiveWeapon(weapon);
    }
}
