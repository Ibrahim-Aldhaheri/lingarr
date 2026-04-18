using System.Text;
using System.Text.RegularExpressions;
using Lingarr.Server.Interfaces.Services.Subtitle;
using Lingarr.Server.Models.FileSystem;

namespace Lingarr.Server.Services.Subtitle;

/// <summary>
/// Pristine copy of the upstream lingarr-translate/lingarr SsaParser — no karaoke
/// style skip, no vector-prefix detection, no layer-omitted fallback. Used when the
/// "Skip karaoke/vector detection" toggle is on, so we can A/B against our version.
/// </summary>
public class SsaParserOriginal : ISubtitleParser
{
    private const string SCRIPT_INFO_SECTION = "[Script Info]";
    private const string V4_PLUS_STYLES_SECTION = "[V4+ Styles]";
    private const string V4_STYLES_SECTION = "[V4 Styles]";
    private const string EVENTS_SECTION = "[Events]";
    private const string DIALOGUE_PREFIX = "Dialogue:";
    private const string WRAP_STYLE_PREFIX = "WrapStyle:";

    public List<SubtitleItem> ParseStream(Stream ssaStream, Encoding encoding)
    {
        if (!ssaStream.CanRead || !ssaStream.CanSeek)
        {
            throw new ArgumentException("Subtitle must be seekable and readable");
        }

        ssaStream.Position = 0;
        using var reader = new StreamReader(ssaStream, encoding, true);

        var items = new List<SubtitleItem>();
        var currentSection = string.Empty;
        var ssaFormat = new SsaFormat();
        Dictionary<string, int>? columnIndexes = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("["))
            {
                currentSection = line;
                switch (currentSection)
                {
                    case SCRIPT_INFO_SECTION:
                        ssaFormat.ScriptInfo.Add(line);
                        break;
                    case V4_PLUS_STYLES_SECTION:
                        ssaFormat.Styles.Add(line);
                        break;
                    case V4_STYLES_SECTION:
                        ssaFormat.Styles.Add(line);
                        break;
                    case EVENTS_SECTION:
                        ssaFormat.EventsFormat.Add(line);
                        break;
                }
                continue;
            }

            switch (currentSection)
            {
                case SCRIPT_INFO_SECTION:
                    ssaFormat.ScriptInfo.Add(line);
                    if (line.StartsWith(WRAP_STYLE_PREFIX, StringComparison.OrdinalIgnoreCase))
                    {
                        var wrapStyleValue = line.Substring(WRAP_STYLE_PREFIX.Length).Trim();
                        if (int.TryParse(wrapStyleValue, out int wrapStyleInt))
                        {
                            ssaFormat.WrapStyle = (SsaWrapStyle)wrapStyleInt;
                        }
                    }
                    break;
                case V4_PLUS_STYLES_SECTION:
                    ssaFormat.Styles.Add(line);
                    break;
                case V4_STYLES_SECTION:
                    ssaFormat.Styles.Add(line);
                    break;
                case EVENTS_SECTION:
                    if (line.StartsWith("Format:"))
                    {
                        ssaFormat.EventsFormat.Add(line);
                        var columns = line.Substring(7).Split(',').Select(c => c.Trim()).ToList();
                        columnIndexes = new Dictionary<string, int>();
                        for (var index = 0; index < columns.Count; index++)
                        {
                            columnIndexes[columns[index]] = index;
                        }
                    }
                    else if (line.StartsWith(DIALOGUE_PREFIX) && columnIndexes != null)
                    {
                        var dialogue = ParseDialogueLine(line, columnIndexes, ssaFormat);
                        if (dialogue != null)
                        {
                            dialogue.SsaFormat = ssaFormat;
                            items.Add(dialogue);
                        }
                    }
                    break;
            }
        }

        if (!items.Any())
        {
            throw new ArgumentException("No valid subtitles found in SSA format");
        }

        return items;
    }

    private static List<string> SplitTextByWrapStyle(string text, SsaWrapStyle wrapStyle)
    {
        return wrapStyle switch
        {
            SsaWrapStyle.Smart or SsaWrapStyle.SmartWideLowerLine =>
                text.Split(["\\N"], StringSplitOptions.None).ToList(),
            SsaWrapStyle.EndOfLine =>
                text.Split(["\\N"], StringSplitOptions.None).ToList(),
            SsaWrapStyle.None =>
                Regex.Split(text, @"\\N|\\n").ToList(),
            _ => [text]
        };
    }

    private SubtitleItem? ParseDialogueLine(string line, Dictionary<string, int> columnIndexes, SsaFormat ssaFormat)
    {
        // Find the first 9 commas (the fields before Text) — no layer-omitted fallback.
        var textFieldStart = -1;
        var commaCount = 0;
        for (var index = DIALOGUE_PREFIX.Length; index < line.Length; index++)
        {
            if (line[index] != ',') continue;
            commaCount++;
            if (commaCount != columnIndexes["Text"]) continue;
            textFieldStart = index + 1;
            break;
        }

        if (textFieldStart == -1 || textFieldStart >= line.Length) return null;

        var dialoguePrefix = line.Substring(DIALOGUE_PREFIX.Length, textFieldStart - DIALOGUE_PREFIX.Length - 1);
        var dialogueParts = dialoguePrefix.Split(',');
        if (dialogueParts.Length < columnIndexes["Text"]) return null;

        var startTime = ParseSsaTimecode(dialogueParts[columnIndexes["Start"]].Trim());
        var endTime = ParseSsaTimecode(dialogueParts[columnIndexes["End"]].Trim());
        var text = line.Substring(textFieldStart).Trim();

        if (startTime < 0 || endTime < 0 || string.IsNullOrEmpty(text)) return null;

        var textLines = SplitTextByWrapStyle(text, ssaFormat.WrapStyle);
        var plaintextLines = textLines.Select(OriginalRemoveMarkup).ToList();

        var ssaDialogue = new SsaDialogue
        {
            Marked = dialogueParts[0].Trim(),
            Style = dialogueParts[columnIndexes["Style"]].Trim(),
            MarginL = dialogueParts[columnIndexes["MarginL"]].Trim(),
            MarginR = dialogueParts[columnIndexes["MarginR"]].Trim(),
            MarginV = dialogueParts[columnIndexes["MarginV"]].Trim(),
            Effect = dialogueParts[columnIndexes["Effect"]].Trim()
        };
        if (columnIndexes.ContainsKey("Name"))
        {
            ssaDialogue.Name = dialogueParts[columnIndexes["Name"]].Trim();
        }

        return new SubtitleItem
        {
            StartTime = startTime,
            EndTime = endTime,
            Lines = textLines,
            PlaintextLines = plaintextLines,
            SsaDialogue = ssaDialogue,
            SsaFormat = ssaFormat
        };
    }

    private static int ParseSsaTimecode(string timestamp)
    {
        if (TimeSpan.TryParse(timestamp, out var timeSpan))
        {
            return (int)timeSpan.TotalMilliseconds;
        }
        return -1;
    }

    // Upstream RemoveMarkup — no drawing-tag stripping, no \h handling, no vector detection.
    private static string OriginalRemoveMarkup(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string cleaned = Regex.Replace(input, @"\{.*?\}", string.Empty);
        cleaned = Regex.Replace(cleaned, @"<.*?>", string.Empty);
        cleaned = cleaned.Replace("\\N", " ").Replace("\\n", " ");
        cleaned = cleaned.Replace("\\t", " ").Replace("\t", " ");
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
        return cleaned.Trim();
    }
}
