using FieldSales.Pricing;

namespace FieldSales.Pricing.Tests;

/// <summary>WI-058 agreed scenarios 1–18 (T-6.4.1; Pricing US-003 S1, S4; US-004 S1, S2, S4; US-005 S2–S4): the price engine's fixed
/// test set, drawn from the stories' worked examples and the handover's example data. Scenario 19 (performance) is its own file.</summary>
[Trait("Category", "Unit")]
public sealed class PriceResolverTests
{
    private static readonly DateOnly Day = new(2026, 9, 22);
    private static readonly DateOnly LongAgo = new(2026, 1, 1);

    private static PricedProduct Product(string unit, decimal list, params QuantityBreak[] breaks) =>
        new(Guid.NewGuid(), unit, [new(LongAgo, list)], breaks);

    // Handover example data: SPF30 Sun Lotion 200ml lists at €12.50; break at 24+ €10.08.
    private static readonly PricedProduct Spf30 = Product("Each", 12.50m, new QuantityBreak(24m, 10.08m));
    private static readonly PricedProduct HandCream = Product("Each", 4.40m);
    // US-004 S1 and S2.
    private static readonly PricedProduct Lozenges = Product("Each", 2.20m, new(10m, 2.00m), new(30m, 1.75m));
    private static readonly PricedProduct HerbalTea = Product("kg", 4.80m, new(10m, 4.00m), new(20m, 3.60m));

    private static PriceListTier Listing(string name, params (PricedProduct Product, decimal Price)[] prices) =>
        new(name, prices.ToDictionary(price => price.Product.Id, price => price.Price));

    private static ResolvedPrice Resolve(PricedProduct product, decimal quantity, params PriceTier[] tiers) =>
        PriceResolver.Resolve(product, tiers, quantity, Day)!;

    private static (string Kind, string Label, decimal Amount)[] All(ResolvedPrice price) =>
        [.. price.Candidates.Select(candidate => (candidate.Kind, candidate.Label, candidate.Amount))];

    // Scenario 1 (US-005 S2).
    [Fact]
    public void Should_ShowTierBAt1120WithList1250_When_TierBPricesSpf30At1120AndNoPromotion()
    {
        ResolvedPrice price = Resolve(Spf30, 1m, Listing("Tier B", (Spf30, 11.20m)));

        Assert.Equal("€11.20 — Tier B (list €12.50)", price.Summary);
        Assert.Equal((PriceKinds.Tier, 11.20m), (price.Winner.Kind, price.Winner.Amount));
        Assert.Null(price.RunnerUpTier);
    }

    // Scenario 2 (US-005 S3): a tier the customer holds that doesn't price this product changes nothing.
    [Fact]
    public void Should_ShowListPriceWithNoRunnerUp_When_NoTierOrBreakApplies()
    {
        ResolvedPrice price = Resolve(Spf30, 1m, Listing("Tier B", (HandCream, 4.00m)));

        Assert.Equal("€12.50 — list price", price.Summary);
        Assert.Equal([(PriceKinds.List, "List price", 12.50m)], All(price));
        Assert.Null(price.RunnerUpTier);
    }

    // Scenario 3 (US-003 S1): Tier B is 10% off; Suncare Deal 2027 lists SPF30 at €10.50 and nothing else.
    [Fact]
    public void Should_TakeTheBestOfBothTiers_When_TheCustomerHoldsTierBAndSuncareDeal2027()
    {
        PriceTier[] held = [new PercentageTier("Tier B", 10m), Listing("Suncare Deal 2027", (Spf30, 10.50m))];

        ResolvedPrice spf30 = Resolve(Spf30, 1m, held);
        ResolvedPrice handCream = Resolve(HandCream, 1m, held);

        Assert.Equal(("Suncare Deal 2027", 10.50m), (spf30.Winner.Label, spf30.Winner.Amount));
        Assert.Contains((PriceKinds.Tier, "Tier B", 11.25m), All(spf30)); // Considered, and beaten.
        Assert.Equal(("Tier B", 3.96m), (handCream.Winner.Label, handCream.Winner.Amount));
    }

    // Scenario 4 (US-003 S4).
    [Fact]
    public void Should_PriceAtListPlusAnyBreak_When_TheCustomerHasNoTier()
    {
        Assert.Equal("€2.20 — list price", Resolve(Lozenges, 1m).Summary);
        Assert.Equal("€2.00 — Break: 10+ (list €2.20)", Resolve(Lozenges, 12m).Summary);
    }

    // Scenario 5 (US-004 S1).
    [Fact]
    public void Should_Price12At200And30At175_When_ThroatLozengesHasBreaksFrom10And30()
    {
        Assert.Equal((PriceKinds.List, 2.20m), (Resolve(Lozenges, 9m).Winner.Kind, Resolve(Lozenges, 9m).Winner.Amount));
        Assert.Equal(("Break: 10+", 2.00m), (Resolve(Lozenges, 12m).Winner.Label, Resolve(Lozenges, 12m).Winner.Amount));
        Assert.Equal(("Break: 30+", 1.75m), (Resolve(Lozenges, 30m).Winner.Label, Resolve(Lozenges, 30m).Winner.Amount));
    }

