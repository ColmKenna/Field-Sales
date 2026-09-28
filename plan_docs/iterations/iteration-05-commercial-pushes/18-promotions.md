# E18 — Promotions

**Iteration** — 5, Commercial pushes
**Outcome** — Head office runs offers in four shapes for everyone or named tiers over whole days; they apply automatically and best-for-the-customer on every order, offline on the tablet; the order explains what applied, what it saved and what's within reach; and head office is warned when a new offer would never fire.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Promotions US-001 — Create a Buy X get Y promotion | Must | S1–S6 |
| 2 | Promotions US-005 — Set a promotion's audience and period | Must | S1–S5 |
| 3 | Promotions US-007 — Understand offers on an order | Must | S1–S6 (PM007-A with T-18.6.1); also Pricing US-005 S1, S4's promotion candidates and Rep at a Location US-012 S4b, S4d |
| 4 | Promotions US-002 — Create a Bundle promotion | Should | S1–S4 |
| 5 | Promotions US-003 — Create a Mix and Match promotion | Should | S1–S5 |
| 6 | Promotions US-004 — Create a Spend Threshold promotion | Should | S1–S5, and US-007 PM007-A |
| 7 | Promotions US-006 — Be warned when a product is already in other promotions | Should | S1–S4 |
| 8 | Promotions US-008 — Browse and manage promotions | Could | S1–S3 |

**Exit criterion** — Head office can create Buy X get Y, Bundle, Mix and Match and Spend Threshold promotions for all customers or named tiers over whole days, see an overlap warning naming which offer wins, and browse live, scheduled and ended promotions with "Cannot apply" and "May never apply" flags. On the tablet and server alike, offers apply automatically and best-for-the-customer at each order's quantities, repeat up to any cap, and the order's offer summary lists what applied, what it saved, what's within reach and what was lost.

**Capability-class stamp** — Frontier + extended reasoning for the engine slivers (T-18.1.1, T-18.6.1); Frontier workhorse for other tasks and scenario drafting; Fast mid-tier for T-18.8.1. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [promotions.md](../../stories/promotions.md), [pricing.md](../../stories/pricing.md) (US-005, glossary), [02-head-office.md](../../uxdocs/02-head-office.md) (H-23, H-24), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§1 order line and provenance sheet), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-002 rules 5–6, RC-NEW-006).

---

### T-18.1.1-S — Test scenarios for line-level offers in the price engine

**Owner** — Human-Led
**Gates** — T-18.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Buy 10 SPF30, get 2 After Sun free: 10 SPF30 adds 2 After Sun at €0.00 (S1); at €2.00 each (S2). Are free items added automatically as new lines, or only priced when the rep adds them (S5 suggests both)?
- Repeats: 25 SPF30 → applied twice, 4 After Sun, 5 SPF30 not contributing (S3); with Maximum Repeats 2 and 40 SPF30 → 4 After Sun, "Applied 2 times (maximum)" (S4).
- Reward already on the order: 3 After Sun at full price → 2 offer-priced, 1 resolved (S5).
- Trigger from a set: any 10 from Range Summer 2027 (S6) — which items count toward the trigger when several sets overlap?
- "Best Price Wins" per line vs offers: when a tier price on After Sun is lower than the offer's €2.00, which applies?
- Free items consume stock and count against Run-out Remaining (assumption).
- Rounding when an offer spreads a price across lines (MI-48).

---

### T-18.1.1 — Apply Buy X get Y offers automatically in the price engine

**Parent story**

> As a Head Office User, I want to set up "buy a quantity of one product and get another free or cheap" so that I can push a line or introduce a new product.
>
> Acceptance criteria:
> - "Buy 10 SPF30 Sun Lotion 200ml, get 2 After Sun 200ml free" gives an order with 10 SPF30 2 After Sun at €0.00, marked "Autumn offer — free" (S1)
> - A €2.00 reward prices the 2 After Sun at €2.00 each (S2)
> - 25 SPF30 applies it twice: 4 After Sun at €0.00, 5 SPF30 not contributing (S3)
> - Maximum Repeats 2 with 40 SPF30 gives 4 After Sun and "Applied 2 times (maximum)" (S4)
> - 3 After Sun already on at full price: 2 become offer-priced, 1 stays at its resolved price (S5)
> - A trigger of "any 10 from Range Summer 2027" qualifies on any mix of 10 from it (S6)

**Slice** — The price engine evaluates live Buy X get Y offers for the customer at the order's quantities — repeating up to any cap, choosing best-for-the-customer — and returns the offer prices and any free reward lines, identically on tablet and server.
**Spec source** — Promotions US-001 S1–S6; glossary (Line-level offer, Automatic application, Best-for-the-customer selection, Repeats); design decisions "Applied automatically, explained afterwards", "Best for the customer, and offers repeat"
**Depends on** — T-6.4.1, T-9.1.1
**Pattern to follow** — T-6.4.1 (price engine)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — extends the money engine from per-line to whole-order evaluation (Blast Radius High); Edge-Case Discovery High.

**Provisional commit message**

