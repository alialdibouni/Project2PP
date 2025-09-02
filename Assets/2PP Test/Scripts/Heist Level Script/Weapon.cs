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

    [Header("Recoil (visual shake)")]
    [SerializeField] private float recoilKickBack = 0.02f;   // meters backward
    [SerializeField] private float recoilKickUp = 2.0f;      // degrees up
    [SerializeField] private float recoilKickSide = 0.5f;    // degrees horizontal random
    [SerializeField] private float recoilReturnSpeed = 12f;  // how fast target returns to zero
    [SerializeField] private float recoilSnapSpeed = 20f;    // how fast current follows target

    public WeaponSlot Slot => slot;

    public bool IsEquipped { get; private set; }
    public bool IsReloading { get; private set; }
    public string WeaponName => weaponName;
    public int AmmoInMagazine => ammoInMagazine;
    public int MagazineSize => magazineSize;
    public int ReserveAmmo => reserveAmmo;
    public bool IsAutomatic => isAutomatic;

    private float _nextShotTime;

    // Recoil state
    private Vector3 _baseLocalPos;
    private Quaternion _baseLocalRot;
    private Vector3 _recoilPosCurrent, _recoilPosTarget;
    private Vector3 _recoilRotCurrent, _recoilRotTarget;

    // FPS arms visibility cache
    private Transform _fpsArmsRoot;
    private Renderer[] _fpsArmsRenderers;

    private void Start()
    {
        // Ensure FPSArms are hidden in the world by default
        if (!IsEquipped) SetFPSArmsVisible(false);
    }

    // Call when this weapon becomes the active weapon in hands
    public virtual void OnEquip()
    {
        IsEquipped = true;
        gameObject.SetActive(true);

        // Cache current pose as base for recoil offsets
        _baseLocalPos = transform.localPosition;
        _baseLocalRot = transform.localRotation;

        // Reset recoil state
        _recoilPosCurrent = _recoilPosTarget = Vector3.zero;
        _recoilRotCurrent = _recoilRotTarget = Vector3.zero;
        ApplyRecoilTransform();

        // Show FPS arms for this equipped weapon
        SetFPSArmsVisible(true);
    }

    // Call when this weapon is no longer active (switched away or dropped)
    public virtual void OnUnequip()
    {
        IsEquipped = false;

        // Restore transform to base pose
        _recoilPosCurrent = _recoilPosTarget = Vector3.zero;
        _recoilRotCurrent = _recoilRotTarget = Vector3.zero;
        ApplyRecoilTransform();

        // Hide FPS arms when not equipped
        SetFPSArmsVisible(false);
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

        // Add small recoil impulse
        AddRecoilImpulse();
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

    private void Update()
    {
        if (!IsEquipped) return;

        // Targets return to zero over time
        _recoilPosTarget = Vector3.Lerp(_recoilPosTarget, Vector3.zero, recoilReturnSpeed * Time.deltaTime);
        _recoilRotTarget = Vector3.Lerp(_recoilRotTarget, Vector3.zero, recoilReturnSpeed * Time.deltaTime);

        // Currents follow targets (snappy)
        _recoilPosCurrent = Vector3.Lerp(_recoilPosCurrent, _recoilPosTarget, recoilSnapSpeed * Time.deltaTime);
        _recoilRotCurrent = Vector3.Lerp(_recoilRotCurrent, _recoilRotTarget, recoilSnapSpeed * Time.deltaTime);

        ApplyRecoilTransform();
    }

    private void AddRecoilImpulse()
    {
        if (!IsEquipped) return;

        // Small position kick back
        _recoilPosTarget += new Vector3(
            Random.Range(-recoilKickBack, recoilKickBack) * 0.1f, // tiny lateral pos jitter
            Random.Range(-recoilKickBack, recoilKickBack) * 0.1f,
            -recoilKickBack);

        // Small rotation kick (up + slight random horizontal)
        _recoilRotTarget += new Vector3(
            recoilKickUp,
            Random.Range(-recoilKickSide, recoilKickSide),
            0f);

        // Optional clamping to avoid runaway accumulation
        _recoilRotTarget.x = Mathf.Clamp(_recoilRotTarget.x, -10f, 15f);
        _recoilRotTarget.y = Mathf.Clamp(_recoilRotTarget.y, -10f, 10f);
        _recoilPosTarget.z = Mathf.Clamp(_recoilPosTarget.z, -0.1f, 0.05f);
    }

    private void ApplyRecoilTransform()
    {
        transform.localPosition = _baseLocalPos + _recoilPosCurrent;
        transform.localRotation = _baseLocalRot * Quaternion.Euler(_recoilRotCurrent);
    }

    // --- FPS Arms helpers ---

    private void CacheFPSArms()
    {
        if (_fpsArmsRoot != null) return;

        // Prefer tag, fallback to name
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t.CompareTag("FPSArms") || t.name == "FPSArms")
            {
                _fpsArmsRoot = t;
                _fpsArmsRenderers = _fpsArmsRoot.GetComponentsInChildren<Renderer>(true);
                break;
            }
        }
    }

    private void SetFPSArmsVisible(bool visible)
    {
        CacheFPSArms();
        if (_fpsArmsRenderers == null) return;

        for (int i = 0; i < _fpsArmsRenderers.Length; i++)
        {
            var r = _fpsArmsRenderers[i];
            if (r != null) r.enabled = visible;
        }
    }
}
