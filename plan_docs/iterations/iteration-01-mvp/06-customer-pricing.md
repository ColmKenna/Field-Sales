# E6 — Customer pricing on the order line

**Iteration** — 1, MVP: orders from the field reach the warehouse
**Outcome** — Every order line shows the customer's correct price at its quantity, with where it came from, offline — and the tablet and server resolve the same price from the same inputs.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Pricing US-001 — Create a Price Tier | Must | S1–S5 |
| 2 | Pricing US-003 — Assign tiers to a Customer | Must | S1–S4 |
| 3 | Pricing US-004 — Set quantity breaks on a product | Must | S1–S4 |
| 4 | Pricing US-005 — See and understand the price on a line | Must | S1–S6 (promotion candidates in S1 and S4 arrive with E18) |
| 5 | Pricing US-006 — Be prompted when a break is within reach | Should | S1–S5 |

Also delivered here: Rep at a Location US-012 S4b and S4c and US-008 S9, which restate PR-005 and PR-006 on the tablet.

**Exit criterion** — Head office can create percentage and price-list tiers with per-product overrides, give a customer several tiers with a live sample, and set quantity breaks; on the tablet, offline, every Order Pad row and order line shows the lowest applicable price at its quantity with its source, the runner-up tier price and the full candidate list on demand, and a break prompt says in money what a little more would save.

**Capability-class stamp** — Frontier + extended reasoning for the price engine (T-6.4.1); Frontier workhorse for Agent-Assisted tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [pricing.md](../../stories/pricing.md), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§1 The order line and the price provenance sheet), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-07), [02-head-office.md](../../uxdocs/02-head-office.md) (H-20, H-21, H-22), [handover.md](../../uxdocs/handover.md) §11 (consistent example data).

---

### T-6.4.1-S — Test scenarios for the price engine

**Owner** — Human-Led
**Gates** — T-6.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Which worked examples are the oracle? Start from Pricing US-003 S1, S4; US-004 S1, S2, S4; US-005 S2, S3; the handover's example data (SPF30: tier €11.20, break 24+ €10.08; Hand Cream: tier €4.00).
- Rounding (MI-48): percentage tiers to the cent, per line or per unit? Half up? Per-kg prices times decimal quantities?
- Category-level overrides (MI-47): Pricing US-001 S2 says "Suncare products at 15%", but the glossary defines per-product overrides only. Is a category override real, and if so does a product override beat it?
- Ties: two candidates at the same price — which source is shown?
- Evaluated at quantity: at 9 the tier wins, at 10 the break wins — confirm both.
- Is the base price "on the day" resolved in the business time zone (MI-53)?

---

### T-6.4.1 — Resolve the best price for a line at its quantity

**Parent story**

> As a Field Salesperson, I want each line to show the price, where it came from, and what else was considered so that I can answer the customer without guessing.
>
> Acceptance criteria:
> - With no promotion, the line shows "€11.20 — Tier B (list €12.50)" (S2)
> - With no tier or promotion, "€12.50 — list price" with no runner-up (S3)
> - Offline, all resolution happens on the tablet from the morning snapshot, including every tier the customer holds (S5)
> - Holding Tier B and "Suncare Deal 2027", each line takes the best of both (US-003 S1); with no tier, base plus any break (US-003 S4)
> - "Throat Lozenges 36s" €2.20 with breaks 10 → €2.00 and 30 → €1.75: 12 prices €2.00, 30 prices €1.75 (US-004 S1); 12 kg of Loose Herbal Tea prices €4.00 per kg (US-004 S2)
> - A tier price of €1.90 beats the €2.00 break at 12 (US-004 S4)

**Slice** — Given a product, a customer's tiers, a quantity and a date, one price function returns the lowest candidate — base price on that day, each tier, the applicable quantity break — with its source, the runner-up tier price and every candidate considered, identically on tablet and server.
**Spec source** — Pricing US-005 S2, S3, S5; US-003 S1, S4; US-004 S1, S2, S4; glossary (Candidate Price, Best Price Wins, Evaluated at quantity, Winning Source); design decisions "Best for the customer wins; nothing compounds", "Price is resolved at the line's quantity"
**Depends on** — T-1.3.1, T-1.4.1
**Pattern to follow** — novel — see design notes
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — money, run on every surface; rounding and category overrides are open (MI-47, MI-48); Edge-Case Discovery High.

**Provisional commit message**