```
feat(pricing): apply buy-x-get-y offers automatically in the engine

- A qualifying order always gets the offer without the rep spotting it,
  consistent with best price wins
- Offers repeat while the order allows, with an optional cap for the rare
  chain order that would otherwise run away
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Whole-order evaluation in the money engine.

**Work package**

Increments:
1. Extend the engine from "price one line" to "price an order": inputs are all lines, the customer's tiers, and live promotions for the date and audience; output per line (winner, source, candidates) plus offer applications.
2. Buy X get Y: trigger (a product or a set by product list, Range or Category) and quantity; reward product, free or at a price; repeats and Maximum Repeats.
3. Reward handling: when reward units aren't on the order, add offer lines marked as belonging to the offer; when some are, price that many at the offer.
4. Candidates per line keep Best Price Wins: an offer price is a candidate; nothing compounds.
5. Shared test vectors extended with the agreed scenarios.

Decision points:
- Are free reward lines added automatically, or suggested (S1 says gains)?
- How do overlapping offers on one product combine — each line takes the best single offer?
- Where does the order-level evaluation run on the server (validation, H-02 working, self-service)?

Delegable slivers:
- **Offer test vectors** — Encode the agreed scenarios from T-18.1.1-S as shared test vectors in the existing vector format. No engine code.
- **Set resolver** — Implement the function that resolves an offer's eligible set (product list, Range or Category and beneath) to products at a date. Pure; no pricing.

---

### T-18.1.2-S — Test scenarios for creating a Buy X get Y promotion

**Owner** — Scenario Review
**Gates** — T-18.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for creating a Buy X get Y promotion on H-24
(task T-18.1.2). Read plan_docs/stories/promotions.md US-001 and
plan_docs/uxdocs/02-head-office.md H-24 (H24.1). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Cover
creating each variant (free reward, priced reward, set trigger, cap), then
derivable edges: reward equal to trigger product, zero quantities, changing
shape after entering fields. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-18.1.2 — Create a Buy X get Y promotion with its shape chosen first

**Parent story**

> As a Head Office User, I want to set up "buy a quantity of one product and get another free or cheap" so that I can push a line or introduce a new product.
>
> Acceptance criteria:
> - Creating "Buy 10 SPF30 Sun Lotion 200ml, get 2 After Sun 200ml free", all customers, 1–31 October 2026 saves the offer (S1)
> - The reward can be free or at a set price (S2); the trigger can be a product or a set (S6); Maximum Repeats is optional (S4)

**Slice** — A Head Office User chooses the Buy X get Y shape, enters the trigger (product or set, and quantity), the reward (product, free or at a price) and an optional repeat cap, and saves the promotion.
**Spec source** — Promotions US-001 S1, S2, S4, S6; uxdocs 02 H-24 (H24.1 shape chosen first)
**Depends on** — T-18.1.1
**Pattern to follow** — T-6.1.1 (head office pricing pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — H-24 is a draft awaiting confirmation (MI-61); the form feeds the reviewed engine.

**Provisional commit message**

```
feat(promotions): create buy-x-get-y promotions with shape chosen first

- Each shape needs different fields, so the shape is the first question
  and the form follows from it
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A drafted form with a review pause.

**Agent prompt**

```
Role: You are building Buy X get Y promotion setup (H-24) for the Field Sales
Management System's head office website.

Context:
- Slice: a Head Office User chooses the Buy X get Y shape, enters the trigger
  (product or set, and quantity), the reward (product, free or at a price) and
  an optional repeat cap, and saves the promotion.
- Specs: plan_docs/stories/promotions.md US-001 and glossary (Shapes,
  Repeats); plan_docs/uxdocs/02-head-office.md H-24 (H24.1 shape is the first
  question and switches the fields; changing shape later clears
  shape-specific fields after a warning).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine offer model
  (T-18.1.1), products, Ranges and Categories (E1, E9).
- Pattern to follow: T-6.1.1's pricing pages.

Acceptance criteria:
1. Shape is the first choice; choosing Buy X get Y shows trigger and reward
   fields.
2. The trigger is a product or a set (product list, Range or Category) with
   a quantity.
3. The reward is a product, free or at a set price, with a quantity.
4. Maximum Repeats is optional and blank by default.
5. Saving "Buy 10 SPF30 Sun Lotion 200ml, get 2 After Sun 200ml free" with
   its audience and period (T-18.2.1) creates the promotion.
6. Changing shape after entering fields warns, then clears the shape-specific
   fields.

Constraints:
- Use the project's existing conventions and test framework.
- Store the offer in the shape the engine consumes.
- No tests of framework internals or trivial members.
- This task opts in to the promotion tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the form renders against H-24 and saves an offer the engine
applies, stop and show it. Resume only on "Continue T-18.1.2".

Steps: 1. promotion storage; 2. shape-first form; 3. BXGY fields;
4. save and engine round trip; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-18.1.2-S; do not design
your own.

Definition of done: a Head Office User chooses the Buy X get Y shape, enters
the trigger (product or set, and quantity), the reward (product, free or at a
price) and an optional repeat cap, and saves the promotion.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: audience and period rules (T-18.2.1), other shapes (T-18.4.1,
T-18.5.1, T-18.6.1), overlap warning (T-18.7.1).
```

