using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 按 TileTransitionGenerator 的固定 mask 槽位顺序显示方向提示，
/// 并禁止拖动排序，避免配置顺序与运行时查表错位。
/// </summary>
[CustomEditor(typeof(TileTransitionSet))]
public class TileTransitionSetEditor : Editor
{
    private static readonly string[] SlotLabels =
    {
        "0 上侧 (Up)",
        "1 下侧 (Down)",
        "2 左侧 (Left)",
        "3 右侧 (Right)",
        "4 右上 (Up-Right)",
        "5 右下 (Down-Right)",
        "6 左上 (Up-Left)",
        "7 左下 (Down-Left)",
        "8 右上角/上右组合 (Up+Right)",
        "9 左上角/上左组合 (Up+Left)",
        "10 右下角/下右组合 (Down+Right)",
        "11 左下角/下左组合 (Down+Left)",
        "12 对角交错 A (Up-Left + Down-Right)",
        "13 对角交错 B (Up-Right + Down-Left)"
    };

    private static readonly string[] SlotTooltips =
    {
        "上方邻接/直边",
        "下方邻接/直边",
        "左侧邻接/直边",
        "右侧邻接/直边",
        "右上方向邻接",
        "右下方向邻接",
        "左上方向邻接",
        "左下方向邻接",
        "上+右组合角",
        "上+左组合角",
        "下+右组合角",
        "下+左组合角",
        "左上/右下交替对角",
        "右上/左下交替对角"
    };

    private ReorderableList transitionSlotList;
    private SerializedProperty transitionSlots;

    private void OnEnable()
    {
        transitionSlots = serializedObject.FindProperty("transitionSlots");

        transitionSlotList = new ReorderableList(
            serializedObject,
            transitionSlots,
            false,
            true,
            true,
            true);

        transitionSlotList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "过渡瓦片槽位(方向指的是与当前瓦片不同类型的邻居所处的位置)");
        };

        transitionSlotList.elementHeightCallback = index =>
        {
            SerializedProperty element = transitionSlots.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true);
        };

        transitionSlotList.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = transitionSlots.GetArrayElementAtIndex(index);
            if (index >= 0 && index < SlotLabels.Length)
            {
                var content = new GUIContent(SlotLabels[index], SlotTooltips[index]);
                EditorGUI.PropertyField(rect, element, content, true);
            }
            else
            {
                EditorGUI.PropertyField(rect, element, new GUIContent("Slot " + index), true);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        transitionSlotList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }
}