    // Scenario 6 (US-004 S2): per kg, at decimal quantities.
    [Fact]
    public void Should_Price12KgAt400PerKg_When_LooseHerbalTeaHasBreaksFrom10And20Kg()
    {
        Assert.Equal("€4.80 per kg — list price", Resolve(HerbalTea, 9.5m).Summary);
        Assert.Equal("€4.00 per kg — Break: 10 kg+ (list €4.80 per kg)", Resolve(HerbalTea, 12m).Summary);
        Assert.Equal(3.60m, Resolve(HerbalTea, 20m).Winner.Amount);
    }

    // Scenario 7 (US-004 S4).
    [Fact]
    public void Should_ChooseTheTierAt190OverThe200Break_When_TwelveAreOrdered()
    {
        ResolvedPrice price = Resolve(Lozenges, 12m, Listing("Tier B", (Lozenges, 1.90m)));

        Assert.Equal("€1.90 — Tier B (list €2.20)", price.Summary);
        Assert.Contains((PriceKinds.Break, "Break: 10+", 2.00m), All(price));
    }

    // Scenario 8: evaluated at quantity — the winner changes as the quantity crosses the break.
    [Fact]
    public void Should_LetTheTierWinAt23AndTheBreakAt24_When_Spf30HasABreakFrom24()
    {
        PriceListTier tierB = Listing("Tier B", (Spf30, 11.20m));

        Assert.Equal(("Tier B", 11.20m), (Resolve(Spf30, 23m, tierB).Winner.Label, Resolve(Spf30, 23m, tierB).Winner.Amount));
        Assert.Equal(("Break: 24+", 10.08m), (Resolve(Spf30, 24m, tierB).Winner.Label, Resolve(Spf30, 24m, tierB).Winner.Amount));
    }

    // Scenario 9 (D4, US-004 S3): a break priced above the one beneath it never applies.
    [Fact]
    public void Should_NeverApplyTheRisingBreak_When_From30At230SitsAboveFrom10At200()
    {
        PricedProduct rising = Product("Each", 2.20m, new(10m, 2.00m), new(30m, 2.30m));

        ResolvedPrice price = Resolve(rising, 30m);

        Assert.Equal(("Break: 10+", 2.00m), (price.Winner.Label, price.Winner.Amount));
        Assert.DoesNotContain(price.Candidates, candidate => candidate.Amount == 2.30m);
    }

    // Scenario 10 (D1, MI-48): the unit price is rounded to the cent, halves away from zero, as the percentage is applied.
    [Fact]
    public void Should_RoundEachUnitPriceToTheCentHalfUp_When_APercentageTierIsApplied()
    {
        PercentageTier fifteen = new("Tier A", 15m), ten = new("Tier C", 10m);

        Assert.Equal(11.04m, Resolve(Product("Each", 12.99m), 1m, fifteen).Winner.Amount); // 11.0415
        Assert.Equal(1.13m, Resolve(Product("Each", 1.25m), 1m, ten).Winner.Amount); // 1.125, a half, rounds up
        ResolvedPrice perKg = Resolve(Product("kg", 4.80m), 2.5m, fifteen); // 4.08 per kg
        Assert.Equal(4.08m, perKg.Winner.Amount);
        Assert.Equal(10.20m, Money.LineValue(perKg.Winner.Amount, 2.5m));
        Assert.Equal(0.13m, Money.LineValue(0.025m, 5m)); // A line value of 0.125 rounds up too.
    }

    // Scenario 11 (D2): a product's own percentage or fixed price replaces the tier's default.
    [Fact]
    public void Should_UseTheProductOverrideOverTheDefaultPercentage_When_ATierHasOneForThatProduct()
    {
        PercentageTier tierB = new("Tier B", 10m, new Dictionary<Guid, TierOverride>
        {
            [Spf30.Id] = TierOverride.Percent(20m),
            [HandCream.Id] = TierOverride.Fixed(4.00m)
        });

        Assert.Equal(10.00m, Resolve(Spf30, 1m, tierB).Winner.Amount);
        Assert.Equal(4.00m, Resolve(HandCream, 1m, tierB).Winner.Amount);
        Assert.Equal(1.98m, Resolve(Lozenges, 1m, tierB).Winner.Amount); // No override: the default 10%.
    }

    // Scenario 12 (D2): a price list says nothing about a product it doesn't list.
    [Fact]
    public void Should_OfferNoTierPrice_When_APriceListTierDoesNotListTheProduct()
    {
        ResolvedPrice price = Resolve(Spf30, 1m, Listing("Tier B", (HandCream, 4.00m)), new PercentageTier("Tier A", 4m));

        Assert.Equal([(PriceKinds.Tier, "Tier A", 12.00m), (PriceKinds.List, "List price", 12.50m)], All(price));
    }

