namespace Lingarr.Server.Interfaces.Services;

public interface ISubtitleFormatterService
{
    /// <summary>
    /// Removes SSA/ASS and HTML-style markup from a subtitle line, and cleans special characters.
    /// </summary>
    /// <param name="input">The subtitle line with potential markup.</param>
    /// <param name="skipKaraokeDetection">When true, bypass the vector-prefix karaoke filter.</param>
    /// <returns>The cleaned subtitle text without markup.</returns>
    static abstract string RemoveMarkup(string input, bool skipKaraokeDetection = false);
}