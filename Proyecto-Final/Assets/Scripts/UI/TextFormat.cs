using System;
using UnityEngine;

// Formats designer-editable texts such as "Plant limit: +{0} ({1})".
// An empty or invalid format never breaks the game: it logs a warning and uses the default text.
public static class TextFormat
{
    public static string Apply(string format, string defaultFormat, params object[] args)
    {
        if (!string.IsNullOrEmpty(format))
        {
            try { return string.Format(format, args); }
            catch (FormatException) { Debug.LogWarning($"Invalid text format \"{format}\"; using \"{defaultFormat}\" instead."); }
        }
        return string.Format(defaultFormat, args);
    }

    // The format to use for repeated updates (e.g. a counter): warns once if it is invalid, then returns the default.
    public static string Resolve(string format, string defaultFormat, params object[] sampleArgs)
    {
        if (string.IsNullOrEmpty(format)) return defaultFormat;
        try { string.Format(format, sampleArgs); return format; }
        catch (FormatException)
        {
            Debug.LogWarning($"Invalid text format \"{format}\"; using \"{defaultFormat}\" instead.");
            return defaultFormat;
        }
    }
}
