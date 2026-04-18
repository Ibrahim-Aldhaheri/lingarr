using System.Text.RegularExpressions;
using Lingarr.Server.Interfaces.Services;

namespace Lingarr.Server.Services.Subtitle;

public class SubtitleFormatterService : ISubtitleFormatterService
{
    // Matches pure SVG vector paths with no readable text — supports
    // multi-command chains where command letters are interspersed with
    // coordinates, e.g. "m 0 0 l 100 100 b 50 50 200 200".
    private static readonly Regex BareVectorPattern = new(
        @"^[mlcbsnMLCBSN](?:\s+(?:[mlcbsnMLCBSN]|[+-]?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?))+$",
        RegexOptions.Compiled);

    // Matches ASS drawing-mode blocks {\pN}...{\pM}. Stripped before the
    // generic tag remover so the inner vector commands do not leak through.
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

        // Skip pure SVG vector paths (bare vector commands with no readable text)
        if (BareVectorPattern.IsMatch(result)) {
            return string.Empty;
        }

        return result;
    }
}
