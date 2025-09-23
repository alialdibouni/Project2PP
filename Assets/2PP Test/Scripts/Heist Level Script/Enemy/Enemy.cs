using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    private StateMachine stateMachine;
    private NavMeshAgent agent;
    private GameObject player;
    private Vector3 lastKnownPos;
    private bool isPlayerInGuardArea;

    public NavMeshAgent Agent { get => agent; }
    public GameObject Player { get => player; }
    public Vector3 LastKnownPos { get => lastKnownPos; set => lastKnownPos = value; }
    public bool IsPlayerInGuardArea { get => isPlayerInGuardArea; private set => isPlayerInGuardArea = value; }

    public Path path;
    //public GameObject debugsphere;
    [Header("Sight Values")]
    public float sightDistance = 20f;
    public float fieldOfView = 85f;
    public float eyeHeight;
    [Header("Weapon Values")]
    public Transform gunBarrel;
    [Range(0.1f, 10f)]
    public float fireRate;

    [Header("Chase Audio")]
    public AudioSource chaseAudioSource;
    public AudioClip firstChaseClip;
    public AudioClip[] subsequentChaseClips;

    [SerializeField] private string currentState; //for debugging purposes

    // Internal audio state
    private bool hasPlayedFirstChaseClip;
    private List<int> shuffleBag;
    private int shuffleIndex;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        stateMachine = GetComponent<StateMachine>();
        agent = GetComponent<NavMeshAgent>();
        stateMachine.Initialise();
        player = GameObject.FindGameObjectWithTag("Player");
        RebuildShuffleBag();
    }

    // Update is called once per frame
    void Update()
    {
        CanSeePlayer();
        currentState = stateMachine.activeState.ToString();
        //debugsphere.transform.position = lastKnownPos;
    }

    public bool CanSeePlayer() 
    {
        if (player != null)
        {
            //is the player close enough to be seen
            if (Vector3.Distance(transform.position, player.transform.position) < sightDistance)
            {
                Vector3 targetDirection = player.transform.position - transform.position - (Vector3.up * eyeHeight);
                float angleToPlayer = Vector3.Angle(targetDirection, transform.forward);
                if (angleToPlayer >= -fieldOfView && angleToPlayer <= fieldOfView)
                {
                    Ray ray = new Ray(transform.position + (Vector3.up * eyeHeight), targetDirection);
                    RaycastHit hitInfo = new RaycastHit();
                    if (Physics.Raycast(ray, out hitInfo, sightDistance))
                    {
                        if (hitInfo.transform.gameObject == player)
                        {
                            Debug.DrawRay(ray.origin, ray.direction * sightDistance, Color.red);
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    // Called by GuardArea trigger
    public void SetPlayerInGuardArea(bool inside)
    {
        IsPlayerInGuardArea = inside;
    }

    // Audio control for chase state – plays once on state enter
    public void PlayChaseAudio()
    {
        if (chaseAudioSource == null) return;

        AudioClip clipToPlay = null;

        if (!hasPlayedFirstChaseClip && firstChaseClip != null)
        {
            clipToPlay = firstChaseClip;
            hasPlayedFirstChaseClip = true;
        }
        else
        {
            int idx = GetNextShuffleIndex();
            if (idx >= 0) clipToPlay = subsequentChaseClips[idx];
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

    public void StopChaseAudio()
    {
        if (chaseAudioSource != null && chaseAudioSource.isPlaying)
        {
            chaseAudioSource.Stop();
        }
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
            int j = Random.Range(0, i + 1);
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
}