```
feat(pricing): resolve the best price for a line at its quantity

- One rule — the lowest candidate wins, nothing compounds — so the rep can
  explain any price and head office can add tiers without checking each
- Pure and shared by tablet and server, with the documented examples as
  its fixed test set, so the two can never disagree
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The most consequential rule in the system; slivers must not drift from it.

**Work package**

Increments:
1. Agree the rounding and override decisions (MI-47, MI-48) and write them into the scenario list.
2. Pure function `resolve(product, customerTiers, quantity, date) → { winner, source, runnerUpTier, candidates[] }` with no I/O. Inputs: dated base prices, tiers (percentage with per-product overrides; price list), breaks. Verify against every agreed scenario.
3. Decide how tablet and server share it: one module if the stack allows (decision from T-1.1.1), otherwise two implementations driven by one shared file of test vectors.
4. Extension points for later candidates without changing the signature: promotions (E18) and rep allowances (E19) add to the candidate list or post-process the winner.
5. Performance: resolving every product on a 260-product pad at quantity 1 must not delay the pad on the target tablet.

Decision points:
- Rounding (MI-48) and category-level overrides (MI-47).
- Tie-break order when candidates are equal (the customer pays the same; which source does the rep see?).
- How the future E18 promotions and E19 allowances fit — confirm the output shape carries a "working" list for H-02 and the provenance sheet.

Delegable slivers:
- **Shared test vectors** — Encode the agreed scenario list from T-6.4.1-S as a data file of inputs and expected outputs usable by any test runner. Do not write engine code.
- **Tier candidate generator** — Given the agreed tier model, implement the function that turns a customer's tiers into candidate prices for one product (percentage with per-product overrides; price list falls back to nothing). Pure; no persistence; do not choose the winner.
- **Break candidate generator** — Implement the function that returns the applicable quantity break price for a product at a quantity (highest threshold at or below the quantity), for Each and measure units. Pure; do not choose the winner.

---

### T-6.1.1-S — Test scenarios for percentage tiers

**Owner** — Scenario Review
**Gates** — T-6.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for percentage price tiers with per-product
overrides (task T-6.1.1). Read plan_docs/stories/pricing.md US-001 S1, S2 and
glossary (Price Tier, Percentage tier, Per-Product Overrides). Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover S1 and S2, then derivable edges: 0% and 100% tiers, an override
on an archived product, a base price change flowing through. S2 mentions a
category ("Suncare products at 15%") though the glossary defines per-product
overrides only — mark that "Needs a decision" (MI-47). Write no test code;
change no files.
```

---

### T-6.1.1 — Create a percentage tier with per-product overrides

**Parent story**

> As a Head Office User, I want to set up a tier either as a percentage off list or as a list of agreed prices so that a negotiated deal is recorded once and applied everywhere.
>
> Acceptance criteria:
> - Tier "B" at 8% off base prices every product 8% below its current base for customers holding it, and base changes flow through (S1)
> - Overrides on Tier B — "Suncare" products at 15% and "SPF30 Sun Lotion 200ml" at a fixed €10.50 — apply to those products; everything else uses 8% (S2; the category override depends on MI-47)

