using UnityEngine;

public class Filler : MonoBehaviour
{
    public GameObject prefab;
    public float startTime = 1f;
    public float delay = 3f;

    private float fillTime;
    private bool isFilled;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        fillTime = Time.time + startTime;
    }

    void Update()
    {
        if(isFilled == false && fillTime < Time.time)
        {
            Instantiate<GameObject>(prefab, transform.position, transform.rotation);
            isFilled = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody.isKinematic == true)
            return;

        if (!other.gameObject.name.Contains(prefab.name))
            return;

        isFilled = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.gameObject.name.Contains(prefab.name))
            return;

        isFilled = false;
        fillTime = Time.time + delay;
    }
}
