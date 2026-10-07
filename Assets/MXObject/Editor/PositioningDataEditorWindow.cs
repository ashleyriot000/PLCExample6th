using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Callbacks; // 에셋 더블클릭 이벤트를 위해 추가

public class PositioningDataEditorWindow : EditorWindow
{
    private ServoPositioningData positioningData;
    private SerializedObject serializedData;
    private SerializedProperty axesProperty;

    private ReorderableList stepList;
    private int selectedAxisIndex = 0;
    private Vector2 scrollPosition;

    // 상단 메뉴에서 창 열기
    [MenuItem("Digital Twin/Robot Parameter Editor")]
    public static void ShowWindow()
    {
        PositioningDataEditorWindow window = GetWindow<PositioningDataEditorWindow>("PositioningData Editor");
        window.minSize = new Vector2(800, 400);
    }

    // 외부(인스펙터 버튼이나 더블클릭)에서 특정 데이터를 넣고 창을 열 때 사용하는 메서드
    public static void OpenWindowWithData(ServoPositioningData targetData)
    {
        PositioningDataEditorWindow window = GetWindow<PositioningDataEditorWindow>("PositioningData Editor");
        window.minSize = new Vector2(800, 400);

        window.positioningData = targetData;
        window.InitializeData();

        window.Show();
        window.Focus(); // 창을 최상단으로 가져옴
    }

