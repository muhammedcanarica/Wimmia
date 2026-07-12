using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private Room startingRoom;
    [SerializeField] private int activePriority = 100;
    [SerializeField] private int inactivePriority = 0;

    private Room currentRoom;
    private Room[] rooms;
    private float lastScreenAspect = -1f;
    private CinemachineBrain cinemachineBrain;
    private CinemachineBlendDefinition defaultBlend;
    private Coroutine restoreBlendCoroutine;
    private bool hasDefaultBlend;

    public Room CurrentRoom => currentRoom;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate {nameof(CameraManager)} found on '{name}'. Destroying this instance.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        CacheDefaultBlend();
        DeactivateAllRooms();
        LogDuplicatePlayers();
    }

    private void Start()
    {
        lastScreenAspect = GetScreenAspect();

        if (startingRoom != null)
        {
            ActivateRoom(startingRoom);
            return;
        }

        ResetMainCameraViewport();
    }

    private void OnDestroy()
    {
        RestoreDefaultBlend();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        float currentAspect = GetScreenAspect();
        if (currentRoom != null)
        {
            currentRoom.FitCameraToRoom(Camera.main, currentAspect);
        }
        else if (!Mathf.Approximately(currentAspect, lastScreenAspect))
        {
            ResetMainCameraViewport();
        }

        lastScreenAspect = currentAspect;
    }

    public void ActivateRoom(Room room)
    {
        if (room == null)
        {
            return;
        }

        float currentAspect = GetScreenAspect();

        if (room == currentRoom)
        {
            room.FitCameraToRoom(Camera.main, currentAspect);
            lastScreenAspect = currentAspect;
            return;
        }

        if (currentRoom != null)
        {
            currentRoom.SetPriority(inactivePriority);
        }

        PrepareRoomTransition(room);
        room.SetPriority(activePriority);
        room.FitCameraToRoom(Camera.main, currentAspect);
        currentRoom = room;
        lastScreenAspect = currentAspect;
    }

    private void DeactivateAllRooms()
    {
        if (rooms == null)
        {
            return;
        }

        foreach (Room room in rooms)
        {
            if (room == null)
            {
                continue;
            }

            room.SetPriority(inactivePriority);
        }
    }

    private void PrepareRoomTransition(Room destinationRoom)
    {
        RestoreDefaultBlend();

        if (destinationRoom == null || !destinationRoom.UsesArenaOverview)
        {
            return;
        }

        CacheDefaultBlend();
        if (cinemachineBrain == null || !hasDefaultBlend)
        {
            return;
        }

        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut,
            destinationRoom.ArenaTransitionDuration);
        restoreBlendCoroutine = StartCoroutine(RestoreDefaultBlendAfterCameraUpdate());
    }

    private IEnumerator RestoreDefaultBlendAfterCameraUpdate()
    {
        yield return new WaitForEndOfFrame();

        if (cinemachineBrain != null && hasDefaultBlend)
        {
            cinemachineBrain.DefaultBlend = defaultBlend;
        }

        restoreBlendCoroutine = null;
    }

    private void CacheDefaultBlend()
    {
        if (hasDefaultBlend)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        cinemachineBrain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
        if (cinemachineBrain == null)
        {
            return;
        }

        defaultBlend = cinemachineBrain.DefaultBlend;
        hasDefaultBlend = true;
    }

    private void RestoreDefaultBlend()
    {
        if (restoreBlendCoroutine != null)
        {
            StopCoroutine(restoreBlendCoroutine);
            restoreBlendCoroutine = null;
        }

        if (cinemachineBrain != null && hasDefaultBlend)
        {
            cinemachineBrain.DefaultBlend = defaultBlend;
        }
    }

    private float GetScreenAspect()
    {
        if (Screen.height <= 0)
        {
            return 16f / 9f;
        }

        return (float)Screen.width / Screen.height;
    }

    private void ResetMainCameraViewport()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private void LogDuplicatePlayers()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length <= 1)
        {
            return;
        }

        string[] playerNames = new string[players.Length];
        for (int i = 0; i < players.Length; i++)
        {
            playerNames[i] = $"{players[i].name}#{players[i].GetInstanceID()}";
        }

        Debug.LogWarning(
            $"[{nameof(CameraManager)}] Multiple player instances detected: {string.Join(", ", playerNames)}. " +
            "Room triggers may activate the wrong camera target until duplicate players are removed.",
            this);
    }
}
