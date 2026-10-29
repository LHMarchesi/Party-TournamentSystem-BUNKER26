using Bunker26.Party;
using UnityEditor;
using UnityEngine;

namespace Bunker26.Party.Editor
{
    [CustomEditor(typeof(GameInputDefinition))]
    internal sealed class GameInputDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(10);
            GameInputDefinition definition = (GameInputDefinition)target;

            using (new EditorGUI.DisabledScope(definition.actionsAsset == null))
            {
                if (GUILayout.Button("Refresh Editable Bindings From Input Actions", GUILayout.Height(30)))
                {
                    Undo.RecordObject(definition, "Refresh editable input bindings");
                    definition.RefreshEditableBindingsFromAsset();
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssets();
                }
            }

            if (definition.actionsAsset == null)
            {
                EditorGUILayout.HelpBox("Assign the game's InputActionAsset. Bindings must have control-scheme groups to appear in the runtime editor.", MessageType.Info);
                return;
            }

            if (definition.bindingSettings == null)
                EditorGUILayout.HelpBox("Assign PartyInputBindingSettings so Party can resolve keyboard slots and gamepad schemes.", MessageType.Warning);
            if (string.IsNullOrWhiteSpace(definition.gameId))
                EditorGUILayout.HelpBox("Game ID is used as part of the persistence key. It should remain stable across updates.", MessageType.Warning);
            if (definition.editableBindings == null || definition.editableBindings.Count == 0)
                EditorGUILayout.HelpBox("No editable bindings are listed. Press the refresh button after configuring binding groups in the InputActionAsset.", MessageType.Warning);
        }
    }
}
