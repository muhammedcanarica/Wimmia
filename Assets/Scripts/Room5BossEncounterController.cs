using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Room5BossEncounterController : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private OctopusBossController bossController;
    [SerializeField] private OctopusBossAttackSelector attackSelector;

    [Header("Encounter Trigger")]
    [SerializeField] private Collider2D encounterTrigger;
    [SerializeField] private LayerMask playerLayer = 1;
    [SerializeField] private bool startOnlyOnce = true;

    [Header("Doors")]
    [SerializeField] private Door entranceDoor;
    [SerializeField] private Door exitDoor;

    [Header("Room5 Camera")]
    [SerializeField] private Room5EnterTrigger room5Camera;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float introDelay = 1.25f;
    [SerializeField, Min(0f)] private float deathDelay = 1.25f;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource bossMusicSource;
    [SerializeField] private AudioClip bossMusicClip;
    [SerializeField] private AudioClip victoryMusicClip;

    [Header("Optional UI")]
    [SerializeField] private GameObject bossHealthBar;

    private Coroutine introRoutine;
    private Coroutine deathRoutine;
    private bool hasStarted;
    private bool encounterActive;
    private bool encounterCompleted;
    private bool bossDeathSubscribed;

    public bool HasStarted => hasStarted;
    public bool IsEncounterActive => encounterActive;
    public bool IsEncounterCompleted => encounterCompleted;

    private void Reset()
    {
        ResolveReferences();
        ConfigureTrigger();
        ResolveDefaultPlayerLayer();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureTrigger();
        PrepareEncounterBeforePlayerEntry();
        LogMissingRequiredReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToBossDeath();

        if (bossController != null && bossController.IsDead)
            HandleBossDied();
    }

    private void OnDisable()
    {
        UnsubscribeFromBossDeath();
        StopEncounterCoroutines();
    }

    private void OnValidate()
    {
        introDelay = Mathf.Max(0f, introDelay);
        deathDelay = Mathf.Max(0f, deathDelay);
        ResolveReferences();
        ConfigureTrigger();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayerCollider(other))
            BeginEncounter();
    }

    public void BeginEncounter()
    {
        ResolveReferences();

        if (encounterCompleted || encounterActive)
            return;

        if (startOnlyOnce && hasStarted)
            return;

        if (bossController == null)
        {
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Encounter cannot start because Boss Controller is missing on '{name}'.", this);
            return;
        }

        if (bossController.IsDead)
        {
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Encounter was not started because the boss is already dead.", this);
            return;
        }

        hasStarted = true;
        encounterActive = true;

        attackSelector?.StopLoopAndCleanupActiveAttacks();
        bossController.EnterIdleState();
        room5Camera?.ActivateRoom5Camera();
        entranceDoor?.Close();
        exitDoor?.Close();
        SetBossHealthBarVisible(true);
        StartBossMusic();

        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        if (introDelay > 0f)
            yield return new WaitForSeconds(introDelay);

        introRoutine = null;

        if (!encounterActive || encounterCompleted || bossController == null || bossController.IsDead)
            yield break;

        if (attackSelector == null)
        {
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Intro finished, but no Attack Selector is assigned.", this);
            yield break;
        }

        attackSelector.StartLoop();
    }

    private void HandleBossDied()
    {
        if (encounterCompleted)
            return;

        encounterActive = false;
        encounterCompleted = true;

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        attackSelector?.StopLoopAndCleanupActiveAttacks();
        PlayVictoryMusicOrStopBossMusic();

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);

        deathRoutine = StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (deathDelay > 0f)
            yield return new WaitForSeconds(deathDelay);

        deathRoutine = null;

        if (exitDoor != null)
            exitDoor.Open();
        else
            entranceDoor?.Open();

        SetBossHealthBarVisible(false);
    }

    private void PrepareEncounterBeforePlayerEntry()
    {
        if (bossController != null && bossController.IsDead)
        {
            encounterCompleted = true;
            exitDoor?.Open();
            SetBossHealthBarVisible(false);
            return;
        }

        attackSelector?.StopLoopAndCleanupActiveAttacks();
        bossController?.EnterIdleState();
        entranceDoor?.Open();
        exitDoor?.Close();
        SetBossHealthBarVisible(false);

        if (bossMusicSource != null)
            bossMusicSource.Stop();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        GameObject bodyObject = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        int otherLayerMask = 1 << other.gameObject.layer;
        int bodyLayerMask = 1 << bodyObject.layer;
        if ((playerLayer.value & otherLayerMask) == 0 && (playerLayer.value & bodyLayerMask) == 0)
            return false;

        if (other.CompareTag("Player") || bodyObject.CompareTag("Player"))
            return true;

        return other.GetComponentInParent<PlayerController>() != null;
    }

    private void StartBossMusic()
    {
        if (bossMusicSource == null || bossMusicClip == null)
            return;

        bossMusicSource.Stop();
        bossMusicSource.clip = bossMusicClip;
        bossMusicSource.loop = true;
        bossMusicSource.Play();
    }

    private void PlayVictoryMusicOrStopBossMusic()
    {
        if (bossMusicSource == null)
            return;

        bossMusicSource.Stop();

        if (victoryMusicClip == null)
            return;

        bossMusicSource.clip = victoryMusicClip;
        bossMusicSource.loop = false;
        bossMusicSource.Play();
    }

    private void SetBossHealthBarVisible(bool visible)
    {
        if (bossHealthBar != null)
            bossHealthBar.SetActive(visible);
    }

    private void ResolveReferences()
    {
        if (encounterTrigger == null)
            encounterTrigger = GetComponent<Collider2D>();

        if (room5Camera == null)
            room5Camera = GetComponent<Room5EnterTrigger>();

        Room room = GetComponentInParent<Room>();
        if (bossController == null && room != null)
            bossController = room.GetComponentInChildren<OctopusBossController>(true);

        if (attackSelector == null && bossController != null)
            attackSelector = bossController.GetComponent<OctopusBossAttackSelector>();
    }

    private void ResolveDefaultPlayerLayer()
    {
        if (playerLayer.value != 0)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            playerLayer = 1 << player.gameObject.layer;
    }

    private void ConfigureTrigger()
    {
        if (encounterTrigger != null)
            encounterTrigger.isTrigger = true;
    }

    private void SubscribeToBossDeath()
    {
        if (bossDeathSubscribed || bossController == null)
            return;

        bossController.Died += HandleBossDied;
        bossDeathSubscribed = true;
    }

    private void UnsubscribeFromBossDeath()
    {
        if (!bossDeathSubscribed)
            return;

        if (bossController != null)
            bossController.Died -= HandleBossDied;

        bossDeathSubscribed = false;
    }

    private void StopEncounterCoroutines()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
    }

    private void LogMissingRequiredReferences()
    {
        if (bossController == null)
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Boss Controller is not assigned on '{name}'.", this);

        if (attackSelector == null)
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Attack Selector is not assigned on '{name}'.", this);

        if (encounterTrigger == null)
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Encounter Trigger is not assigned on '{name}'.", this);

        if (room5Camera == null)
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Room5 camera trigger/controller is not assigned on '{name}'.", this);

        if (entranceDoor == null)
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Entrance Door is not assigned on '{name}'.", this);

        if (exitDoor == null)
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Exit Door is not assigned on '{name}'.", this);
    }
}
