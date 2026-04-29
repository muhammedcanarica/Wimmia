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
        DeactivateAllRooms();
    }

    private void Start()
    {
        if (startingRoom != null)
        {
            ActivateRoom(startingRoom);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ActivateRoom(Room room)
    {
        if (room == null || room == currentRoom)
        {
            return;
        }

        if (currentRoom != null)
        {
            currentRoom.SetPriority(inactivePriority);
        }

        room.SetPriority(activePriority);
        currentRoom = room;
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
}
