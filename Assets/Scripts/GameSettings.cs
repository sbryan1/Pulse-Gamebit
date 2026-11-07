using UnityEngine;

// Settings chosen in the main menu, read by the game scene.
// Saved with PlayerPrefs so they're remembered between sessions.
public static class GameSettings
{
    const string SkillKey = "PulseGambit.SkillLevel";
    const string SideKey  = "PulseGambit.PlayerIsWhite";

    // Stockfish skill level, 0 (weakest) to 20 (strongest).
    public static int SkillLevel
    {
        get => PlayerPrefs.GetInt(SkillKey, 10);
        set { PlayerPrefs.SetInt(SkillKey, Mathf.Clamp(value, 0, 20)); PlayerPrefs.Save(); }
    }

    // Which side the player controls. Stockfish plays the other one.
    // When the player is Black the board is shown from Black's side.
    public static bool PlayerIsWhite
    {
        get => PlayerPrefs.GetInt(SideKey, 1) == 1;
        set { PlayerPrefs.SetInt(SideKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
