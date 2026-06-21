using FluentAssertions;
using OmniSift.Web.Services;

namespace OmniSift.UnitTests.Web;

public sealed class SseParserTests
{
    [Fact]
    public void ParseLine_Delta_ReturnsDeltaEvent()
    {
        var line = """data: {"type":"delta","content":"Hello"}""";
        var result = SseParser.ParseLine(line);

        result.Should().BeOfType<SseEvent.Delta>();
        ((SseEvent.Delta)result!).Content.Should().Be("Hello");
    }

    [Fact]
    public void ParseLine_Final_ReturnsFinalEvent()
    {
        var line = """data: {"type":"final","pluginsUsed":["VectorSearch"],"durationMs":42,"sources":[]}""";
        var result = SseParser.ParseLine(line);

        result.Should().BeOfType<SseEvent.Final>();
        var final = (SseEvent.Final)result!;
        final.Data.DurationMs.Should().Be(42);
        final.Data.PluginsUsed.Should().ContainSingle().Which.Should().Be("VectorSearch");
        final.Data.Sources.Should().BeEmpty();
    }

    [Fact]
    public void ParseLine_EmptyDataLine_ReturnsNull()
    {
        var result = SseParser.ParseLine("data: ");
        result.Should().BeNull();
    }

    [Fact]
    public void ParseLine_NonDataLine_ReturnsNull()
    {
        var result = SseParser.ParseLine("event: message");
        result.Should().BeNull();
    }

    [Fact]
    public void ParseLine_BlankLine_ReturnsNull()
    {
        var result = SseParser.ParseLine(string.Empty);
        result.Should().BeNull();
    }

    [Fact]
    public void ParseLine_UnknownType_ReturnsNull()
    {
        var result = SseParser.ParseLine("""data: {"type":"ping"}""");
        result.Should().BeNull();
    }

    [Fact]
    public void ParseLine_MalformedJson_ReturnsNull()
    {
        var result = SseParser.ParseLine("data: {not valid json}");
        result.Should().BeNull();
    }

    [Fact]
    public void ParseLine_DeltaEmptyContent_ReturnsEmptyString()
    {
        var result = SseParser.ParseLine("""data: {"type":"delta","content":""}""");
        result.Should().BeOfType<SseEvent.Delta>();
        ((SseEvent.Delta)result!).Content.Should().BeEmpty();
    }

    [Fact]
    public void ParseLine_DeltaMissingContent_ReturnsEmptyString()
    {
        var result = SseParser.ParseLine("""data: {"type":"delta"}""");
        result.Should().BeOfType<SseEvent.Delta>();
        ((SseEvent.Delta)result!).Content.Should().BeEmpty();
    }

    [Fact]
    public void ParseLine_FinalWithSources_DeserializesCorrectly()
    {
        var line = """data: {"type":"final","pluginsUsed":[],"durationMs":100,"sources":[{"type":"document","title":"Test Doc"}]}""";
        var result = SseParser.ParseLine(line);

        result.Should().BeOfType<SseEvent.Final>();
        var final = (SseEvent.Final)result!;
        final.Data.Sources.Should().ContainSingle()
            .Which.Title.Should().Be("Test Doc");
    }

    [Fact]
    public void MultipleDeltas_AccumulateToFullText()
    {
        var lines = new[]
        {
            """data: {"type":"delta","content":"Hello"}""",
            """data: {"type":"delta","content":", "}""",
            """data: {"type":"delta","content":"world"}"""
        };

        var sb = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (SseParser.ParseLine(line) is SseEvent.Delta d)
                sb.Append(d.Content);
        }

        sb.ToString().Should().Be("Hello, world");
    }
}
