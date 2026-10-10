#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SwapHunter.Editor
{
    [CustomEditor(typeof(MissionConfiguration))]
    public sealed class MissionConfigurationInspector : UnityEditor.Editor
    {
        int chapter;
        bool validated;
        List<string> errors = new List<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("本资产编辑任务、角色预算、触发和预警。章节及目标/遭遇 ID 保持稳定；增加区域或物理出生槽位仍需同步地图绑定。进行中的行动使用配置快照。", MessageType.Info);
            var missions = serializedObject.FindProperty("missions");
            if (missions == null || !missions.isArray || missions.arraySize == 0)
            {
                EditorGUILayout.HelpBox("配置数组缺失或为空。请恢复有效资产；首次准备不会覆盖现有文件。", MessageType.Error);
                DrawDefaultInspector();
                return;
            }

            var labels = new string[missions.arraySize];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = (i + 1).ToString("00") + " / " + (i < ExpeditionCatalog.Levels.Length ? ExpeditionCatalog.Levels[i].name : "无效额外章节");
            chapter = EditorGUILayout.Popup("编辑章节", Mathf.Clamp(chapter, 0, labels.Length - 1), labels);
            EditorGUILayout.PropertyField(missions.GetArrayElementAtIndex(chapter), new GUIContent("任务与遭遇参数"), true);
            if (serializedObject.ApplyModifiedProperties()) validated = false;

            EditorGUILayout.Space();
            if (GUILayout.Button("校验十二关合同"))
            {
                errors = MissionCatalog.ValidationErrors(((MissionConfiguration)target).missions);
                validated = true;
            }
            if (validated)
            {
                if (errors.Count == 0) EditorGUILayout.HelpBox("十二关配置校验通过。场景几何、净空和实际步行仍由原生路线检查验证。", MessageType.Info);
                else foreach (string error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            var game = DemoGame.I;
            bool hasRuntimeMap = Application.isPlaying && game && game.expeditionMap != null;
            using (new EditorGUI.DisabledScope(!hasRuntimeMap))
                if (GUILayout.Button("查看运行场景的区域与岗位箭头"))
                {
                    Selection.activeGameObject = game.expeditionMap.root.gameObject;
                    if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
                }
            EditorGUILayout.LabelField("Gizmo 图例", "青色：触发区域；黄色：普通岗位；品红：固定岗位");
            EditorGUILayout.LabelField("部署预算", "未触发 → 预警 → 已部署；关站可取消未落地预算");
        }
    }
}
#endif

