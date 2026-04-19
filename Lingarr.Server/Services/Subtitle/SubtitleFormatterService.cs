using System.Text.RegularExpressions;
using Lingarr.Server.Interfaces.Services;

namespace Lingarr.Server.Services.Subtitle;

public class SubtitleFormatterService : ISubtitleFormatterService
{
    // Echoed context-prompt scaffold tokens — some models repeat these in their
    // answer. Lines starting with any of these are dropped during sanitation.
    private static readonly string[] ScaffoldLinePrefixes =
        { "[TARGET]", "[CONTEXT]", "[/CONTEXT]" };

    private static readonly Regex ScaffoldArrowLine = new(@"^>>>.*<<<$", RegexOptions.Compiled);
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

        // Drop vector/karaoke lines unless the caller opts out.
        if (!skipKaraokeDetection && VectorPrefixPattern.IsMatch(result)) {
            return string.Empty;
        }

        return result;
    }

    /// <summary>
    /// Cleans an LLM translation response so it is safe to write to a subtitle text
    /// field: strips context-prompt scaffold the model may echo (<c>[TARGET]</c>,
    /// <c>[CONTEXT]</c>, <c>[/CONTEXT]</c>, <c>&gt;&gt;&gt; … &lt;&lt;&lt;</c>), collapses any
    /// newlines or carriage-returns to spaces, and trims surrounding whitespace.
    /// Real ASS/SSA line breaks must use the <c>\N</c> escape — raw newlines from the
    /// model would otherwise produce blank physical lines inside the output file.
    /// If a <c>[/CONTEXT]</c> marker is present, only the text AFTER it is kept
    /// (the actual answer the model produced after echoing the scaffold).
    /// </summary>
    /// <param name="translated">Translated string returned by the model.</param>
    /// <returns>Single-line translation safe to write to the subtitle file.</returns>
    public static string SanitizeLlmResponse(string translated)
    {
        if (string.IsNullOrEmpty(translated)) return translated ?? string.Empty;

        var endMarker = translated.LastIndexOf("[/CONTEXT]", StringComparison.OrdinalIgnoreCase);
        if (endMarker >= 0)
        {
            translated = translated[(endMarker + "[/CONTEXT]".Length)..];
        }

        var lines = translated.Split('\n');
        var keep = new List<string>(lines.Length);
        foreach (var raw in lines)
        {
            var line = raw.Replace("\r", "").Trim();
            if (line.Length == 0) continue;
            if (ScaffoldLinePrefixes.Any(p => line.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
            if (ScaffoldArrowLine.IsMatch(line)) continue;
            keep.Add(line);
        }
        var collapsed = string.Join(' ', keep);
        return Regex.Replace(collapsed, @"\s{2,}", " ").Trim();
    }
}
