using System.Text.RegularExpressions;
using Lingarr.Server.Interfaces.Services;

namespace Lingarr.Server.Services.Subtitle;

public class SubtitleFormatterService : ISubtitleFormatterService
{
    // Matches text that BEGINS with a vector drawing prefix — a command letter
    // followed by at least two coord/command tokens. Catches both pure vector
    // paths ("m 0 0 l 100 100") and karaoke lines where drawing data leaks
    // alongside a trailing syllable ("m 0 0 ka", "m 0 0 l 10 10 Take").
    private static readonly Regex VectorPrefixPattern = new(
        @"^[mlcbsnMLCBSN](?:\s+(?:[mlcbsnMLCBSN]|[+-]?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)){2,}(?=\s|$)",
        RegexOptions.Compiled);

    // Matches ASS drawing-mode blocks {\pN}...{\pM}. Stripped before the
    // generic tag remover so the inner vector commands do not leak through.
    private static readonly Regex DrawingTagPattern = new(
        @"\\[pP][0-9].*?\\[pP][0-9]",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <inheritdoc />
    public static string RemoveMarkup(string input, bool skipKaraokeDetection = false)
    {
        if (string.IsNullOrWhiteSpace(input)) {
            return string.Empty;
        }

        // Strip ASS drawing-mode blocks {\pN}...{\p0} before generic tag removal
        string stripped = DrawingTagPattern.Replace(input, string.Empty);

        // Remove SSA/ASS style tags: {\...}
        stripped = Regex.Replace(stripped, @"\{.*?\}", string.Empty);

        // Remove HTML-style tags: <...>
        stripped = Regex.Replace(stripped, @"<.*?>", string.Empty);

        // Replace SSA line breaks and hard spaces with spaces
        stripped = stripped.Replace("\\N", " ").Replace("\\n", " ").Replace("\\h", " ");

        // Replace tab characters (escaped or literal)
        stripped = stripped.Replace("\\t", " ").Replace("\t", " ");

        // Collapse multiple whitespace into a single space
        stripped = Regex.Replace(stripped, @"\s{2,}", " ");

        var result = stripped.Trim();

        // Skip lines that begin with a vector drawing prefix — these are
        // pure vector paths or karaoke where drawing data leaked into text.
        // When the caller opts out (skipKaraokeDetection=true), leave the
        // text alone so the user can get raw behaviour.
        if (!skipKaraokeDetection && VectorPrefixPattern.IsMatch(result)) {
            return string.Empty;
        }

        return result;
    }
}
