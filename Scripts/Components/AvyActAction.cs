using JetBrains.Annotations;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

// ReSharper disable once CheckNamespace
namespace A1ST.AvyAct.Components
{
    public class AvyActAction : MonoBehaviour, IEditorOnly
    {
        // Menu
        public ModularAvatarMenuItem menu;
        public ToggleType toggleType;
        public bool defaultBoolValue;
        public float defaultFloatValue;
        public string parameter;
        public float value;
        public bool saved;
        public bool synced;
        public int priority;

        // Clip Data
        public AnimationClip onClip;
        public AnimationClip offClip;

        // Propagation Data
        public PropagationType propagationType;
        public GameObject recursiveTarget;
        public bool propagateOnPreprocess;

        // Types
        public enum PropagationType
        {
            [UsedImplicitly]
            Everything,
            Recursive
        }

        public enum ToggleType
        {
            [UsedImplicitly]
            BoolToggle,
            IntToggle
        }

        public void GetCreateMenu()
        {
            if (menu != null)
                return;
            menu = gameObject.GetComponent<ModularAvatarMenuItem>();
            if (menu == null)
                menu = gameObject.AddComponent<ModularAvatarMenuItem>();
        }

        public void LoadMenu()
        {
            menu = gameObject.GetComponent<ModularAvatarMenuItem>();
            if (menu.Control.value > 1)
                toggleType = ToggleType.IntToggle;
            parameter =
                menu.Control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet
                    ? menu.Control.subParameters[0].name
                    : menu.Control.parameter.name;
            value = menu.Control.value;
        }
    }
}
