using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;

namespace MySmartFarm.EditorTools
{
    public static class ProjectSetup
    {
        [MenuItem("My Smart Farm/1) Create Main Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(sc, "Assets/Scenes/Main.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            EditorUtility.DisplayDialog("My Smart Farm", "Main scene created. GameController bootstraps itself at runtime.", "OK");
        }

        [MenuItem("My Smart Farm/2) Configure Android")]
        public static void ConfigureAndroid()
        {
            PlayerSettings.productName = "My Smart Farm";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.example.mysmartfarm"); // CHANGE to your own id
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUtility.DisplayDialog("My Smart Farm", "Android basics set. Still do manually: Player > Other Settings > Active Input Handling = Both (or Input Manager (Old)); set your own package id and keystore.", "OK");
        }
    }
}
