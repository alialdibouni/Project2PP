using UnityEngine;

public class PlayerBossMovement : MonoBehaviour
{
    public float speed = 5.0f; // Set player's movement speed.
    public float rotationSpeed = 120.0f; // Set player's rotation speed.
    public float jumpForce = 5.0f; // Set player's jump force.

    public Transform enemyTarget; // Reference to the enemy to target.

    private Rigidbody rb; // Reference to player's Rigidbody.
    public bool isBattleMode = false; // Tracks current control scheme.

    // Start is called before the first frame update
    private void Start()
    {
        rb = GetComponent<Rigidbody>(); // Access player's Rigidbody.
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Jump"))
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange); // Apply jump force.
        }

        // Hold right mouse button to enter battle mode, release to exit
        isBattleMode = Input.GetMouseButton(1);
    }

    // Handle physics-based movement and rotation.
    private void FixedUpdate()
    {
        if (!isBattleMode)
        {
            // Normal movement
            float moveVertical = Input.GetAxis("Vertical");
            Vector3 movement = transform.forward * moveVertical * speed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            float turn = Input.GetAxis("Horizontal") * rotationSpeed * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
        else
        {
            // Battle mode movement: WASD for movement relative to player, always face enemy
            float moveVertical = Input.GetAxis("Vertical") * -1f;
            float moveHorizontal = Input.GetAxis("Horizontal") * -1f; // Invert left/right movement
            Vector3 movement = (transform.forward * moveVertical + transform.right * moveHorizontal) * speed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            // Rotate to face enemy
            if (enemyTarget != null)
            {
                Vector3 directionToEnemy = enemyTarget.position - transform.position;
                directionToEnemy.y = 0f; // Ignore vertical difference for rotation
                if (directionToEnemy.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);
                    Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
                    rb.MoveRotation(newRotation);
                }
            }
        }
    }
}
