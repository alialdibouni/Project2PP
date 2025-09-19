using UnityEngine;

public class ObjectReportObject : MonoBehaviour
{
    public float amp;
    public float freq;
    Vector3 initPos;




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3(initPos.x, Mathf.Sin(Time.time * freq)* amp + initPos.y, initPos.z);
        transform.Rotate(new Vector3(0, 50, 0) * Time.deltaTime);
    }
}