    // Scenario 13 (D3): equal prices show the tier, then the break, then the list; equal tiers by name.
    [Fact]
    public void Should_ShowTheTierThenTheBreakThenTheList_When_CandidatesAreEqual()
    {
        PricedProduct flat = Product("Each", 2.00m, new QuantityBreak(10m, 2.00m));

        Assert.Equal([PriceKinds.Tier, PriceKinds.Break, PriceKinds.List],
            Resolve(flat, 12m, Listing("Tier B", (flat, 2.00m))).Candidates.Select(candidate => candidate.Kind));
        Assert.Equal(PriceKinds.Break, Resolve(flat, 12m).Winner.Kind);
        Assert.Equal(["Suncare Deal 2027", "Tier B"],
            Resolve(Spf30, 1m, Listing("Tier B", (Spf30, 10.00m)), Listing("Suncare Deal 2027", (Spf30, 10.00m)))
                .Candidates.Where(candidate => candidate.Kind == PriceKinds.Tier).Select(candidate => candidate.Label));
    }

    // Scenario 14: the list price on the day, and a percentage tier that follows it.
    [Fact]
    public void Should_UseTheListPriceInEffectOnTheDate_When_ThePriceChangesTomorrow()
    {
        PricedProduct rising = new(Guid.NewGuid(), "Each", [new(LongAgo, 12.50m), new(Day.AddDays(1), 13.00m)]);
        PriceTier[] tierB = [new PercentageTier("Tier B", 10m)];

        ResolvedPrice today = PriceResolver.Resolve(rising, tierB, 1m, Day)!;
        ResolvedPrice tomorrow = PriceResolver.Resolve(rising, tierB, 1m, Day.AddDays(1))!;

        Assert.Equal((12.50m, 11.25m), (today.ListPrice, today.Winner.Amount));
        Assert.Equal((13.00m, 11.70m), (tomorrow.ListPrice, tomorrow.Winner.Amount));
    }

    // Scenario 15 (D6): the best tier price is named when something else beats it.
    [Fact]
    public void Should_NameTheBestTierAsRunnerUp_When_ABreakBeatsTheTier()
    {
        ResolvedPrice price = Resolve(Spf30, 24m, Listing("Tier A", (Spf30, 11.60m)), Listing("Tier B", (Spf30, 11.20m)));

        Assert.Equal("€10.08 — Break: 24+ (better than your tier price €11.20)", price.Summary);
        Assert.Equal(("Tier B", 11.20m), (price.RunnerUpTier!.Label, price.RunnerUpTier.Amount));
    }

    // Scenario 16 (US-005 S4's data, from the provenance sheet): list €14.00, Symbol Group A 20% off, break from 24 at €12.60, and the
    // Autumn promotion at €9.99.
    [Fact]
    public void Should_ListEveryCandidateCheapestFirstWithTheWinnerMarked_When_ThePriceIsResolved()
    {
        PricedProduct sheet = Product("Each", 14.00m, new QuantityBreak(24m, 12.60m));

        ResolvedPrice price = PriceResolver.Resolve(sheet, [new PercentageTier("Symbol Group A", 20m)], 24m, Day,
            [new PriceCandidate("Promotion", "Autumn promotion", 9.99m)])!;

        Assert.Equal(
            [("Promotion", "Autumn promotion", 9.99m), (PriceKinds.Tier, "Symbol Group A", 11.20m), (PriceKinds.Break, "Break: 24+", 12.60m),
                (PriceKinds.List, "List price", 14.00m)],
            All(price));
        Assert.Same(price.Candidates[0], price.Winner);
    }

    // Scenario 17 (D5): before any list price starts, a percentage has nothing to come off; a fixed tier price still prices it.
    [Fact]
    public void Should_ResolveNoPrice_When_ThereIsNoListPriceOnTheDateAndNoFixedTierPrice()
    {
        PricedProduct notYet = new(Guid.NewGuid(), "Each", [new(Day.AddDays(9), 3.50m)]);

        Assert.Null(PriceResolver.Resolve(notYet, [new PercentageTier("Tier B", 10m)], 1m, Day));
        Assert.Null(PriceResolver.Resolve(notYet, [], 1m, Day));
        ResolvedPrice listed = PriceResolver.Resolve(notYet, [Listing("Tier B", (notYet, 3.00m))], 1m, Day)!;
        Assert.Equal(("€3.00 — Tier B", (decimal?)null), (listed.Summary, listed.ListPrice));
    }

    // Scenario 18 (D8): a later source's candidate competes under the same rule (US-005 S1's shape), and loses a tie to the tier.
    [Fact]
    public void Should_LetAnExtraCandidateWinUnderTheSameRule_When_ALaterSourceSuppliesOne()
    {
        PriceTier[] tierB = [Listing("Tier B", (Spf30, 11.20m))];

        ResolvedPrice promoted = PriceResolver.Resolve(Spf30, tierB, 1m, Day, [new PriceCandidate("Promotion", "Autumn promotion", 9.99m)])!;
        ResolvedPrice tied = PriceResolver.Resolve(Spf30, tierB, 1m, Day, [new PriceCandidate("Promotion", "Autumn promotion", 11.20m)])!;

        Assert.Equal("€9.99 — Autumn promotion (better than your tier price €11.20)", promoted.Summary);
        Assert.Equal("Tier B", tied.Winner.Label);
    }
}
