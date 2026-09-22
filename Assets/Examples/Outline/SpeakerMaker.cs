using UnityEngine;
using UnityEngine.EventSystems;

public class SpeakerMaker : MonoBehaviour, IPointerClickHandler
{
    public GameObject uiObject;
    public void OnPointerClick(PointerEventData eventData)
    {
        uiObject.SetActive(true);
    }
}
