//#define HAS_PS4
//#define HAS_FMOD

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class BatchBuilderSettings : ScriptableObject
{
    [Serializable]
    public struct Settings
    {
        public string Name;
        public bool Active;
        public BuildTarget Target;
        public BuildTargetGroup Group;
        public Symbol[] symbols;
        public string Path;
        public string Filename;
        public bool Zip;
        public bool UnityLogo;
        public bool Sign;

        public string Version;
        public string AppID;
        public int ReleaseNumber;

        public string NPTitleDatPath;
        public PS4BuildSubtarget ps4PackageType;
        public int parentalLevel;
        public int DefaultAgeRestriction;
#if HAS_PS4
        public Platform.PS4Data.AgeRestriction[] AgeRestrictions;
#endif
        public UnityEditor.AnimatedValues.AnimBool Editing;
    }

#if HAS_PS4
    public Platform Platform;
#endif

    [HideInInspector] public Settings[] Builds = new Settings[0];

    [Serializable]
    public struct Symbol
    {
        public bool Active;
        public string Name;
    }

    [HideInInspector] public Symbol[] GeneralSymbols = new Symbol[0];

    [HideInInspector] public List<string> BuildPlatformForAssetBundles = new List<string>();

    [Tooltip("Removes debug log, adds optimizations")]
    public bool Master = true;
    [Tooltip("Allow connecting a debugger to running program")]
    public bool ScriptDebugging = false;
    public bool LZ4Compression = true;
    [Tooltip("Identifier to use when signing macOS app packages - only when building from macOS!")]
    public string CodesignID;
}

class BatchBuilderObject : EditorWindow
{
    public static void Build()
    {
        BatchBuilderObject bbo = CreateWindow<BatchBuilderObject>();

        string[] args = System.Environment.GetCommandLineArgs();
        string input = "";
        for (int i = 0; i < args.Length; i++)
        {
            Debug.Log("ARG " + i + ": " + args[i]);
            if (args[i] == "-settingsInput")
            {
                input = args[i + 1];
            }
        }
        Debug.Log(input);
        string json = File.ReadAllText(input);

        bbo.settings = ScriptableObject.CreateInstance<BatchBuilderSettings>();
        AssetDatabase.CreateAsset(bbo.settings, "Assets/RemoteBuild.asset");

        JsonUtility.FromJsonOverwrite(json, bbo.settings);

        bbo.StartBuilding();
    }

    void OnEnable()
    {
        EditorApplication.update += Update;
    }

    void OnDisable()
    {
        EditorApplication.update -= Update;
    }

    enum Step
    {
        SetSymbols,
        Platform,
        Compilation,
        DataCopy,
        AssetBundles,
        Build,
        Zip
    }

    int building = -1;
    int succeeded = 0;
    int failed = 0;
    Step step = Step.SetSymbols;
    Stopwatch timer = new Stopwatch();

    BatchBuilderSettings settings;

    public void StartBuilding()
    {
        timer.Start();
        building = 0;
        succeeded = 0;
        failed = 0;
        step = Step.SetSymbols;
    }

    void BuildPlayers()
    {
        ref var build = ref settings.Builds[building];

        if (build.Active)
        {
            switch (step)
            {
                case Step.SetSymbols:
                    {
                        SetSymbols(build);
#if HAS_XBOXONE
                    if (build.Target == BuildTarget.XboxOne)
                        PlayerSettings.XboxOne.Version = build.Version;
#endif
                        step = Step.Platform;
                        break;
                    }

                case Step.Platform:
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(build.Group, build.Target);
                        step = Step.Compilation;
                        break;
                    }

                case Step.Compilation:
                    {
                        if (!EditorApplication.isCompiling)
                        {
                            step = Step.DataCopy;
                        }
                        break;
                    }

                case Step.DataCopy:
                    {
#if HAS_FMOD
                    FMODUnity.EventManager.CopyToStreamingAssets(build.Target);
#endif

                        step = Step.Build;

                        break;
                    }

                case Step.Build:
                    {
                        if (Build(build))
                        {
                            building = -1;
                            Debug.LogWarning("Builds canceled");
                        }
                        else
                        {
                            step = Step.Zip;
                        }
                        break;
                    }

                case Step.Zip:
                    {
                        if (build.Zip)
                        {
                            string buildpath = build.Path;
                            string zipname = buildpath;
                            if (zipname.LastIndexOf("/") == zipname.Length - 1)
                                zipname.Remove(zipname.Length - 1);
                            zipname += ".zip";

                            Jackdaw.BuildHelper.Compress(buildpath, zipname);
                        }

                        step = Step.SetSymbols;
                        if (building != -1)
                            building++;
                        break;
                    }
            }
        }
        else
        {
            building++;
        }

