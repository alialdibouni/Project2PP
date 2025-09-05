using TMPro;
using UnityEngine;

public class WeaponHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform weaponRoot;                 // Assign WeaponCamera; if null will try to find it
    [SerializeField] private TextMeshProUGUI weaponNameText;       // e.g. "M4A1"
    [SerializeField] private TextMeshProUGUI ammoText;             // e.g. "30 / 120"

    [Header("Display")]
    [SerializeField] private string noWeaponName = "--";
    [SerializeField] private string noAmmoText = "-- / --";

    private Weapon _current;
    private int _lastMag = int.MinValue;
    private int _lastReserve = int.MinValue;
    private string _lastName;
    private bool _uiActive;

    private void Awake()
    {
        if (weaponRoot == null)
        {
            var wc = GameObject.Find("WeaponCamera");
            if (wc != null) weaponRoot = wc.transform;
            else if (Camera.main != null) weaponRoot = Camera.main.transform;
        }

        SetTextActive(false);
    }

    private void Update()
    {
        var equipped = FindEquippedWeapon();
        if (equipped != _current)
        {
            _current = equipped;
            _lastName = null; // force refresh
            _lastMag = int.MinValue;
            _lastReserve = int.MinValue;

            // Toggle visibility based on whether a weapon is equipped
            SetTextActive(_current != null);
        }

        if (_current == null)
        {
            return;
        }

        // Update name if changed
        if (_current.WeaponName != _lastName)
        {
            _lastName = _current.WeaponName;
            if (weaponNameText != null)
                weaponNameText.text = _lastName;
        }

        // Update ammo if changed
        int mag = _current.AmmoInMagazine;
        int reserve = _current.ReserveAmmo;
        if (mag != _lastMag || reserve != _lastReserve)
        {
            _lastMag = mag;
            _lastReserve = reserve;
            if (ammoText != null)
                ammoText.text = $"{mag} / {reserve}";
        }
    }

    private Weapon FindEquippedWeapon()
    {
        if (weaponRoot == null) return null;

        // Prefer explicitly equipped
        var weapons = weaponRoot.GetComponentsInChildren<Weapon>(true);
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i].IsEquipped)
                return weapons[i];
        }

        // Fallback: if none marked as equipped (edge cases), prefer an active one
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i].gameObject.activeInHierarchy)
                return weapons[i];
        }

        return null;
    }

    private void SetTextActive(bool active)
    {
        _uiActive = active;
        if (weaponNameText != null) weaponNameText.gameObject.SetActive(active);
        if (ammoText != null) ammoText.gameObject.SetActive(active);

        // Optionally clear placeholders when hiding
        if (!active)
        {
            if (weaponNameText != null) weaponNameText.text = noWeaponName;
            if (ammoText != null) ammoText.text = noAmmoText;
        }
    }
}