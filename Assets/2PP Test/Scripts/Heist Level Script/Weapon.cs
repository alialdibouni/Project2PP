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

    // Runtime instance of the muzzle flash we actually play
    private ParticleSystem _muzzleFlashInstance;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;


        CacheMuzzleAndFlash();
        if (Application.isPlaying)
            EnsureMuzzleFlashInstance();
    }

    private void OnValidate()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

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

        _baseLocalPos = transform.localPosition;
        _baseLocalRot = transform.localRotation;

        _recoilPosCurrent = _recoilPosTarget = Vector3.zero;
        _recoilRotCurrent = _recoilRotTarget = Vector3.zero;
        ApplyRecoilTransform();

        SetFPSArmsVisible(true);
    }

    // Call when this weapon is no longer active (switched away or dropped)
    public virtual void OnUnequip()
    {
        IsEquipped = false;

        _recoilPosCurrent = _recoilPosTarget = Vector3.zero;
        _recoilRotCurrent = _recoilRotTarget = Vector3.zero;
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
    }

    private void Update()
    {
        if (!IsEquipped) return;

        _recoilPosTarget = Vector3.Lerp(_recoilPosTarget, Vector3.zero, recoilReturnSpeed * Time.deltaTime);
        _recoilRotTarget = Vector3.Lerp(_recoilRotTarget, Vector3.zero, recoilReturnSpeed * Time.deltaTime);

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
        transform.localPosition = _baseLocalPos + _recoilPosCurrent;
        transform.localRotation = _baseLocalRot * Quaternion.Euler(_recoilRotCurrent);
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
