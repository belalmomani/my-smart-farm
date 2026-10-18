using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MySmartFarm.EditorTools
{
    /// <summary>Headless Android build used by .github/workflows/unity.yml. Debug-signed only: NOT suitable for Play Store release.</summary>
    public static class CiBuild
    {
        public static void BuildAndroid()
        {
            string format = "apk"; var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-buildFormat") format = args[i + 1];
            bool aab = format == "aab";

            ProjectSetup.CreateSceneSilent(); ProjectSetup.ConfigureAndroidSilent();
            EditorUserBuildSettings.buildAppBundle = aab;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath }, target = BuildTarget.Android, options = BuildOptions.None,
                locationPathName = "build/Android/MySmartFarm" + (aab ? ".aab" : ".apk")
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Console.WriteLine("Build result: " + report.summary.result + ", size: " + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
