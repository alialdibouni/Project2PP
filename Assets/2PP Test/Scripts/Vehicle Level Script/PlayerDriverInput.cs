using UnityEngine;

[RequireComponent(typeof(CarController))]
public class PlayerDriverInput : MonoBehaviour
{
    private CarController carController;
    private CarInputActions carControls;

    void Awake()
    {
        carController = GetComponent<CarController>();
        carControls = new CarInputActions();
    }

    void OnEnable() => carControls.Enable();
    void OnDisable() => carControls.Disable();

    void Update()
    {
        Vector2 input = carControls.Car.Movement.ReadValue<Vector2>();
        carController.inputVector = input;
    }
}