**Checkpoint**

Produces before pausing — the H-24 form saving an offer that the engine applies.
Human reviews — Does the form match the confirmed H-24, and does a saved offer behave in the engine exactly as entered?
Resume trigger — `Continue T-18.1.2`

---

### T-18.2.1-S — Test scenarios for promotion audience and period

**Owner** — Scenario Review
**Gates** — T-18.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a promotion's audience and whole-day period
(task T-18.2.1). Read plan_docs/stories/promotions.md US-005 S1–S5, glossary
(Audience) and design decisions "Audience is all customers or named tiers",
"Whole-day periods". Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S1–S5,
then derivable edges: a tier used only as an audience, a promotion ending
today with a tablet not synced tomorrow, a customer gaining a tier mid-period.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-18.2.1 — Target a promotion at everyone or named tiers over whole days

**Parent story**

> As a Head Office User, I want to choose who gets an offer and when it runs so that tier deals stay with those customers and nothing starts early.
>
> Acceptance criteria:
> - All customers means every customer qualifies, including self-service (S1)
> - Tier A and Tier B means only customers holding one of them qualify (S2)
> - 1–31 October 2026 runs from the start of 1 October to the end of 31 October, with no time of day (S3)
> - A promotion starting next month is Scheduled, isn't live in any snapshot, but its dates travel so tablets apply it on the day (S4)
> - Ending a live promotion today stops it from tomorrow; captured orders keep their prices (S5)

**Slice** — Each promotion runs for all customers or for customers holding named tiers, from the start of its first day to the end of its last, reaches tablets in advance so it switches on the right day offline, and can be ended early from tomorrow.
**Spec source** — Promotions US-005 S1–S5; uxdocs 02 H-24, H-23 (H23.2 End early on the detail)
**Depends on** — T-18.1.1, T-6.2.1
**Pattern to follow** — T-1.3.2 (future-dated values applied on the tablet on their day)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — money eligibility and day boundaries (MI-53); follows the reviewed dated-price pattern.

**Provisional commit message**

```
feat(promotions): target offers at all customers or tiers over whole days

- Promotions start and end on day boundaries because the tablet works
  from a morning snapshot; a mid-day change would leave a rep quoting a
  price nobody honours
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Eligibility and dates with a review pause.

**Agent prompt**

```
Role: You are adding promotion audience and period to the Field Sales
Management System.

Context:
- Slice: each promotion runs for all customers or for customers holding named
  tiers, from the start of its first day to the end of its last, reaches
  tablets in advance so it switches on the right day offline, and can be
  ended early from tomorrow.
