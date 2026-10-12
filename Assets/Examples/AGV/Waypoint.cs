using System.Collections.Generic;
using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Header("웨이포인트 설정")]
    public int id;  //고유 아이디 1, 2, 3

    [Header("연결된 주변 웨이포인트 리스트")]
    public List<Waypoint> neighbors = new List<Waypoint>();

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.02f);

        Vector3 direction = Vector3.zero;
        for(int i = 0; i < neighbors.Count; ++i)
        {
            direction = neighbors[i].transform.position - transform.position;
            direction.y = 0f;
            Vector3 linePos = transform.position + Vector3.up * 0.07f;
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.ArrowHandleCap(
                i, linePos, Quaternion.LookRotation(direction), 0.1f, EventType.Repaint);
        }

        //씬 뷰에서 ID가 시각적으로 표현.
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.05f, $"ID : {id}");

        if (neighbors == null)
            return;

        Gizmos.color = Color.blue;
        foreach(var neighbor in neighbors)
        {
            if(neighbor != null)
            {
                Gizmos.DrawLine(transform.position, neighbor.transform.position);
            }
        }
    }
#endif
}
