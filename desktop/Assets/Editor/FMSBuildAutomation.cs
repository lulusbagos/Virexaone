#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Virexa.FMS.Editor
{
    public static class FMSBuildAutomation
    {
        private const string MAIN_SCENE_PATH = "Assets/Scenes/FMS_Mine_Main.unity";
        private const string STANDALONE_OUTPUT_DIR = "Builds/Windows_x64";
        private const string STANDALONE_EXE_NAME = "VirexaOne_FMS.exe";
        private const string WEBGL_OUTPUT_PATH = @"D:\Publish\Virexa_one";

        #region --- WEBGL BUILD (HTML5) ---

        [MenuItem("Virexa/🌐 Build WebGL -> D:\\Publish\\Virexa_one", false, 50)]
        public static void BuildWebGL()
        {
            BuildWebCustom(WEBGL_OUTPUT_PATH);
        }

        [MenuItem("Virexa/📁 Buka Folder WebGL (D:\\Publish\\Virexa_one)", false, 51)]
        public static void OpenWebGLFolder()
        {
            if (!Directory.Exists(WEBGL_OUTPUT_PATH))
            {
                Directory.CreateDirectory(WEBGL_OUTPUT_PATH);
            }
            EditorUtility.RevealInFinder(WEBGL_OUTPUT_PATH);
        }

        public static void BuildWebCustom(string targetDirectory)
        {
            if (string.IsNullOrEmpty(targetDirectory))
                targetDirectory = WEBGL_OUTPUT_PATH;

            if (!Directory.Exists(targetDirectory))
            {
                try
                {
                    Directory.CreateDirectory(targetDirectory);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FMSBuildAutomation] Gagal membuat direktori publish: {ex.Message}");
                    EditorUtility.DisplayDialog("❌ Error Direktori", $"Tidak dapat membuat folder: {targetDirectory}\n\n{ex.Message}", "OK");
                    return;
                }
            }

            // Check if WebGL target is supported in this Unity installation
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                string msg = "Modul 'WebGL Build Support' belum terpasang pada versi Unity Editor ini.\n\n" +
                             "Cara Mengaktifkan:\n" +
                             "1. Buka Unity Hub\n" +
                             "2. Masuk ke tab 'Installs'\n" +
                             "3. Klik ikon Gear (⚙️) pada versi Unity Anda -> 'Add modules'\n" +
                             "4. Centang 'WebGL Build Support' lalu klik Install.";
                Debug.LogError("[FMSBuildAutomation] " + msg);
                EditorUtility.DisplayDialog("⚠️ WebGL Module Required", msg, "OK");
                return;
            }

            // Optimize WebGL Player Settings for instant compatibility & responsiveness
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.runInBackground = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FMSBuildAutomation] Warning setting WebGL options: {ex.Message}");
            }

            string[] scenes = new string[] { MAIN_SCENE_PATH };

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = targetDirectory,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[FMSBuildAutomation] 🚀 Starting WebGL HTML5 Build -> {targetDirectory}...");
            EditorUtility.DisplayProgressBar("Building Virexa One WebGL", "Compiling WebAssembly (WASM), shaders & web assets...", 0.3f);

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                BuildSummary summary = report.summary;

                EditorUtility.ClearProgressBar();

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[FMSBuildAutomation] ✅ WebGL Build Succeeded! Total Size: {summary.totalSize / (1024 * 1024):F1} MB, Path: {targetDirectory}");
                    if (Application.isBatchMode) return;
                    
                    bool open = EditorUtility.DisplayDialog(
                        "✅ WebGL Build Berhasil!",
                        $"Aplikasi Web Virexa One berhasil di-build ke:\n\n📁 {targetDirectory}\nUkuran: {summary.totalSize / (1024 * 1024):F1} MB\n\nFile siap di-host pada web server IIS / Nginx / Apache / Python HTTP server.\n\nBuka folder output sekarang?",
                        "Buka Folder",
                        "Tutup"
                    );

                    if (open)
                    {
                        EditorUtility.RevealInFinder(targetDirectory);
                    }
                }
                else if (summary.result == BuildResult.Failed)
                {
                    Debug.LogError($"[FMSBuildAutomation] ❌ WebGL Build Gagal dengan {summary.totalErrors} errors.");
                    EditorUtility.DisplayDialog("❌ Build WebGL Gagal", $"Build WebGL gagal dibuat. Silakan periksa Console Unity untuk rincian error.", "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[FMSBuildAutomation] Exception during WebGL build: {ex.Message}");
                EditorUtility.DisplayDialog("❌ Build Error", $"Terjadi kesalahan:\n{ex.Message}", "OK");
            }
        }

        #endregion

        #region --- WINDOWS STANDALONE BUILD (.EXE) ---

        [MenuItem("Virexa/🚀 Build Windows Standalone (.EXE)", false, 100)]
        public static void BuildWindowsExe()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputFolder = Path.Combine(projectRoot, STANDALONE_OUTPUT_DIR);
            string outputExePath = Path.Combine(outputFolder, STANDALONE_EXE_NAME);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string[] scenes = new string[] { MAIN_SCENE_PATH };

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[FMSBuildAutomation] Starting Windows x64 Standalone Build -> {outputExePath}...");
            EditorUtility.DisplayProgressBar("Building Virexa One Standalone", "Compiling shaders and building executable (.EXE)...", 0.3f);

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                BuildSummary summary = report.summary;

                EditorUtility.ClearProgressBar();

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[FMSBuildAutomation] Build Succeeded! Size: {summary.totalSize / (1024 * 1024):F1} MB, Output: {outputExePath}");
                    if (Application.isBatchMode) return;
                    
                    bool open = EditorUtility.DisplayDialog(
                        "✅ Build Berhasil!",
                        $"Aplikasi Virexa One berhasil dibuat sebagai file executable (.EXE):\n\n📁 {outputExePath}\nUkuran: {summary.totalSize / (1024 * 1024):F1} MB\n\nBuka folder output sekarang?",
                        "Buka Folder",
                        "Tutup"
                    );

                    if (open)
                    {
                        EditorUtility.RevealInFinder(outputExePath);
                    }
                }
                else if (summary.result == BuildResult.Failed)
                {
                    Debug.LogError($"[FMSBuildAutomation] Build Failed with {summary.totalErrors} errors.");
                    EditorUtility.DisplayDialog("❌ Build Gagal", $"Build gagal dibuat. Periksa jendela Console Unity untuk detail error.", "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[FMSBuildAutomation] Exception during build: {ex.Message}");
            }
        }

        [MenuItem("Virexa/📁 Buka Folder Build Executable", false, 101)]
        public static void OpenBuildFolder()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputFolder = Path.Combine(projectRoot, STANDALONE_OUTPUT_DIR);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            EditorUtility.RevealInFinder(outputFolder);
        }

        #endregion
    }

    public sealed class FMSBuildBranding : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            PlayerSettings.companyName = "Astha Virexa Technology";
            PlayerSettings.productName = "Virexa One";
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            if (report.summary.platform == BuildTarget.WebGL)
            {
                PlayerSettings.WebGL.template = "PROJECT:VirexaOne";
                PlayerSettings.WebGL.nameFilesAsHashes = true;
            }

            if (report.summary.platform == BuildTarget.StandaloneWindows64)
            {
                const string iconPath = "Assets/Branding/VirexaOneIcon.png";
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (icon == null)
                    throw new BuildFailedException("Virexa One application icon is missing: " + iconPath);

                int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
                Texture2D[] icons = new Texture2D[sizes.Length];
                for (int i = 0; i < icons.Length; i++) icons[i] = icon;
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            }
        }
    }
}
#endif
