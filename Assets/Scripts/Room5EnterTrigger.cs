using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Room5EnterTrigger : MonoBehaviour
{
    [Header("Room5 Camera")]
    [SerializeField] private Room room5;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool activateOnce = true;

    [Header("Optional Boss Defeat Restore")]
    [SerializeField] private OctopusBossController boss;
    [SerializeField] private bool restoreNormalCameraOnBossDefeated = true;
    [SerializeField] private Room roomToActivateAfterBossDefeat;

    private bool hasActivated;
    private bool warnedMissingRoom;
    private bool warnedMissingCameraManager;

    private void Reset()
    {
        EnsureTriggerCollider();
        ResolveRoom5();
        ResolveBoss();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveRoom5();
        ResolveBoss();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        ResolveRoom5();
        ResolveBoss();
    }

    private void OnEnable()
    {
        ResolveBoss();
        if (boss != null)
        {
            boss.Died += HandleBossDefeated;
        }
    }

    private void OnDisable()
    {
        if (boss != null)
        {
            boss.Died -= HandleBossDefeated;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag(playerTag))
        {
            return;
        }

        ActivateRoom5Camera();
    }

    public void ActivateRoom5Camera()
    {
        if (activateOnce && hasActivated)
        {
            return;
        }

        Room targetRoom = ResolveRoom5();
        if (targetRoom == null)
        {
            if (!warnedMissingRoom)
            {
                warnedMissingRoom = true;
                Debug.LogWarning($"[{nameof(Room5EnterTrigger)}] No Room5 reference is assigned on '{name}'.", this);
            }

            return;
        }

        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager == null)
        {
            if (!warnedMissingCameraManager)
            {
                warnedMissingCameraManager = true;
                Debug.LogWarning($"[{nameof(Room5EnterTrigger)}] No active CameraManager was found.", this);
            }

            return;
        }

        targetRoom.SetCameraOverrideActive(true);
        cameraManager.ActivateRoom(targetRoom);
        hasActivated = true;
    }

    public void RestoreNormalCamera()
    {
        Room targetRoom = ResolveRoom5();
        if (targetRoom != null)
        {
            targetRoom.SetCameraOverrideActive(false);
        }

        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager != null && roomToActivateAfterBossDefeat != null)
        {
            cameraManager.ActivateRoom(roomToActivateAfterBossDefeat);
        }
    }

    public void ResetActivation()
    {
        hasActivated = false;
    }

    private void HandleBossDefeated()
    {
        if (restoreNormalCameraOnBossDefeated)
        {
            RestoreNormalCamera();
        }
    }

    private Room ResolveRoom5()
    {
        if (room5 == null)
        {
            room5 = GetComponentInParent<Room>();
        }

        return room5;
    }

    private void ResolveBoss()
    {
        if (boss == null)
        {
            boss = GetComponentInParent<OctopusBossController>();
        }

        if (boss == null && room5 != null)
        {
            boss = room5.GetComponentInChildren<OctopusBossController>(true);
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