- Specs: plan_docs/stories/promotions.md US-005 S1–S5, glossary (Audience)
  and design decisions "Audience is all customers or named tiers",
  "Whole-day periods"; plan_docs/uxdocs/02-head-office.md H-24, H-23 (H23.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine (T-6.4.1,
  T-18.1.1), tiers on customers (T-6.2.1), snapshot (T-4.1.1), dated-value
  pattern (T-1.3.2).
- Pattern to follow: T-1.3.2.

Acceptance criteria:
1. Audience All customers: every customer qualifies, including self-service.
2. Audience Tier A and Tier B: only customers holding one of them qualify;
   others price without the offer.
3. 1–31 October 2026 is live from the start of 1 October to the end of 31
   October in the business time zone (MI-53).
4. A promotion starting next month is Scheduled; its dates travel in the
   snapshot so tablets apply it on its first day without syncing again.
5. End early on a live promotion stops it from tomorrow; captured orders
   keep their prices.
6. A tier can be used only as an audience, with no prices of its own.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions go through T-4.1.1's versioning and its owner's review.
- No editing of a live promotion other than End early (assumption, MI-26).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond audience
  and period fields.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after eligibility and period evaluation are wired into the engine
and the snapshot, stop and show a tablet applying a scheduled promotion on its
first day. Resume only on "Continue T-18.2.1".

Steps: 1. audience and period fields; 2. engine eligibility by date and tier;
3. snapshot; 4. End early; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-18.2.1-S; do not design
your own.

Definition of done: each promotion runs for all customers or for customers
holding named tiers, from the start of its first day to the end of its last,
reaches tablets in advance so it switches on the right day offline, and can
be ended early from tomorrow.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the promotion list (T-18.8.1), editing live promotions.
```

**Checkpoint**

Produces before pausing — a tablet applying a scheduled promotion on its first day without syncing.
Human reviews — Does every surface agree on whether an offer is live on a given day and for a given customer?
Resume trigger — `Continue T-18.2.1`

---

### T-18.3.1-S — Test scenarios for offers on the order

**Owner** — Scenario Review
**Gates** — T-18.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing offers on an order (task T-18.3.1).
Read plan_docs/stories/promotions.md US-007 S1–S6, plan_docs/stories/
pricing.md US-005 S1, S4, plan_docs/stories/rep-at-a-location-tablet.md US-012
S4b, S4d, and plan_docs/uxdocs/00-conventions-and-shared-elements.md §1.
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Start with characterisation scenarios pinning T-6.4.3's line
display and provenance sheet. Cover the story scenarios for Buy X get Y
(bundle, mix-and-match and spend threshold add their own later), then
derivable edges: an offer lost after removing a line, an offer on a free
line. Mark undecided cases as "Needs a decision". Write no test code; change
no files.
```

---

### T-18.3.1 — Explain offers on the order: applied, saved, within reach, lost

**Parent story**

> As a Field Salesperson, I want the order to say what offers applied, what they saved, and what's within reach so that I can explain the total and offer the customer more.
>
> Acceptance criteria:
> - With two offers applied, the Offer Summary reads "2 offers applied — saving €19.70" and expands to each with its saving (S1)
> - Lines added by an offer read "€0.00 — Autumn offer (free)" and are visibly not rep-entered (S2)
> - "1 more Suncare product for the 6 for €15.00 offer" when within reach (S3); an offer lost is stated with what would restore it (S4)
> - Offline, offers resolve from the morning snapshot including their periods (S5)
> - Partly qualifying for 3 offers shows "3 offers available on this order", expanding to the detail (S6)
> - A promotion-won line reads "€9.99 — Autumn promotion (better than your tier price €11.20)" and the provenance sheet lists the promotion among candidates (Pricing US-005 S1, S4; Rep at a Location US-012 S4b)

**Slice** — On the tablet order, every line shows when a promotion set its price, free lines added by an offer are marked as such, and an offer summary states what applied and saved, what's within reach and what was just lost.
**Spec source** — Promotions US-007 S1–S6; Pricing US-005 S1, S4; Rep at a Location US-012 S4b, S4d; uxdocs 00 §1 (1b, 1c, provenance sheet)
**Depends on** — T-18.1.1, T-18.2.1, T-6.4.3
**Pattern to follow** — T-6.4.3 (line display), T-6.5.1 (within-reach prompts)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the order line and provenance sheet (characterisation first); reads the reviewed engine output.

**Provisional commit message**

```
feat(promotions): explain offers on the order

- Offers apply without the rep accepting them, so totals move as lines are
  added; the summary carries the explanation, including an offer lost
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
The densest screen area, behind a characterisation pass.

**Agent prompt**

```
Role: You are adding offer display and the Offer Summary to the tablet's
order entry (T-07) in the Field Sales Management System.

Context:
- Slice: on the tablet order, every line shows when a promotion set its
  price, free lines added by an offer are marked as such, and an offer
  summary states what applied and saved, what's within reach and what was
  just lost.
- Specs: plan_docs/stories/promotions.md US-007 S1–S6 and glossary (Offer
  Summary); plan_docs/stories/pricing.md US-005 S1, S4;
  plan_docs/stories/rep-at-a-location-tablet.md US-012 S4b, S4d;
  plan_docs/uxdocs/00-conventions-and-shared-elements.md §1.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine order output
  (T-18.1.1), line display and provenance (T-6.4.3), break prompt (T-6.5.1).
- Pattern to follow: T-6.4.3's line display.

Acceptance criteria:
1. A line won by a promotion reads "€9.99 — Autumn promotion (better than
   your tier price €11.20)"; the provenance sheet lists the promotion among
   candidates.
2. Lines added by an offer read "€0.00 — Autumn offer (free)" and are marked
   as not rep-entered.
3. The Offer Summary reads e.g. "2 offers applied — saving €19.70" and
   expands to each offer with its saving.
4. Within reach: "1 more Suncare product for the 6 for €15.00 offer";
   partly qualifying for 3 offers shows "3 offers available on this order",
   expanding.
5. An offer lost by a change is stated with what would restore it.
6. Everything works offline from the snapshot.

Constraints:
- Use the project's existing conventions and test framework.
- Read the engine's output; compute nothing here.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-6.4.3's line and sheet; stop and show them passing, then the summary on a
device. Resume only on "Continue T-18.3.1".

Steps: 1. characterisation tests; 2. line source for promotions; 3. offer
lines; 4. Offer Summary; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-18.3.1-S; do not design
your own.

