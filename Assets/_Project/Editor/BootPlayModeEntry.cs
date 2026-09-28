using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastMachinist.Editor
{
    // Editor Play must enter the existing composition scene, even while an
    // empty or environment-only scene is open. Does not modify that scene.
    [InitializeOnLoad]
    internal static class BootPlayModeEntry
    {
        private const string BootPath = "Assets/_Project/Scenes/Boot.unity";

        static BootPlayModeEntry()
        {
            EditorApplication.delayCall += Configure;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) Configure();
            };
        }

        private static void Configure()
        {
            // Automated tests explicitly control their own scenes.
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath);
            if (boot == null)
            {
                Debug.LogError("Last Machinist: Boot scene missing at " + BootPath);
                return;
            }
            EditorSceneManager.playModeStartScene = boot;
        }
    }
}
