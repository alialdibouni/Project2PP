using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Assign your cameras in order (1-4)")]
    public Camera[] cameras;

    private int currentCameraIndex = 0;

    void Start()
    {
        ActivateCamera(currentCameraIndex);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToCamera(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToCamera(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToCamera(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToCamera(3);

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
            ActivateCamera(currentCameraIndex);
        }
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
}