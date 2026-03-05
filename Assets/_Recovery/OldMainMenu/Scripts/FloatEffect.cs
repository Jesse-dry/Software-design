using UnityEngine;

public class FloatEffect : MonoBehaviour
{
    public float amplitude = 10f;
    public float speed = 1f;
    Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        transform.localPosition = startPos +
            new Vector3(0, Mathf.Sin(Time.time * speed) * amplitude, 0);
    }
}