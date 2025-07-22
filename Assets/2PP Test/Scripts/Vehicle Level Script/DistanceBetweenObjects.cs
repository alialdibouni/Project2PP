using UnityEngine;
using TMPro;
public class DistanceBetweenObjects : MonoBehaviour
{

    public TMP_Text textDistance;

    public GameObject pointA;
    public GameObject pointB;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float distance = Vector3.Distance(pointA.transform.position, pointB.transform.position);
        //textDistance.text = "Distance from Target: " + distance.ToString() + " meters"; 
        textDistance.text = "Distance from Target: " + Mathf.RoundToInt(distance).ToString() + " meters";
    }
}
