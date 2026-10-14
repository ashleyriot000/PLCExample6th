using UnityEditor;
using UnityEngine;
using Preliy.Flange;

[CustomEditor(typeof(TargetScheduler))]
public class TargetSchedulerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TargetScheduler scheduler = (TargetScheduler)target;

        // 1. DrawDefaultInspector() 대신 수동으로 프로퍼티를 순회하며 렌더링
        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property);
            }
            else if (property.name == "availableTools") // 툴 배열 발견 시
            {
                EditorGUILayout.PropertyField(property, false); // Header 출력

                if (property.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    int newSize = EditorGUILayout.DelayedIntField("Size", property.arraySize);
                    if (newSize != property.arraySize) property.arraySize = newSize;

                    // 내부 Element 이름(Element 0 -> Tool 1) 강제 변경 렌더링
                    for (int i = 0; i < property.arraySize; i++)
                    {
                        SerializedProperty elementProp = property.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(elementProp, new GUIContent($"Tool {i + 1}"));
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(property, true); // 나머지 기본 변수 렌더링
            }
        }
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Joint Controls (Forward Kinematics)", EditorStyles.boldLabel);
        MechanicalGroup group = scheduler.GetController?.MechanicalGroup;
        if (group != null && group.Joints.Count > 0)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < group.Joints.Count; i++)
            {
                var joint = group.Joints[i];
                if (joint == null) continue;

                // 조인트 이름 가져오기
                string jointName = joint.transform != null ? joint.transform.name : $"Joint {i + 1}";

                EditorGUI.BeginChangeCheck();
                // 관절 슬라이더 (-360도 ~ 360도 범위 지원)
                float newVal = EditorGUILayout.Slider(jointName, joint.Position.Value, joint.Config.Limits[0], joint.Config.Limits[1]);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(scheduler, "Change Joint Angle");
                    // 슬라이더 값이 변하면 스케줄러 내부의 FK 동기화 함수 호출
                    scheduler.SetJointAngleFromInspector(i, newVal);
                    EditorUtility.SetDirty(scheduler);
                }
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Tool Management (Connect / Release)", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();

        // 2. 숫자를 직접 타이핑하는 IntField 대신 안전한 드롭다운(Popup) UI로 교체
        if (scheduler.availableTools.Count > 0)
        {
            // 인덱스 초과 에러 방지를 위한 Clamp
            scheduler.toolIndexToConnect = Mathf.Clamp(scheduler.toolIndexToConnect, 0, scheduler.availableTools.Count - 1);

            string[] toolOptions = new string[scheduler.availableTools.Count];
            for (int i = 0; i < scheduler.availableTools.Count; i++)
            {
                string objName = scheduler.availableTools[i] != null ? scheduler.availableTools[i].name : "Empty";
                toolOptions[i] = $"Tool {i + 1} ({objName})"; // 예: Tool 1 (Gripper_Model)
            }

            // 드롭다운 선택 메뉴
            scheduler.toolIndexToConnect = EditorGUILayout.Popup(scheduler.toolIndexToConnect, toolOptions);
        }
        else
        {
            EditorGUILayout.LabelField("No Tools Available", GUILayout.ExpandWidth(true));
        }

        // Connect 버튼
        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
        if (GUILayout.Button("Connect", GUILayout.Width(70), GUILayout.Height(20)))
        {
            scheduler.ConnectTool(scheduler.toolIndexToConnect);
            EditorUtility.SetDirty(scheduler);
        }

        // Release 버튼
        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.2f);
        if (GUILayout.Button("Release", GUILayout.Width(70), GUILayout.Height(20)))
        {
            scheduler.ReleaseTool();
            EditorUtility.SetDirty(scheduler);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        // --- 기존 네비게이션 및 컨트롤러 UI 유지 ---
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Waypoint Navigation", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        scheduler.targetWaypointIndex = EditorGUILayout.IntField("Target Index", scheduler.targetWaypointIndex);

        if (GUILayout.Button("Go", GUILayout.Width(60), GUILayout.Height(20)))
        {
            var waypoints = scheduler.waypoints;
            if (waypoints.Count == 0 || scheduler.targetWaypointIndex < 0 || scheduler.targetWaypointIndex >= waypoints.Count)
            {
                Debug.LogWarning("지정된 인덱스가 존재하지 않습니다. 웨이포인트를 추가하거나 올바른 인덱스를 설정해주세요.");
                return;
            }

            scheduler.JumpToWaypoint(scheduler.targetWaypointIndex);
            EditorUtility.SetDirty(scheduler);
        }

        GUI.backgroundColor = new Color(0.2f, 0.6f, 1.0f);
        if (GUILayout.Button("Home", GUILayout.Width(60), GUILayout.Height(20)))
        {
            scheduler.ReturnToHome();
            EditorUtility.SetDirty(scheduler);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Waypoint Controller", EditorStyles.boldLabel);

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
        if (GUILayout.Button("Create New Target from Current State", GUILayout.Height(35)))
        {
            scheduler.CreateNewWaypoint();
            EditorUtility.SetDirty(scheduler);
        }

        GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        if (GUILayout.Button("Clear Waypoints", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear Waypoints", "모든 타겟 웨이포인트를 삭제하시겠습니까?", "Yes", "Cancel"))
            {
                scheduler.ClearWaypoints();
                scheduler.targetWaypointIndex = 0;
                EditorUtility.SetDirty(scheduler);
            }
        }
        GUI.backgroundColor = Color.white;
    }
}