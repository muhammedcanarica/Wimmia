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

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

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

    private IEnumerator Start()
    {
        // Trigger callbacks are not guaranteed when the player is already past
        // the entrance trigger at scene start (checkpoint/test spawn).
        yield return null;
        TryStartForPlayerAlreadyInsideRoom5();
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
        TryStartFromTrigger(other, "OnTriggerEnter2D");
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartFromTrigger(other, "OnTriggerStay2D");
    }

    public void BeginEncounter()
    {
        ResolveReferences();

        if (encounterCompleted)
        {
            DebugEncounter("Encounter başlatılamadı: encounter daha önce tamamlandı.");
            return;
        }

        if (encounterActive)
        {
            DebugEncounter("Encounter başlatılamadı: encounter zaten aktif.");
            return;
        }

        if (startOnlyOnce && hasStarted)
        {
            DebugEncounter("Encounter başlatılamadı: startOnlyOnce açık ve encounter daha önce başlatıldı.");
            return;
        }

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
        DebugEncounter($"Intro başladı. Süre: {introDelay:0.##} saniye. Boss state: {bossController.CurrentState}.");

        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        if (introDelay > 0f)
            yield return new WaitForSeconds(introDelay);

        introRoutine = null;

        if (!encounterActive || encounterCompleted)
        {
            DebugEncounter("Attack selector başlatılmadı: encounter artık aktif değil veya tamamlandı.");
            yield break;
        }

        if (bossController == null || bossController.IsDead)
        {
            DebugEncounter("Attack selector başlatılmadı: boss referansı eksik veya boss ölü.");
            yield break;
        }

        if (attackSelector == null)
        {
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Intro finished, but no Attack Selector is assigned.", this);
            yield break;
        }

        if (!attackSelector.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[{nameof(Room5BossEncounterController)}] Attack Selector GameObject is inactive, so the attack loop cannot start.", this);
            yield break;
        }

        if (!attackSelector.enabled)
        {
            attackSelector.enabled = true;
            DebugEncounter("Attack Selector component disabled durumdaydı ve encounter tarafından etkinleştirildi.");
        }

        attackSelector.StartLoop();

        if (attackSelector.IsLoopRunning)
        {
            DebugEncounter($"Attack selector başlatıldı. Boss state: {bossController.CurrentState}.");
        }
        else
        {
            Debug.LogWarning(
                $"[{nameof(Room5BossEncounterController)}] Attack Selector StartLoop çağrıldı fakat loop başlamadı. " +
                $"Selector enabled: {attackSelector.enabled}, Boss state: {bossController.CurrentState}, Boss dead: {bossController.IsDead}.",
                this);
        }
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

    private void TryStartFromTrigger(Collider2D other, string callbackName)
    {
        DebugEncounter($"Encounter trigger algılandı ({callbackName}): {(other != null ? other.name : "null")}.");

        if (!IsPlayerCollider(other))
        {
            DebugEncounter("Encounter başlatılmadı: collider Player layer/tag/controller doğrulamasından geçmedi.");
            return;
        }

        DebugEncounter("Player doğrulandı. Encounter başlatılıyor.");
        BeginEncounter();
    }

    private void TryStartForPlayerAlreadyInsideRoom5()
    {
        if (hasStarted || encounterActive || encounterCompleted)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            DebugEncounter("Başlangıç kontrolü encounter başlatmadı: aktif PlayerController bulunamadı.");
            return;
        }

        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (IsPlayerCollider(playerCollider) && OverlapsIn2D(encounterTrigger, playerCollider))
            {
                DebugEncounter("Player sahne başlangıcında EncounterTrigger içinde bulundu. Encounter başlatılıyor.");
                BeginEncounter();
                return;
            }
        }

        Room room5 = GetComponentInParent<Room>();
        Collider2D roomArea = room5 != null ? room5.GetComponent<Collider2D>() : null;
        if (roomArea != null && roomArea.OverlapPoint(player.transform.position))
        {
            DebugEncounter("Player giriş trigger'ını geçmiş ve Room5 içinde başladı. Encounter güvenli şekilde başlatılıyor.");
            BeginEncounter();
        }
    }

    private static bool OverlapsIn2D(Collider2D first, Collider2D second)
    {
        if (first == null || second == null || !first.enabled || !second.enabled)
            return false;

        Bounds firstBounds = first.bounds;
        Bounds secondBounds = second.bounds;
        return firstBounds.min.x <= secondBounds.max.x &&
            firstBounds.max.x >= secondBounds.min.x &&
            firstBounds.min.y <= secondBounds.max.y &&
            firstBounds.max.y >= secondBounds.min.y;
    }

    private void DebugEncounter(string message)
    {
        if (debugLogs)
            Debug.Log($"[{nameof(Room5BossEncounterController)}] {message}", this);
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
