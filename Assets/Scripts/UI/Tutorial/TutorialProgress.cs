using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which intro cards the player has told the game not to show again, kept in PlayerPrefs so it survives
/// closing the game.
///
/// It is deliberately tiny and lives apart from the card itself (<see cref="TutorialIntro"/>), for the same
/// reason <see cref="LevelProgress"/> lives apart from the level list: the answer is one flag per tutorial, it
/// outlives whatever scene asked for it, and anything - a card, a settings screen offering "show all intros
/// again", a testing tool - can ask the same question in the same way rather than keeping its own idea of it.
///
/// A tutorial is named by the scene it is in unless it is given a name of its own, and the flag is keyed on that
/// name, so two cards in different levels never hide each other and a card that moves to another level takes
/// its own answer with it.
/// </summary>
public static class TutorialProgress
{
    private const string HiddenPrefix = "Tutorial.Hidden.";

    /// <summary>
    /// The names that have been hidden, so they can all be cleared at once. PlayerPrefs cannot be listed, so
    /// this is the only record of what to delete - it is written alongside every flag and is the reason
    /// <see cref="ShowAllAgain"/> can exist at all.
    /// </summary>
    private const string RegistryKey = "Tutorial.HiddenNames";

    /// <summary>Whether this tutorial has been switched off.</summary>
    public static bool IsHidden(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        return PlayerPrefs.GetInt(HiddenPrefix + name, 0) != 0;
    }

    /// <summary>
    /// Switches one tutorial off - "don't show this again" - or back on.
    ///
    /// Only writes when the answer actually changes, so a card opened and dismissed over and over decides
    /// nothing new and does not keep writing to disk for it.
    /// </summary>
    public static void SetHidden(string name, bool hidden)
    {
        if (string.IsNullOrEmpty(name)) return;

        if (IsHidden(name) == hidden) return;

        if (hidden)
        {
            PlayerPrefs.SetInt(HiddenPrefix + name, 1);
            AddToRegistry(name);
        }
        else
        {
            PlayerPrefs.DeleteKey(HiddenPrefix + name);
            RemoveFromRegistry(name);
        }

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Forgets every "don't show again", so every intro plays again. This is what a "show intros again" button
    /// on a settings screen would call, and what a testing session wants.
    /// </summary>
    public static void ShowAllAgain()
    {
        string[] names = Registry();

        for (int i = 0; i < names.Length; i++)
            PlayerPrefs.DeleteKey(HiddenPrefix + names[i]);

        PlayerPrefs.DeleteKey(RegistryKey);
        PlayerPrefs.Save();
    }

    /// <summary>Every tutorial that has been switched off, in no particular order.</summary>
    public static string[] HiddenNames()
    {
        return Registry();
    }

    // ---------------------------------------------------------------- the registry

    private static string[] Registry()
    {
        string stored = PlayerPrefs.GetString(RegistryKey, string.Empty);

        if (string.IsNullOrEmpty(stored)) return new string[0];

        List<string> names = new List<string>();
        string[] parts = stored.Split(',');

        for (int i = 0; i < parts.Length; i++)
        {
            string name = parts[i].Trim();
            if (name.Length > 0 && !names.Contains(name)) names.Add(name);
        }

        return names.ToArray();
    }

    private static void AddToRegistry(string name)
    {
        List<string> names = new List<string>(Registry());

        if (names.Contains(name)) return;

        names.Add(name);
        PlayerPrefs.SetString(RegistryKey, string.Join(",", names.ToArray()));
    }

    private static void RemoveFromRegistry(string name)
    {
        List<string> names = new List<string>(Registry());

        if (!names.Remove(name)) return;

        PlayerPrefs.SetString(RegistryKey, string.Join(",", names.ToArray()));
    }
}
