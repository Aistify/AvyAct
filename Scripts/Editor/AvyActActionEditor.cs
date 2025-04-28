#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

// ReSharper disable once CheckNamespace
namespace A1ST.AvyAct.Components
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(AvyActAction))]
    public class AvyActActionEditor : Editor
    {
        private static bool _menuDataFoldout = true;
        private static bool _clipDataFoldout = true;
        private static bool _propagationDataFoldout = true;

        private SerializedProperty _menuProperty;
        private SerializedProperty _toggleTypeProperty;
        private SerializedProperty _parameterProperty;
        private SerializedProperty _defaultValueProperty;
        private SerializedProperty _onClipProperty;
        private SerializedProperty _offClipProperty;
        private SerializedProperty _propagationTypeProperty;
        private SerializedProperty _recursiveTargetProperty;
        private SerializedProperty _propagateOnPreprocessProperty;
        private SerializedProperty _savedProperty;
        private SerializedProperty _syncedProperty;
        private SerializedProperty _priorityProperty;

        private static GUIStyle _foldoutStyle;

        private void OnEnable()
        {
            if (EditorApplication.isPlaying)
                return;
            ((AvyActAction)target).GetCreateMenu();
            _menuProperty = serializedObject.FindProperty("menu");
            _toggleTypeProperty = serializedObject.FindProperty("toggleType");
            _parameterProperty = serializedObject.FindProperty("parameter");
            _onClipProperty = serializedObject.FindProperty("onClip");
            _offClipProperty = serializedObject.FindProperty("offClip");
            _propagationTypeProperty = serializedObject.FindProperty("propagationType");
            _recursiveTargetProperty = serializedObject.FindProperty("recursiveTarget");
            _propagateOnPreprocessProperty = serializedObject.FindProperty("propagateOnPreprocess");
            _savedProperty = serializedObject.FindProperty("saved");
            _syncedProperty = serializedObject.FindProperty("synced");
            _priorityProperty = serializedObject.FindProperty("priority");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var script = (AvyActAction)target;

            InitGUI(script);

            // Wait for variable to initialize properly
            if (script.menu.Control?.parameter == null)
                return;

            var menuType = ((AvyActAction)target).menu.Control.type;
            if (
                menuType
                is not (
                    VRCExpressionsMenu.Control.ControlType.Button
                    or VRCExpressionsMenu.Control.ControlType.Toggle
                    or VRCExpressionsMenu.Control.ControlType.RadialPuppet
                )
            )
            {
                EditorGUILayout.LabelField("Invalid Menu Type");
                return;
            }

            _menuDataFoldout = Foldout("Menu Data", _menuDataFoldout);
            if (_menuDataFoldout)
            {
                EditorGUILayout.PropertyField(_menuProperty);
                _defaultValueProperty = serializedObject.FindProperty(
                    menuType == VRCExpressionsMenu.Control.ControlType.RadialPuppet
                        ? "defaultFloatValue"
                        : "defaultBoolValue"
                );

                if (_defaultValueProperty.propertyType != SerializedPropertyType.Float)
                    EditorGUILayout.PropertyField(_toggleTypeProperty);

                script.parameter =
                    script.menu.Control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet
                        ? script.menu.Control.subParameters[0].name
                        : script.menu.Control.parameter.name;

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(_parameterProperty, true);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.PropertyField(
                    _defaultValueProperty,
                    new GUIContent("Default Value")
                );
                EditorGUILayout.PropertyField(_savedProperty);
                EditorGUILayout.PropertyField(_syncedProperty);
                EditorGUILayout.PropertyField(_priorityProperty);
            }

            _clipDataFoldout = Foldout("Animation", _clipDataFoldout);
            if (_clipDataFoldout)
            {
                EditorGUI.indentLevel++;
                DrawAnimation(_onClipProperty, "On");
                if (
                    script.menu.Control.type != VRCExpressionsMenu.Control.ControlType.RadialPuppet
                    && _toggleTypeProperty.enumValueIndex != (int)AvyActAction.ToggleType.IntToggle
                )
                    DrawAnimation(_offClipProperty, "Off");
                EditorGUI.indentLevel--;
            }

            _propagationDataFoldout = Foldout("Propagation", _propagationDataFoldout);
            if (_propagationDataFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_propagationTypeProperty);
                if (
                    _propagationTypeProperty.enumValueIndex
                    == (int)AvyActAction.PropagationType.Recursive
                )
                {
                    EditorGUILayout.PropertyField(_recursiveTargetProperty);
                }
                EditorGUILayout.PropertyField(_propagateOnPreprocessProperty);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            return;

            void DrawAnimation(SerializedProperty clipProperty, string suffix)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.PropertyField(clipProperty);

                if (GUILayout.Button("New", GUILayout.Width(48)))
                {
                    clipProperty.objectReferenceValue = CreateNewClip(
                        $"{script.menu.name} {suffix}",
                        (AnimationClip)clipProperty.objectReferenceValue
                    );
                }

                if (GUILayout.Button("Edit", GUILayout.Width(48)))
                {
                    var avatar = script.gameObject
                        .GetComponentInParents<VRCAvatarDescriptor>()
                        .gameObject;

                    var clipLoader =
                        avatar.GetComponent<AvyActClipLoader>()
                        ?? avatar.AddComponent<AvyActClipLoader>();

                    clipLoader.clips.Clear();
                    clipLoader.clips.Add(clipProperty.objectReferenceValue as AnimationClip);

                    Selection.activeObject = avatar;
                    EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
                }

                EditorGUILayout.EndHorizontal();
            }

            bool Foldout(string title, bool isFoldedOut)
            {
                EditorGUILayout.BeginVertical(_foldoutStyle);
                EditorGUI.indentLevel += 1;
                isFoldedOut = EditorGUILayout.Foldout(isFoldedOut, title);
                EditorGUI.indentLevel -= 1;
                EditorGUILayout.EndVertical();

                return isFoldedOut;
            }

            AnimationClip CreateNewClip(string clipName, AnimationClip clip)
            {
                const string lastPathKey = "A1ST.AvyAct.SavePath";
                var lastPath = EditorPrefs.GetString(lastPathKey, "Assets/");
                if (lastPath == "")
                    lastPath = "Assets/";
                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Animation Clip",
                    clipName,
                    "anim",
                    "Please enter a file name to save the animation clip",
                    lastPath
                );

                if (string.IsNullOrEmpty(path))
                {
                    Debug.Log("Clip creation cancelled.");
                    return clip;
                }

                var newClip = new AnimationClip();

                AssetDatabase.CreateAsset(newClip, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorPrefs.SetString(lastPathKey, path);
                return newClip;
            }
        }

        private static void InitGUI(AvyActAction script)
        {
            _foldoutStyle ??= new GUIStyle("ShurikenModuleTitle")
            {
                font = EditorStyles.label.font,
                fontSize = EditorStyles.label.fontSize,
                fontStyle = EditorStyles.label.fontStyle,
                border = new RectOffset(15, 7, 4, 4),
                contentOffset = new Vector2(20f, -2f),
                fixedHeight = 22
            };

            script.LoadMenu();
        }
    }

    public static class CatressControlExtensions
    {
        public static T GetComponentInParents<T>(this GameObject child)
            where T : Component
        {
            while (true)
            {
                var parentTransform = child.transform.parent;
                if (parentTransform == null)
                    return null;

                var component = parentTransform.GetComponent<T>();
                if (component != null)
                    return component;
                child = parentTransform.gameObject;
            }
        }
    }
}
#endif
