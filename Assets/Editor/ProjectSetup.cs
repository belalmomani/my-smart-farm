using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;

namespace MySmartFarm.EditorTools
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        /// <summary>Creates an empty scene (GameController bootstraps itself at runtime) and registers it in Build Settings.</summary>
        public static void CreateSceneSilent()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(sc, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        public static void ConfigureAndroidSilent()
        {
            PlayerSettings.productName = "My Smart Farm";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "io.github.belalmomani.mysmartfarm"); // permanent once published on Google Play
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        }

        [MenuItem("My Smart Farm/1) Create Main Scene")]
        public static void CreateScene() { CreateSceneSilent(); EditorUtility.DisplayDialog("My Smart Farm", "Main scene created.", "OK"); }

        [MenuItem("My Smart Farm/2) Configure Android")]
        public static void ConfigureAndroid() { ConfigureAndroidSilent(); EditorUtility.DisplayDialog("My Smart Farm", "Android basics set. Set your own package id and keystore before publishing.", "OK"); }
    }
}
