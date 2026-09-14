using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MagneticSensor : MonoBehaviour
{
    public LayerMask detectableMask;    //감지 가능한 레이어.
    public string detectableTag;        //감지 가능한 태그.
    public string detectableName;       //감지 가능한 이름.

    public List<Collider> triggerList = new List<Collider>();   //감지한 콜라이더들의 리스트
    public UnityEvent<bool> onChangedDetected;      //감지 결과가 변경될 때 호출되는 콜백함수를 담는 델리게이트.

    private bool hasDetected;
    public bool HasDetected
    {
        get => hasDetected;
        private set
        {
            if (hasDetected == value)
                return;

            hasDetected = value;
            onChangedDetected?.Invoke(value);
        }
    }

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if(col != null)
            col.isTrigger = true;       //영역으로 변환해서 감지용으로 변경.
    }

    private void OnTriggerEnter(Collider other)
    {
        //감지 가능한 레이어가 아니면 반환.
        if ((detectableMask.value & 1 << other.gameObject.layer) == 0)
            return;

        //감지가능한 태그가 들어있으면서 태그가 다르면 반환.
        if (!string.IsNullOrEmpty(detectableTag) && other.gameObject.tag != detectableTag)
            return;

        //감지 가능한 이름이 들어있으면서 이름이 포함되지 않으면 반환.
        if (!string.IsNullOrEmpty(detectableName) && !other.gameObject.name.Contains(detectableName))
            return;
        
        //리스트에 이미 들어있으면 반환
        if (triggerList.Contains(other))
            return;

        triggerList.Add(other);
        HasDetected = triggerList.Count > 0;
    }

    private void OnTriggerExit(Collider other)
    {
        // 01100                    //00001 => 01000
        //감지 가능한 레이어가 아니면 반환.
        if ((detectableMask.value & 1 << other.gameObject.layer) == 0)
            return;

        //감지가능한 태그가 들어있으면서 태그가 다르면 반환.
        if (!string.IsNullOrEmpty(detectableTag) && other.gameObject.tag != detectableTag)
            return;

        //감지 가능한 이름이 들어있으면서 이름이 포함되지 않으면 반환.
        if (!string.IsNullOrEmpty(detectableName) && !other.gameObject.name.Contains(detectableName))
            return;

        //리스트에 들어 있지 않으면 반환
        if (!triggerList.Contains(other))
            return;

        triggerList.Remove(other);
        HasDetected = triggerList.Count > 0;
    }
}
