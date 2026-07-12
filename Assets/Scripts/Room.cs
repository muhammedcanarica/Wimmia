using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class Room : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCameraBase virtualCamera;
    [SerializeField] private BoxCollider2D cameraBounds;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool fitCameraToRoom = true;
    [SerializeField] private bool showEntireRoom;
    [SerializeField] private Vector2 cameraPadding = Vector2.zero;
    [SerializeField, Min(0f)] private float arenaTransitionDuration = 0.35f;
    [SerializeField] private float minimumOrthographicSize = 0.1f;
    [SerializeField] private Color activeGizmoFillColor = new Color(1f, 0.9f, 0.1f, 0.16f);
    [SerializeField] private Color activeGizmoOutlineColor = new Color(1f, 0.9f, 0.1f, 1f);
    [SerializeField] private Color inactiveGizmoFillColor = new Color(1f, 0.15f, 0.15f, 0.12f);
    [SerializeField] private Color inactiveGizmoOutlineColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    [SerializeField] private Color cameraFrameColor = new Color(0.3f, 1f, 0.3f, 0.95f);
    [SerializeField] private Color cameraCenterColor = new Color(0.3f, 1f, 0.3f, 0.75f);

    private const string CameraBoundsChildName = "CameraBounds";

    private Transform trackedPlayer;
    private float preferredOrthographicSize = -1f;

    public CinemachineVirtualCameraBase VirtualCamera => virtualCamera;
    public bool UsesArenaOverview => fitCameraToRoom && showEntireRoom;
    public float ArenaTransitionDuration => arenaTransitionDuration;

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveCameraBoundsCollider();
        CachePreferredOrthographicSize();

        if (virtualCamera == null)
        {
            Debug.LogWarning($"[{nameof(Room)}] No virtual camera assigned on '{name}'.", this);
        }
    }

    private void Reset()
    {
        EnsureTriggerCollider();
        ResolveCameraBoundsCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        ResolveCameraBoundsCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        TrackPlayer(other);
        CameraManager.Instance?.ActivateRoom(this);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        TrackPlayer(other);
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

    public void FitCameraToRoom(Camera outputCamera, float screenAspect)
    {
        ResetOutputCameraViewport(outputCamera);

        if (!fitCameraToRoom || virtualCamera == null)
        {
            return;
        }

        CinemachineCamera cinemachineCamera = virtualCamera as CinemachineCamera;
        if (cinemachineCamera == null)
        {
            return;
        }

        CachePreferredOrthographicSize(cinemachineCamera);

        float safeAspect = screenAspect > 0.0001f ? screenAspect : GetCurrentScreenAspect();
        Bounds bounds = GetCameraBounds();
        float targetOrthographicSize = showEntireRoom
            ? CalculateOverviewOrthographicSize(bounds, safeAspect)
            : CalculateOrthographicSize(bounds, safeAspect);

        LensSettings lens = cinemachineCamera.Lens;
        lens.OrthographicSize = targetOrthographicSize;
        cinemachineCamera.Lens = lens;

        if (showEntireRoom)
        {
            AlignCameraToRoomCenter(bounds);
            return;
        }

        AlignCameraWithinBounds(bounds, ResolveFollowPosition(bounds.center), safeAspect, targetOrthographicSize);
    }

    private void TrackPlayer(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        trackedPlayer = body != null ? body.transform : other.transform;
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider2D roomTrigger = GetComponent<BoxCollider2D>();
        if (roomTrigger != null)
        {
            roomTrigger.isTrigger = true;
        }
    }

    private BoxCollider2D ResolveCameraBoundsCollider()
    {
        Transform boundsChild = transform.Find(CameraBoundsChildName);
        if (boundsChild != null)
        {
            BoxCollider2D childBounds = boundsChild.GetComponent<BoxCollider2D>();
            if (childBounds != null)
            {
                cameraBounds = childBounds;
                return cameraBounds;
            }
        }

        if (cameraBounds != null)
        {
            return cameraBounds;
        }

        cameraBounds = GetComponent<BoxCollider2D>();

        return cameraBounds;
    }

    private void CachePreferredOrthographicSize(CinemachineCamera cinemachineCamera = null)
    {
        if (preferredOrthographicSize > 0f)
        {
            return;
        }

        if (cinemachineCamera == null)
        {
            cinemachineCamera = virtualCamera as CinemachineCamera;
        }

        if (cinemachineCamera == null)
        {
            preferredOrthographicSize = Mathf.Max(0.01f, minimumOrthographicSize);
            return;
        }

        preferredOrthographicSize = Mathf.Max(0.01f, cinemachineCamera.Lens.OrthographicSize);
    }

    private Bounds GetCameraBounds()
    {
        BoxCollider2D boundsCollider = ResolveCameraBoundsCollider();
        if (boundsCollider == null)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        return BuildBounds(boundsCollider);
    }

    private float CalculateOrthographicSize(Bounds bounds, float screenAspect)
    {
        float usableWidth = Mathf.Max(0.01f, bounds.size.x - (cameraPadding.x * 2f));
        float usableHeight = Mathf.Max(0.01f, bounds.size.y - (cameraPadding.y * 2f));
        float maxSizeByWidth = usableWidth / (2f * Mathf.Max(screenAspect, 0.0001f));
        float maxSizeByHeight = usableHeight * 0.5f;
        float maxAllowedSize = Mathf.Max(0.01f, Mathf.Min(maxSizeByWidth, maxSizeByHeight));
        float desiredSize = Mathf.Max(0.01f, Mathf.Max(minimumOrthographicSize, preferredOrthographicSize));
        return Mathf.Min(desiredSize, maxAllowedSize);
    }

    private float CalculateOverviewOrthographicSize(Bounds bounds, float screenAspect)
    {
        float horizontalPadding = Mathf.Max(0f, cameraPadding.x);
        float verticalPadding = Mathf.Max(0f, cameraPadding.y);
        float paddedWidth = Mathf.Max(0.01f, bounds.size.x + (horizontalPadding * 2f));
        float paddedHeight = Mathf.Max(0.01f, bounds.size.y + (verticalPadding * 2f));
        float sizeForWidth = paddedWidth / (2f * Mathf.Max(screenAspect, 0.0001f));
        float sizeForHeight = paddedHeight * 0.5f;
        return Mathf.Max(minimumOrthographicSize, sizeForWidth, sizeForHeight);
    }

    private Vector3 ResolveFollowPosition(Vector3 fallbackPosition)
    {
        if (trackedPlayer == null || !trackedPlayer.gameObject.activeInHierarchy)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            trackedPlayer = taggedPlayer != null ? taggedPlayer.transform : null;
        }

        return trackedPlayer != null ? trackedPlayer.position : fallbackPosition;
    }

    private void AlignCameraWithinBounds(Bounds bounds, Vector3 followPosition, float screenAspect, float orthographicSize)
    {
        Transform cameraTransform = virtualCamera.transform;
        float halfHeight = orthographicSize;
        float halfWidth = orthographicSize * Mathf.Max(screenAspect, 0.0001f);
        float minX = bounds.min.x + cameraPadding.x + halfWidth;
        float maxX = bounds.max.x - cameraPadding.x - halfWidth;
        float minY = bounds.min.y + cameraPadding.y + halfHeight;
        float maxY = bounds.max.y - cameraPadding.y - halfHeight;

        float targetX = minX <= maxX
            ? Mathf.Clamp(followPosition.x, minX, maxX)
            : bounds.center.x;
        float targetY = minY <= maxY
            ? Mathf.Clamp(followPosition.y, minY, maxY)
            : bounds.center.y;

        Vector3 currentPosition = cameraTransform.position;
        cameraTransform.position = new Vector3(targetX, targetY, currentPosition.z);
    }

    private void AlignCameraToRoomCenter(Bounds bounds)
    {
        Transform cameraTransform = virtualCamera.transform;
        Vector3 currentPosition = cameraTransform.position;
        cameraTransform.position = new Vector3(bounds.center.x, bounds.center.y, currentPosition.z);
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D roomTrigger = GetComponent<BoxCollider2D>();
        if (roomTrigger == null)
        {
            return;
        }

        bool isActiveRoom = CameraManager.Instance != null && CameraManager.Instance.CurrentRoom == this;
        Color fillColor = isActiveRoom ? activeGizmoFillColor : inactiveGizmoFillColor;
        Color outlineColor = isActiveRoom ? activeGizmoOutlineColor : inactiveGizmoOutlineColor;

        DrawColliderGizmo(roomTrigger, fillColor, outlineColor, true);

        BoxCollider2D boundsCollider = cameraBounds != null ? cameraBounds : transform.Find(CameraBoundsChildName)?.GetComponent<BoxCollider2D>();
        if (boundsCollider != null && boundsCollider != roomTrigger)
        {
            DrawColliderGizmo(boundsCollider, new Color(0.1f, 0.7f, 1f, 0.08f), new Color(0.15f, 0.75f, 1f, 0.9f), false);
        }

        DrawCameraGizmo();
    }

    private void DrawColliderGizmo(BoxCollider2D collider2D, Color fillColor, Color outlineColor, bool drawCorners)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = collider2D.transform.localToWorldMatrix;

        Vector3 colliderCenter = collider2D.offset;
        Vector3 colliderSize = collider2D.size;

        Gizmos.color = fillColor;
        Gizmos.DrawCube(colliderCenter, colliderSize);

        Gizmos.color = outlineColor;
        Gizmos.DrawWireCube(colliderCenter, colliderSize);

        if (drawCorners)
        {
            float cornerSize = Mathf.Clamp(Mathf.Min(colliderSize.x, colliderSize.y) * 0.12f, 0.35f, 2.5f);
            float halfWidth = colliderSize.x * 0.5f;
            float halfHeight = colliderSize.y * 0.5f;

            DrawCorner(colliderCenter, new Vector3(-halfWidth, halfHeight, 0f), Vector3.right, Vector3.down, cornerSize);
            DrawCorner(colliderCenter, new Vector3(halfWidth, halfHeight, 0f), Vector3.left, Vector3.down, cornerSize);
            DrawCorner(colliderCenter, new Vector3(-halfWidth, -halfHeight, 0f), Vector3.right, Vector3.up, cornerSize);
            DrawCorner(colliderCenter, new Vector3(halfWidth, -halfHeight, 0f), Vector3.left, Vector3.up, cornerSize);
        }

        Gizmos.matrix = previousMatrix;
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

        CinemachineCamera cinemachineCamera = virtualCamera as CinemachineCamera;
        Camera mainCamera = Camera.main;
        if (cinemachineCamera == null || mainCamera == null || !mainCamera.orthographic)
        {
            return;
        }

        Transform cameraTransform = virtualCamera.transform;
        float halfHeight = cinemachineCamera.Lens.OrthographicSize;
        float halfWidth = halfHeight * GetViewportAspect(mainCamera);
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

    private static Bounds BuildBounds(BoxCollider2D collider2D)
    {
        Vector3 center = collider2D.transform.TransformPoint(collider2D.offset);
        Vector3 lossyScale = collider2D.transform.lossyScale;
        Vector3 size = new Vector3(
            Mathf.Abs(collider2D.size.x * lossyScale.x),
            Mathf.Abs(collider2D.size.y * lossyScale.y),
            0f);
        return new Bounds(center, size);
    }

    private static void ResetOutputCameraViewport(Camera outputCamera)
    {
        if (outputCamera != null)
        {
            outputCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    private static float GetViewportAspect(Camera camera)
    {
        if (camera == null)
        {
            return 16f / 9f;
        }

        Rect rect = camera.rect;
        if (rect.height <= 0.0001f || Screen.height <= 0)
        {
            return 16f / 9f;
        }

        float screenAspect = (float)Screen.width / Screen.height;
        return screenAspect * (rect.width / rect.height);
    }

    private static float GetCurrentScreenAspect()
    {
        if (Screen.height <= 0)
        {
            return 16f / 9f;
        }

        return (float)Screen.width / Screen.height;
    }
}
