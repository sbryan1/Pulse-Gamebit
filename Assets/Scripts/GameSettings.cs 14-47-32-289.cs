using UnityEngine;

// Settings chosen in the main menu, read by the game scene.
// Saved with PlayerPrefs so they're remembered between sessions.
public static class GameSettings
{
    const string SkillKey = "PulseGambit.SkillLevel";

    // Stockfish skill level, 0 (weakest) to 20 (strongest).
    public static int SkillLevel
    {
        get => PlayerPrefs.GetInt(SkillKey, 10);
        set { PlayerPrefs.SetInt(SkillKey, Mathf.Clamp(value, 0, 20)); PlayerPrefs.Save(); }
    }
}