    // 프로젝트 창에서 에셋을 '더블클릭' 했을 때 실행되는 콜백 (보너스 UX)
    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceID, int line)
    {
        ServoPositioningData targetData = EditorUtility.EntityIdToObject(instanceID) as ServoPositioningData;
        if (targetData != null)
        {
            OpenWindowWithData(targetData);
            return true; // 우리가 직접 창을 열었으므로 true 반환
        }
        return false;
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (positioningData == null)
        {
            EditorGUILayout.HelpBox("서보 포지셔닝 데이터 에셋을 위 슬롯에 연결하거나 새로 생성하세요.", MessageType.Info);
            return;
        }

        serializedData.Update();

        GUILayout.BeginHorizontal();
        DrawSidebar();
        DrawMainPanel();
        GUILayout.EndHorizontal();

        serializedData.ApplyModifiedProperties();
    }

    // --- 상단 툴바 (데이터 로드 및 저장) ---
    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();
        positioningData = (ServoPositioningData)EditorGUILayout.ObjectField(positioningData, typeof(ServoPositioningData), false, GUILayout.Width(300));

        if (EditorGUI.EndChangeCheck() && positioningData != null)
        {
            InitializeData();
        }

        GUILayout.FlexibleSpace();

        // 저장 버튼 로직
        if (positioningData != null)
        {
            bool isDirty = EditorUtility.IsDirty(positioningData);

            EditorGUI.BeginDisabledGroup(!isDirty);
            if (isDirty) GUI.contentColor = Color.yellow;

            string buttonText = isDirty ? "Save Data (*)" : "Saved";
            if (GUILayout.Button(buttonText, EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                GUI.FocusControl(null);
                AssetDatabase.SaveAssets();
                Repaint();
            }

            GUI.contentColor = Color.white;
            EditorGUI.EndDisabledGroup();
        }

        GUILayout.EndHorizontal();
    }

    private void InitializeData()
    {
        serializedData = new SerializedObject(positioningData);
        axesProperty = serializedData.FindProperty("axes");
        selectedAxisIndex = 0;
        SetupReorderableList();
    }

    // --- 좌측 사이드바 (축 관리) ---
    private void DrawSidebar()
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(150), GUILayout.ExpandHeight(true));
        GUILayout.Label("Axes", EditorStyles.boldLabel);

        for (int i = 0; i < axesProperty.arraySize; i++)
        {
            SerializedProperty axisProp = axesProperty.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = axisProp.FindPropertyRelative("axisName");

            bool isSelected = (selectedAxisIndex == i);
            if (GUILayout.Toggle(isSelected, nameProp.stringValue, "Button", GUILayout.ExpandWidth(true)))
            {
                if (selectedAxisIndex != i)
                {
                    selectedAxisIndex = i;
                    SetupReorderableList();
                    GUI.FocusControl(null);
                }
            }
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+", EditorStyles.miniButtonLeft))
        {
            axesProperty.arraySize++;
            int newIndex = axesProperty.arraySize - 1;
            axesProperty.GetArrayElementAtIndex(newIndex).FindPropertyRelative("axisName").stringValue = "Axis " + (newIndex + 1);
        }
        if (GUILayout.Button("-", EditorStyles.miniButtonRight) && axesProperty.arraySize > 0)
        {
            axesProperty.arraySize--;
            if (selectedAxisIndex >= axesProperty.arraySize) selectedAxisIndex = Mathf.Max(0, axesProperty.arraySize - 1);
            SetupReorderableList();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    // --- 우측 메인 패널 (데이터 테이블) ---
    private void DrawMainPanel()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (axesProperty.arraySize == 0)
        {
            GUILayout.Label("왼쪽 아래의 '+' 버튼을 눌러 축을 추가하세요.");
            GUILayout.EndVertical();
            return;
        }

        SerializedProperty currentAxis = axesProperty.GetArrayElementAtIndex(selectedAxisIndex);
        EditorGUILayout.PropertyField(currentAxis.FindPropertyRelative("axisName"), new GUIContent("Axis Name"));
        EditorGUILayout.Space(10);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        if (stepList != null)
        {
            stepList.DoLayoutList();
        }
        GUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    // --- ReorderableList 초기화 (엑셀 형태의 표 그리기) ---
    private void SetupReorderableList()
    {
        if (axesProperty == null || axesProperty.arraySize == 0) return;

        SerializedProperty stepDataProp = axesProperty.GetArrayElementAtIndex(selectedAxisIndex).FindPropertyRelative("stepDataList");

        stepList = new ReorderableList(serializedData, stepDataProp, true, true, true, true);

        stepList.drawHeaderCallback = (Rect rect) => {
            float space = 2f;
            float startX = rect.x + 14f;
            float w = rect.width - 14f;

            EditorGUI.LabelField(new Rect(startX, rect.y, w * 0.06f - space, rect.height), "No.", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(startX + w * 0.06f, rect.y, w * 0.14f - space, rect.height), "Pattern", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(startX + w * 0.20f, rect.y, w * 0.14f - space, rect.height), "Method", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(startX + w * 0.34f, rect.y, w * 0.20f - space, rect.height), "Address", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(startX + w * 0.54f, rect.y, w * 0.20f - space, rect.height), "Speed", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(startX + w * 0.74f, rect.y, w * 0.13f - space, rect.height), "Dwell", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(startX + w * 0.87f, rect.y, w * 0.13f, rect.height), "M-Code", EditorStyles.miniBoldLabel);
        };

        stepList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            SerializedProperty element = stepList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            float h = EditorGUIUtility.singleLineHeight;
            float space = 2f;
            float startX = rect.x;
            float w = rect.width;

            SerializedProperty methodProp = element.FindPropertyRelative("method");
            SerializedProperty addressProp = element.FindPropertyRelative("positioningAddress");

            ControlMethod currentMethod = (ControlMethod)methodProp.enumValueIndex;
            bool isSpeedControl = (currentMethod == ControlMethod.Forward_Speed || currentMethod == ControlMethod.Reverse_Speed);

            GUIStyle indexStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(new Rect(startX, rect.y, w * 0.06f - space, h), (index + 1).ToString(), indexStyle);

            EditorGUI.PropertyField(new Rect(startX + w * 0.06f, rect.y, w * 0.14f - space, h), element.FindPropertyRelative("pattern"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(startX + w * 0.20f, rect.y, w * 0.14f - space, h), methodProp, GUIContent.none);

            EditorGUI.BeginDisabledGroup(isSpeedControl);
            EditorGUI.PropertyField(new Rect(startX + w * 0.34f, rect.y, w * 0.20f - space, h), addressProp, GUIContent.none);
            EditorGUI.EndDisabledGroup();

            EditorGUI.PropertyField(new Rect(startX + w * 0.54f, rect.y, w * 0.20f - space, h), element.FindPropertyRelative("commandSpeed"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(startX + w * 0.74f, rect.y, w * 0.13f - space, h), element.FindPropertyRelative("dwellTime"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(startX + w * 0.87f, rect.y, w * 0.13f, h), element.FindPropertyRelative("mCode"), GUIContent.none);
        };
    }
}

[CustomEditor(typeof(ServoPositioningData))]
public class PositioningDataInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space(15);

        // 안내 문구 표시
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        EditorGUILayout.LabelField("Servo Multi-Axis Parameter Data", titleStyle);
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("이 데이터는 서보 모듈의 포지셔닝 데이터를 관리하기 위한 전용 파일입니다. 아래 버튼을 눌러 에디터를 열고 데이터를 편집하세요.", MessageType.Info);

        EditorGUILayout.Space(15);

        // 크고 클릭하기 쉬운 Edit 버튼 배치
        if (GUILayout.Button("에디터 창 열기 (Edit)", GUILayout.Height(40)))
        {
            // 위에서 만든 전용 창 열기 메서드 호출
            PositioningDataEditorWindow.OpenWindowWithData((ServoPositioningData)target);
        }
    }
}