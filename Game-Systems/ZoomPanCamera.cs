using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ZoomPanCamera : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;

    // Most zoomed-in size
    public float minSize = 3f;

    // Fully zoomed-out size
    public float maxSize = 14f;

    [Header("Pan Settings")]
    public float panSpeed = 0.5f;

    private Camera cam;

    // Default camera position
    private Vector3 basePosition;

    // Pan offset in camera-local coordinates
    private Vector2 panOffsetLocal = Vector2.zero;

    private bool isPanning;

    void Awake()
    {
        cam = GetComponent<Camera>();

        if (!cam.orthographic)
            Debug.LogWarning(
                "ZoomPanCamera is designed for an Orthographic Camera."
            );

        basePosition = transform.position;

        cam.orthographicSize = maxSize;
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
        ClampPanToMaxView();
        ApplyPosition();
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (!Mathf.Approximately(scroll, 0f))
        {
            cam.orthographicSize -= scroll * zoomSpeed;

            cam.orthographicSize = Mathf.Clamp(
                cam.orthographicSize,
                minSize,
                maxSize
            );
        }
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(2))
            isPanning = true;

        if (Input.GetMouseButtonUp(2))
            isPanning = false;

        if (!isPanning)
            return;

        // Scale pan speed with zoom
        float scale = cam.orthographicSize * 0.1f;

        float dx =
            -Input.GetAxis("Mouse X") *
            panSpeed *
            scale;

        float dy =
            -Input.GetAxis("Mouse Y") *
            panSpeed *
            scale;

        panOffsetLocal += new Vector2(dx, dy);
    }

    private void ClampPanToMaxView()
    {
        // Limit panning to the fully zoomed-out bounds
        float sizeDifference =
            maxSize - cam.orthographicSize;

        float maxPanY = sizeDifference;
        float maxPanX = sizeDifference * cam.aspect;

        panOffsetLocal.x = Mathf.Clamp(
            panOffsetLocal.x,
            -maxPanX,
            maxPanX
        );

        panOffsetLocal.y = Mathf.Clamp(
            panOffsetLocal.y,
            -maxPanY,
            maxPanY
        );
    }

    private void ApplyPosition()
    {
        // Convert local pan offset to world space
        Vector3 offsetWorld =
            transform.right * panOffsetLocal.x +
            transform.up * panOffsetLocal.y;

        transform.position =
            basePosition + offsetWorld;
    }
}