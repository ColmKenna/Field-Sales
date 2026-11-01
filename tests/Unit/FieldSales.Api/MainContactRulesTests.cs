using FieldSales.Api.Directory;
using FieldSales.Directory.Contracts;

namespace FieldSales.Api.Tests;

/// <summary>Main Contact rules on the Location and Contact entities (WI-018, WI-020), without the database.
/// The SQL-backed Web contact tests still cover the database guard, rowversion locking and the page flows.</summary>
[Trait("Category", "Unit")]
public sealed class MainContactRulesTests
{
    private static readonly ContactType Owner = ContactType.Create("Owner", null);

    [Fact]
    public void Should_MakeTheFirstActiveContactMain_When_ItIsLinkedToALocationWithoutOne()
    {
        Location shop = Shop();

        Contact mary = Person("Mary", shop);

        Assert.Equal(mary.Id, shop.MainContactId);
        Assert.True(Assert.Single(mary.Locations).IsMain);
    }

    [Fact]
    public void Should_KeepTheExistingMain_When_AnotherContactIsLinked()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);

        Contact john = Person("John", shop);

        Assert.Equal(mary.Id, shop.MainContactId);
        Assert.False(Assert.Single(john.Locations).IsMain);
        Assert.Equal(2, shop.Contacts.Count);
    }

    [Fact]
    public void Should_NotLinkTwice_When_TheContactIsAlreadyLinked()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);

        Assert.False(shop.LinkContact(mary));
        Assert.Single(shop.Contacts);
        Assert.Single(mary.Locations);
    }

    [Fact]
    public void Should_RefuseTheLink_When_TheContactIsInactive()
    {
        Location first = Shop(), second = Shop();
        Person("Mary", first);
        Contact john = Person("John", first);
        john.SetStatus(ContactStatus.Inactive);

        var error = Assert.Throws<CustomerDirectoryValidationException>(() => second.LinkContact(john));

        Assert.Equal("ContactId", error.Field);
        Assert.Empty(second.Contacts);
    }

    [Fact]
    public void Should_RefuseTheChange_When_TheMainChangedSinceTheScreenWasLoaded()
    {
        Location shop = Shop();
        Person("Mary", shop);
        Contact john = Person("John", shop);

        Assert.Throws<MainContactChangedException>(() => shop.SetMain(john, expectedMainContactId: Guid.NewGuid(), confirmReplacement: true));
        Assert.Throws<MainContactChangedException>(() => shop.SetMain(john, expectedMainContactId: null, confirmReplacement: true));
    }

    [Fact]
    public void Should_AskForConfirmation_When_ReplacingAnExistingMain()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);
        Contact john = Person("John", shop);

        var error = Assert.Throws<MainContactReplacementRequiredException>(() => shop.SetMain(john, mary.Id, confirmReplacement: false));

        Assert.Equal(mary.Id, error.OutgoingContactId);
        Assert.Equal(mary.Id, shop.MainContactId);
    }

    [Fact]
    public void Should_ReplaceTheMainAndClearTheRetiredMain_When_TheReplacementIsConfirmed()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);
        Contact john = Person("John", shop);
        shop.RetireMain(mary);

        shop.SetMain(john, expectedMainContactId: null, confirmReplacement: false);

        Assert.Equal(john.Id, shop.MainContactId);
        Assert.Null(shop.RetiredMainContactId);
        Assert.False(shop.MainContactReplacementNeeded);
    }

    [Fact]
    public void Should_AllowSettingTheCurrentMainAgain_When_NoReplacementIsConfirmed()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);

        shop.SetMain(mary, mary.Id, confirmReplacement: false);

        Assert.Equal(mary.Id, shop.MainContactId);
    }

    [Fact]
    public void Should_RefuseTheMain_When_TheContactIsNotLinkedOrIsInactive()
    {
        Location shop = Shop(), elsewhere = Shop();
        Contact mary = Person("Mary", shop);
        Contact stranger = Person("Aoife", elsewhere);
        Contact john = Person("John", shop);
        john.SetStatus(ContactStatus.Inactive);

        Assert.Equal("ContactId", Assert.Throws<CustomerDirectoryValidationException>(
            () => shop.SetMain(stranger, mary.Id, confirmReplacement: true)).Field);
        Assert.Equal("ContactId", Assert.Throws<CustomerDirectoryValidationException>(
            () => shop.SetMain(john, mary.Id, confirmReplacement: true)).Field);
        Assert.Equal(mary.Id, shop.MainContactId);
    }

    [Fact]
    public void Should_RecordTheRetiredMainAndLeaveNoMain_When_TheMainIsRetired()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);

        shop.RetireMain(mary);

        Assert.Null(shop.MainContactId);
        Assert.Equal(mary.Id, shop.RetiredMainContactId);
        Assert.True(shop.MainContactReplacementNeeded);
    }

    [Fact]
    public void Should_NotPromoteANewlyLinkedContact_When_AReplacementIsStillNeeded()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);
        shop.RetireMain(mary);

        Contact john = Person("John", shop);

        Assert.Null(shop.MainContactId);
        Assert.False(Assert.Single(john.Locations).IsMain);
    }

    [Fact]
    public void Should_IgnoreTheRetirement_When_TheContactIsNotMain()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);
        Contact john = Person("John", shop);

        shop.RetireMain(john);

        Assert.Equal(mary.Id, shop.MainContactId);
        Assert.Null(shop.RetiredMainContactId);
    }

    [Fact]
    public void Should_RefuseToUnlink_When_TheContactIsTheMainOrTheRetiredMain()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);
        Contact john = Person("John", shop);

        Assert.Equal("ReplacementContactId", Assert.Throws<CustomerDirectoryValidationException>(() => shop.UnlinkContact(mary)).Field);
        shop.SetMain(john, mary.Id, confirmReplacement: true);
        shop.RetireMain(john);
        Assert.Equal("ReplacementContactId", Assert.Throws<CustomerDirectoryValidationException>(() => shop.UnlinkContact(john)).Field);
        Assert.Equal(2, shop.Contacts.Count);
    }

    [Fact]
    public void Should_RemoveTheLinkFromBothSides_When_AnotherContactIsUnlinked()
    {
        Location shop = Shop();
        Contact mary = Person("Mary", shop);
        Contact john = Person("John", shop);

        LocationContact removed = shop.UnlinkContact(john);

        Assert.Equal((shop.Id, john.Id), (removed.LocationId, removed.ContactId));
        Assert.Equal(mary.Id, Assert.Single(shop.Contacts).ContactId);
        Assert.Empty(john.Locations);
    }

    [Fact]
    public void Should_RefuseToDeactivate_When_TheContactIsMainAtAnyLinkedLocation()
    {
        Location first = Shop(), second = Shop();
        Person("John", first);
        Contact mary = Contact.Create("Mary", Owner, null, null, [first, second]);
        Assert.Equal(mary.Id, second.MainContactId);

        var error = Assert.Throws<CustomerDirectoryValidationException>(() => mary.SetStatus(ContactStatus.Inactive));

        Assert.Equal("Status", error.Field);
        Assert.Equal(ContactStatus.Active, mary.Status);
    }

    [Fact]
    public void Should_RefuseAnUnknownStatus()
    {
        Contact mary = Person("Mary", Shop());

        Assert.Equal("Status", Assert.Throws<CustomerDirectoryValidationException>(() => mary.SetStatus((ContactStatus)99)).Field);
    }

    [Fact]
    public void Should_LinkTheContactOnceToEachLocation_When_ALocationIsRepeatedOnCreate()
    {
        Location shop = Shop();

        Contact mary = Contact.Create("Mary", Owner, null, null, [shop, shop]);

        Assert.Single(mary.Locations);
        Assert.Single(shop.Contacts);
    }

    [Fact]
    public void Should_RefuseTheContact_When_NoLocationIsChosen()
    {
        Assert.Equal("LocationIds", Assert.Throws<CustomerDirectoryValidationException>(
            () => Contact.Create("Mary", Owner, null, null, [])).Field);
    }

    private static Location Shop() => Location.Create(Guid.NewGuid(), "Hickey's Pharmacy", Guid.NewGuid(), null);

    private static Contact Person(string name, Location location) => Contact.Create(name, Owner, null, null, [location]);
}
