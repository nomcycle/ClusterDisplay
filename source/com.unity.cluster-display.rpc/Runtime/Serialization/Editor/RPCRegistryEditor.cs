using System;
using System.Linq;
using UnityEngine;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditorInternal;
using UnityEditor;

namespace Unity.ClusterDisplay.RPC
{
    internal partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>
    {
        class RPCRegistrySettingsProvider : SettingsProvider
        {
            public RPCRegistrySettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) {}

            [SettingsProvider]
            public static SettingsProvider CreateMyCustomSettingsProvider()
            {
                var provider = new RPCRegistrySettingsProvider("Project/Cluster Display", SettingsScope.Project);
                return provider;
            }

            RPCRegistryEditor cachedRPCRegistryEditor;
            public override void OnGUI(string searchContext)
            {
                if (!TryGetInstance(out var rpcRegistryInstance))
                    return;

                if (cachedRPCRegistryEditor == null)
                    cachedRPCRegistryEditor = UnityEditor.Editor.CreateEditor(rpcRegistryInstance, typeof(RPCRegistryEditor)) as RPCRegistryEditor;
                cachedRPCRegistryEditor.OnInspectorGUI();
            }
        }

        [CustomEditor(typeof(RPCRegistry))]
        class RPCRegistryEditor : UnityEditor.Editor
        {
            Assembly[] cachedAllAssemblies;
            Vector2 registeredAssemblyListScrollPosition;
            Vector2 registeredMethodListPosition;

            // [MenuItem("Unity/Cluster Display/RPC Registry")]
            static void SelectRegistry ()
            {
                if (!RPCRegistry.TryGetInstance(out var instance))
                    return;
                Selection.objects = new[] { instance };
            }

            void ListRegisteredMethods ()
            {
                var rpcRegistry = target as RPCRegistry;

                // Type methodTypeInRemoval = null;
                RPCMethodInfo ? rpcMethodToRemove = null;

                if (RPCRegistry.RPCCount > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Registered Methods", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    registeredMethodListPosition = EditorGUILayout.BeginScrollView(registeredMethodListPosition, GUILayout.Height(300));

                    rpcRegistry.Foreach((Action<RPCMethodInfo>)((rpcMethodInfo) =>
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(30);

                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            if (EditorUtility.DisplayDialog("Unregister Method?", "Are you sure you want to unregister this method?", "Yes", "Cancel"))
                                rpcMethodToRemove = rpcMethodInfo;
                        }

                        EditorGUILayout.LabelField($"RPC UUID: {rpcMethodInfo.rpcId}, Signature: \"{ReflectionUtils.GetMethodSignature(rpcMethodInfo.methodInfo)}\"");
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        EditorGUILayout.LabelField("Execution Stage:", GUILayout.Width(125));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        EditorGUILayout.LabelField("Override:", GUILayout.Width(55));

                        var newOverrideRPCExecutionStage = EditorGUILayout.Toggle(rpcMethodInfo.overrideRPCExecutionStage, GUILayout.Width(25));
                        RPCExecutionStage newRPCExecutionStage = rpcMethodInfo.rpcExecutionStage;

                        if (newOverrideRPCExecutionStage)
                            newRPCExecutionStage = (RPCExecutionStage)EditorGUILayout.EnumPopup(rpcMethodInfo.rpcExecutionStage);
                        else EditorGUILayout.LabelField(rpcMethodInfo.rpcExecutionStage.ToString());

                        if (newRPCExecutionStage != rpcMethodInfo.rpcExecutionStage || newOverrideRPCExecutionStage != rpcMethodInfo.overrideRPCExecutionStage)
                        {
                            rpcMethodInfo.overrideRPCExecutionStage = newOverrideRPCExecutionStage;
                            rpcMethodInfo.rpcExecutionStage = newRPCExecutionStage;

                            rpcRegistry.TryUpdateRPC(ref rpcMethodInfo);
                        }

                        EditorGUILayout.EndHorizontal();
                    }));

                    EditorGUILayout.EndScrollView();
                    RPCEditorGUICommon.HorizontalLine();
                }

                else EditorGUILayout.LabelField("No methods reigstered.");

                if (rpcMethodToRemove != null)
                    RPCRegistry.UnmarkRPC(rpcMethodToRemove.Value.rpcId);
            }

            public override void OnInspectorGUI()
            {
                RPCRegistry rpcRegistry = target as RPCRegistry;

                if (GUILayout.Button("Reset"))
                    rpcRegistry.Clear();

                ListRegisteredMethods();
            }
        }

        protected override void OnAwake() {}
    }
}
#endif
