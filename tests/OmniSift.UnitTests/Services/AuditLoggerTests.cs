using FluentAssertions;
using OmniSift.Api.Services;

namespace OmniSift.UnitTests.Services;

/// <summary>
/// Verifies structural invariants of AuditLogger.
/// Full persistence behavior is covered in integration tests
/// (InMemory EF provider is only available there).
/// </summary>
public sealed class AuditLoggerTests
{
    [Fact]
    public void AuditLogger_ImplementsIAuditLogger()
    {
        typeof(AuditLogger).Should().Implement<IAuditLogger>();
    }

    [Fact]
    public void IAuditLogger_HasLogAsyncMethod()
    {
        var method = typeof(IAuditLogger).GetMethod(nameof(IAuditLogger.LogAsync));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
    }
}
