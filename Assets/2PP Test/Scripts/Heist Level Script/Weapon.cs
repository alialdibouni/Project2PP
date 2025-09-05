using System.Collections;
using UnityEngine;

public enum WeaponSlot
{
    Primary,
    Secondary
}

[RequireComponent(typeof(AudioSource))]
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
    [SerializeField] protected int maxReserveAmmo = 90;
    [SerializeField] protected float reloadTime = 1.6f;

    [Header("Visuals / Audio")]
    [SerializeField] protected Transform muzzle;
    [SerializeField] protected ParticleSystem muzzleFlash; // Can be a prefab asset or scene object
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip fireSfx;
    [SerializeField] protected AudioClip reloadSfx;

    [Header("Recoil (visual shake)")]
    [SerializeField] private float recoilKickBack = 0.02f;   // meters backward
    [SerializeField] private float recoilKickUp = 2.0f;      // degrees up
    [SerializeField] private float recoilKickSide = 0.5f;    // degrees horizontal random
    [SerializeField] private float recoilReturnSpeed = 12f;  // how fast target returns to zero
    [SerializeField] private float recoilSnapSpeed = 20f;    // how fast current follows target

    [Header("Aiming (ADS)")]
    [Tooltip("Optional transform defining the weapon pose when aiming.")]
    [SerializeField] private Transform adsPose;
    [Tooltip("Fallback ADS local position offset from hip if no adsPose is provided.")]
    [SerializeField] private Vector3 adsLocalPosition = new Vector3(0f, -0.05f, 0.1f);
    [Tooltip("Fallback ADS local rotation (Euler) from hip if no adsPose is provided.")]
    [SerializeField] private Vector3 adsLocalEulerAngles = Vector3.zero;
    [SerializeField, Range(0.01f, 1.5f)] private float aimInTime = 0.12f;
    [SerializeField, Range(0.01f, 1.5f)] private float aimOutTime = 0.12f;
    [SerializeField] private float hipFov = 60f;
    [SerializeField] private float adsFov = 50f;

    public WeaponSlot Slot => slot;

    public bool IsEquipped { get; private set; }
    public bool IsReloading { get; private set; }
    public string WeaponName => weaponName;
    public int AmmoInMagazine => ammoInMagazine;
    public int MagazineSize => magazineSize;
    public int ReserveAmmo => reserveAmmo;
    public int MaxReserveAmmo => maxReserveAmmo;
    public bool IsAutomatic => isAutomatic;

    private float _nextShotTime;

    // Recoil state
    private Vector3 _baseLocalPos;
    private Quaternion _baseLocalRot;
    private Vector3 _recoilPosCurrent, _recoilPosTarget;
    private Vector3 _recoilRotCurrent, _recoilRotTarget;

    // ADS state
    private Vector3 _hipLocalPos;
    private Quaternion _hipLocalRot;
    private Vector3 _adsTargetPos;
    private Quaternion _adsTargetRot;
    private float _aimT;           // 0..1 blend to ADS
    private bool _aimRequested;    // set from PlayerShoot

    // FPS arms visibility cache
    private Transform _fpsArmsRoot;
    private Renderer[] _fpsArmsRenderers;

    // Runtime instance of the muzzle flash we actually play
    private ParticleSystem _muzzleFlashInstance;

    // Cameras for FOV blending
    private Camera _mainCam;
    private Camera _weaponCam;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Keep max reserve sane with existing serialized data
        if (maxReserveAmmo < reserveAmmo) maxReserveAmmo = reserveAmmo;
        if (maxReserveAmmo < 0) maxReserveAmmo = 0;

        CacheMuzzleAndFlash();
        if (Application.isPlaying)
            EnsureMuzzleFlashInstance();
    }

    private void OnValidate()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Keep max reserve sane in editor too
        if (maxReserveAmmo < reserveAmmo) maxReserveAmmo = reserveAmmo;
        if (maxReserveAmmo < 0) maxReserveAmmo = 0;

        CacheMuzzleAndFlash();
        // Avoid instantiating in edit-time; only wire an existing child if present
        if (!Application.isPlaying)
            _muzzleFlashInstance = FindExistingFlashInChildren();
    }

    private void Start()
    {
        if (!IsEquipped) SetFPSArmsVisible(false);
    }

    // Call when this weapon becomes the active weapon in hands
    public virtual void OnEquip()
    {
        IsEquipped = true;
        gameObject.SetActive(true);

        // Cache hip pose from current transform (set by pickup offsets)
        _baseLocalPos = transform.localPosition;
        _baseLocalRot = transform.localRotation;
        _hipLocalPos = _baseLocalPos;
        _hipLocalRot = _baseLocalRot;

        // Resolve ADS target pose (from transform if provided, else fallback offsets)
        if (adsPose != null)
        {
            _adsTargetPos = adsPose.localPosition;
            _adsTargetRot = adsPose.localRotation;
        }
        else
        {
            _adsTargetPos = _hipLocalPos + adsLocalPosition;
            _adsTargetRot = _hipLocalRot * Quaternion.Euler(adsLocalEulerAngles);
        }

        // Reset recoil and aiming state
        _recoilPosCurrent = _recoilPosTarget = Vector3.zero;
        _recoilRotCurrent = _recoilRotTarget = Vector3.zero;
        _aimRequested = false;
        _aimT = 0f;

        // Cache cameras and set FOV to hip to avoid pops
        _mainCam = Camera.main;
        var wcGo = GameObject.Find("WeaponCamera");
        _weaponCam = wcGo != null ? wcGo.GetComponent<Camera>() : null;
        if (_mainCam != null) _mainCam.fieldOfView = hipFov;
        if (_weaponCam != null) _weaponCam.fieldOfView = hipFov;

        ApplyRecoilTransform();

        SetFPSArmsVisible(true);
    }

    // Call when this weapon is no longer active (switched away or dropped)
    public virtual void OnUnequip()
    {
        IsEquipped = false;

        _recoilPosCurrent = _recoilPosTarget = Vector3.zero;
        _recoilRotCurrent = _recoilRotTarget = Vector3.zero;
        _aimRequested = false; // release aim on unequip
        _aimT = 0f;

        // Reset pose/FOV back toward hip
        if (_mainCam != null) _mainCam.fieldOfView = hipFov;
        if (_weaponCam != null) _weaponCam.fieldOfView = hipFov;

        ApplyRecoilTransform();

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
        SafePlayMuzzleFlash();

        if (audioSource != null)
        {
            if (fireSfx != null)
            {
                audioSource.PlayOneShot(fireSfx);
            }
            else
            {
                Debug.LogWarning($"[{name}] Fire SFX not assigned on Weapon.", this);
            }
        }
        else
        {
            Debug.LogWarning($"[{name}] AudioSource not assigned on Weapon.", this);
        }

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

        if (audioSource != null)
        {
            if (reloadSfx != null)
            {
                audioSource.PlayOneShot(reloadSfx);
            }
            else
            {
                Debug.LogWarning($"[{name}] Reload SFX not assigned on Weapon.", this);
            }
        }
        else
        {
            Debug.LogWarning($"[{name}] AudioSource not assigned on Weapon.", this);
        }

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
        reserveAmmo = Mathf.Min(reserveAmmo, maxReserveAmmo);
    }

    // Fully refills magazine and reserve, cancels any reload in progress
    public void RefillAllAmmo()
    {
        StopAllCoroutines();
        IsReloading = false;
        ammoInMagazine = magazineSize;
        reserveAmmo = maxReserveAmmo;
    }

    private void Update()
    {
        if (!IsEquipped) return;

        // Aiming blend (time-scaled)
        float target = _aimRequested ? 1f : 0f;
        float speed = (_aimRequested ? 1f / Mathf.Max(aimInTime, 0.001f) : 1f / Mathf.Max(aimOutTime, 0.001f));
        _aimT = Mathf.MoveTowards(_aimT, target, speed * Time.deltaTime);

        // FOV blend
        if (_mainCam != null) _mainCam.fieldOfView = Mathf.Lerp(hipFov, adsFov, _aimT);
        if (_weaponCam != null) _weaponCam.fieldOfView = Mathf.Lerp(hipFov, adsFov, _aimT);

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

        _recoilPosTarget += new Vector3(
            Random.Range(-recoilKickBack, recoilKickBack) * 0.1f,
            Random.Range(-recoilKickBack, recoilKickBack) * 0.1f,
            -recoilKickBack);

        _recoilRotTarget += new Vector3(
            recoilKickUp,
            Random.Range(-recoilKickSide, recoilKickSide),
            0f);

        _recoilRotTarget.x = Mathf.Clamp(_recoilRotTarget.x, -10f, 15f);
        _recoilRotTarget.y = Mathf.Clamp(_recoilRotTarget.y, -10f, 10f);
        _recoilPosTarget.z = Mathf.Clamp(_recoilPosTarget.z, -0.1f, 0.05f);
    }

    private void ApplyRecoilTransform()
    {
        // Blend hip -> ADS base pose, then apply recoil
        Vector3 basePos = Vector3.Lerp(_hipLocalPos, _adsTargetPos, _aimT);
        Quaternion baseRot = Quaternion.Slerp(_hipLocalRot, _adsTargetRot, _aimT);

        transform.localPosition = basePos + _recoilPosCurrent;
        transform.localRotation = baseRot * Quaternion.Euler(_recoilRotCurrent);
    }

    // --- ADS control from external input ---

    public void SetAiming(bool aiming)
    {
        _aimRequested = aiming;
    }

    // --- FPS Arms helpers ---

    private void CacheFPSArms()
    {
        if (_fpsArmsRoot != null) return;

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

    // --- Muzzle helpers ---

    private void CacheMuzzleAndFlash()
    {
        if (muzzle == null)
        {
            Transform found = null;
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t.CompareTag("Muzzle") || t.name == "Muzzle" || t.name.Contains("Muzzle"))
                {
                    found = t;
                    break;
                }
            }
            muzzle = found;
            if (muzzle == null && Application.isPlaying)
                Debug.LogWarning($"[{name}] Muzzle Transform not assigned and not found by tag/name.", this);
        }

        if (muzzleFlash == null)
        {
            // Try find an existing ParticleSystem under muzzle
            _muzzleFlashInstance = FindExistingFlashInChildren();

            if (_muzzleFlashInstance == null)
            {
                // Fallback search by common names anywhere under this weapon
                var allPs = GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in allPs)
                {
                    var n = ps.name;
                    if (n.Contains("Muzzle") || n.Contains("Flash") || n.Contains("MuzzleFlash"))
                    {
                        _muzzleFlashInstance = ps;
                        break;
                    }
                }
            }
        }
    }

    private ParticleSystem FindExistingFlashInChildren()
    {
        if (muzzle == null) return null;
        return muzzle.GetComponentInChildren<ParticleSystem>(true);
    }

    private void EnsureMuzzleFlashInstance()
    {
        // If we already have a valid scene instance, use it.
        if (_muzzleFlashInstance != null && _muzzleFlashInstance.gameObject.scene.IsValid())
            return;

        // If the serialized muzzleFlash is a scene object, use it.
        if (muzzleFlash != null && muzzleFlash.gameObject.scene.IsValid())
        {
            _muzzleFlashInstance = muzzleFlash;
        }
        // If the serialized muzzleFlash is a prefab asset, instantiate it under the muzzle at runtime.
        else if (muzzleFlash != null && muzzle != null)
        {
            _muzzleFlashInstance = Instantiate(muzzleFlash, muzzle, false);
            _muzzleFlashInstance.name = $"{muzzleFlash.name} (Instance)";
        }
        // If nothing assigned, try to find one in children (already attempted in CacheMuzzleAndFlash)
        else if (_muzzleFlashInstance == null)
        {
            _muzzleFlashInstance = FindExistingFlashInChildren();
        }

        if (_muzzleFlashInstance != null)
        {
            var main = _muzzleFlashInstance.main;
            main.playOnAwake = false;

            // Ensure it's stopped and ready
            _muzzleFlashInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else
        {
            Debug.LogWarning($"[{name}] No muzzle flash ParticleSystem available. Assign a prefab or child PS.", this);
        }
    }

    private void SafePlayMuzzleFlash()
    {
        if (!Application.isPlaying) return;

        if (_muzzleFlashInstance == null)
            EnsureMuzzleFlashInstance();

        var ps = _muzzleFlashInstance;
        if (ps == null)
        {
            Debug.LogWarning($"[{name}] Cannot play muzzle flash: ParticleSystem missing.", this);
            return;
        }

        if (!ps.gameObject.activeInHierarchy)
            ps.gameObject.SetActive(true);

        // Keep emission enabled
        var emission = ps.emission;
        emission.enabled = true;

        // If using Stop Action = Disable, the object stays enabled from above
        ps.Clear(true);
        ps.Play(true);
    }
}