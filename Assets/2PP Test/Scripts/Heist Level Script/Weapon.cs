using System.Collections;
using UnityEngine;

public enum WeaponSlot
{
    Primary,
    Secondary
}

public abstract class Weapon : MonoBehaviour
{
    [Header("Info")]
    [SerializeField] private string weaponName = "Weapon";
    [SerializeField] private WeaponSlot slot = WeaponSlot.Primary; // assign in Inspector

    [Header("Damage / Range")]
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float range = 100f;

    [Header("Firing")]
    [Tooltip("Shots per second (e.g., 10 = 600 RPM)")]
    [SerializeField] protected float fireRate = 10f;
    [SerializeField] protected bool isAutomatic = true;

    [Header("Ammo")]
    [SerializeField] protected int magazineSize = 30;
    [SerializeField] protected int ammoInMagazine = 30;
    [SerializeField] protected int reserveAmmo = 90;
    [SerializeField] protected float reloadTime = 1.6f;

    [Header("Visuals / Audio (optional)")]
    [SerializeField] protected Transform muzzle;
    [SerializeField] protected ParticleSystem muzzleFlash;
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip fireSfx;
    [SerializeField] protected AudioClip reloadSfx;

    public WeaponSlot Slot => slot;

    public bool IsEquipped { get; private set; }
    public bool IsReloading { get; private set; }
    public string WeaponName => weaponName;
    public int AmmoInMagazine => ammoInMagazine;
    public int MagazineSize => magazineSize;
    public int ReserveAmmo => reserveAmmo;
    public bool IsAutomatic => isAutomatic;

    private float _nextShotTime;

    // Call when this weapon becomes the active weapon in hands
    public virtual void OnEquip()
    {
        IsEquipped = true;
        gameObject.SetActive(true);
    }

    // Call when this weapon is no longer active (switched away or dropped)
    public virtual void OnUnequip()
    {
        IsEquipped = false;
    }

    public bool CanFire()
    {
        return IsEquipped && !IsReloading && ammoInMagazine > 0 && Time.time >= _nextShotTime;
    }

    // Entry point to fire. Returns true if a shot was fired.
    public bool TryFire()
    {
        if (!CanFire())
        {
            if (ammoInMagazine == 0) TryReload();
            return false;
        }

        ammoInMagazine--;
        _nextShotTime = Time.time + (fireRate > 0f ? 1f / fireRate : 0f);

        OnFired();
        PerformShot();

        return true;
    }

    // Implement actual shot behavior (hitscan/projectile) in derived class
    protected abstract void PerformShot();

    // Optional VFX/SFX hook
    protected virtual void OnFired()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (audioSource != null && fireSfx != null) audioSource.PlayOneShot(fireSfx);
    }

    public bool TryReload()
    {
        if (IsReloading) return false;
        if (ammoInMagazine >= magazineSize) return false;
        if (reserveAmmo <= 0) return false;

        StartCoroutine(ReloadRoutine());
        return true;
    }

    private IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        OnReloadStarted();

        if (audioSource != null && reloadSfx != null) audioSource.PlayOneShot(reloadSfx);
        yield return new WaitForSeconds(reloadTime);

        int needed = magazineSize - ammoInMagazine;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        ammoInMagazine += toLoad;
        reserveAmmo -= toLoad;

        IsReloading = false;
        OnReloadCompleted();
    }

    protected virtual void OnReloadStarted() { }
    protected virtual void OnReloadCompleted() { }

    public void AddReserveAmmo(int amount)
    {
        reserveAmmo = Mathf.Max(0, reserveAmmo + Mathf.Max(0, amount));
    }
}
