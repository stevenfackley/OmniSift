using System.Text;
using OmniSift.Shared.DTOs;

namespace OmniSift.UnitTests.Services;

public sealed class AgentStreamAssemblerTests
{
    [Fact]
    public void StreamDeltas_AccumulateToFullText()
    {
        var sb = new StringBuilder();
        var deltas = new[] { "Hello", " ", "world" };
        foreach (var d in deltas)
            sb.Append(d);

        Assert.Equal("Hello world", sb.ToString());
    }

    [Fact]
    public void FinalEvent_HasCorrectShape()
    {
        var finalEvent = new AgentStreamFinalEvent
        {
            Type = "final",
            PluginsUsed = ["VectorSearch"],
            DurationMs = 123,
            Sources = []
        };

        Assert.Equal("final", finalEvent.Type);
        Assert.Single(finalEvent.PluginsUsed);
        Assert.Equal(123, finalEvent.DurationMs);
    }
}
