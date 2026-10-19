using System;
using System.Collections.Generic;
using UnityEngine;

public class GripperTool : ToolParent
{
    public Animator anim;
    public List<Rigidbody> triggerList = new List<Rigidbody>();
    private Rigidbody grabbed;

    protected override void OnChangedValue(bool isOn)
    {
        anim.SetBool("IsGrab", isOn);
    }

    public void Grab()
    {
        if(grabbed == null && triggerList.Count > 0)
        {
            grabbed = triggerList[0];
            grabbed.isKinematic = true;
            grabbed.transform.SetParent(transform);
        }
    }

    public void Release()
    {
        if(grabbed != null)
        {
            grabbed.transform.SetParent(null);
            grabbed.isKinematic = false;
            grabbed = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null)
            return;

        if (triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Add(other.attachedRigidbody);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody == null)
            return;

        if (!triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Remove(other.attachedRigidbody);
    }
}
