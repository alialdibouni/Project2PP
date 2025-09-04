using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MissionTrigger : MonoBehaviour
{
    [SerializeField] public string triggerId;

    [Header("Auto fire on trigger enter")]
    [SerializeField] private bool fireOnTriggerEnter = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool once = true;
    private bool _fired;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!fireOnTriggerEnter) return;
        if (once && _fired) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        Fire();
        if (once) _fired = true;
    }

    // Call from UnityEvent (InteractionEvent.onInteract) or any script
    public void Fire()
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        MissionEventBus.RaiseTrigger(triggerId);
#if UNITY_EDITOR
        Debug.Log($"MissionTrigger fired: {triggerId}", this);
#endif
    }

    // Convenient overload for UnityEvent with string parameter
    public void FireWithId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        MissionEventBus.RaiseTrigger(id);
#if UNITY_EDITOR
        Debug.Log($"MissionTrigger fired with id: {id}", this);
#endif
    }
}
