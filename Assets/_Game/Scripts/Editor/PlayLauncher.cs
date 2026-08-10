using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleGymBro.EditorTools
{
    // Convenience entry point for "just run the game": opens the built scene and enters Play mode.
    // Meant to be passed to a GUI (non-batchmode) editor launch via -executeMethod, so the game is
    // already playing by the time the editor window appears.
    public static class PlayLauncher
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("IdleGymBro/Play Game")]
        public static void EnterPlay()
        {
            if (Application.isBatchMode)
            {
                Debug.LogWarning("[PlayLauncher] Play mode needs a real editor window; skipping in batchmode.");
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Deferred: entering play mode during -executeMethod (still inside the load/compile
            // pass) is ignored, so hand it to the next editor update instead.
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }
    }
}
