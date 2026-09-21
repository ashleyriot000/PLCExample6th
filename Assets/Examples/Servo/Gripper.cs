using System.Collections.Generic;
using UnityEngine;

public class Gripper : MonoBehaviour
{
    public Animator anim;
    public List<Rigidbody> triggeredList = new List<Rigidbody>();

    private void Awake()
    {
        if (anim == null)
            anim = GetComponent<Animator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody.isKinematic)
            return;

        if (triggeredList.Contains(other.attachedRigidbody))
            return;

        triggeredList.Add(other.attachedRigidbody);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody.isKinematic)
            return;

        if (!triggeredList.Contains(other.attachedRigidbody))
            return;

        triggeredList.Remove(other.attachedRigidbody);
    }

    public void Grab(bool isGrab)
    {
        if(isGrab)
        {
            anim.SetBool("Grab", true);

            if (triggeredList.Count == 0)
                return;

            triggeredList[0].transform.SetParent(transform);
            triggeredList[0].isKinematic = true;
        }
        else
        {
            anim.SetBool("Grab", false);

            if (triggeredList.Count == 0)
                return;

            triggeredList[0].transform.SetParent(null);
            triggeredList[0].isKinematic = false;
        }
    }

    public void UpDown(bool isUp)
    {
        anim.SetBool("IsUp", isUp);
    }
}
