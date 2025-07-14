using UnityEngine;

public class FollowPlayerCamera : MonoBehaviour
{
    [SerializeField] private Transform player; // Assign the player GameObject in the Inspector

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (player != null)
        {
            transform.LookAt(player);
        }
    }
}