Definition of done: on the tablet order, every line shows when a promotion
set its price, free lines added by an offer are marked as such, and an offer
summary states what applied and saved, what's within reach and what was just
lost.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: bundle, mix-and-match and spend-threshold specifics
(T-18.4.1, T-18.5.1, T-18.6.1), customer-site offer display (E26).
```

**Checkpoint**

Produces before pausing — characterisation tests of the line and sheet, passing, and the Offer Summary on a device.
Human reviews — Can a rep explain every total change from the summary alone?
Resume trigger — `Continue T-18.3.1`

---

### T-18.4.1-S — Test scenarios for bundles

**Owner** — Human-Led
**Gates** — T-18.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "SPF30 + After Sun + Kids Spray for €20.00", normally €24.70: how is the €20.00 split across the three lines (proportionally? rounding, MI-48)?
- Incomplete with two of three: each line at its own price, summary "Add Kids Spray to complete the bundle — saving €4.70" (S2).
- 2 of each applies twice (S3); 2, 2 and 1 applies once?
- A bundle product becoming Unavailable: "Cannot apply — Kids Spray unavailable" on the list (S4). On an order captured before, does the bundle still apply?
- A product in a bundle and a price promotion: which wins per line?

---

### T-18.4.1 — Price a fixed set of products together as a bundle

**Parent story**

> As a Head Office User, I want to price a fixed set of products together so that a themed group sells as one.
>
> Acceptance criteria:
> - "SPF30 + After Sun + Kids Spray for €20.00", normally €24.70: with all three on the order, the lines total €20.00, marked as the bundle; the summary reads "Bundle applied — saving €4.70" (S1)
> - With two of three: no bundle, each at its own price; "Add Kids Spray to complete the bundle — saving €4.70" (S2)
> - 2 of each applies the bundle twice (S3)
> - Kids Spray Unavailable: the bundle can't complete and the list flags "Cannot apply — Kids Spray unavailable" (S4)

**Slice** — Head office sets up a bundle of named products at a set price; an order holding the full set gets it automatically (repeating), a nearly complete order is told what completes it, and a bundle that can't complete is flagged.
**Spec source** — Promotions US-002 S1–S4
**Depends on** — T-18.1.1, T-18.1.2, T-18.3.1
**Pattern to follow** — T-18.1.1 (engine offers), T-18.1.2 (setup)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — extends the reviewed engine offer framework with a new shape (money); the price split needs a human oracle.

**Provisional commit message**

```
feat(promotions): price a fixed set of products together as a bundle

- A themed group sells as one; the order says what completes it, because
  silence loses a sale the customer would have taken
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new offer shape against a human oracle.

**Agent prompt**

```
Role: You are adding the Bundle promotion shape to the Field Sales Management
System.

Context:
- Slice: head office sets up a bundle of named products at a set price; an
  order holding the full set gets it automatically (repeating), a nearly
  complete order is told what completes it, and a bundle that can't complete
  is flagged.
- Specs: plan_docs/stories/promotions.md US-002 S1–S4 and glossary (Bundle,
  Repeats).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine offer framework
  (T-18.1.1), setup form (T-18.1.2), Offer Summary (T-18.3.1), availability
  (T-8.1.1).
- Pattern to follow: T-18.1.1's offer evaluation; T-18.1.2's shape fields.

Acceptance criteria:
1. H-24 offers the Bundle shape: named products and a bundle price.
2. With SPF30, After Sun and Kids Spray on the order (normally €24.70) the
   three lines total €20.00, split per the agreed rule, each marked as the
   bundle; the summary reads "Bundle applied — saving €4.70".
3. With two of three: no bundle; "Add Kids Spray to complete the bundle —
   saving €4.70".
4. 2 of each applies it twice.
5. With Kids Spray Unavailable the bundle can't complete and is flagged
   "Cannot apply — Kids Spray unavailable" (listed by T-18.8.1).
6. Bundle lines never take a rep discount (BR-NEW-002 rule 5; enforced in
   E19).

Constraints:
- Use the project's existing conventions and test framework; extend the
  engine through T-18.1.1's framework, not beside it.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond bundle
  fields.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after bundle evaluation passes every agreed scenario in the
engine, stop and show it. Resume only on "Continue T-18.4.1".

Steps: 1. bundle evaluation; 2. price split; 3. setup fields; 4. summary
messages; 5. cannot-apply flag; 6. tests.

Test expectations: implement exactly the scenarios agreed in T-18.4.1-S. You
are forbidden from designing your own test cases.

Definition of done: head office sets up a bundle of named products at a set
price; an order holding the full set gets it automatically (repeating), a
nearly complete order is told what completes it, and a bundle that can't
complete is flagged.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: mix-and-match (T-18.5.1), overlap warnings (T-18.7.1).
```

**Checkpoint**

Produces before pausing — bundle evaluation passing every agreed scenario in the engine.
Human reviews — Is the bundle price split across lines exactly as agreed, to the cent?
Resume trigger — `Continue T-18.4.1`

---

### T-18.5.1-S — Test scenarios for mix and match

**Owner** — Human-Led
**Gates** — T-18.5.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "Any 6 from Category Suncare for €15.00" with 8 eligible items from €2.20 to €4.80: the 6 highest-priced go in (S1). Highest by what — resolved line price at quantity, or unit price? Ties?
- 12 eligible: twice (S2); 5 eligible: "1 more Suncare product for the 6 for €15.00 offer" (S3).
- Eligible set by Range, Category or product list, resolved at order time (S4).
- Adding a higher-priced eligible item reshuffles the set and the summary explains it (S5).
- The €15.00 split across six lines (MI-48).

---

### T-18.5.1 — Offer any N from a set for a price, best for the customer

**Parent story**

