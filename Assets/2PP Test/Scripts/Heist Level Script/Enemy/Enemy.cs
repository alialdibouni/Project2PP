using System; // ADD

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Synty.AnimationBaseLocomotion.Samples; // for SamplePlayerAnimationController

public class Enemy : MonoBehaviour
{
    // ADD: global notification when the player is caught
    public static event Action PlayerCaught;

    // -------- ADD: Global chase status (reference counted across enemies) --------
    public static event Action<bool> GlobalChaseChanged;
    private static int s_activeChases;
    public static bool AnyChaseActive => s_activeChases > 0;

    public static void SignalChaseStart()
    {
        int prev = s_activeChases;
        s_activeChases++;
        if (prev == 0 && s_activeChases == 1)
            GlobalChaseChanged?.Invoke(true);
    }

    public static void SignalChaseEnd()
    {
        int prev = s_activeChases;
        s_activeChases = Mathf.Max(0, s_activeChases - 1);
        if (prev > 0 && s_activeChases == 0)
            GlobalChaseChanged?.Invoke(false);
    }
    // ---------------------------------------------------------------------------
    
    private StateMachine stateMachine;
    private NavMeshAgent agent;
    private GameObject player;
    private Vector3 lastKnownPos;
    private bool isPlayerInGuardArea;

    public NavMeshAgent Agent => agent;
    public GameObject Player => player;
    public Vector3 LastKnownPos { get => lastKnownPos; set => lastKnownPos = value; }
    public bool IsPlayerInGuardArea { get => isPlayerInGuardArea; private set => isPlayerInGuardArea = value; }

    public Path path;

    [Header("Sight Values")]
    public float sightDistance = 20f;
    public float fieldOfView = 85f;
    public float eyeHeight;

    [Header("Weapon Values")]
    public Transform gunBarrel;
    [Range(0.1f, 10f)]
    public float fireRate = 1f;

    [Header("Chase Audio")]
    public AudioSource chaseAudioSource;
    public AudioClip firstChaseClip;
    public AudioClip[] subsequentChaseClips;

    [Header("Catch / Respawn (Player)")]
    [SerializeField] private float catchDistance = 1.0f;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private float blackHoldTime = 0.4f;
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private bool debugCatch = false;
    [SerializeField] private bool disablePlayerDuringFade = true;

    [Header("CameraMan Respawn")]
    [SerializeField] private bool respawnCameraManOnCatch = true;
    [SerializeField] private GameObject cameraManObject;
    [SerializeField] private Transform cameraManRespawnPoint;
    [SerializeField] private bool cameraManSnapToNavMesh = true;
    [SerializeField] private float cameraManNavSampleRadius = 2f;
    [SerializeField] private bool cameraManUseRespawnRotation = true;

    [SerializeField] private string currentState;

    // Audio state
    private bool hasPlayedFirstChaseClip;
    private List<int> shuffleBag;
    private int shuffleIndex;

    // Catch / chase state
    private bool isProcessingCatch;
    private float originalStoppingDistance = -1f;

    private ScreenFader screenFader;

    // Player components
    private SamplePlayerAnimationController playerAnim;
    private CharacterController playerCC;

    // CameraMan cached
    private NavMeshAgent cameraManAgent;
    private Vector3 cameraManInitialPos;
    private Quaternion cameraManInitialRot;

    public float CatchDistance => catchDistance;
    public bool IsProcessingCatch => isProcessingCatch;

    private void Start()
    {
        stateMachine = GetComponent<StateMachine>();
        agent = GetComponent<NavMeshAgent>();
        stateMachine.Initialise();

        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null && debugCatch)
            Debug.LogWarning($"[Enemy:{name}] Player tag not found.");

        CachePlayerComponents();
        CacheCameraMan();
        RebuildShuffleBag();
        screenFader = FindObjectOfType<ScreenFader>();

