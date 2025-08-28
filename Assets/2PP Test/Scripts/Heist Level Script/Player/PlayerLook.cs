using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    private void Start()
    {
        //lock cursor to center of screen and hide it
        Cursor.lockState = CursorLockMode.Locked;
    }

    public Camera cam;
    private float xRotation = 0f;

    public float xSensitivity = 30f;
    public float ySensitivity = 30f;

    public void ProcessLook(Vector2 input)
    {
        float mouseX = input.x;
        float mouseY = input.y;
        //calculate camera rotation for looking up and down
        xRotation -= (mouseY * Time.deltaTime) * ySensitivity ;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        //apply this rotation to the camera transform
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        //rotate the player object left and right
        transform.Rotate(Vector3.up * (mouseX * Time.deltaTime) * xSensitivity);
    }
}
