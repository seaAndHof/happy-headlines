using System.Text.RegularExpressions;

namespace HappyHeadlines.Shared;

public static class ProfanityFilter
{
    /// <summary>Replaces every whole-word, case-insensitive match of <paramref name="words"/> with asterisks.</summary>
    public static string Mask(string text, IEnumerable<string> words)
    {
        foreach (var word in words)
            text = Regex.Replace(text, $@"\b{Regex.Escape(word)}\b", m => new string('*', m.Length), RegexOptions.IgnoreCase);
        return text;
    }
}