> As a Head Office User, I want "any N from this set for a price" so that customers can choose within an offer.
>
> Acceptance criteria:
> - "Any 6 from Category Suncare for €15.00" with 8 eligible items from €2.20 to €4.80 puts the 6 highest-priced in at €15.00 total; the other 2 keep their prices (S1)
> - 12 eligible applies it twice at €15.00 each (S2)
> - 5 eligible: no offer; "1 more Suncare product for the 6 for €15.00 offer" (S3)
> - The set can be a Range, a Category or a product list, resolved at order time (S4)
> - Adding a higher-priced eligible item moves it into the set, a cheaper one leaves, and the summary explains the change (S5)

**Slice** — Head office sets "any N from a set for a price"; orders with enough eligible items get it automatically with the highest-priced items in the set, repeating, and the summary says how many more are needed or why a line left the set.
**Spec source** — Promotions US-003 S1–S5
**Depends on** — T-18.1.1, T-18.1.2, T-18.3.1
**Pattern to follow** — T-18.4.1 (new shape on the framework)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — money; best-for-the-customer selection and reshuffles need a human oracle.

**Provisional commit message**

```
feat(promotions): offer any n from a set for a price

- The highest-priced eligible items go into the set, matching best price
  wins; a reshuffle is explained at order level rather than line by line
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new offer shape against a human oracle.

**Agent prompt**

```
Role: You are adding the Mix and Match promotion shape to the Field Sales
Management System.

Context:
- Slice: head office sets "any N from a set for a price"; orders with enough
  eligible items get it automatically with the highest-priced items in the
  set, repeating, and the summary says how many more are needed or why a
  line left the set.
- Specs: plan_docs/stories/promotions.md US-003 S1–S5, glossary (Mix and
  Match, Eligible Set, Best-for-the-customer selection).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine offer framework
  and set resolver (T-18.1.1), setup form (T-18.1.2), Offer Summary
  (T-18.3.1).
- Pattern to follow: T-18.4.1.

Acceptance criteria:
1. H-24 offers Mix and Match: eligible set (Range, Category or product list),
   N, and a set price.
2. With 8 eligible Suncare items from €2.20 to €4.80, the 6 highest-priced go
   into the offer at €15.00 total; the other 2 keep their prices.
3. 12 eligible applies it twice.
4. 5 eligible: no offer; the summary reads "1 more Suncare product for the 6
   for €15.00 offer".
5. The set resolves at order time.
6. Adding a higher-priced eligible item moves it in and a cheaper one out;
   the summary explains the change.
7. Mix and Match lines never take a rep discount (enforced in E19).

Constraints:
- Use the project's existing conventions and test framework; extend through
  T-18.1.1's framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond mix and
  match fields.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after evaluation passes every agreed scenario in the engine, stop
and show it. Resume only on "Continue T-18.5.1".

Steps: 1. selection and repeats; 2. price split; 3. setup fields;
4. summary messages; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-18.5.1-S. You
are forbidden from designing your own test cases.

Definition of done: head office sets "any N from a set for a price"; orders
with enough eligible items get it automatically with the highest-priced
items in the set, repeating, and the summary says how many more are needed
or why a line left the set.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: spend thresholds (T-18.6.1).
```

**Checkpoint**

Produces before pausing — mix and match passing every agreed scenario in the engine.
Human reviews — Is the selection always the best result for the customer, including after a reshuffle?
Resume trigger — `Continue T-18.5.1`

---

### T-18.6.1-S — Test scenarios for spend thresholds

**Owner** — Human-Led
**Gates** — T-18.6.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- RC-NEW-006 (MI-25): the threshold discount applies to the order total after rep discounts (assumed) or before? Decide and write it down.
- "Spend €500, get 5% off": subtotal after line offers €520.00 → "Subtotal €520.00 · Spend 500 offer −5% (€26.00) · Total €494.00" (S1); measured after line offers (S2); lost when a bundle drops it to €492 (S3); fixed €25.00 off (S4).
- Qualification is checked on resolved prices before rep discounts (PM007-A): a rep discount never knocks the order under.
- Actuals use gross line value €520.00, with the €26.00 tracked separately (S5).
- Two spend thresholds live at once: only the best applies?

---

### T-18.6.1 — Reward orders that reach a spend threshold

**Parent story**

> As a Head Office User, I want an order reaching a value to get a discount so that larger orders are rewarded without touching line prices.
>
> Acceptance criteria:
> - "Spend €500, get 5% off" on an order of €520.00 after line offers shows "Subtotal €520.00 · Spend 500 offer −5% (€26.00) · Total €494.00" (S1)
> - €520.00 before and €480.00 after line offers doesn't qualify: "€480 after offers — €20 more for 5% off" (S2)
> - Qualifying at €505.00 then a bundle bringing it to €492.00 removes it: "5% off no longer applies — €8 more to qualify again" (S3)
> - €25.00 off rather than a percentage shows the fixed deduction (S4)
> - Per-product actuals use gross line value €520.00; the €26.00 is tracked separately (S5)
> - An order qualifying on resolved prices still qualifies when rep discounts take it below (US-007 PM007-A)

**Slice** — Head office sets "spend €X, get Y% or €Z off"; an order whose subtotal after line offers reaches it gets the discount at order level, told how far off it is or when it's lost, and rep discounts never knock it under.
**Spec source** — Promotions US-004 S1–S5; US-007 PM007-A; BR-NEW-002 rule 6; RC-NEW-006
**Depends on** — T-18.1.1, T-18.3.1
**Pattern to follow** — T-18.1.1 (order-level engine evaluation)
**Ownership** — Impl: Human Tight-Loop ↓ | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — would be Agent-Assisted; downgraded one step because the discount base is unresolved (RC-NEW-006, MI-25) and it changes order totals (money).

**Provisional commit message**

```
feat(promotions): reward orders that reach a spend threshold

