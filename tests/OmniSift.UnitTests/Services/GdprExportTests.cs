using FluentAssertions;
using OmniSift.Shared.DTOs;

namespace OmniSift.UnitTests.Services;

public sealed class GdprExportTests
{
    [Fact]
    public void TenantExportDto_HasExpectedShape()
    {
        var tenantId = Guid.NewGuid();
        var dto = new TenantExportDto
        {
            TenantId = tenantId,
            DataSources =
            [
                new DataSourceDto { Id = Guid.NewGuid(), SourceType = "pdf", Status = "completed", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            ],
            QueryHistory =
            [
                new QueryHistoryDto { Id = Guid.NewGuid(), QueryText = "hello", CreatedAt = DateTime.UtcNow }
            ],
            AuditLog =
            [
                new AuditLogDto { Id = Guid.NewGuid(), TenantId = tenantId, Action = "upload", ResourceType = "data_source", CreatedAt = DateTime.UtcNow }
            ]
        };

        dto.TenantId.Should().Be(tenantId);
        dto.DataSources.Should().HaveCount(1);
        dto.QueryHistory.Should().HaveCount(1);
        dto.AuditLog.Should().HaveCount(1);
        dto.AuditLog[0].Action.Should().Be("upload");
    }
}
