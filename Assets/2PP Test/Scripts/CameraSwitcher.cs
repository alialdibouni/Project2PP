using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Assign your cameras in order")]
    public Camera[] cameras;

    [Header("Assign your cinemachine cameras in order")]
    public GameObject[] cinemachineCameras;

    private int currentCameraIndex = 0;
    private int currentCinemachineIndex = 0;

    void Start()
    {
        if (cameras != null && cameras.Length > 0)
            ActivateCamera(currentCameraIndex);
        if (cinemachineCameras != null && cinemachineCameras.Length > 0)
            ActivateCinemachineCamera(currentCinemachineIndex);
    }

    void Update()
    {
        // Camera switching by number keys
        if (cameras != null && cameras.Length > 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToCamera(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToCamera(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToCamera(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToCamera(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SwitchToCamera(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SwitchToCamera(5);

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
                ActivateCamera(currentCameraIndex);
            }
        }

        // Cinemachine camera switching by number keys
        if (cinemachineCameras != null && cinemachineCameras.Length > 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToCinemachineCamera(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToCinemachineCamera(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToCinemachineCamera(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToCinemachineCamera(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SwitchToCinemachineCamera(4);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SwitchToCinemachineCamera(5);

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                currentCinemachineIndex = (currentCinemachineIndex + 1) % cinemachineCameras.Length;
                ActivateCinemachineCamera(currentCinemachineIndex);
            }

            // Check for Animator on active Cinemachine camera and toggle on J press
            Animator animator = GetActiveCinemachineAnimator();
            if (animator != null && Input.GetKeyDown(KeyCode.J))
            {
                animator.enabled = !animator.enabled;
            }
        }
    }

    private Animator GetActiveCinemachineAnimator()
    {
        if (cinemachineCameras != null &&
            currentCinemachineIndex >= 0 &&
            currentCinemachineIndex < cinemachineCameras.Length)
        {
            return cinemachineCameras[currentCinemachineIndex].GetComponent<Animator>();
        }
        return null;
    }

    void SwitchToCamera(int index)
    {
        if (cameras != null && index >= 0 && index < cameras.Length)
        {
            currentCameraIndex = index;
            ActivateCamera(currentCameraIndex);
        }
    }

    void ActivateCamera(int index)
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].gameObject.SetActive(i == index);
        }
    }   

    void SwitchToCinemachineCamera(int index)
    {
        if (cinemachineCameras != null && index >= 0 && index < cinemachineCameras.Length)
        {
            currentCinemachineIndex = index;
            ActivateCinemachineCamera(currentCinemachineIndex);
        }
    }

    void ActivateCinemachineCamera(int index)
    { 
        for (int i = 0; i < cinemachineCameras.Length; i++)
        {
            cinemachineCameras[i].SetActive(i == index);
        }
    }

    public void ActivateCinemachineCameraByIndex(int index)
    {
        if (cinemachineCameras != null && index >= 0 && index < cinemachineCameras.Length)
        {
            currentCinemachineIndex = index;
            ActivateCinemachineCamera(currentCinemachineIndex);
        }
    }

    public void ActivateCameraByIndex(int index)
    {
        if (cameras != null && index >= 0 && index < cameras.Length)
        {
            currentCameraIndex = index;
            ActivateCamera(currentCameraIndex);
        }       
    }
}