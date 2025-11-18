#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using A1ST.AvyAct.Components;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

// ReSharper disable once CheckNamespace
namespace A1ST.AvyAct.Processor
{
    public class AvyActProcessor : MonoBehaviour
    {
        private static AvyActData[] _avyActDataArr;
        private static AvyActAction[] _allActions;
        private static List<int> _priorities;

        private class AvyActData
        {
            public List<AvyActAction> Actions;
            public ToggleType Type;

            public void GetToggleType()
            {
                if (Actions.Count == 1)
                {
                    if (
                        Actions[0].menu.Control.type
                        == VRCExpressionsMenu.Control.ControlType.RadialPuppet
                    )
                        Type = ToggleType.RadialSlider;
                    if (
                        Actions[0].menu.Control.type
                        is VRCExpressionsMenu.Control.ControlType.Button
                            or VRCExpressionsMenu.Control.ControlType.Toggle
                    )
                        Type = ToggleType.BoolToggle;
                }
                else
                {
                    if (
                        Actions[0].menu.Control.type
                        is VRCExpressionsMenu.Control.ControlType.Button
                            or VRCExpressionsMenu.Control.ControlType.Toggle
                    )
                        Type = ToggleType.IntToggle;
                }
            }

            public enum ToggleType
            {
                RadialSlider,
                BoolToggle,
                IntToggle
            }
        }

        private class BindingData
        {
            public AnimationCurve Curve;
            public string PropertyName;
        }

        public static void Cleanup()
        {
            _allActions.ToList().ForEach(DestroyImmediate);
            _allActions = Array.Empty<AvyActAction>();
        }

        public static void Preprocess(GameObject avatar)
        {
            _priorities = new List<int>();
            avatar.GetComponent<AvyActClipLoader>()?.Destroy();
            var directBlendTreesDict = new Dictionary<string, AvyActData>();
            _allActions = avatar.GetComponentsInChildren<AvyActAction>(true);
            foreach (var action in _allActions)
            {
                if (!IsValidAction(action))
                    continue;

                action.LoadMenu();

                if (directBlendTreesDict.TryGetValue(action.parameter, out var directBlendTree))
                {
                    directBlendTree.Actions.Add(action);
                }
                else
                {
                    directBlendTree = new AvyActData
                    {
                        Actions = new List<AvyActAction> { action }
                    };
                    directBlendTreesDict[action.parameter] = directBlendTree;
                }

                if (!_priorities.Contains(action.priority))
                    _priorities.Add(action.priority);
            }
            _priorities.Sort();

            _avyActDataArr = directBlendTreesDict.Values.ToArray();
            return;

            bool IsValidAction(AvyActAction action)
            {
                if (action.gameObject.GetComponent<ModularAvatarMenuItem>() == null)
                {
                    Debug.Log(
                        $"AvyAct : Action {action.name} is missing its MA Menu Item. Skipping Action."
                    );
                    return false;
                }
                action.LoadMenu();
                if (action.parameter == "")
                {
                    Debug.Log(
                        $"AvyAct : Action {action.name} is missing its parameter. Skipping Action."
                    );
                    return false;
                }
                if (action.onClip == null)
                {
                    Debug.Log(
                        $"AvyAct : Action {action.name} is missing its OnClip. Skipping Action."
                    );
                    return false;
                }
                return true;
            }
        }

