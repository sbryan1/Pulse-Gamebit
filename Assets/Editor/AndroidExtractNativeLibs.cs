#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

// Step 3: makes Android install the Stockfish engine (libstockfish.so) as a real file
// in the app's native-library folder, which is the one place Android allows
// an app to run a program it ships with.
//
// Runs automatically during every Android build. Put this file in Assets/Editor/.
public class AndroidExtractNativeLibs : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        // Unity passes the "unityLibrary" module; the "launcher" module sits next to it.
        string root = Path.GetDirectoryName(unityLibraryPath.TrimEnd('/', '\\'));

        foreach (var module in new[] { "launcher", "unityLibrary" })
        {
            string gradleFile = Path.Combine(root, module, "build.gradle");
            if (!File.Exists(gradleFile)) continue;

            string text = File.ReadAllText(gradleFile);
            if (Regex.IsMatch(text, @"useLegacyPackaging"))
            {
                text = Regex.Replace(text, @"useLegacyPackaging\s*(=\s*)?(true|false)", "useLegacyPackaging true");
            }
            else
            {
                var androidBlock = new Regex(@"^android\s*\{", RegexOptions.Multiline);
                var match = androidBlock.Match(text);
                if (!match.Success) continue;
                const string packaging =
                    "\n    packagingOptions {\n" +
                    "        jniLibs {\n" +
                    "            useLegacyPackaging true   // extract .so files on install (needed to run Stockfish)\n" +
                    "        }\n" +
                    "    }\n";
                text = text.Insert(match.Index + match.Length, packaging);
            }
            File.WriteAllText(gradleFile, text);
            Debug.Log("[Stockfish] Native library extraction enabled in " + module + "/build.gradle");
        }

        // App Bundles (.aab) are covered by useLegacyPackaging above. The old
        // "android.bundle.enableUncompressedNativeLibs" property was removed in Android
        // Gradle Plugin 8.1 and now makes the build fail, so strip it if an earlier
        // version of this script added it.
        string props = Path.Combine(root, "gradle.properties");
        if (File.Exists(props))
        {
            string p = File.ReadAllText(props);
            string cleaned = Regex.Replace(p, @"^\s*android\.bundle\.enableUncompressedNativeLibs\s*=.*\r?\n?", "", RegexOptions.Multiline);
            if (cleaned != p) File.WriteAllText(props, cleaned);
        }
    }
}
#endif
