using UnityEditor;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Android.Types;
using UnityEditor.Android;
#endif

// One-time project settings the game relies on. Runs each time the editor loads
// scripts, but only changes something when it's missing, so your own choices stick.
//   - Adds the "CaptureFX" sorting layer (after Default) used by CapturePulse's shockwave ring.
//   - Android: turns on Debug Symbols (Symbol Table) so crash reports show readable
//     stack traces, which Unity asks for when Diagnostics Data is enabled.
[InitializeOnLoad]
static class ProjectSetupFixes
{
    const string CaptureLayer = "CaptureFX";

    static ProjectSetupFixes() => EditorApplication.delayCall += Run;

    static void Run()
    {
        EnsureSortingLayer(CaptureLayer);
#if UNITY_ANDROID
        if (UserBuildSettings.DebugSymbols.level == DebugSymbolLevel.None)
        {
            UserBuildSettings.DebugSymbols.level = DebugSymbolLevel.SymbolTable;
            Debug.Log("[Setup] Android Debug Symbols set to Symbol Table (for readable crash reports).");
        }
#endif
    }

    static void EnsureSortingLayer(string layerName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("m_SortingLayers");
        if (layers == null) return;

        var usedIds = new System.Collections.Generic.HashSet<long>();
        for (int i = 0; i < layers.arraySize; i++)
        {
            var e = layers.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("name").stringValue == layerName) return; // already there
            usedIds.Add(e.FindPropertyRelative("uniqueID").longValue);
        }

        // Pick an unused, non-zero ID (0 belongs to Default).
        var rng = new System.Random();
        long id;
        do { id = rng.Next(1, int.MaxValue); } while (usedIds.Contains(id));

        // Added at the end of the list, so it draws after (on top of) Default.
        // The new entry copies the previous one, so it starts unlocked like Default.
        int index = layers.arraySize;
        layers.InsertArrayElementAtIndex(index);
        var layer = layers.GetArrayElementAtIndex(index);
        layer.FindPropertyRelative("name").stringValue = layerName;
        layer.FindPropertyRelative("uniqueID").longValue = id;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[Setup] Added sorting layer '" + layerName + "'.");
    }
}
