using System.IO;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    public static class SchemyBuildEditor
    {
        [MenuItem("SSNoir/Rebuild Schemy DLL")]
        public static void RebuildSchemyDll()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string csproj = Path.Combine(
                repoRoot,
                "schemy-master",
                "src",
                "schemy",
                "schemy.csproj");
            string dllSource = Path.Combine(
                repoRoot,
                "schemy-master",
                "src",
                "schemy",
                "bin",
                "Release",
                "netstandard2.0",
                "schemy.dll");
            string dllDestination = Path.Combine(repoRoot, "Engine", "Plugins", "schemy.dll");

            if (!File.Exists(csproj))
            {
                Debug.LogError($"[SchemyBuild] schemy.csproj not found at: {csproj}");
                return;
            }

            Debug.Log("[SchemyBuild] Building schemy (netstandard2.0, Release)...");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csproj}\" -c Release --nologo -v q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[SchemyBuild] Build failed:\n{stderr}\n{stdout}");
                    return;
                }
            }

            if (!File.Exists(dllSource))
            {
                Debug.LogError($"[SchemyBuild] Built DLL not found at: {dllSource}");
                return;
            }

            File.Copy(dllSource, dllDestination, overwrite: true);
            Debug.Log("[SchemyBuild] Copied schemy.dll to Engine/Plugins. Refreshing assets...");
            AssetDatabase.Refresh();
        }
    }
}
