namespace Lingarr.Server.Interfaces.Services;

public interface ISubtitleFormatterService
{
    /// <summary>
    /// Removes SSA/ASS and HTML-style markup from a subtitle line and cleans
    /// special characters. When the karaoke filter is active (the caller using
    /// this service path has opted in), ASS drawing-mode blocks and bare vector
    /// prefixes are collapsed to the empty string so they skip translation.
    /// </summary>
    /// <param name="input">The subtitle line with potential markup.</param>
    /// <returns>The cleaned subtitle text without markup.</returns>
    static abstract string RemoveMarkup(string input);
}
