using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 100f;
    public bool lockCursor = true;

    [Header("Camera Rotation Limits")]
    public float minPitch = -90f;
    public float maxPitch = 90f;
    //public float minYaw = -180f;
    //public float maxYaw = 180f;

    [SerializeField] private float yaw = 0f;
    [SerializeField] private float pitch = 0f;

    [Header("Zoom Settings")]
    public float defaultFOV = 90f;
    public float zoomFOV = 30f;
    public float zoomSpeed = 10f;
    public float minZoomFOV = 15f;
    public float maxZoomFOV = 60f;
    public float zoomScrollSensitivity = 10f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>();
        }
        if (cam != null)
        {
            cam.fieldOfView = defaultFOV;
        }

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        HandleMouseLook();
        HandleCursorToggle();
        HandleZoom();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.eulerAngles = new Vector3(pitch, yaw, 0f);
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (lockCursor && Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void HandleZoom()
    {
        if (cam == null) return;

        // Allow scroll wheel to adjust zoomFOV only while zoomed in
        if (Input.GetMouseButton(1))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                zoomFOV -= scroll * zoomScrollSensitivity;
                zoomFOV = Mathf.Clamp(zoomFOV, minZoomFOV, maxZoomFOV);
            }
        }

        float targetFOV = Input.GetMouseButton(1) ? zoomFOV : defaultFOV;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, zoomSpeed * Time.deltaTime);
    }
}
