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
        ActivateCamera(currentCameraIndex);
        ActivateCinemachineCamera(currentCinemachineIndex);
    }

    void Update()
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

        // Check for Animator on active Cinemachine camera and enable on J press
        Animator animator = GetActiveCinemachineAnimator();
        if (animator != null && Input.GetKeyDown(KeyCode.J))
        {
            animator.enabled = true;
        }
    }

    private Animator GetActiveCinemachineAnimator()
    {
        if (currentCinemachineIndex >= 0 && currentCinemachineIndex < cinemachineCameras.Length)
        {
            return cinemachineCameras[currentCinemachineIndex].GetComponent<Animator>();
        }
        return null;
    }

    void SwitchToCamera(int index)
    {
        if (index >= 0 && index < cameras.Length)
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
        if (index >= 0 && index < cinemachineCameras.Length)
        {
            currentCinemachineIndex = index;
            ActivateCinemachineCamera(currentCinemachineIndex);
        }
    }

    void ActivateCinemachineCamera(int index)
    { 
        for (int i = 0; i < cinemachineCameras.Length; i++)
        {
            /*bool isActive = (i == index);
            cinemachineCameras[i].SetActive(isActive);

            Animator animator = cinemachineCameras[i].GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = isActive;
            }*/

            cinemachineCameras[i].SetActive(i == index); // old code

        }
    }

    public void ActivateCinemachineCameraByIndex(int index)
    {
        if (index >= 0 && index < cinemachineCameras.Length)
        {
            currentCinemachineIndex = index;
            ActivateCinemachineCamera(currentCinemachineIndex);
        }
    }

    public void ActivateCameraByIndex(int index)
    {
        if (index >= 0 && index < cameras.Length)
        {
            currentCameraIndex = index;
            ActivateCamera(currentCameraIndex);
        }       
    }
}