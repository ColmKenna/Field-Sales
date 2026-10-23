using FieldSales.Web.Coverage;
using Microsoft.AspNetCore.DataProtection;

namespace FieldSales.Web.Tests;

// WI-031 approved scenarios 4/14: a confirmation carries the exact actor-bound, expiring review.
[Trait("Category", "Unit")]
public sealed class PermissionRemovalReviewTests
{
    private readonly TestClock _clock = new();
    private readonly EphemeralDataProtectionProvider _keys = new();

    [Theory]
    [InlineData("valid")] [InlineData("missing")] [InlineData("altered")]
    [InlineData("expired")] [InlineData("another-actor")] [InlineData("another-purpose")]
    public void Should_RejectConfirmation_When_ReviewIsMissingAlteredExpiredOrForAnotherActor(string kind)
    {
        var service = new PermissionRemovalReviews(_keys, _clock);
        Guid group = Guid.NewGuid(), lastChange = Guid.NewGuid();
        var review = service.Create("niamh", group, "Pharmacy-only medicines", false,
            [new("colm", "Colm", lastChange)], "  Completed training review  ");
        string? proof = review.Proof;
        string actor = "niamh";
        switch (kind)
        {
            case "missing": proof = null; break;
            case "altered":
                int middle = proof.Length / 2;
                proof = proof[..middle] + (proof[middle] == 'A' ? 'B' : 'A') + proof[(middle + 1)..];
                break;
            case "expired": _clock.Now = _clock.Now.AddMinutes(30); break;
            case "another-actor": actor = "orla"; break;
            case "another-purpose": proof = _keys.CreateProtector("Another feature").Protect("Not a permission review"); break;
        }

        // The existing key ring supports a new service instance reading the same reviewed command.
        var state = new PermissionRemovalReviews(_keys, _clock).Read(proof, actor);
        if (kind != "valid") { Assert.Null(state); return; }
        Assert.NotNull(state);
        Assert.Equal(("niamh", group, "Pharmacy-only medicines", false, "Completed training review"),
            (state.ActorSubject, state.GroupId, state.GroupName, state.Archived, state.Reason));
        Assert.Equal(new PermissionRemovalRep("colm", "Colm", lastChange), Assert.Single(state.Reps));
        Assert.Equal(_clock.Now.AddMinutes(30), state.ExpiresAt);
    }

    [Theory]
    [InlineData(null, true)] [InlineData("   ", true)] [InlineData("  Review expired  ", true)]
    [InlineData("limit", true)] [InlineData("over-limit", false)] [InlineData("Reason\nwith control", false)]
    public void Should_PreserveReasonRules_When_ReviewingAndConfirmingRemoval(string? reason, bool accepted)
    {
        var service = new PermissionRemovalReviews(_keys, _clock);
        reason = reason switch { "limit" => new string('r', 1000), "over-limit" => new string('r', 1001), _ => reason };
        PermissionRemovalReview Create() => service.Create("niamh", Guid.NewGuid(), "Pharmacy-only medicines", false,
            [new("colm", "Colm", Guid.NewGuid())], reason);
        if (!accepted) { Assert.Throws<ArgumentException>(Create); return; }
        var review = Create();
        Assert.Equal(string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), service.Read(review.Proof, "niamh")!.Reason);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(2, true)]
    public void Should_ShowOneConsequenceWithRepCount_When_ReviewingACohort(int count, bool archived)
    {
        var service = new PermissionRemovalReviews(_keys, _clock);
        PermissionRemovalRep[] reps = [new("colm", "Colm", Guid.NewGuid()), new("aoife", "Aoife", Guid.NewGuid())];
        var review = service.Create("niamh", Guid.NewGuid(), "Pharmacy-only medicines", archived, reps[..count], null, isCohort: true);
        var state = service.Read(review.Proof, "niamh")!;
        Assert.True(state.IsCohort);
        Assert.Equal(reps[..count], state.Reps);
        Assert.Contains($"{count} {(count == 1 ? "rep" : "reps")}", review.Consequence);
        Assert.Contains("Any lines already on their unsent orders will still send.", review.Consequence);
        if (archived)
        {
            Assert.Contains("already grants no access", review.Consequence);
            Assert.DoesNotContain("will lose", review.Consequence);
        }
        else Assert.Equal($"{count} {(count == 1 ? "rep" : "reps")} will lose Pharmacy-only medicines at their next Sync. Any lines already on their unsent orders will still send.", review.Consequence);
    }

    [Theory]
    [InlineData("empty")] [InlineData("duplicate")] [InlineData("self")] [InlineData("no-change-id")]
    [InlineData("multiple-in-single-review")]
    public void Should_RejectTheWholeRemoval_When_SelectionIsEmptyDuplicateStaleOrNotHeld(string kind)
    {
        PermissionRemovalRep colm = new("colm", "Colm", Guid.NewGuid());
        PermissionRemovalRep[] reps = kind switch
        {
            "empty" => [], "duplicate" => [colm, colm], "self" => [colm with { Subject = "niamh" }],
            "no-change-id" => [colm with { ExpectedLastChangeId = Guid.Empty }],
            _ => [colm, new("aoife", "Aoife", Guid.NewGuid())]
        };
        var service = new PermissionRemovalReviews(_keys, _clock);
        Assert.Throws<ArgumentException>(() => service.Create("niamh", Guid.NewGuid(), "Pharmacy-only medicines", false,
            reps, null, isCohort: kind != "multiple-in-single-review"));
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
