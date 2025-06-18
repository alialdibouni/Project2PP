using UnityEngine;

public class CameraTriggerZone : MonoBehaviour
{
    public int cinemachineCameraIndex;
    public CameraSwitcher cameraSwitcher;
    public int cameraIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && cameraSwitcher != null)
        {
            cameraSwitcher.ActivateCameraByIndex(cameraIndex);
            cameraSwitcher.ActivateCinemachineCameraByIndex(cinemachineCameraIndex);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
