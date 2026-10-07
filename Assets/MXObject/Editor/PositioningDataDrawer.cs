using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(PositioningData))]
public class PositioningDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // UI 그리기 시작
        EditorGUI.BeginProperty(position, label, property);

        // 배열의 인덱스(예: Element 0) 라벨을 그리고, 나머지 공간을 데이터 입력란으로 사용
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // 들여쓰기 초기화 (가로 배치를 위해)
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // 6개 항목을 가로로 배치하기 위한 너비 계산 (비율 조정 가능)
        float w = position.width;
        float space = 2f; // 항목 간 여백

        // 각 필드의 Rect (x, y, width, height) 계산
        Rect rectPattern = new Rect(position.x, position.y, w * 0.15f - space, position.height);
        Rect rectMethod = new Rect(position.x + w * 0.15f, position.y, w * 0.15f - space, position.height);
        Rect rectAddress = new Rect(position.x + w * 0.30f, position.y, w * 0.20f - space, position.height);
        Rect rectSpeed = new Rect(position.x + w * 0.50f, position.y, w * 0.20f - space, position.height);
        Rect rectDwell = new Rect(position.x + w * 0.70f, position.y, w * 0.15f - space, position.height);
        Rect rectMCode = new Rect(position.x + w * 0.85f, position.y, w * 0.15f, position.height);

        // 필드 프로퍼티 가져오기
        var propPattern = property.FindPropertyRelative("operationPattern");
        var propMethod = property.FindPropertyRelative("controlMethod");
        var propAddress = property.FindPropertyRelative("positioningAddress");
        var propSpeed = property.FindPropertyRelative("commandSpeed");
        var propDwell = property.FindPropertyRelative("dwellTime");
        var propMCode = property.FindPropertyRelative("mCode");

        // 라벨 없이 값만 한 줄에 렌더링 (GUIContent.none 사용)
        EditorGUI.PropertyField(rectPattern, propPattern, GUIContent.none);
        EditorGUI.PropertyField(rectMethod, propMethod, GUIContent.none);
        EditorGUI.PropertyField(rectAddress, propAddress, GUIContent.none);
        EditorGUI.PropertyField(rectSpeed, propSpeed, GUIContent.none);
        EditorGUI.PropertyField(rectDwell, propDwell, GUIContent.none);
        EditorGUI.PropertyField(rectMCode, propMCode, GUIContent.none);

        // 들여쓰기 원래대로 복구
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }
}