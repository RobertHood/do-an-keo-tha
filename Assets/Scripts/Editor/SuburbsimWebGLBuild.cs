using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the WebGL player used by the Suburbsim web app (Suburban-House-Design-Simulation-FE).
/// Output: Builds/WebGL/Build/* plus Builds/WebGL/manifest.json, which tells the web app the
/// (hashed) file names to load.
///
/// Editor:  Build > Suburbsim WebGL (also copies into the web app's public/unity folder, chosen once)
/// CLI:    Unity.exe -batchmode -quit -projectPath "&lt;project&gt;" -buildTarget WebGL
///            -executeMethod SuburbsimWebGLBuild.BuildFromCommandLine [-copyTo "&lt;FE&gt;/public/unity"]
/// </summary>
public static class SuburbsimWebGLBuild
{
    private const string OutputFolder = "Builds/WebGL";
    private const string ManifestFileName = "manifest.json";

    [Serializable]
    private class BuildManifest
    {
        public string productName;
        public string productVersion;
        public string companyName;
        public string builtAt;
        public string loaderUrl;
        public string dataUrl;
        public string frameworkUrl;
        public string codeUrl;
    }

    private const string CopyToPrefKey = "SuburbsimWebGLBuild.CopyTo";
    private const string DefaultFrontendFolder = "Suburban-House-Design-Simulation-FE/public/unity";

    /// <summary>
    /// Builds, then copies the build into the web app's public/unity folder.
    /// The folder is asked for once and remembered (change it with "Choose Web App Folder...").
    /// </summary>
    [MenuItem("Build/Suburbsim WebGL")]
    public static void BuildFromMenu()
    {
        string copyTo = EditorPrefs.GetString(CopyToPrefKey, "");
        if (string.IsNullOrEmpty(copyTo) || !Directory.Exists(copyTo))
        {
            copyTo = ChooseCopyFolder();
        }

        if (!Build(out string error))
        {
            EditorUtility.DisplayDialog("Suburbsim WebGL build failed", error, "OK");
            return;
        }

        if (string.IsNullOrEmpty(copyTo))
        {
            EditorUtility.RevealInFinder(Path.Combine(OutputFolder, ManifestFileName));
            return;
        }

        if (CopyOutput(copyTo, out error))
        {
            EditorUtility.DisplayDialog("Suburbsim WebGL build", "Build copied to:\n" + Path.GetFullPath(copyTo) + "\n\nHard-refresh the web app (Ctrl+Shift+R) to load it.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Suburbsim WebGL copy failed", error, "OK");
        }
    }

    [MenuItem("Build/Suburbsim WebGL - Choose Web App Folder...")]
    public static void ChooseCopyFolderFromMenu()
    {
        ChooseCopyFolder();
    }

    private static string ChooseCopyFolder()
    {
        string guess = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", DefaultFrontendFolder));
        string current = EditorPrefs.GetString(CopyToPrefKey, "");
        string start = Directory.Exists(current) ? current : (Directory.Exists(guess) ? guess : Directory.GetCurrentDirectory());

        string picked = EditorUtility.OpenFolderPanel("Web app's public/unity folder (Cancel = don't copy)", start, "");
        if (!string.IsNullOrEmpty(picked))
        {
            EditorPrefs.SetString(CopyToPrefKey, picked);
        }
        return picked;
    }

    public static void BuildFromCommandLine()
    {
        bool ok = Build(out string error);
        if (ok)
        {
            string copyTo = GetArgument("-copyTo");
            if (!string.IsNullOrEmpty(copyTo))
            {
                ok = CopyOutput(copyTo, out error);
            }
        }

        if (!ok)
        {
            Debug.LogError("[SuburbsimWebGLBuild] " + error);
            EditorApplication.Exit(1);
        }
    }

    private static bool Build(out string error)
    {
        error = null;

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            error = "Could not switch to the WebGL build target. Is WebGL Build Support installed?";
            return false;
        }

        // Brotli + decompression fallback: the build loads from any static host (Cloudflare, S3, R2)
        // without needing Content-Encoding headers. Hashed names let hosts cache files forever.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.dataCaching = true;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            error = "No scenes are enabled in Build Settings.";
            return false;
        }

        if (Directory.Exists(OutputFolder))
        {
            Directory.Delete(OutputFolder, true);
        }

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            error = $"Build {report.summary.result} with {report.summary.totalErrors} error(s). See the Console / log.";
            return false;
        }

        return WriteManifest(out error);
    }

    private static bool WriteManifest(out string error)
    {
        error = null;
        string buildDir = Path.Combine(OutputFolder, "Build");
        if (!Directory.Exists(buildDir))
        {
            error = "Build folder not found: " + buildDir;
            return false;
        }

        string[] files = Directory.GetFiles(buildDir).Select(Path.GetFileName).ToArray();
        string Find(string marker) => files.FirstOrDefault(f => f.Contains(marker));

        BuildManifest manifest = new BuildManifest
        {
            productName = PlayerSettings.productName,
            productVersion = PlayerSettings.bundleVersion,
            companyName = PlayerSettings.companyName,
            builtAt = DateTime.UtcNow.ToString("o"),
            loaderUrl = Find(".loader.js"),
            dataUrl = Find(".data"),
            frameworkUrl = Find(".framework.js"),
            codeUrl = Find(".wasm")
        };

        if (manifest.loaderUrl == null || manifest.dataUrl == null || manifest.frameworkUrl == null || manifest.codeUrl == null)
        {
            error = "Could not identify all build files in " + buildDir + ": " + string.Join(", ", files);
            return false;
        }

        manifest.loaderUrl = "Build/" + manifest.loaderUrl;
        manifest.dataUrl = "Build/" + manifest.dataUrl;
        manifest.frameworkUrl = "Build/" + manifest.frameworkUrl;
        manifest.codeUrl = "Build/" + manifest.codeUrl;

        File.WriteAllText(Path.Combine(OutputFolder, ManifestFileName), JsonUtility.ToJson(manifest, true));
        Debug.Log("[SuburbsimWebGLBuild] Build succeeded: " + Path.GetFullPath(OutputFolder));
        return true;
    }

    /// <summary>
    /// Replaces target/Build and target/manifest.json with the new build (nothing else in target is touched).
    /// </summary>
    private static bool CopyOutput(string target, out string error)
    {
        error = null;
        try
        {
            string targetBuild = Path.Combine(target, "Build");
            if (Directory.Exists(targetBuild)) Directory.Delete(targetBuild, true);
            Directory.CreateDirectory(targetBuild);

            foreach (string file in Directory.GetFiles(Path.Combine(OutputFolder, "Build")))
            {
                File.Copy(file, Path.Combine(targetBuild, Path.GetFileName(file)));
            }

            File.Copy(Path.Combine(OutputFolder, ManifestFileName), Path.Combine(target, ManifestFileName), true);
            Debug.Log("[SuburbsimWebGLBuild] Copied build to: " + Path.GetFullPath(target));
            return true;
        }
        catch (Exception e)
        {
            error = "Copy to '" + target + "' failed: " + e.Message;
            return false;
        }
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }
}
