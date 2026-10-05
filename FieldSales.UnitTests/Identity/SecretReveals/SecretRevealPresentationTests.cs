using FieldSales.Identity.Presentation;
using FieldSales.Identity.Services.SecretReveals;
using Moq;

namespace FieldSales.Identity.Admin.Tests.SecretReveals;

public class SecretRevealPresentationTests
{
    [Theory]
    [InlineData(null, "target")]
    [InlineData("", "target")]
    [InlineData("handle", "")]
    [InlineData("handle", " ")]
    public async Task MissingHandleOrTargetDoesNotConsume(string? handle, string targetId)
    {
        var service = new Mock<ISecretRevealService>(MockBehavior.Strict);
        Assert.Null(await service.Object.TryRevealAsync(new(SecretRevealPurpose.ClientCreated, targetId), handle, default));
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(SecretRevealPurpose.ClientCreated)]
    [InlineData(SecretRevealPurpose.ClientSecretGenerated)]
    [InlineData(SecretRevealPurpose.ApiResourceSecretGenerated)]
    public async Task PurposeTargetHandleAndCancellationReachTheService(SecretRevealPurpose purpose)
    {
        var service = new Mock<ISecretRevealService>(MockBehavior.Strict);
        var target = new SecretRevealTarget(purpose, "target");
        using var source = new CancellationTokenSource();
        var handle = SecretRevealHandle.Create("handle");
        service.Setup(s => s.IssueAsync(target, "plaintext", source.Token))
            .ReturnsAsync(new SecretRevealTicket(handle, DateTimeOffset.UtcNow));
        service.Setup(s => s.ConsumeAsync(target, handle, source.Token))
            .ReturnsAsync(SecretRevealConsumeResult.Revealed("plaintext"));
        Assert.Equal("handle", await service.Object.IssueHandleAsync(target, "plaintext", source.Token));
        Assert.Equal("plaintext", await service.Object.TryRevealAsync(target, "handle", source.Token));
        service.VerifyAll();
    }
}
