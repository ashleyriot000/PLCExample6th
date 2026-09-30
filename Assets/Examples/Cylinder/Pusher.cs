
using System.Collections.Generic;
using UnityEngine;

public class Pusher : MonoBehaviour
{
    public string pushableName = "Cube";
    public float pushSpeed = 3f;
    public List<Rigidbody> triggerList = new ();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.name.Contains(pushableName))
            return;

        if (triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Add(other.attachedRigidbody);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.gameObject.name.Contains(pushableName))
            return;

        if (!triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Remove(other.attachedRigidbody);
    }

    private void FixedUpdate()
    {
        foreach(var trigger in triggerList)
        {
            trigger.MovePosition(trigger.position + transform.forward * pushSpeed * Time.fixedDeltaTime);
        }
    }
}
