using FieldSales.Identity.Services.AuditLogs;

namespace FieldSales.Identity.Admin.Tests.AuditLogs;

[Trait("Category", "Unit")]

public class AuditOperationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_WriteOnceAndPreserveOriginalException_When_OperationFails(bool loaded)
    {
        var writer = new RecordingWriter();
        var failure = new InvalidOperationException("private exception text");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => AuditOperation.RunAsync<int>(
            writer, AuditCategory.Client, AuditAction.Update, "id", "id", operation =>
            {
                if (loaded) operation.TargetName = "Loaded client";
                throw failure;
            }, default));
        Assert.Same(failure, actual);
        var entry = Assert.Single(writer.Events);
        Assert.Equal(loaded ? "Loaded client" : "id", entry.TargetName);
        Assert.Equal(AuditOutcome.Failed, entry.Outcome);
        Assert.DoesNotContain("private", entry.Details);
    }

    [Fact]
    public async Task Should_ProduceOneEventForEachInvocation_When_ExceptionIsReused()
    {
        var writer = new RecordingWriter();
        var failure = new InvalidOperationException();
        for (int i = 0; i < 2; i++)
            await Assert.ThrowsAsync<InvalidOperationException>(() => AuditOperation.RunAsync<int>(
                writer, AuditCategory.Client, AuditAction.Update, "id", "name", _ => throw failure, default));
        Assert.Equal(2, writer.Events.Count);
        Assert.Empty(failure.Data);
    }

    [Fact]
    public async Task Should_PreserveCancellation_When_WriterFails()
    {
        var writer = new RecordingWriter { Throw = true };
        using var source = new CancellationTokenSource();
        source.Cancel();
        var failure = new OperationCanceledException(source.Token);
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => AuditOperation.RunAsync<int>(
            writer, AuditCategory.Client, AuditAction.Update, "id", "name", _ => throw failure, source.Token));
        Assert.Same(failure, actual);
        Assert.Single(writer.Events);
    }

    private sealed class RecordingWriter : IAuditWriter
    {
        public List<AdminAuditEvent> Events { get; } = [];
        public bool Throw { get; init; }
        public Task WriteAsync(AdminAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            if (Throw) throw new InvalidOperationException("writer unavailable");
            return Task.CompletedTask;
        }
    }
}
