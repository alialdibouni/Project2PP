using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class DriverAnimatorController : MonoBehaviour
{
    public Animator animator;
    public CarController carController; // Reference to your CarController
    public GameObject steeringWheel;
    [SerializeField] private float animatorTurnAngle; // Angle for the animator to control turning
    [SerializeField] private float horizontal; // Horizontal input for steering

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (carController != null)
        {
            animatorTurnAngle = carController.CurrentSteeringInput;
        }

        animatorTurnAngle = Mathf.Lerp(animatorTurnAngle, -horizontal, 20 * Time.deltaTime);
        animator.SetFloat("turnAngle", animatorTurnAngle);

        steeringWheel.transform.localRotation = Quaternion.Euler(0, 0 , -animatorTurnAngle * 35);
    }
}
