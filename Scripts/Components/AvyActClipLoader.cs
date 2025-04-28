using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

// ReSharper disable once CheckNamespace
namespace A1ST.AvyAct.Components
{
    public class AvyActClipLoader : MonoBehaviour, IAnimationClipSource, IEditorOnly
    {
        public List<AnimationClip> clips = new();

        public void GetAnimationClips(List<AnimationClip> results)
        {
            results.AddRange(clips);
        }

        public void Destroy()
        {
            DestroyImmediate(gameObject.GetComponent<AvyActClipLoader>());
        }
    }
}