        public static void Execute(GameObject avatar, UnityEngine.Object assetContainer)
        {
            var aac = AacV1.Create(
                new AacConfiguration
                {
                    SystemName = "AvyAct",
                    AnimatorRoot = avatar.transform,
                    DefaultValueRoot = avatar.transform,
                    AssetContainer = assetContainer,
                    AssetKey = "AvyActAssetDBT",
                    DefaultsProvider = new AacDefaultsProvider(writeDefaults: true)
                }
            );
            var avyAct = new GameObject
            {
                name = "AvyAct",
                transform = { parent = avatar.transform }
            };
            var ma = MaAc.Create(avyAct);

            var controller = aac.NewAnimatorController();

            var directBlendTrees = new List<AacFlBlendTreeDirect>();
            var dbtLayers = new List<AacFlLayer>();
            var ones = new List<AacFlFloatParameter>();
            foreach (var priority in _priorities)
            {
                var dbt = aac.NewBlendTree().Direct();
                var layer = controller.NewLayer($"DBT_{priority}");
                layer.NewState("AvyActDBT").WithWriteDefaultsSetTo(true).WithAnimation(dbt);
                var one = layer.FloatParameter("AvyAct_One");
                layer.OverrideValue(one, 1f);
                directBlendTrees.Add(dbt);
                dbtLayers.Add(layer);
                ones.Add(layer.FloatParameter("AvyAct_One"));
            }

            foreach (var actData in _avyActDataArr)
            {
                actData.GetToggleType();

                switch (actData.Type)
                {
                    case AvyActData.ToggleType.RadialSlider:
                        if (IsOnClipEmpty(actData.Actions[0]))
                            break;
                        AddRadialSlider(actData.Actions[0]);
                        break;
                    case AvyActData.ToggleType.BoolToggle:
                        if (IsOnClipEmpty(actData.Actions[0]))
                            break;
                        AddBoolToggle(actData.Actions[0]);
                        break;
                    case AvyActData.ToggleType.IntToggle:
                        AddIntToggle(actData.Actions);
                        break;
                    default:
                        continue;
                }
            }

            ma.NewMergeAnimator(controller, VRCAvatarDescriptor.AnimLayerType.FX);

            return;

            bool IsOnClipEmpty(AvyActAction action)
            {
                var isEmpty = action.onClip.empty;
                if (isEmpty)
                    Debug.Log($"AvyAct : Action {action.name}'s OnClip is empty. Skipping Action.");

                return isEmpty;
            }

            void AddRadialSlider(AvyActAction action)
            {
                var index = _priorities.FindIndex(i => i == action.priority);
                var dbtLayer = dbtLayers[index];
                var rsOnClip = PropagateClip(action, action.onClip, avatar);

                var rsControlParam = dbtLayer.FloatParameter(action.parameter);
                dbtLayer.OverrideValue(rsControlParam, action.defaultFloatValue);
                var rsAvyParam = ma.NewParameter(rsControlParam);
                rsAvyParam.WithDefaultValue(action.defaultFloatValue);
                if (!action.saved)
                    rsAvyParam.NotSaved();
                if (!action.synced)
                    rsAvyParam.NotSynced();

                var rsSubTree = aac.NewBlendTree().Simple1D(rsControlParam);

                var keyTimestamps = GetTimestamps(rsOnClip);
                for (var i = 0; i < keyTimestamps.Count; i++)
                {
                    var rsSubClip = aac.CopyClip(
                        SubTreeClipAtTimestamp(rsOnClip, keyTimestamps[i])
                    );
                    var step = 1f / (keyTimestamps.Count - 1);
                    rsSubTree.WithAnimation(rsSubClip, i * step);
                }

                directBlendTrees[index].WithAnimation(rsSubTree, ones[index]);
                return;

                List<float> GetTimestamps(AnimationClip clip)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    var timestamps = new List<float>();
                    foreach (var binding in bindings)
                    {
                        var keyframes = AnimationUtility.GetEditorCurve(clip, binding);

                        foreach (var keyframe in keyframes.keys)
                        {
                            if (!timestamps.Contains(keyframe.time))
                                timestamps.Add(keyframe.time);
                        }
                    }

                    timestamps.Sort();
                    return timestamps;
                }

                AnimationClip SubTreeClipAtTimestamp(AnimationClip input, float timestamp)
                {
                    var curveBindings = AnimationUtility.GetCurveBindings(input);
                    var outClip = new AnimationClip { name = $"{input.name}_{timestamp:0.00}f" };
                    foreach (var binding in curveBindings)
                    {
                        var curve = AnimationUtility.GetEditorCurve(input, binding);
                        var tempKeyframe = new Keyframe(time: 0.00f, curve.Evaluate(timestamp));

                        outClip.SetCurve(
                            binding.path,
                            binding.type,
                            binding.propertyName,
                            new AnimationCurve(tempKeyframe)
                        );
                    }

                    return outClip;
                }
            }
            void AddBoolToggle(AvyActAction action)
            {
                var index = _priorities.FindIndex(i => i == action.priority);
                var dbtLayer = dbtLayers[index];

                var btOnClip = PropagateClip(action, action.onClip, avatar);
                var btOffClip = PropagateClip(action, action.offClip, avatar);

                var btControlParam = dbtLayer.FloatParameter(action.parameter);
                dbtLayer.OverrideValue(btControlParam, action.defaultBoolValue ? 1.00f : 0.00f);

                var btAvyParam = ma.NewBoolToFloatParameter(btControlParam);
                btAvyParam.WithDefaultValue(action.defaultBoolValue);
                if (!action.saved)
                    btAvyParam.NotSaved();
                if (!action.synced)
                    btAvyParam.NotSynced();

                var btSubTree = aac.NewBlendTree().Simple1D(btControlParam);
                btSubTree.WithAnimation(btOffClip, 0);
                btSubTree.WithAnimation(btOnClip, 1);

                directBlendTrees[index].WithAnimation(btSubTree, ones[index]);
            }
            void AddIntToggle(List<AvyActAction> actions)
            {
                var index = _priorities.FindIndex(i => i == actions[0].priority);
                var dbtLayer = dbtLayers[index];

                var itControlParam = dbtLayer.FloatParameter(actions[0].parameter);
                dbtLayer.OverrideValue(itControlParam, actions[0].defaultFloatValue);
                var itAvyParam = ma.NewParameter(dbtLayer.IntParameter(actions[0].parameter));

                var itSubTree = aac.NewBlendTree().Simple1D(itControlParam);

                foreach (var action in actions.Where(action => !IsOnClipEmpty(action)))
                {
                    if (!action.saved)
                        itAvyParam.NotSaved();
                    if (!action.synced)
                        itAvyParam.NotSynced();

                    if (action.defaultBoolValue)
                    {
                        itAvyParam.WithDefaultValue((int)action.value);
                        dbtLayer.OverrideValue(itControlParam, action.value);
                    }

                    var itSubClip = PropagateClip(action, action.onClip, avatar);
                    itSubTree.WithAnimation(itSubClip, action.value);
                }

                directBlendTrees[index].WithAnimation(itSubTree, ones[index]);
            }
        }

