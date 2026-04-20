using Lingarr.Server.Services.Subtitle;
using Xunit;

namespace Lingarr.Server.Tests.Services.Subtitle;

/// <summary>
/// Tests for <see cref="SubtitleFormatterService.SanitizeLlmResponse"/> —
/// defends the writer from raw newlines and carriage returns in translations
/// (raw newlines in an ASS text field would produce blank physical lines).
/// </summary>
public class SubtitleFormatterServiceSanitizeTests
{
    [Fact]
    public void SanitizeLlmResponse_PlainArabic_ReturnsUnchanged()
    {
        Assert.Equal("مرحبا.", SubtitleFormatterService.SanitizeLlmResponse("مرحبا."));
    }

    [Fact]
    public void SanitizeLlmResponse_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SubtitleFormatterService.SanitizeLlmResponse(""));
    }

    [Fact]
    public void SanitizeLlmResponse_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SubtitleFormatterService.SanitizeLlmResponse(null!));
    }

    [Fact]
    public void SanitizeLlmResponse_TrailingNewline_IsStripped()
    {
        // LLM returns text with trailing \n — would otherwise produce a blank
        // physical line in the output .ass file.
        Assert.Equal("نعم.", SubtitleFormatterService.SanitizeLlmResponse("نعم.\n"));
    }

    [Fact]
    public void SanitizeLlmResponse_CrLf_Normalised()
    {
        Assert.Equal("نعم.", SubtitleFormatterService.SanitizeLlmResponse("نعم.\r\n"));
    }

    [Fact]
    public void SanitizeLlmResponse_InternalNewline_CollapsesToSpace()
    {
        Assert.Equal("سطر أول سطر ثان",
            SubtitleFormatterService.SanitizeLlmResponse("سطر أول\nسطر ثان"));
    }

    [Fact]
    public void SanitizeLlmResponse_MultipleConsecutiveSpaces_Collapsed()
    {
        Assert.Equal("a b", SubtitleFormatterService.SanitizeLlmResponse("a    b"));
    }
}