- Lines keep the prices the rep quoted; the discount appears once at the
  total, measured after line offers so it reflects what the customer spends
- Qualification is checked before rep discounts, so a discount can never
  cost the customer the threshold offer
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Order-total money rule with an open decision.

**Work package**

Increments:
1. Resolve MI-25 (discount base) and record the rule.
2. Engine: order-level evaluation after line offers; qualification on resolved prices before rep discounts; percentage or fixed amount; best single threshold.
3. Summary messages: applied, within reach ("€20 more for 5% off"), lost ("€8 more to qualify again").
4. Order record: gross line value and the order-level discount stored separately for actuals (E28 reads them).
5. Setup fields on H-24 with the open assumption noted until decided (H24.3).

Decision points:
- MI-25: final total after rep discounts, or pre-discount?
- Several thresholds live at once: best one only?
- Is the discount rounded per order or spread for invoicing (external)?

Delegable slivers:
- **Threshold summary messages** — Show the applied, within-reach and lost messages in the Offer Summary from the engine's order-level output, with the exact wording in Promotions US-004 S1–S3. No evaluation logic.
- **Setup fields** — Add the Spend Threshold shape to H-24 (threshold value; percentage or fixed amount) with the RC-NEW-006 note beside it until decided. No engine changes.

---

### T-18.7.1-S — Test scenarios for the overlap warning

**Owner** — Human-Led
**Gates** — T-18.7.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- SPF30 in "Autumn Deal" at €9.99; a new offer gives €10.50: "SPF30 is in 2 other live promotions. Autumn Deal gives €9.99; this offer gives €10.50, so it won't apply." (S1) — at which quantity and for which customer is the comparison made?
- Different quantity thresholds (30 vs 10): both stated, noting they apply at different quantities (S2).
- Different audiences (Tier A vs all): noted as possibly both applying to different customers (S3).
- No overlap: no warning (S4). Scheduled overlapping offers count?
- Multi-product shapes (bundle, mix and match): how is "which wins" stated?

---

### T-18.7.1 — Warn at setup when a product is already in other promotions

**Parent story**

> As a Head Office User, I want to be told which offer would win when a product is in several so that I don't set up an offer that never fires.
>
> Acceptance criteria:
> - SPF30 in "Autumn Deal" at €9.99, added to a new offer at €10.50: "SPF30 is in 2 other live promotions. Autumn Deal gives €9.99; this offer gives €10.50, so it won't apply." — and I can proceed (S1)
> - Different quantities: both stated, noting each applies at different quantities (S2)
> - Different audiences: noted, both may apply to different customers (S3)
> - No overlap: no warning (S4)

**Slice** — When a product added to a new promotion is already in other live or scheduled promotions, the setup form states which offer would win and whether the new one would ever apply, noting different quantities or audiences, and never blocks saving.
**Spec source** — Promotions US-006 S1–S4; design decision "Overlap is warned about at setup, not prevented"; uxdocs 02 H-24 (H24.2)
**Depends on** — T-18.1.2, T-18.2.1, T-18.4.1, T-18.5.1
**Pattern to follow** — T-18.1.1 (engine comparisons)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — "would it ever apply" is a judgement across quantities and audiences that needs a human oracle; the agent builds the comparison via the engine.

**Provisional commit message**

```
feat(promotions): warn at setup which overlapping offer would win

- An overlap is never an error under best price wins, just possibly
  pointless; only a comparison tells a legitimate overlap from a masked one
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Engine-driven comparisons against a human oracle.

**Agent prompt**

```
Role: You are adding the overlap warning to promotion setup (H-24) in the
Field Sales Management System.

Context:
- Slice: when a product added to a new promotion is already in other live or
  scheduled promotions, the setup form states which offer would win and
  whether the new one would ever apply, noting different quantities or
  audiences, and never blocks saving.
- Specs: plan_docs/stories/promotions.md US-006 S1–S4 and design decision
  "Overlap is warned about at setup, not prevented";
  plan_docs/uxdocs/02-head-office.md H-24 (H24.2 the warning appears under the
  form as soon as a product or set is chosen and never blocks saving).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — engine (T-18.1.1,
  T-18.4.1, T-18.5.1), setup form (T-18.1.2), audiences (T-18.2.1).
- Pattern to follow: compare offers by running the engine, not by a second
  rule.

Acceptance criteria:
1. Adding SPF30 (in "Autumn Deal" at €9.99) to a new offer at €10.50 shows
   "SPF30 is in 2 other live promotions. Autumn Deal gives €9.99; this offer
   gives €10.50, so it won't apply." and saving is still allowed.