        private static AnimationClip PropagateClip(
            AvyActAction action,
            AnimationClip inClip,
            GameObject avatar
        )
        {
            if (inClip is null)
                return new AnimationClip();

            if (!action.propagateOnPreprocess)
                return inClip;

            var propagationTarget =
                action.propagationType == AvyActAction.PropagationType.Recursive
                    ? action.recursiveTarget
                    : avatar.gameObject;

            var propagationRenderers = propagationTarget.GetAllRenderers();
            var tempBindingsL = new List<BindingData>();
            var bindings = AnimationUtility.GetCurveBindings(inClip);

            foreach (var binding in bindings)
            {
                var tempBinding = new BindingData();
                var keyframes = AnimationUtility.GetEditorCurve(inClip, binding).keys;
                tempBinding.Curve = new AnimationCurve(keyframes);
                tempBinding.PropertyName = binding.propertyName;

                if (!tempBindingsL.Exists(x => x.PropertyName == binding.propertyName))
                    tempBindingsL.Add(tempBinding);
            }

            var propagatedClip = new AnimationClip { name = $"{inClip.name} Propagated" };
            propagatedClip.ClearCurves();

            var avatarTransform = propagationRenderers[0].transform;
            while (avatarTransform.parent != null)
            {
                if (avatarTransform.TryGetComponent<VRCAvatarDescriptor>(out _))
                    break;
                avatarTransform = avatarTransform.parent;
            }

            foreach (var binding in tempBindingsL)
                foreach (var relativePath in propagationRenderers.Select(gameObj => GetRelativePath(gameObj.transform, avatarTransform)))
                {
                    propagatedClip.SetCurve(
                        relativePath,
                        typeof(SkinnedMeshRenderer),
                        binding.PropertyName,
                        binding.Curve
                    );
                }

            return propagatedClip;

            string GetRelativePath(Transform target, Transform root)
            {
                var path = target.name;
                var current = target.parent;

                while (current != null && current != root)
                {
                    path = current.name + "/" + path;
                    current = current.parent;
                }

                return path;
            }
        }
    }

    public static class AvyActExtensions
    {
        public static void AppendToClip(this AnimationClip clip, AnimationClip clipToCopy)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clipToCopy))
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    AnimationUtility.GetEditorCurve(clipToCopy, binding)
                );
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clipToCopy))
            {
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    binding,
                    AnimationUtility.GetObjectReferenceCurve(clipToCopy, binding)
                );
            }
        }

        public static List<GameObject> GetAllRenderers(this GameObject target)
        {
            var skinnedMeshRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshRenderers = target.GetComponentsInChildren<MeshRenderer>(true);

            var renderers = meshRenderers.Select(meshRenderer => meshRenderer.gameObject).ToList();
            renderers.AddRange(
                skinnedMeshRenderers.Select(skinnedMeshRenderer => skinnedMeshRenderer.gameObject)
            );

            return renderers;
        }
    }
}
#endif
