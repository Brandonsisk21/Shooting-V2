using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ArenaShooter.EditorTools
{
    /// <summary>
    /// Puts steam_appid.txt (app 480, Valve's test app) next to the built game so Steam invites
    /// and online play work in builds you send to friends, not just in the editor (GDD 9.1).
    /// </summary>
    public class SteamAppIdPostBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            string exePath = report.summary.outputPath;
            string folder = Directory.Exists(exePath) ? exePath : Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(folder)) return;
            File.WriteAllText(Path.Combine(folder, "steam_appid.txt"), "480");
        }
    }
}
