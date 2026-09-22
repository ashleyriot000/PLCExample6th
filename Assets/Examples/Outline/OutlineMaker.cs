using UnityEngine;
using UnityEngine.EventSystems;

public class OutlineMaker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string enterLayer = "Outline";
    public MeshRenderer[] renderers;
    private int defaultLayer;

    private void Start()
    {
        defaultLayer = gameObject.layer;
        renderers = GetComponentsInChildren<MeshRenderer>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        foreach (var r in renderers)
        {
            r.gameObject.layer = LayerMask.NameToLayer(enterLayer);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        foreach (var r in renderers)
        {
            r.gameObject.layer = defaultLayer;
        }
    }
}
