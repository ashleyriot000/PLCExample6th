using System;
using System.Collections.Generic;
using UnityEngine;

public class AGVGripperController : MonoBehaviour
{
    public LayerMask grabbableLayer;    //잡을 수 있는 콜라이더의 레이어 설정
    public List<Rigidbody> triggerList = new List<Rigidbody>();

    public Transform container;     //저장할 공간
    public Rigidbody grabbed;       //붙잡은 게임오브젝트의 리지드바디.

    private void OnTriggerEnter(Collider other)
    {
        if ((grabbableLayer.value & 1 << other.gameObject.layer) == 0)
            return;

        if (triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Add(other.attachedRigidbody);
    }

    private void OnTriggerExit(Collider other)
    {
        if ((grabbableLayer.value & 1 << other.gameObject.layer) == 0)
            return;

        if (!triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Remove(other.attachedRigidbody);
    }

    public void Grab(bool isGrab)
    {
        if(isGrab)
        {
            if(grabbed == null && triggerList.Count > 0)
            {
                triggerList[0].isKinematic = true;
                triggerList[0].transform.SetParent(transform);
                grabbed = triggerList[0];
            }
        }
        else
        {
            if(grabbed != null)
            {
                grabbed.isKinematic = false;
                grabbed.transform.SetParent(null);
                grabbed = null;
            }
        }
    }

    public void PutProduct()
    {
        if(grabbed != null)
        {
            grabbed.transform.SetParent(container);
            grabbed = null;
        }
    }
    public void PullProduct()
    {
        if(triggerList.Count > 0)
        {
            triggerList[0].transform.SetParent(transform);
            grabbed = triggerList[0];
        }
    }
}
