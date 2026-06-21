// ============================================================
// Unit Tests — ResearchReportBuilder
// Verifies sections, citations rendered, and edge cases.
// ============================================================

using FluentAssertions;
using OmniSift.Api.Services;
using OmniSift.Shared.DTOs;

namespace OmniSift.UnitTests.Services;

public sealed class ResearchReportBuilderTests
{
    private static readonly DateTime FixedNow =
        new(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);

    // ── Title block ──────────────────────────────────────────

    [Fact]
    public void Build_DefaultTitle_IncludesResearchReportHeading()
    {
        var req = new ReportRequest { Messages = [] };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("# Research Report");
    }

    [Fact]
    public void Build_CustomTitle_IncludesCustomHeading()
    {
        var req = new ReportRequest { Title = "My Study", Messages = [] };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("# My Study");
    }

    [Fact]
    public void Build_AlwaysIncludesTimestamp()
    {
        var req = new ReportRequest { Messages = [] };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("2026-06-21T12:00:00Z");
    }

    [Fact]
    public void Build_ExplicitTimestampOverridesUtcNow()
    {
        var req = new ReportRequest
        {
            Timestamp = "2025-01-01T00:00:00Z",
            Messages = []
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("2025-01-01T00:00:00Z");
        md.Should().NotContain("2026-06-21");
    }

    // ── Sections ─────────────────────────────────────────────

    [Fact]
    public void Build_UserAndAssistantTurn_RendersQuerySection()
    {
        var req = new ReportRequest
        {
            Messages =
            [
                new ReportMessage { Role = "user", Content = "What is the capital of France?" },
                new ReportMessage { Role = "assistant", Content = "Paris." }
            ]
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("## Query 1");
        md.Should().Contain("What is the capital of France?");
    }

    [Fact]
    public void Build_AssistantTurn_RendersAnswerSection()
    {
        var req = new ReportRequest
        {
            Messages =
            [
                new ReportMessage { Role = "assistant", Content = "Paris is the capital." }
            ]
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("### Answer");
        md.Should().Contain("Paris is the capital.");
    }

    [Fact]
    public void Build_MultiTurnConversation_NumbersQueriesSequentially()
    {
        var req = new ReportRequest
        {
            Messages =
            [
                new ReportMessage { Role = "user", Content = "First question" },
                new ReportMessage { Role = "assistant", Content = "First answer" },
                new ReportMessage { Role = "user", Content = "Second question" },
                new ReportMessage { Role = "assistant", Content = "Second answer" }
            ]
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("## Query 1");
        md.Should().Contain("## Query 2");
    }

    // ── Citations ────────────────────────────────────────────

    [Fact]
    public void Build_AssistantTurnWithWebCitation_RendersInlineCitation()
    {
        var req = new ReportRequest
        {
            Messages =
            [
                new ReportMessage
                {
                    Role = "assistant",
                    Content = "Paris is the capital.",
                    Citations =
                    [
                        new SourceCitation
                        {
                            Type = "web",
                            Title = "France Wikipedia",
                            Url = "https://en.wikipedia.org/wiki/France",
                            RelevanceScore = 0.92
                        }
                    ]
                }
            ]
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("[1]");
        md.Should().Contain("France Wikipedia");
        md.Should().Contain("https://en.wikipedia.org/wiki/France");
        md.Should().Contain("0.920");
    }

    [Fact]
    public void Build_MultipleTurnsWithCitations_GlobalBibliographyIncludesAll()
    {
        var req = new ReportRequest
        {
            Messages =
            [
                new ReportMessage
                {
                    Role = "assistant",
                    Content = "Answer 1",
                    Citations =
                    [
                        new SourceCitation { Type = "web", Title = "Source A", Url = "https://a.com" }
                    ]
                },
                new ReportMessage
                {
                    Role = "assistant",
                    Content = "Answer 2",
                    Citations =
                    [
                        new SourceCitation { Type = "web", Title = "Source B", Url = "https://b.com" }
                    ]
                }
            ]
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("## Sources Bibliography");
        md.Should().Contain("Source A");
        md.Should().Contain("Source B");
    }

    [Fact]
    public void Build_SameCitationInTwoTurns_AppearsOnceInBibliography()
    {
        var sharedCitation = new SourceCitation
        {
            Type = "web",
            Title = "Shared Source",
            Url = "https://shared.com"
        };

        var req = new ReportRequest
        {
            Messages =
            [
                new ReportMessage
                {
                    Role = "assistant",
                    Content = "Answer 1",
                    Citations = [sharedCitation]
                },
                new ReportMessage
                {
                    Role = "assistant",
                    Content = "Answer 2",
                    Citations = [sharedCitation]
                }
            ]
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        // Should only have one "[1]" entry in bibliography, not two
        var bibliographySection = md[(md.IndexOf("## Sources Bibliography", StringComparison.Ordinal))..];
        var occurrences = CountOccurrences(bibliographySection, "[1]");
        occurrences.Should().Be(1);
    }

    [Fact]
    public void Build_DocumentCitationWithSnippet_SnippetInBibliography()
    {
        var req = new ReportRequest
        {
            Messages =
            [
                new ReportMessage
                {
                    Role = "assistant",
                    Content = "Found in doc.",
                    Citations =
                    [
                        new SourceCitation
                        {
                            Type = "document",
                            DataSourceId = Guid.NewGuid(),
                            ChunkId = Guid.NewGuid(),
                            Title = "report.pdf",
                            RelevanceScore = 0.85,
                            Snippet = "This is an excerpt."
                        }
                    ]
                }
            ]
        };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("This is an excerpt.");
    }

    // ── Empty messages ───────────────────────────────────────

    [Fact]
    public void Build_NoMessages_ReturnsPlaceholderText()
    {
        var req = new ReportRequest { Messages = [] };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("No assistant responses to report.");
    }

    // ── Separator / structure ────────────────────────────────

    [Fact]
    public void Build_Report_ContainsHorizontalRuleSeparator()
    {
        var req = new ReportRequest { Messages = [] };
        var md = ResearchReportBuilder.Build(req, FixedNow);

        md.Should().Contain("---");
    }

    // ── Null guard ───────────────────────────────────────────

    [Fact]
    public void Build_NullRequest_Throws()
    {
        var act = () => ResearchReportBuilder.Build(null!, FixedNow);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Idempotency ──────────────────────────────────────────

    [Fact]
    public void Build_CalledTwiceWithSameInput_ProducesSameOutput()
    {
        var req = new ReportRequest
        {
            Title = "Test",
            Timestamp = "2026-01-01T00:00:00Z",
            Messages =
            [
                new ReportMessage { Role = "user", Content = "Question?" },
                new ReportMessage
                {
                    Role = "assistant",
                    Content = "Answer.",
                    Citations = [new SourceCitation { Type = "web", Title = "X", Url = "https://x.com" }]
                }
            ]
        };

        ResearchReportBuilder.Build(req, FixedNow)
            .Should().Be(ResearchReportBuilder.Build(req, FixedNow));
    }

    // ── helpers ──────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