2. Offers at different quantities are both stated, noting each applies at
   different quantities.
3. Offers for different audiences are noted as possibly both applying to
   different customers.
4. With no overlap, no warning.
5. The warning appears as soon as a product or set is chosen.

Constraints:
- Use the project's existing conventions and test framework.
- Comparisons run through the engine for representative quantities and
  audiences agreed in the scenarios; no hand-rolled price logic.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the comparison passes every agreed scenario, stop and show
it. Resume only on "Continue T-18.7.1".

Steps: 1. find overlapping offers; 2. engine comparison; 3. sentences;
4. H-24 placement; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-18.7.1-S. You
are forbidden from designing your own test cases.

Definition of done: when a product added to a new promotion is already in
other live or scheduled promotions, the setup form states which offer would
win and whether the new one would ever apply, noting different quantities or
audiences, and never blocks saving.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: list flags (T-18.8.1).
```

**Checkpoint**

Produces before pausing — the overlap comparison passing every agreed scenario.
Human reviews — Does the warning say clearly when a new offer can never fire, and never when it can?
Resume trigger — `Continue T-18.7.1`

---

### T-18.8.1 — Browse promotions by status with their flags

**Parent story**

> As a Head Office User, I want to see live, scheduled and ended promotions with what each is doing so that the trading calendar is visible in one place.
>
> Acceptance criteria:
> - Promotions shows Live by default, with Scheduled and Ended filters, each with shape, audience, period and product count (S1)
> - A bundle with an Unavailable product is flagged "Cannot apply" (S2)
> - An offer beaten on every product by another live offer is flagged "May never apply" (S3)

**Slice** — The promotion list opens on live promotions with their shape, audience, period and product count, filters to scheduled or ended, flags offers that can't or may never apply, and each opens to a detail with End early.
**Spec source** — Promotions US-008 S1–S3; uxdocs 02 H-23 (H23.1 flags as sentences under the row; H23.2 End early on the detail)
**Depends on** — T-18.7.1, T-18.4.1
**Pattern to follow** — T-9.3.1 (list by status)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: M | Confidence: M
  (inferred) — a list reusing reviewed flag logic; Blast Radius Low; Taste Medium (H-23 draft) — treat as Agent-Assisted if H-23 is unconfirmed (MI-61).

**Provisional commit message**

```
feat(promotions): browse the trading calendar with offer flags

- One list shows what is live, coming and finished, with offers that can't
  or may never apply called out rather than discovered later
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A list over reviewed logic.

**Agent prompt**

```
Role: You are building the promotion list (H-23) for the Field Sales
Management System's head office website.

Context:
- Slice: the promotion list opens on live promotions with their shape,
  audience, period and product count, filters to scheduled or ended, flags
  offers that can't or may never apply, and each opens to a detail with End
  early.
- Specs: plan_docs/stories/promotions.md US-008 S1–S3;
  plan_docs/uxdocs/02-head-office.md H-23 (H23.1, H23.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — promotions (T-18.1.2,
  T-18.2.1), cannot-apply check (T-18.4.1), masking comparison (T-18.7.1).
- Pattern to follow: T-9.3.1's list.

Acceptance criteria:
1. Live is the default view; Scheduled and Ended are filters; each row shows
   shape, audience, period and product count.
2. A bundle with an Unavailable product shows "Cannot apply" with its cause
   as a sentence under the row.
3. An offer beaten on every product by another live offer shows "May never
   apply" with its cause.
4. Each row opens a detail page with End early (T-18.2.1's action).

Constraints:
- Use the project's existing conventions and test framework.
- Flags come from the reviewed checks; no new rules.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. list query; 2. filters; 3. flags; 4. detail with End early;
5. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: the promotion list opens on live promotions with their
shape, audience, period and product count, filters to scheduled or ended,
flags offers that can't or may never apply, and each opens to a detail with
End early.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: promotion performance reporting (not designed, MI-41).
```

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | PRO-001 (T-18.1.1, T-18.1.2), PRO-005 (T-18.2.1), PRO-007 (T-18.3.1; PM007-A in T-18.6.1), PRO-002 (T-18.4.1), PRO-003 (T-18.5.1), PRO-004 (T-18.6.1), PRO-006 (T-18.7.1), PRO-008 (T-18.8.1) |
| Every task satisfies the three slice criteria | Pass | 9 of 9 |
| Every task carries a tier with a rationale citing dimensions | Pass | 9 of 9 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-18.6.1 (MI-25), rounding (MI-48), day boundary (MI-53), live edits (MI-26) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-18.3.1; the engine tight-loop task owns its extension of T-6.4.1's vectors |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-18.8.1 only |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-18.1.1, T-18.4.1, T-18.5.1, T-18.6.1, T-18.7.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-18.1.2, T-18.2.1, T-18.3.1, T-18.4.1, T-18.5.1, T-18.7.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-18.1.1, T-18.6.1 (downgraded, stated) |
| Every scenario task precedes the task it gates | Pass | 8 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-25, 26, 48, 53, 61 |
