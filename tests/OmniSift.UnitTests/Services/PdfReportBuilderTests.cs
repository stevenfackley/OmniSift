using System.Text;
using OmniSift.Api.Services;

namespace OmniSift.UnitTests.Services;

public sealed class PdfReportBuilderTests
{
    [Fact]
    public void Build_ReturnsNonEmptyPdf()
    {
        var request = new ReportRequest
        {
            Title = "Test Report",
            Messages =
            [
                new ReportMessage { Role = "user", Content = "What is X?" },
                new ReportMessage { Role = "assistant", Content = "X is Y.", Citations = [] }
            ]
        };

        var pdf = PdfReportBuilder.Build(request, DateTime.UtcNow);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 100);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Build_EmptyMessages_ReturnsValidPdf()
    {
        var request = new ReportRequest { Title = "Empty Report", Messages = [] };

        var pdf = PdfReportBuilder.Build(request);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 100);
    }
}
