using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class Room : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCameraBase virtualCamera;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Color activeGizmoFillColor = new Color(1f, 0.9f, 0.1f, 0.16f);
    [SerializeField] private Color activeGizmoOutlineColor = new Color(1f, 0.9f, 0.1f, 1f);
    [SerializeField] private Color inactiveGizmoFillColor = new Color(1f, 0.15f, 0.15f, 0.12f);
    [SerializeField] private Color inactiveGizmoOutlineColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    [SerializeField] private Color cameraFrameColor = new Color(0.3f, 1f, 0.3f, 0.95f);
    [SerializeField] private Color cameraCenterColor = new Color(0.3f, 1f, 0.3f, 0.75f);

    public CinemachineVirtualCameraBase VirtualCamera => virtualCamera;

    private void Awake()
    {
        if (virtualCamera == null)
        {
            Debug.LogWarning($"[{nameof(Room)}] No virtual camera assigned on '{name}'.", this);
        }
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        CameraManager.Instance?.ActivateRoom(this);
    }

    public void SetPriority(int priority)
    {
        if (virtualCamera == null)
        {
            return;
        }

        PrioritySettings prioritySettings = virtualCamera.Priority;
        prioritySettings.Enabled = true;
        prioritySettings.Value = priority;
        virtualCamera.Priority = prioritySettings;
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider2D roomBounds = GetComponent<BoxCollider2D>();
        roomBounds.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D roomBounds = GetComponent<BoxCollider2D>();
        if (roomBounds == null)
        {
            return;
        }

        bool isActiveRoom = CameraManager.Instance != null && CameraManager.Instance.CurrentRoom == this;
        Color fillColor = isActiveRoom ? activeGizmoFillColor : inactiveGizmoFillColor;
        Color outlineColor = isActiveRoom ? activeGizmoOutlineColor : inactiveGizmoOutlineColor;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 colliderCenter = roomBounds.offset;
        Vector3 colliderSize = roomBounds.size;
        float cornerSize = Mathf.Clamp(Mathf.Min(colliderSize.x, colliderSize.y) * 0.12f, 0.35f, 2.5f);
        float halfWidth = colliderSize.x * 0.5f;
        float halfHeight = colliderSize.y * 0.5f;

        Gizmos.color = fillColor;
        Gizmos.DrawCube(colliderCenter, colliderSize);

        Gizmos.color = outlineColor;
        Gizmos.DrawWireCube(colliderCenter, colliderSize);

        DrawCorner(colliderCenter, new Vector3(-halfWidth, halfHeight, 0f), Vector3.right, Vector3.down, cornerSize);
        DrawCorner(colliderCenter, new Vector3(halfWidth, halfHeight, 0f), Vector3.left, Vector3.down, cornerSize);
        DrawCorner(colliderCenter, new Vector3(-halfWidth, -halfHeight, 0f), Vector3.right, Vector3.up, cornerSize);
        DrawCorner(colliderCenter, new Vector3(halfWidth, -halfHeight, 0f), Vector3.left, Vector3.up, cornerSize);

        Gizmos.matrix = previousMatrix;

        DrawCameraGizmo();
    }

    private void DrawCorner(Vector3 center, Vector3 cornerOffset, Vector3 horizontalDirection, Vector3 verticalDirection, float length)
    {
        Vector3 corner = center + cornerOffset;
        Gizmos.DrawLine(corner, corner + horizontalDirection * length);
        Gizmos.DrawLine(corner, corner + verticalDirection * length);
    }

    private void DrawCameraGizmo()
    {
        if (virtualCamera == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null || !mainCamera.orthographic)
        {
            return;
        }

        Transform cameraTransform = virtualCamera.transform;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 center = cameraTransform.position;
        Vector3 size = new Vector3(halfWidth * 2f, halfHeight * 2f, 0f);
        float crossSize = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.08f, 0.2f, 1.25f);

        Gizmos.color = cameraFrameColor;
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = cameraCenterColor;
        Gizmos.DrawLine(center + Vector3.left * crossSize, center + Vector3.right * crossSize);
        Gizmos.DrawLine(center + Vector3.up * crossSize, center + Vector3.down * crossSize);
        Gizmos.DrawLine(transform.position, center);
    }
}
