using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class DriverAnimatorController : MonoBehaviour
{
    public Animator animator;
    public CarController carController; // Reference to your CarController
    //public GameObject steeringWheel;
    private float animatorTurnAngle; // Angle for the animator to control turning
    private float horizontal; // Horizontal input for steering

    private bool wasSteeringLeft = false;
    private bool wasSteeringRight = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float steeringInput = 0f;
        if (carController != null)
        {
            steeringInput = carController.CurrentSteeringInput;
        }

        animatorTurnAngle = Mathf.Lerp(animatorTurnAngle, -horizontal, 20 * Time.deltaTime);
        animator.SetFloat("turnAngle", steeringInput);

        //steeringWheel.transform.localRotation = Quaternion.Euler(0, animatorTurnAngle * 35, 0);
    }
}
