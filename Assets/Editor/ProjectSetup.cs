using System.IO;
using ContainerDefense.Domain;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContainerDefense.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/ContainerYard.unity";
        static ProjectSetup()
        {
            EditorApplication.delayCall += () => {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && (!File.Exists("Assets/Resources/DefaultMatch.asset") || !File.Exists("Assets/Resources/Characters.asset") || !File.Exists("Assets/Resources/Collections.asset"))) Prepare();
            };
        }

        [MenuItem("Container Defense/Prepare Project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Resources");
            if (AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/Resources/DefaultMatch.asset") == null)
            {
                var config = ScriptableObject.CreateInstance<MatchConfig>();
                AssetDatabase.CreateAsset(config,"Assets/Resources/DefaultMatch.asset");
            }
            if (AssetDatabase.LoadAssetAtPath<CharacterConfig>("Assets/Resources/Characters.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CharacterConfig>(),"Assets/Resources/Characters.asset");
            if (AssetDatabase.LoadAssetAtPath<CollectionConfig>("Assets/Resources/Collections.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CollectionConfig>(),"Assets/Resources/Collections.asset");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("Game session").AddComponent<GameSession>();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath,true) };
            PlayerSettings.companyName = "Container Defense Studio";
            PlayerSettings.productName = "Container Defense";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            QualitySettings.antiAliasing = 4;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Container Defense/Run Core Checks")]
        public static void RunChecks()
        {
            Prepare();
            string result = CoreTests.Run() + " " + SaveTests.Run();
            Directory.CreateDirectory("TestResults"); File.WriteAllText("TestResults/unity-core-results.txt",result);
            Debug.Log(result);
            var config = AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/Resources/DefaultMatch.asset");
            config.Rules.Validate();
            AssetDatabase.LoadAssetAtPath<CharacterConfig>("Assets/Resources/Characters.asset").Validate();
            AssetDatabase.LoadAssetAtPath<CollectionConfig>("Assets/Resources/Collections.asset").Catalog();

            foreach (var skin in AssetDatabase.LoadAssetAtPath<CollectionConfig>("Assets/Resources/Collections.asset").Skins)
                if (Resources.Load<Texture2D>("Art2D/" + skin.SkinId) == null) throw new System.Exception("Missing portrait: " + skin.SkinId);
            if (Resources.Load<Texture2D>("Art2D/room") == null || Resources.Load<Font>("Fonts/NunitoBold") == null || Resources.Load<Texture2D>("Art2D/houses") == null || Resources.Load<Texture2D>("Art2D/boss") == null || Resources.Load<Texture2D>("Art2D/yard") == null || Resources.Load<Font>("Fonts/Nunito") == null)
                throw new System.Exception("Menu background or UI font is missing.");
        }

        [MenuItem("Container Defense/Build Windows Playtest")]
        public static void BuildWindows()
        { BuildAt("Builds/Windows"); }
        public static void BuildMilestonePreview()
        { BuildAt("Builds/Milestones"); }
        private static void BuildAt(string output)
        {
            RunChecks(); Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output + "/ContainerDefense.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);
            Directory.CreateDirectory(output + "/Licenses");
            File.Copy("Assets/Resources/Fonts/OFL.txt",output + "/Licenses/Nunito-OFL.txt",true);
            Debug.Log("Windows playtest built successfully: " + report.summary.totalSize + " bytes.");
        }
    }
}
