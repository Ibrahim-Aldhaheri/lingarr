using Lingarr.Server.Services.Subtitle;
using Xunit;

namespace Lingarr.Server.Tests.Services.Subtitle;

/// <summary>
/// Tests for <see cref="SubtitleFormatterService.SanitizeLlmResponse"/> — defends
/// the writer from raw newlines and context-prompt scaffold leaking out of the LLM.
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
    public void SanitizeLlmResponse_CrLf_Normalized()
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
    public void SanitizeLlmResponse_ScaffoldEchoBeforeAnswer_DropsScaffoldKeepsAnswer()
    {
        // Model echoed the context-prompt scaffold and then produced the real answer.
        // Everything up to and including [/CONTEXT] must be discarded.
        var raw = "[TARGET] hello\n[CONTEXT]\nprev line\n>>> hello <<<\nnext line\n[/CONTEXT]\nمرحبا.";
        Assert.Equal("مرحبا.", SubtitleFormatterService.SanitizeLlmResponse(raw));
    }

    [Fact]
    public void SanitizeLlmResponse_ScaffoldWithoutClosingMarker_StripsEchoedLines()
    {
        // When there is no [/CONTEXT] anchor we still drop the marker lines we
        // recognise and keep the rest joined with spaces.
        var raw = "[TARGET] foo\n[CONTEXT]\n>>> foo <<<\nترجمة.";
        Assert.Equal("ترجمة.", SubtitleFormatterService.SanitizeLlmResponse(raw));
    }

    [Fact]
    public void SanitizeLlmResponse_MultiLineAnswer_JoinedWithSpaces()
    {
        // After stripping scaffold, remaining content lines are joined with a
        // single space (ASS text fields cannot hold raw newlines).
        var raw = "[/CONTEXT]\nسطر أول\nسطر ثان";
        Assert.Equal("سطر أول سطر ثان", SubtitleFormatterService.SanitizeLlmResponse(raw));
    }

    [Fact]
    public void SanitizeLlmResponse_MultipleConsecutiveSpaces_Collapsed()
    {
        Assert.Equal("a b", SubtitleFormatterService.SanitizeLlmResponse("a    b"));
    }

    [Fact]
    public void SanitizeLlmResponse_ArrowMarkerOnOwnLine_Dropped()
    {
        // Recognise the ">>> … <<<" pattern even without the surrounding scaffold.
        Assert.Equal("النص.", SubtitleFormatterService.SanitizeLlmResponse(">>> x <<<\nالنص."));
    }
}