        if (building >= settings.Builds.Length)
        {
            building = -1;
            timer.Stop();
            Debug.LogFormat("Builds complete: {0} succeeded, {1} failed ({2})", succeeded, failed, timer.Elapsed);
            if (succeeded > 0)
            {
                EditorUtility.RevealInFinder("Builds");
                EditorApplication.Beep();
            }
            EditorApplication.Exit(0);
            AssetDatabase.DeleteAsset("Assets/RemoteBuild.asset");
        }
    }

    private void Update()
    {
        if (building >= 0)
        {
            BuildPlayers();
        }
    }

    void SetSymbols(BatchBuilderSettings.Settings build)
    {
        string Symbols = "";

        foreach (BatchBuilderSettings.Symbol symbol in build.symbols)
            if (symbol.Active)
                Symbols += ";" + symbol.Name;

        foreach (BatchBuilderSettings.Symbol symbol in settings.GeneralSymbols)
            if (symbol.Active)
                Symbols += ";" + symbol.Name;

        PlayerSettings.SetScriptingDefineSymbolsForGroup(build.Group, Symbols);
    }

    bool Build(BatchBuilderSettings.Settings build)
    {
        if (Directory.Exists(build.Path))
            Directory.Delete(build.Path, true);

        // set Application.version
        var now = DateTime.Now;
        PlayerSettings.bundleVersion = $"{now.Year % 100:00}.{now.Month:00}";

        Directory.CreateDirectory(build.Path);

        string file = Path.Combine(build.Path, build.Filename);

        BuildOptions flags = BuildOptions.StrictMode;

        if (!settings.Master)
            flags |= BuildOptions.Development;

        if (settings.ScriptDebugging)
            flags |= BuildOptions.AllowDebugging;

        if (settings.LZ4Compression && build.Target != BuildTarget.XboxOne)
            flags |= BuildOptions.CompressWithLz4HC;

        PlayerSettings.SplashScreen.showUnityLogo = build.UnityLogo;
        PlayerSettings.SplashScreen.show = build.UnityLogo;

        switch (build.Target)
        {
            case BuildTarget.XboxOne:
                {
                    EditorUserBuildSettings.xboxBuildSubtarget = settings.Master ? XboxBuildSubtarget.Master : XboxBuildSubtarget.Development;
                    EditorUserBuildSettings.xboxOneDeployMethod = XboxOneDeployMethod.Package;
                    EditorUserBuildSettings.streamingInstallLaunchRange = EditorBuildSettings.scenes.Length;
                    PlayerSettings.XboxOne.Version = build.Version;
                    break;
                }

            case BuildTarget.Switch:
                {
                    EditorUserBuildSettings.switchCreateRomFile = true;
                    EditorUserBuildSettings.switchCreateSolutionFile = false; // too slow
                    PlayerSettings.Switch.releaseVersion = build.ReleaseNumber.ToString();
                    PlayerSettings.Switch.displayVersion = "1.0." + build.ReleaseNumber.ToString();
                    PlayerSettings.Switch.applicationID = build.AppID;
                    PlayerSettings.Switch.presenceGroupId = build.AppID;
                    PlayerSettings.Switch.localCommunicationIds = new string[] { build.AppID };
                    break;
                }

            case BuildTarget.PS4:
                {
                    EditorUserBuildSettings.compressFilesInPackage = true;
                    EditorUserBuildSettings.compressWithPsArc = true;
                    EditorUserBuildSettings.ps4BuildSubtarget = build.ps4PackageType;
                    PlayerSettings.PS4.parentalLevel = build.parentalLevel;
                    PlayerSettings.PS4.masterVersion = build.Version;
                    PlayerSettings.PS4.contentID = build.AppID;
                    PlayerSettings.PS4.NPtitleDatPath = build.NPTitleDatPath;
                    SetPS4AgeRestrictions(build);
                    break;
                }
        }

        Debug.LogFormat("Starting build {0} ({1})", build.Name, PlayerSettings.GetScriptingDefineSymbolsForGroup(build.Group));
        bool success = false, canceled = false;
        try
        {
            List<string> scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    scenes.Add(scene.path);

            BuildPlayerOptions options = new BuildPlayerOptions()
            {
                locationPathName = file,
                scenes = scenes.ToArray(),
                target = build.Target,
                targetGroup = build.Group,
                options = flags,
                assetBundleManifestPath = $"AssetBundles/{build.Target}/{build.Target}.manifest"
            };

            var report = BuildPipeline.BuildPlayer(options);

            success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Cancelled)
            {
                Debug.LogFormat("Canceled batch build on {0} ({1})", build.Name, PlayerSettings.GetScriptingDefineSymbolsForGroup(build.Group));
                canceled = true;
            }
            else if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                Debug.LogErrorFormat("Error while building {0}, {1} errors, {2} warnings",
                    build.Name, report.summary.totalErrors, report.summary.totalWarnings);
            }

#if UNITY_EDITOR_OSX
            if (success && build.Sign)
                BuildTag.Run("codesign", $"--deep -f -v -s \"{settings.CodesignID}\" {file}");
#endif
        }
        catch (Exception ex)
        {
            success = false;
            Debug.LogErrorFormat("Error while building {0}: {1}", build.Name, ex.Message);
        }

        Debug.LogFormat("Finished build {0} ({1})", build.Name, PlayerSettings.GetScriptingDefineSymbolsForGroup(build.Group));

        if (success)
            succeeded++;
        else
            failed++;

        return canceled;
    }

    private void SetPS4AgeRestrictions(BatchBuilderSettings.Settings build)
    {
#if HAS_PS4
        settings.Platform.PS4Settings.DefaultAgeRestriction = build.DefaultAgeRestriction;
        settings.Platform.PS4Settings.AgeRestrictions = build.AgeRestrictions;
        EditorUtility.SetDirty(settings.Platform);
        AssetDatabase.SaveAssets();
        string platformPath = AssetDatabase.GetAssetPath(settings.Platform);
        AssetDatabase.ImportAsset(platformPath);
#endif
    }


}