        if (debugCatch && respawnPoint == null)
            Debug.LogWarning($"[Enemy:{name}] No player respawnPoint assigned.");
    }

    private void CachePlayerComponents()
    {
        if (player == null) return;
        playerAnim = player.GetComponent<SamplePlayerAnimationController>() ??
                     player.GetComponentInParent<SamplePlayerAnimationController>();
        playerCC = player.GetComponent<CharacterController>() ??
                   player.GetComponentInParent<CharacterController>();

        if (debugCatch)
            Debug.Log($"[Enemy:{name}] Cache player: anim={(playerAnim != null)} cc={(playerCC != null)}");
    }

    private void CacheCameraMan()
    {
        if (cameraManObject == null)
            cameraManObject = GameObject.FindGameObjectWithTag("CameraMan");

        if (cameraManObject != null)
        {
            cameraManAgent = cameraManObject.GetComponent<NavMeshAgent>();
            cameraManInitialPos = cameraManObject.transform.position;
            cameraManInitialRot = cameraManObject.transform.rotation;
            if (debugCatch)
                Debug.Log($"[Enemy:{name}] CameraMan cached (agent={(cameraManAgent != null)}).");
        }
        else if (respawnCameraManOnCatch && debugCatch)
        {
            Debug.LogWarning($"[Enemy:{name}] Could not find CameraMan (tag 'CameraMan').");
        }
    }

    private void Update()
    {
        CanSeePlayer();
        if (stateMachine != null && stateMachine.activeState != null)
            currentState = stateMachine.activeState.ToString();
    }

    public bool CanSeePlayer()
    {
        if (player == null) return false;
        if (Vector3.Distance(transform.position, player.transform.position) >= sightDistance) return false;

        Vector3 targetDirection = player.transform.position - transform.position - (Vector3.up * eyeHeight);
        float angleToPlayer = Vector3.Angle(targetDirection, transform.forward);
        if (angleToPlayer < -fieldOfView || angleToPlayer > fieldOfView) return false;

        Ray ray = new Ray(transform.position + (Vector3.up * eyeHeight), targetDirection);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, sightDistance))
        {
            if (hitInfo.transform.gameObject == player)
            {
                Debug.DrawRay(ray.origin, ray.direction * sightDistance, Color.red);
                return true;
            }
        }
        return false;
    }

    public void SetPlayerInGuardArea(bool inside) => IsPlayerInGuardArea = inside;

    #region Original Chase Audio API (Restored)

    // Called once per chase start by ChaseState.Enter
    public void PlayChaseAudio()
    {
        if (chaseAudioSource == null) return;

        AudioClip clipToPlay = null;

        if (!hasPlayedFirstChaseClip && firstChaseClip != null)
        {
            clipToPlay = firstChaseClip;
            hasPlayedFirstChaseClip = true; // never play intro again
        }
        else
        {
            int idx = GetNextShuffleIndex();
            if (idx >= 0)
            {
                clipToPlay = subsequentChaseClips[idx];
            }
        }

        if (clipToPlay != null)
        {
            if (chaseAudioSource.isPlaying)
                chaseAudioSource.Stop();

            chaseAudioSource.loop = false; // ensure one-shot
            chaseAudioSource.clip = clipToPlay;
            chaseAudioSource.Play();
        }
    }

    // Called once when chase ends by ChaseState.Exit
    public void StopChaseAudio()
    {
        if (chaseAudioSource != null && chaseAudioSource.isPlaying)
            chaseAudioSource.Stop();
    }

    private void RebuildShuffleBag()
    {
        shuffleBag = new List<int>();
        if (subsequentChaseClips != null)
        {
            for (int i = 0; i < subsequentChaseClips.Length; i++)
            {
                if (subsequentChaseClips[i] != null)
                    shuffleBag.Add(i);
            }
        }
        // Fisher-Yates shuffle
        for (int i = shuffleBag.Count - 1; i > 0; i--)
        {
            // Replace this line:
            // int j = Random.Range(0, i + 1);
            // With this line:
            int j = UnityEngine.Random.Range(0, i + 1);
            int tmp = shuffleBag[i];
            shuffleBag[i] = shuffleBag[j];
            shuffleBag[j] = tmp;
        }
        shuffleIndex = 0;
    }

    private int GetNextShuffleIndex()
    {
        if (subsequentChaseClips == null || subsequentChaseClips.Length == 0) return -1;
        if (shuffleBag == null || shuffleBag.Count == 0 || shuffleIndex >= shuffleBag.Count)
            RebuildShuffleBag();

        if (shuffleBag.Count == 0) return -1; // all nulls case
        return shuffleBag[shuffleIndex++];
    }

    #endregion

    public void CatchPlayer()
    {
        if (isProcessingCatch || player == null || respawnPoint == null) return;
        if (debugCatch)
            Debug.Log($"[Enemy:{name}] CatchPlayer (distance <= {catchDistance}).");
        StartCoroutine(CatchPlayerRoutine());
    }

    private IEnumerator CatchPlayerRoutine()
    {
        isProcessingCatch = true;

        // ADD: notify listeners immediately
        PlayerCaught?.Invoke();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (playerAnim == null) CachePlayerComponents();
        if (respawnCameraManOnCatch && cameraManAgent == null) CacheCameraMan();

        if (playerAnim != null && disablePlayerDuringFade)
            playerAnim.PauseUpdates = true;

        if (screenFader == null)
            screenFader = FindObjectOfType<ScreenFader>();

        if (screenFader != null)
            yield return screenFader.FadeOut(fadeDuration);
        else
            yield return new WaitForSeconds(fadeDuration);

        bool playerTeleported = ForcePlayerRespawn();
        bool camTeleported = false;
        if (respawnCameraManOnCatch)
            camTeleported = ForceCameraManRespawn();

        yield return new WaitForSeconds(blackHoldTime);

        if (playerTeleported)
            lastKnownPos = respawnPoint.position;
        else if (debugCatch)
            Debug.LogWarning($"[Enemy:{name}] Player teleport failed.");

        // IMPORTANT: Do NOT reset hasPlayedFirstChaseClip here.

        if (stateMachine != null)
            stateMachine.ChangeState(new PatrolState());

        if (agent != null)
            agent.isStopped = false;

        if (screenFader != null)
            yield return screenFader.FadeIn(fadeDuration);
        else
            yield return new WaitForSeconds(fadeDuration);

        if (playerAnim != null && disablePlayerDuringFade)
            playerAnim.PauseUpdates = false;

        isProcessingCatch = false;
        if (debugCatch)
            Debug.Log($"[Enemy:{name}] Catch done. Player teleported={playerTeleported} CameraMan teleported={camTeleported}");
    }

    private bool ForcePlayerRespawn()
    {
        if (respawnPoint == null || player == null) return false;

        Vector3 targetPos = respawnPoint.position;
        if (snapToGround)
        {
            if (Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                targetPos = hit.point;
            }
        }

        if (playerAnim != null)
        {
            playerAnim.ForceRespawn(targetPos, resetMovement: true);
            if (debugCatch)
                Debug.Log($"[Enemy:{name}] Player ForceRespawn -> {targetPos}");
            return true;
        }

        if (playerCC == null)
            playerCC = player.GetComponent<CharacterController>();

        if (playerCC != null)
        {
            bool wasEnabled = playerCC.enabled;
            playerCC.enabled = false;
            player.transform.position = targetPos;
            Physics.SyncTransforms();
            playerCC.enabled = wasEnabled;
            if (debugCatch)
                Debug.Log($"[Enemy:{name}] Player manual CC teleport -> {targetPos}");
            return true;
        }

        player.transform.position = targetPos;
        Physics.SyncTransforms();
        if (debugCatch)
            Debug.Log($"[Enemy:{name}] Player basic transform teleport -> {targetPos}");
        return true;
    }

    private bool ForceCameraManRespawn()
    {
        if (cameraManAgent == null) return false;

        Vector3 targetPos = cameraManRespawnPoint ? cameraManRespawnPoint.position : cameraManInitialPos;
        Quaternion targetRot = cameraManUseRespawnRotation && cameraManRespawnPoint
            ? cameraManRespawnPoint.rotation
            : cameraManAgent.transform.rotation;

        if (cameraManSnapToNavMesh)
        {
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, cameraManNavSampleRadius, NavMesh.AllAreas))
                targetPos = hit.position;
        }

        bool warped = cameraManAgent.Warp(targetPos);
        if (!warped)
        {
            cameraManAgent.enabled = false;
            cameraManAgent.transform.position = targetPos;
            cameraManAgent.enabled = true;
        }
        cameraManAgent.transform.rotation = targetRot;

        if (debugCatch)
            Debug.Log($"[Enemy:{name}] CameraMan respawn -> {targetPos} (warped={warped})");
        return true;
    }

    public void ApplyChaseStoppingDistance()
    {
        if (agent == null) return;
        if (originalStoppingDistance < 0f)
            originalStoppingDistance = agent.stoppingDistance;
        agent.stoppingDistance = 0f;
    }

    public void RestoreOriginalStoppingDistance()
    {
        if (agent == null) return;
        if (originalStoppingDistance >= 0f)
            agent.stoppingDistance = originalStoppingDistance;
    }
}