**Slice** — A Head Office User creates a percentage tier and adds per-product overrides as a percentage or a fixed price, and the tier's prices follow base price changes automatically.
**Spec source** — Pricing US-001 S1, S2; uxdocs 02 H-20 (H20.1)
**Depends on** — T-6.4.1
**Pattern to follow** — T-1.2.1 (head office record pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — money inputs to the engine; H-20 is a draft; the category override is ambiguous (MI-47).

**Provisional commit message**

```
feat(pricing): create percentage tiers with per-product overrides

- Negotiated terms are recorded once as a percentage that tracks list
  price, with fixed or different-percentage exceptions per product
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Engine inputs built under review.

**Agent prompt**

```
Role: You are building percentage price tiers with overrides (H-20) for the
Field Sales Management System's head office website.

Context:
- Slice: a Head Office User creates a percentage tier and adds per-product
  overrides as a percentage or a fixed price; the tier's prices follow base
  price changes automatically.
- Specs: plan_docs/stories/pricing.md US-001 S1, S2, glossary (Price Tier,
  Per-Product Overrides) and design decision "Tiers are per Customer,
  several allowed, two shapes"; plan_docs/uxdocs/02-head-office.md H-20
  (H20.1: the detail leads with overrides).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — price engine input
  types (T-6.4.1), products (T-1.2.1).
- Pattern to follow: T-1.2.1's record pages; store tiers in the shape the
  engine (T-6.4.1) consumes.

Acceptance criteria:
1. Tier "B" as 8% off base prices every product 8% below its current base
   for customers holding Tier B (checked through the engine).
2. A base price change flows through the tier automatically.
3. An override of "SPF30 Sun Lotion 200ml" at a fixed €10.50 applies to that
   product; everything else uses 8%.
4. A per-product override may be a percentage instead of a fixed price.
5. Category overrides ("Suncare products at 15%") are built only if MI-47 is
   decided in favour; otherwise stop and report.

Constraints:
- Use the project's existing conventions and test framework.
- Store only what the engine needs; never store resolved prices.
- No tests of framework internals or trivial members.
- This task opts in to the tier and tier-override tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the tier storage model and its mapping to the engine's
input types are written, stop and show them. Resume only on
"Continue T-6.1.1".

Steps: 1. tier and override model; 2. mapping to engine inputs; 3. H-20 list
and detail; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-6.1.1-S; do not design
your own.

Definition of done: a Head Office User creates a percentage tier and adds
per-product overrides as a percentage or a fixed price, and the tier's prices
follow base price changes automatically.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated (or 5 reported as blocked)
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: price-list tiers and warnings (T-6.1.2), assigning tiers
(T-6.2.1), drift indicators (E19), tiers as promotion audiences (E18).
```

**Checkpoint**

Produces before pausing — the tier storage model and its mapping to the engine's input types.
Human reviews — Does storage hold only the inputs the engine needs, with no resolved prices that could go stale?
Resume trigger — `Continue T-6.1.1`

---

### T-6.1.2-S — Test scenarios for price-list tiers and tier warnings

**Owner** — Scenario Review
**Gates** — T-6.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for price-list tiers and tier warnings (task
T-6.1.2). Read plan_docs/stories/pricing.md US-001 S3–S5. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning T-6.1.1's percentage tiers,
then cover S3–S5, then derivable edges: a price-list entry equal to base, a
product later archived, changing back from price list to percentage. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-6.1.2 — Create price-list tiers and warn when a price can never apply

**Parent story**

> As a Head Office User, I want to set up a tier either as a percentage off list or as a list of agreed prices so that a negotiated deal is recorded once and applied everywhere.
>
> Acceptance criteria:
> - Tier "Suncare Deal 2027" as a price list with 20 products priced individually: those 20 use their prices; all others fall back to Base Price (S3)
> - A fixed override of €13.00 where base is €12.50 shows "This is above list price and will never apply", and can be saved or corrected (S4)
> - Changing Tier B from percentage to price list warns that the default percentage will no longer apply and unlisted products revert to base (S5)

**Slice** — A Head Office User creates a price-list tier, is warned when a fixed price is above list and so can never apply, and is told what stops applying when a tier changes shape.
**Spec source** — Pricing US-001 S3–S5; uxdocs 02 H-20
**Depends on** — T-6.1.1
**Pattern to follow** — T-6.1.1
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the same money model as T-6.1.1, extended; checkpoint on the warnings.

**Provisional commit message**

```
feat(pricing): price-list tiers with never-applies warnings

- Product-by-product deals are stored as fixed prices; a price above list
  can never win, so head office is told rather than left to wonder
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Extends reviewed money inputs.

**Agent prompt**

```
Role: You are adding price-list tiers and tier warnings (H-20) to the Field
Sales Management System.

Context:
- Slice: a Head Office User creates a price-list tier, is warned when a fixed
  price is above list and so can never apply, and is told what stops applying
  when a tier changes shape.
- Specs: plan_docs/stories/pricing.md US-001 S3–S5 and glossary (Price-list
  tier); plan_docs/uxdocs/02-head-office.md H-20.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — tier model (T-6.1.1),
  engine (T-6.4.1).
- Pattern to follow: T-6.1.1.

Acceptance criteria:
1. Tier "Suncare Deal 2027" as a price list with 20 products priced
   individually: those 20 resolve to their listed prices; all other products
   fall back to Base Price.
2. A fixed price of €13.00 where base is €12.50 shows "This is above list
   price and will never apply", and can be saved or corrected.
3. Changing Tier B from percentage to price list warns that the default
   percentage will no longer apply and unlisted products revert to base.
4. The warning in 2 is evaluated against the current base price.

Constraints:
- Use the project's existing conventions and test framework.
- No stored resolved prices.
- No tests of framework internals or trivial members.
- This task opts in to a tier shape column and price-list entries.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-6.1.1's percentage tiers; stop and show them passing. Resume only on
"Continue T-6.1.2".

Steps: 1. characterisation tests; 2. price-list shape; 3. never-applies
warning; 4. change-shape warning; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-6.1.2-S; do not design
your own.

Definition of done: a Head Office User creates a price-list tier, is warned
when a fixed price is above list and so can never apply, and is told what
stops applying when a tier changes shape.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: drift indicators (E19), assigning tiers (T-6.2.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of percentage tiers, passing.
Human reviews — Do percentage tiers still resolve exactly as before?
Resume trigger — `Continue T-6.1.2`

---

### T-6.3.1-S — Test scenarios for quantity breaks

**Owner** — Scenario Review
**Gates** — T-6.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for setting quantity breaks on a product (task
T-6.3.1). Read plan_docs/stories/pricing.md US-004 S1–S3 and
plan_docs/uxdocs/02-head-office.md H-22 (H22.1, H22.2). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover S1–S3, then derivable edges: a break at quantity 1, a break above base
price, two breaks at the same quantity, a measure break not on the step. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-6.3.1 — Set quantity breaks on a product

**Parent story**

> As a Head Office User, I want to set prices that apply from a quantity upward, for counted and measured products, so that bulk buying is rewarded consistently.
>
> Acceptance criteria:
> - "Throat Lozenges 36s" (Each, €2.20) with breaks "from 10: €2.00" and "from 30: €1.75": 12 prices €2.00, 30 prices €1.75 (S1)
> - "Loose Herbal Tea" (kg, €4.80, step 0.5) with "from 10 kg: €4.00" and "from 20 kg: €3.60": 12 kg prices €4.00 per kg (S2)
> - "from 30: €2.30" above "from 10: €2.00" warns "This break is higher than the one below it and will never apply" (S3)

**Slice** — A Head Office User adds quantity breaks to a product for whole or measured quantities, and a break that could never apply is warned about but can still be saved.
**Spec source** — Pricing US-004 S1–S3; uxdocs 02 H-22 (H22.1, H22.2)
**Depends on** — T-6.4.1, T-1.4.1
**Pattern to follow** — T-1.6.1 (product record section)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — money inputs to the engine; H-22 is a draft.

**Provisional commit message**

```
feat(pricing): set quantity breaks on products

- Breaks reward bulk buying and compete with tier prices as ordinary
  candidates; a break that can never win is flagged but kept, since it may
  be set ahead of a price change
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Engine inputs built under review.

**Agent prompt**

```
Role: You are adding quantity breaks to the product record (H-22) of the
Field Sales Management System.

Context:
- Slice: a Head Office User adds quantity breaks to a product for whole or
  measured quantities; a break that could never apply is warned about but can
  still be saved.
- Specs: plan_docs/stories/pricing.md US-004 S1–S3 and glossary (Quantity
  Break); plan_docs/uxdocs/02-head-office.md H-22 (H22.1 breaks edit in place
  as a section of H-13, sorted by quantity, with base price above; H22.2 a
  never-applies break warns but still saves).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — product record
  (T-1.2.1, T-1.6.1), units (T-1.4.1), engine (T-6.4.1).
- Pattern to follow: T-1.6.1's record sections.

Acceptance criteria:
1. "Throat Lozenges 36s" (Each, €2.20) with breaks "from 10: €2.00" and
   "from 30: €1.75" resolves 12 at €2.00 and 30 at €1.75 through the engine.
2. "Loose Herbal Tea" (kg, €4.80, step 0.5) with "from 10 kg: €4.00" and
   "from 20 kg: €3.60" resolves 12 kg at €4.00 per kg.
3. "from 30: €2.30" above "from 10: €2.00" warns "This break is higher than
   the one below it and will never apply" and can still be saved.
4. Breaks are sorted by quantity, with the base price shown above them.
5. Measure break thresholds are multiples of the product's step.

Constraints:
- Use the project's existing conventions and test framework.
- Breaks belong to the product, not to a tier.
- No tests of framework internals or trivial members.
- This task opts in to a quantity-break table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the break model and its mapping to the engine's inputs are
written, stop and show them. Resume only on "Continue T-6.3.1".

Steps: 1. break model; 2. mapping to engine inputs; 3. H-22 section;
4. warnings; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-6.3.1-S; do not design
your own.

Definition of done: a Head Office User adds quantity breaks to a product for
whole or measured quantities, and a break that could never apply is warned
about but can still be saved.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: breaks in the snapshot (T-6.4.2), break prompts (T-6.5.1).
```

**Checkpoint**

Produces before pausing — the break model and its mapping to the engine's inputs.
Human reviews — Do breaks reach the engine exactly as the scenarios expect, for Each and for measures?
Resume trigger — `Continue T-6.3.1`

---

### T-6.2.1-S — Test scenarios for assigning tiers to a customer

**Owner** — Scenario Review
**Gates** — T-6.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for assigning tiers to a customer (task
T-6.2.1). Read plan_docs/stories/pricing.md US-003 S1–S4 and
plan_docs/uxdocs/02-head-office.md H-21 (H21.1, H21.2). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover S1–S4, then derivable edges: assigning the same tier twice, a customer
with 13 locations, removing a tier while a rep's tablet still has it in the
snapshot. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-6.2.1 — Assign tiers to a customer with a sample of resulting prices

**Parent story**

> As a Head Office User, I want to give a Customer one or more tiers so that all its Locations get the agreed terms.
>
> Acceptance criteria:
> - Assigning Tier B and "Suncare Deal 2027" to Hickey's Pharmacies makes all 13 of its Locations price against both, best price winning per line (S1)
> - The Customer shows "Tiers: B, Suncare Deal 2027" and a sample of products with the price each would get (S2)
> - Removing "Suncare Deal 2027" affects new orders only; captured orders keep their prices (S3)
> - A Customer with no tier prices at Base Price plus any break (S4)

**Slice** — A Head Office User ticks one or more tiers on a customer's record, sees a live sample of the resulting prices before saving, and every shop of that customer then prices against them.
**Spec source** — Pricing US-003 S1–S4; uxdocs 02 H-21 (H21.1, H21.2), H-25 (H25.2)
**Depends on** — T-6.1.2, T-2.3.1
**Pattern to follow** — T-6.1.1 (tier pages), T-2.3.1 (Customer record)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — money; H-21 is a draft; the sample calls the engine.

**Provisional commit message**

```
feat(pricing): assign tiers to a customer with a price sample

- Terms are negotiated with the buying organisation, so tiers sit on the
  Customer and every shop inherits them
- A live sample shows the effect before saving, so a wrong tier is caught
  at the desk rather than at the counter
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Money-affecting assignment with a review pause.

**Agent prompt**

```
Role: You are adding tier assignment to the Customer record (H-21) of the
Field Sales Management System.

Context:
- Slice: a Head Office User ticks one or more tiers on a customer's record,
  sees a live sample of the resulting prices before saving, and every shop of
  that customer then prices against them.
- Specs: plan_docs/stories/pricing.md US-003 S1–S4; plan_docs/uxdocs/
  02-head-office.md H-21 (H21.1 a section of the Customer record; the sample
  recalculates as tiers are ticked; H21.2 the sample uses the customer's
  most-ordered products, with a search for any other) and H-25 (H25.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Customer record
  (T-2.3.1), tiers (T-6.1.1, T-6.1.2), engine (T-6.4.1).
- Pattern to follow: T-6.1.1's pages.

Acceptance criteria:
1. Assigning Tier B and "Suncare Deal 2027" to Hickey's Pharmacies makes all
   13 of its Locations price against both, best price winning per line.
2. The Customer shows "Tiers: B, Suncare Deal 2027" and a sample of products
   with the price each would get, recalculated as tiers are ticked, before
   saving.
3. Removing "Suncare Deal 2027" affects new orders only; captured orders keep
   their prices.
4. A Customer with no tier prices at Base Price plus any break.
5. The sample shows the customer's most-ordered products and lets the user
   search for any other.

Constraints:
- Use the project's existing conventions and test framework.
- The sample must call the engine (T-6.4.1); no second price rule.
- No tests of framework internals or trivial members.
- This task opts in to a customer–tier link table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the section renders with a live sample for a customer with
two tiers, stop and show it. Resume only on "Continue T-6.2.1".

Steps: 1. customer–tier link; 2. H-21 section; 3. live sample via the
engine; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-6.2.1-S; do not design
your own.

Definition of done: a Head Office User ticks one or more tiers on a
customer's record, sees a live sample of the resulting prices before saving,
and every shop of that customer then prices against them.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: tiers on the tablet (T-6.4.2), promotions by tier (E18), drift
(E19).
```

**Checkpoint**

Produces before pausing — the H-21 section with a live sample for a customer holding two tiers.
Human reviews — Does the sample agree with what the tablet will show, and does it match the confirmed H-21?
Resume trigger — `Continue T-6.2.1`

---

### T-6.4.2-S — Test scenarios for resolved prices on the Order Pad

**Owner** — Scenario Review
**Gates** — T-6.4.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for carrying tiers and breaks to the tablet and
showing resolved prices on the Order Pad (task T-6.4.2). Read
plan_docs/stories/pricing.md US-005 S5, S6 and
plan_docs/stories/rep-at-a-location-tablet.md US-012 S1. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning the pad's current base-price
display (T-5.1.2), then cover S5 and S6, then derivable edges: a customer
whose tier changed since the last sync, a shop of a customer with no tier,
an order started before a sync and continued after. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-6.4.2 — Show the customer's resolved price on the Order Pad offline

**Parent story**

> As a Field Salesperson, I want each line to show the price, where it came from, and what else was considered so that I can answer the customer without guessing.
>
> Acceptance criteria:
> - Offline, all resolution happens on the tablet from the morning snapshot, including every tier the customer holds (S5)
> - The Order Pad shows each product's resolved price at quantity 1 for this customer, not the base price (S6)

**Slice** — After a sync, the tablet holds each customer's tiers and each product's breaks, and the Order Pad and quantity popover show every product at the customer's resolved price, offline.
**Spec source** — Pricing US-005 S5, S6; Rep at a Location US-012 S1
**Depends on** — T-6.2.1, T-6.3.1, T-5.1.2
**Pattern to follow** — T-4.1.1 (snapshot versioning), T-6.4.1 (engine)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — changes the snapshot contract and the pad's price display (T-5.1.2), so characterisation comes first.

**Provisional commit message**

```
feat(pricing): show customers' resolved prices on the tablet offline

- Prices resolve on the tablet from the snapshot with no call at order
  time, so a rep in a shop with no signal still quotes the right price
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Snapshot and display change behind a characterisation pass.

**Agent prompt**

```
Role: You are carrying tiers and breaks to the tablet and showing resolved
prices on the Order Pad in the Field Sales Management System.

Context:
- Slice: after a sync, the tablet holds each customer's tiers and each
  product's breaks, and the Order Pad and quantity popover show every product
  at the customer's resolved price, offline.
- Specs: plan_docs/stories/pricing.md US-005 S5, S6;
  plan_docs/stories/rep-at-a-location-tablet.md US-012 S1 and assumptions
  ("Price resolution runs entirely on the tablet from the snapshot").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine (T-6.4.1),
  snapshot builder (T-4.1.1), pad (T-5.1.2), popover (T-5.5.1), captured
  price on lines (T-5.1.1).
- Pattern to follow: T-4.1.1's snapshot versioning.

Acceptance criteria:
1. The snapshot carries the tiers held by each assigned Location's Customer
   and each product's quantity breaks.
2. With no signal, the Order Pad shows each product's resolved price at
   quantity 1 for this customer, not the base price.
3. The quantity popover and each order line show the engine's winner at the
   line's quantity; the line captures that price and source.
4. Lines captured before this change keep their captured prices.

Constraints:
- Use the project's existing conventions and test framework.
- Call the engine (T-6.4.1) for every price; no second rule on the tablet.
- Snapshot additions go through T-4.1.1's versioning and its owner's review.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  snapshot fields.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the pad's and popover's current price display and the captured price on
lines; stop and show them passing with the snapshot additions. Resume only on
"Continue T-6.4.2".

Steps: 1. characterisation tests; 2. snapshot fields for tiers and breaks;
3. pad and popover use the engine; 4. capture the winner and source; 5. tests
from agreed scenarios.

Test expectations: implement the scenarios agreed in T-6.4.2-S; do not design
your own.

Definition of done: after a sync, the tablet holds each customer's tiers and
each product's breaks, and the Order Pad and quantity popover show every
product at the customer's resolved price, offline.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: source and runner-up display and the provenance sheet
(T-6.4.3), break prompts (T-6.5.1), promotions (E18).
```

**Checkpoint**

Produces before pausing — characterisation tests of current price display and capture, passing, and the snapshot additions for tiers and breaks.
Human reviews — Is every price on the tablet now coming from the one engine, within the snapshot size budget?
Resume trigger — `Continue T-6.4.2`

---

### T-6.4.3-S — Test scenarios for the price on the line and its provenance

**Owner** — Scenario Review
**Gates** — T-6.4.3
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing a line's price, source and
candidates (task T-6.4.3). Read plan_docs/stories/pricing.md US-005 S1–S4,
plan_docs/stories/rep-at-a-location-tablet.md US-012 S4b, and
plan_docs/uxdocs/00-conventions-and-shared-elements.md §1 (the order line and
the price provenance sheet). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning T-6.4.2's line price display. Cover S2–S4
(promotions arrive with E18), then derivable edges: a break winning, a
measure product, equal candidates. Note that the source stories put the
runner-up tier price on the line while the conventions draft (DECISION 1.1)
moves it into the sheet — mark it "Needs a decision" (MI-54). Write no test
code; change no files.
```

---

### T-6.4.3 — Show where a line's price came from, with every candidate on demand

**Parent story**

> As a Field Salesperson, I want each line to show the price, where it came from, and what else was considered so that I can answer the customer without guessing.
>
> Acceptance criteria:
> - With a tier winning: "€11.20 — Tier B (list €12.50)" (S2)
> - With base winning: "€12.50 — list price" with no runner-up (S3)
> - Tapping the price shows every candidate considered, each named, with the winner marked (S4)
> - The promotion case "€9.99 — Autumn promotion (better than your tier price €11.20)" arrives with E18 (S1; US-012 S4b)

**Slice** — Each order line shows its price and source, with the runner-up tier price where one exists, and the (i) marker opens a sheet listing every candidate price with the one applied marked.
**Spec source** — Pricing US-005 S1–S4; Rep at a Location US-012 S4b; uxdocs 00 §1 (price provenance sheet, DECISIONS 1.1, 1.2); design decision "The line explains itself, briefly"
**Depends on** — T-6.4.2
**Pattern to follow** — T-5.1.1 (order line)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the order line (T-5.1.1, T-6.4.2), so characterisation comes first; where the runner-up appears is open (MI-54).

**Provisional commit message**

```
feat(pricing): explain each line's price and its candidates

- The customer knows their tier price and will ask after it, so the line
  names its source and the full candidate list is one tap away
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
The densest element in the app, behind a characterisation pass.

**Agent prompt**

```
Role: You are adding price source, runner-up and the provenance sheet to the
order line in the Field Sales Management System's tablet app.

Context:
- Slice: each order line shows its price and source, with the runner-up tier
  price where one exists, and the (i) marker opens a sheet listing every
  candidate price with the one applied marked.
- Specs: plan_docs/stories/pricing.md US-005 S1–S4 and design decision "The
  line explains itself, briefly"; plan_docs/stories/rep-at-a-location-tablet.md
  US-012 S4b; plan_docs/uxdocs/00-conventions-and-shared-elements.md §1 (price
  provenance sheet frames: APPLIED and ALSO CONSIDERED; DECISION 1.2: (i) is
  an explicit target, not "tap the price").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine output
  (T-6.4.1), line prices (T-6.4.2).
- Pattern to follow: T-5.1.1's order line.

Acceptance criteria:
1. A line where Tier B wins reads "€11.20 — Tier B (list €12.50)".
2. A line where base wins reads "€12.50 — list price" with no runner-up.
3. A line where a break wins shows its source "Break: 10+".
4. The (i) marker opens a sheet with APPLIED (price and source) and ALSO
   CONSIDERED (every other candidate, named), matching the §1 frame.
5. Where the runner-up tier price appears — on the line, as the stories
   say, or only in the sheet, as conventions DECISION 1.1 drafts — follows
   the decision recorded against MI-54; if unrecorded, stop and ask.

Constraints:
- Use the project's existing conventions and test framework.
- Read everything from the engine's output; compute nothing here.
- Status and source are never conveyed by colour alone.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-6.4.2's line price display; stop and show them passing, then show the line
and sheet rendered on a device. Resume only on "Continue T-6.4.3".

Steps: 1. characterisation tests; 2. source label and runner-up; 3. (i)
marker and sheet; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-6.4.3-S; do not design
your own.

Definition of done: each order line shows its price and source, with the
runner-up tier price where one exists, and the (i) marker opens a sheet
listing every candidate price with the one applied marked.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: promotion candidates and the offer summary (E18), rep prices
and their working (E19), break prompts (T-6.5.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of the line price, passing, and the line and sheet rendered on a tablet.
Human reviews — Is the line still readable standing in a shop, and is the runner-up where the MI-54 decision put it?
Resume trigger — `Continue T-6.4.3`

---

### T-6.5.1-S — Test scenarios for break prompts

**Owner** — Scenario Review
**Gates** — T-6.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for break prompts (task T-6.5.1). Read
plan_docs/stories/pricing.md US-006 S1–S5 and design decision "Offers are
shown relative to the order, not as a price list";
plan_docs/stories/rep-at-a-location-tablet.md US-012 S4c and US-008 S9.
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Start with characterisation scenarios pinning T-6.4.3's line
display and T-5.5.1's popover. Cover S1–S5, then derivable edges: exactly at
a break, one short of a break, typing in the popover. "Within reach" is
assumed to mean the next break only (MI-12); rounding of savings follows
MI-48. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-6.5.1 — Prompt when a quantity break is within reach

**Parent story**

> As a Field Salesperson, I want to be told in money terms what ordering a little more would give so that I can offer it while the customer is in front of me.
>
> Acceptance criteria:
> - Breaks at 10 for €2.00 and the line at 8 for €2.20: "2 more for €2.00 each — save €4.00 on 10" (S1)
> - At 10 the price becomes €2.00, sourced "Break: 10+", and the prompt refers to the next break at 30 (S2)
> - 8 kg with a break at 10 kg: "2 kg more for €4.00 per kg — save €8.00 on 10 kg" (S3)
> - Line at 2 with the first break at 30: no prompt (S4)
> - The customer's tier already beats every break: no prompt (S5)
> - In the quantity popover, the prompt appears beneath the price and updates as the rep types (US-008 S9)

**Slice** — When the next quantity break is within reach, the line and the quantity popover say in money what ordering a little more would save, updating as the rep types, and say nothing when it isn't worth it.
**Spec source** — Pricing US-006 S1–S5; Rep at a Location US-012 S4c, US-008 S9
**Depends on** — T-6.4.3, T-5.5.1
**Pattern to follow** — T-6.4.3 (line display), T-5.5.1 (popover)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the line and popover, so characterisation comes first; money wording depends on MI-12 and MI-48.

**Provisional commit message**

```
feat(pricing): prompt when a quantity break is within reach

- The rep is mid-conversation and needs a sentence to say aloud, not a
  break table; silence loses a sale the customer would have taken
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Money wording on shared components behind a characterisation pass.

**Agent prompt**

```
Role: You are adding break prompts to the order line and quantity popover in
the Field Sales Management System's tablet app.

Context:
- Slice: when the next quantity break is within reach, the line and the
  quantity popover say in money what ordering a little more would save,
  updating as the rep types, and say nothing when it isn't worth it.
- Specs: plan_docs/stories/pricing.md US-006 S1–S5 and glossary (Break
  Prompt); plan_docs/stories/rep-at-a-location-tablet.md US-012 S4c, US-008
  S9; plan_docs/uxdocs/00-conventions-and-shared-elements.md §1 (1b, 1c).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine (T-6.4.1), line
  display (T-6.4.3), popover (T-5.5.1).
- Pattern to follow: T-6.4.3's line display.

Acceptance criteria:
1. Breaks at 10 for €2.00; the line at 8 for €2.20 reads "2 more for €2.00
   each — save €4.00 on 10".
2. At 10 the price becomes €2.00 sourced "Break: 10+", and the prompt refers
   to the next break at 30.
3. 8 kg with a break at 10 kg reads "2 kg more for €4.00 per kg — save €8.00
   on 10 kg".
4. A line at 2 with the first break at 30 shows no prompt.
5. If the customer's tier already beats every break, no prompt.
6. In the quantity popover the prompt appears beneath the price and updates
   as the rep types.

Constraints:
- Use the project's existing conventions and test framework.
- Compute the prompt by asking the engine for the price at the next break's
  quantity; no second price rule. "Within reach" = the next break only.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the line and popover displays; stop and show them passing with the prompt
function. Resume only on "Continue T-6.5.1".

Steps: 1. characterisation tests; 2. prompt function over the engine;
3. line and popover display; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-6.5.1-S; do not design
your own.

Definition of done: when the next quantity break is within reach, the line
and the quantity popover say in money what ordering a little more would save,
updating as the rep types, and say nothing when it isn't worth it.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: promotion "within reach" messages and the offer summary (E18).
```

**Checkpoint**

Produces before pausing — characterisation tests of the line and popover, passing, and the prompt function.
Human reviews — Are the savings figures and wording exactly what a rep can say aloud, for Each and for kg?
Resume trigger — `Continue T-6.5.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | PR-001 (T-6.1.1, T-6.1.2), PR-003 (T-6.2.1), PR-004 (T-6.3.1), PR-005 (T-6.4.1–T-6.4.3), PR-006 (T-6.5.1) |
| Every task satisfies the three slice criteria | Pass | 8 of 8 |
| Every task carries a tier with a rationale citing dimensions | Pass | 8 of 8 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-6.4.1 (MI-47, 48, 53), T-6.4.3 (MI-54) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-6.1.2, T-6.4.2, T-6.4.3, T-6.5.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | No Agent-Autonomous task in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-6.4.1 is Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | 7 of 7 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-6.4.1 |
| Every scenario task precedes the task it gates | Pass | 8 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-12, 47, 48, 53, 54 |
