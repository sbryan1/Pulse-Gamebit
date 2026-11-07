using UnityEditor;
using UnityEngine;

namespace PulseGambit.Settings.EditorTools
{
    static class NeonSettingsMenuEditor
    {
        [MenuItem("GameObject/Pulse Gambit/Neon Settings Menu", false, 10)]
        static void Create(MenuCommand cmd)
        {
            var go = new GameObject("NeonSettingsMenu", typeof(NeonSettingsMenu));
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Neon Settings Menu");
            Selection.activeObject = go;
        }

        [MenuItem("Tools/Pulse Gambit/Reset Audio Settings")]
        static void ResetPrefs()
        {
            PlayerPrefs.DeleteKey("PulseGambit.MusicOn");
            PlayerPrefs.DeleteKey("PulseGambit.MoveSoundsOn");
            PlayerPrefs.Save();
            Debug.Log("Pulse Gambit: music and move sounds reset to ON.");
        }
    }
}
