using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [Header("How we find the equipped weapon")]
    [SerializeField] private Transform searchRoot;          // If null, defaults to camera transform
    [SerializeField] private float recheckInterval = 0.25f; // How often we try to rediscover the weapon

    private InputManager inputManager;
    private PlayerLook playerLook;
    private Weapon currentWeapon;
    private float recheckTimer;

    void Start()
    {
        inputManager = GetComponent<InputManager>();
        playerLook = GetComponent<PlayerLook>();

        if (searchRoot == null && playerLook != null && playerLook.cam != null)
            searchRoot = playerLook.cam.transform;

        TryFindEquippedWeapon();
    }

    void Update()
    {
        // Keep tracking the currently equipped weapon
        if (currentWeapon == null || !currentWeapon.IsEquipped)
        {
            recheckTimer += Time.deltaTime;
            if (recheckTimer >= recheckInterval)
            {
                recheckTimer = 0f;
                TryFindEquippedWeapon();
            }
            return;
        }

        // Read shoot input
        bool shootHeld = inputManager != null && inputManager.onFoot.Shoot.IsPressed();
        bool shootPressed = inputManager != null && inputManager.onFoot.Shoot.triggered;

        // Read reload input
        bool reloadPressed = inputManager != null && inputManager.onFoot.Reload.triggered;
        if (reloadPressed)
        {
            currentWeapon.TryReload();
        }

        // Fire logic: automatic = hold to sustain fire; semi-auto = only on press
        if (currentWeapon.IsAutomatic)
        {
            if (shootHeld)
                currentWeapon.TryFire();
        }
        else
        {
            if (shootPressed)
                currentWeapon.TryFire();
        }
    }

    // Optionally let other systems set the current weapon explicitly
    public void SetCurrentWeapon(Weapon weapon)
    {
        currentWeapon = weapon;
        if (currentWeapon != null && !currentWeapon.IsEquipped)
            currentWeapon.OnEquip();
    }

    private void TryFindEquippedWeapon()
    {
        currentWeapon = null;
        if (searchRoot == null) return;

        // Prefer a weapon that already reports equipped under the search root
        currentWeapon = searchRoot.GetComponentInChildren<Weapon>(true);
        if (currentWeapon != null && !currentWeapon.IsEquipped)
        {
            // If a weapon is parented here by the pickup but wasn't notified, equip it now
            currentWeapon.OnEquip();
        }
    }
}
