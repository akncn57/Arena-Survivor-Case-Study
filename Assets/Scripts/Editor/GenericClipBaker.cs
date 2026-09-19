using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ArenaSurvivor.Editor
{
    /// <summary>
    /// Bakes the enemy's Humanoid (Mixamo) clips into Generic clips for the optimized enemy skeleton.
    ///
    /// Why: a Humanoid Animator retargets the clip from the shared "human" format onto the skeleton every frame,
    /// for every enemy. Baking does that retargeting once, in the Editor; at runtime the Generic clip is played
    /// straight onto the bones. The poses are identical because they are recorded from the Humanoid playback.
    ///
    /// How: the Humanoid clip is sampled frame by frame (30 FPS) onto a temporary instance of the optimized enemy
    /// whose bone GameObjects are restored, and every bone's local position and rotation is recorded with
    /// <see cref="GameObjectRecorder"/>. Scale curves are dropped (scale never changes).
    ///
    /// Requirement: the source clips must be sampled while the enemy model is imported as Humanoid. The model is
    /// switched to Humanoid for the bake and back to Generic afterwards.
    /// </summary>
    public static class GenericClipBaker
    {
        private const string ModelPath = "Assets/Optimized/Enemy/Enemy_Optimized.fbx";
        private const string OutputFolder = "Assets/Optimized/Enemy/Animations";
        private const float SampleRate = 30f;

        private static readonly (string source, string output, bool loop)[] Clips =
        {
            ("Assets/Animations/Enemy/Y Bot@Zombie Walk.fbx", "Enemy_Walk", true),
            ("Assets/Animations/Enemy/Y Bot@Zombie Attack.fbx", "Enemy_Attack", true),
            ("Assets/Animations/Enemy/Y Bot@Zombie Death.fbx", "Enemy_Death", false),
        };

        [MenuItem("Tools/Arena Survivor/Bake Enemy Generic Clips")]
        public static void Bake()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            SetRig(importer, ModelImporterAnimationType.Human);

            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                AnimatorUtility.DeoptimizeTransformHierarchy(instance);
                Transform hips = FindChild(instance.transform, "mixamorig:Hips");

                foreach ((string source, string output, bool loop) in Clips)
                {
                    BakeClip(instance, hips, LoadClip(source), OutputFolder + "/" + output + ".anim", loop);
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }

                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                SetRig(importer, ModelImporterAnimationType.Generic);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Baked {Clips.Length} enemy clips into {OutputFolder}.");
        }

        private static void BakeClip(GameObject instance, Transform hips, AnimationClip source, string outputPath, bool loop)
        {
            var recorder = new GameObjectRecorder(instance);
            recorder.BindComponentsOfType<Transform>(hips.gameObject, true);

            int frames = Mathf.RoundToInt(source.length * SampleRate);
            AnimationMode.StartAnimationMode();
            for (int frame = 0; frame <= frames; frame++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(instance, source, Mathf.Min(frame / SampleRate, source.length));
                AnimationMode.EndSampling();
                recorder.TakeSnapshot(frame == 0 ? 0f : 1f / SampleRate);
            }

            AnimationMode.StopAnimationMode();

            var clip = new AnimationClip { frameRate = SampleRate };
            recorder.SaveToClip(clip, SampleRate);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, outputPath);
            }
        }

        private static void SetRig(ModelImporter importer, ModelImporterAnimationType type)
        {
            importer.animationType = type;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        private static AnimationClip LoadClip(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            throw new InvalidOperationException($"No animation clip in {path}.");
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            throw new InvalidOperationException($"{name} not found under {root.name}.");
        }
    }
}
