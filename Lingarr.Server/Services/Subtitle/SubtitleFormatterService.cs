using System.Text.RegularExpressions;
using Lingarr.Server.Interfaces.Services;

namespace Lingarr.Server.Services.Subtitle;

public class SubtitleFormatterService : ISubtitleFormatterService
{
    // Text starting with a vector drawing prefix — pure vector paths or
    // karaoke where a trailing syllable follows the drawing ("m 0 0 ka").
    private static readonly Regex VectorPrefixPattern = new(
        @"^[mlcbsnMLCBSN](?:\s+(?:[mlcbsnMLCBSN]|[+-]?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)){2,}(?=\s|$)",
        RegexOptions.Compiled);

    // ASS drawing-mode blocks {\pN}...{\pM} — stripped before generic tag removal.
    private static readonly Regex DrawingTagPattern = new(
        @"\\[pP][0-9].*?\\[pP][0-9]",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <inheritdoc />
    public static string RemoveMarkup(string input)
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

        // Drop lines that are purely vector drawing commands.
        if (VectorPrefixPattern.IsMatch(result)) {
            return string.Empty;
        }

        return result;
    }

    /// <summary>
    /// Normalises newlines in an LLM translation response so it is safe to write
    /// to a subtitle text field: collapses <c>\r</c>, <c>\n</c>, <c>\r\n</c> to
    /// single spaces and trims surrounding whitespace. Real ASS/SSA line breaks
    /// must use the <c>\N</c> escape — raw newlines returned by the model
    /// would otherwise produce blank physical lines inside the output file.
    /// </summary>
    /// <param name="translated">Translated string returned by the model.</param>
    /// <returns>Single-line translation safe to write to the subtitle file.</returns>
    public static string SanitizeLlmResponse(string translated)
    {
        if (string.IsNullOrEmpty(translated)) return translated ?? string.Empty;
        var collapsed = translated.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        return Regex.Replace(collapsed, @"\s{2,}", " ").Trim();
    }
}
