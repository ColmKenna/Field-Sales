# E19 — Rep discounts and free goods

**Iteration** — 5, Commercial pushes
**Outcome** — A rep can lower a price or give discontinuing stock free on the spot, within limits a manager sets and the tablet enforces offline, with no head office decision; anyone reading the order later sees how the price was reached; and head office sees where a fixed tier price has drifted from base.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Pricing US-007 — Ask for a price override | Should | S2, S3, S4, S5, PR007-A–H (S1's approval wording superseded) |
| 2 | Pricing US-009 — Give a product free of charge | Should | S2, S4 (see MI-60), S6, PR009-A–D (S1, S3, S5 superseded) |
| 3 | Pricing US-002 — See when a fixed tier price has drifted | Should | S1–S3 |
| 4 | Pricing US-008 — Decide a price override | Should (superseded) | No tasks: overrides are applied within the allowance (US-007) and no longer decided by head office |
| 5 | Rep at a Location US-014 — Check a Sent Call or Order (part) | Must | A1014-B only; the rest is in E7, E8 and E13 |

Also carried here, with no story: setting up the commercial policy that holds each allowance (`{{NEEDS ACCEPTANCE CRITERIA}}`, MI-24).

**Exit criterion** — A manager can put products into a commercial policy with a rep discount percentage and a monthly free-of-charge allowance; on Review, offline, the override sheet shows the lowest price the rep can offer before they type, applies prices inside it with a reason and explains when no discount can be added; the free-of-charge picker lists only eligible Discontinuing products with the rep's remaining monthly units and refuses more; the provenance sheet, H-02 and the sent order show rep prices and free lines as facts with their working; and H-20 shows drift on fixed tier prices, sortable.

**Capability-class stamp** — Frontier + extended reasoning for the policy model and the allowance calculation (T-19.1.1, T-19.1.2); Frontier workhorse for other tasks and scenario drafting; Fast mid-tier for T-19.3.1. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [pricing.md](../../stories/pricing.md) (US-002, US-007, US-008, US-009; Requires Clarification 8, 9), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-014 A1014-B), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-002, BR-NEW-003, EC-NEW-001–003, RC-NEW-009), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-07 override sheet and free-of-charge picker; T7.2, T7.9, T7.10, T7.12), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§1 provenance sheet with a rep discount), [02-head-office.md](../../uxdocs/02-head-office.md) (H-02 H2.8, H-20 H20.2), [handover.md](../../uxdocs/handover.md) §6.4, §6.5.

---

### T-19.1.1-S — Test scenarios for the commercial policy

**Owner** — Human-Led
**Gates** — T-19.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- RC-NEW-009 (MI-24): is the commercial policy a new entity, an extension of the one-per-product Product Profile, or explicit business rules? The answer decides what the scenarios talk about.
- Can a product be in more than one policy? If so, which allowance applies (the largest, the smallest, or it's refused)?
- Is the allowance per rep, per policy, or per rep per policy — "12 FOC units per rep per month" across all products in the policy, or per product?
- Who is a "manager" here (MI-59's role question applies again)?
- A change to a policy mid-month: does the rep's used FOC carry over? Does it reach tablets only at the next sync?
- A product leaving a policy with an unsent order holding a rep price on it: judged as captured (BR-NEW-006)?
- Month boundaries in the business time zone (MI-53).

---

### T-19.1.1 — Set up commercial policies that hold rep discount and free-goods allowances

**Parent story**

> As a Field Salesperson, I want to apply a lower price with a reason, within my discount allowance, so that I can close a sale on the spot.
>
> Acceptance criteria:
> - A manager groups eligible products into a commercial policy such as "Allow 10% rep discount and 12 FOC units per rep per month" (BR-NEW-002, BR-NEW-003 rule 2)
> - Products outside a policy can't be discounted or given free by the rep (BR-NEW-002, BR-NEW-003 rule 3)
> - The tablet snapshot carries the applicable policy rules, product membership, and the rep's month-to-date FOC use (BR-NEW-002 Data; BR-NEW-003 Data)
> - {{NEEDS ACCEPTANCE CRITERIA}} for setting up and changing a commercial policy (MI-24)

**Slice** — A manager creates a commercial policy with a rep discount percentage and a monthly free-of-charge unit allowance per rep, puts products into it, and at the next sync each rep's tablet holds the policies, their product membership and the rep's free-of-charge units used so far this month.
**Spec source** — BR-NEW-002 (opening paragraph, Data); BR-NEW-003 rules 2, 3 and Data; RC-NEW-009; Pricing Requires Clarification 9; uxdocs 01 T7.9 "Policy scope — working model"; handover §6.4 item 8
**Depends on** — T-1.1.1, T-1.2.1, T-4.1.1
**Pattern to follow** — T-6.1.1 (head office pricing pages), T-4.1.1 (snapshot)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — no story and no screen exist, and the entity itself is unsettled (RC-NEW-009, MI-24): Ambiguity High; Blast Radius High (every rep price and free line depends on it); snapshot contract change.

**Provisional commit message**

```
feat(pricing): hold rep discount and free-goods allowances in commercial policies

- Limits are enforced at capture rather than decided by head office, so
  the tablet needs the rules and each rep's month-to-date use offline
- Kept apart from the catalogue's product profile until the two models
  are reconciled
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
An unsettled data model that every discount depends on.

**Work package**

Increments:
1. Resolve RC-NEW-009 and write the missing story with acceptance criteria (MI-24), replacing `{{NEEDS ACCEPTANCE CRITERIA}}`; name the entity.
2. Policy record: name, rep discount percentage, monthly free-of-charge units per rep, product membership; who changed it and when.
3. Management screen (undesigned): list, create, edit, add and remove products; the note that the effective maximum discount is nearly twice the allowance (BR-NEW-002 note).
4. Month-to-date free-of-charge use per rep, counted from captured order lines that still exist (BR-NEW-003 rule 5).
5. Snapshot: policies, membership for products in the rep's snapshot, and the rep's month-to-date use, through T-4.1.1's versioning.

Decision points:
- RC-NEW-009: new entity, extension of the Product Profile, or rules.
- Overlapping membership and which allowance wins.
- Whether the allowance can vary per rep (per rep override) or only per policy.
- Where the screen sits in head office navigation and who may use it.

Delegable slivers:
- **Month-to-date counter** — Once the model is fixed, implement a query returning a rep's free-of-charge units used this calendar month from order lines that still exist, in the business time zone. No UI.
- **Policy list page** — Once the entity and screen are agreed, build the read-only list of policies with their percentage, monthly units and product count, following T-6.1.1's list. No editing.

---

### T-19.1.2-S — Test scenarios for the override sheet and the lowest price a rep can offer

**Owner** — Human-Led
**Gates** — T-19.1.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- The four states against the handover example data, each to the cent:
  - stacks: Hand Cream tier €4.00, Spring offer €3.80 (5% off), 10% allowance → lowest €3.42 (PR007-D)
  - falls back: SPF30 ×24, Autumn €9.99 (11% off), break €10.08 → lowest €9.07, "10% off the bulk price" (PR007-E, EC-NEW-001)
  - blocked by promotion: SPF30 ×8, Autumn €9.99, tier €11.20 → no price field, Close (PR007-F)
  - blocked by multi-buy: Sudocrem in buy 3 get 1 free → no price field, Close (PR007-G, EC-NEW-002)
- "Promotion larger than the allowance" is measured as head office set it, against tier or list (rule 4) — which one when both exist?
- Rounding of the lowest price (MI-48): down to the cent, or to the nearest?
- A product not in any policy: does the sheet open and explain, or is the control absent?
- Quantity changes after a rep price is applied (e.g. 24 → 8 loses the break): does the rep price stay, get re-checked, or get removed?
- A promotion that ends between capture and sync, or a policy that changes: the price is judged as captured (BR-NEW-006).
- Removing the override before sync returns the line to its resolved price (S5).
- The spend threshold is checked before rep discounts (BR-NEW-002 rule 6; T-18.6.1).

---

### T-19.1.2 — Apply a rep price on Review, within the allowance, with the lowest price shown first

**Parent story**

> As a Field Salesperson, I want to apply a lower price with a reason, within my discount allowance, so that I can close a sale on the spot.
>
> Acceptance criteria:
> - The sheet shows the resolved price with its source and the lowest price I can offer before I type anything (PR007-A)
> - A price no lower than the lowest, with a reason, is applied immediately, with no head office decision (PR007-B); below it, Apply is unavailable (PR007-C)
> - An upward price is rejected with "An override can only be lower than the resolved price" (S2); no reason is rejected with "Give a reason for the override" (S3)
> - A promotion no larger than the allowance: the lowest is the allowance off the promotion price (PR007-D)
> - A promotion larger than the allowance but above the allowance off the tier or break price: the lowest is that, named ("10% off the bulk price") (PR007-E)
> - A promotion lower than anything the allowance reaches: no price field, an explanation and Close (PR007-F)
> - A buy X get Y, bundle or mix-and-match line: named, no rep discount, no price field (PR007-G)
> - Offline, the price is saved with the order and uploads at the next sync (S4); removing it before sync returns the resolved price (S5)
> - The provenance sheet shows an applied price as a calculation: base or promotion price, the discount and the reason (PR007-H)

**Slice** — On Review, the rep opens a line's override sheet and sees the lowest price they can offer under their policy and the line's promotions before typing; a price inside it with a reason is applied at once offline, anything below can't be applied, a blocked line explains why in a sentence, and the provenance sheet shows the working.
**Spec source** — Pricing US-007 S2–S5, PR007-A–H; BR-NEW-002 rules 1–6; EC-NEW-001, EC-NEW-002; uxdocs 01 T-07 price override sheet (four frames), T7.2, T7.9, T7.12; uxdocs 00 §1 provenance sheet with a rep discount
**Depends on** — T-19.1.1, T-18.1.1, T-18.4.1, T-18.5.1, T-6.3.1, T-6.4.3, T-5.1.1
**Pattern to follow** — T-6.4.1 (shared engine code and test vectors), T-6.4.3 (provenance sheet)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — money rule with four interacting cases (Edge-Case Discovery High); modifies the provenance sheet; the calculation must match on tablet and server.

**Provisional commit message**

```
feat(pricing): apply rep prices within the allowance on review

- A guardrail replaces head office approval: the rep can't promise a
  price that would later be refused
- The lowest price is shown before typing, and a blocked line says why,
  because the rep is usually answering "can you do better?"
- Falling back to the allowance off the bulk price keeps promotions from
  ever lowering the best price a rep can reach
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The allowance rule interacts with every price source.

**Work package**

Increments:
1. Allowance calculator in the shared engine: from a line's candidates (T-6.4.1, T-18.1.1) and the policy, return one of four states — stacks, falls back, blocked by promotion, blocked by multi-buy — with the lowest price and its named base. Test vectors from T-19.1.2-S.
2. Line record: rep price, the discount applied, the base it was taken from, reason; stored with the order and uploaded like any line; the server re-validates against the policy as captured (BR-NEW-006).
3. Characterisation tests pinning T-6.4.3's line display and provenance sheet.
4. Override sheet on Review in the four T-07 frames, including the blocked states with Close only (T7.12).
5. Line and provenance: "Your price" with the calculation and reason (uxdocs 00 §1).
6. Remove before sync; quantity-change handling per the agreed scenario.

Decision points:
- The quantity-change behaviour above.
- What the sheet does for a product in no policy.
- Whether the server rejects, strips or accepts a rep price that doesn't validate (it should never happen; decide the safety net).

Delegable slivers:
- **Sheet frames** — Given the calculator's output (state, lowest price, base name, promotion name), render the four T-07 frames exactly as drawn in plan_docs/uxdocs/01-tablet-day.md, with Apply unavailable below the lowest price and the S2/S3 messages. No calculation.
- **Provenance working** — Show an applied rep price in the provenance sheet as "Your price" with "Spring offer EUR 3.80, less 10% rep discount" and the quoted reason, above ALSO CONSIDERED, per plan_docs/uxdocs/00-conventions-and-shared-elements.md §1. Characterisation tests first.

---

### T-19.2.1-S — Test scenarios for free-of-charge lines

**Owner** — Human-Led
**Gates** — T-19.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-60: is a reason required for a free-of-charge line? PR-009 S4 says yes ("Give a reason for the free goods") and T7.2 says both need a reason; the superseded note removes "free-text reason" and the T-07 picker has no reason field.
- Balance offline across several orders in a day: "12 free units left this month - 0 used today" — derived from the snapshot plus local lines (BR-NEW-003 Data). Two unsent orders adding 8 and 6: the second is refused?
- Reducing or deleting a free line releases units immediately on the tablet (rule 5).
- Removal at the cut-off (Unavailable Line path, BR-NEW-001): the units come back at the next sync (rule 5, EC-NEW-003).
- The month changes between capture and sync: whose month do the units count in (MI-53)?
- A product that stops being Discontinuing (e.g. goes to Run-out, or Unavailable) after the snapshot: still offered in the picker until sync?
- Run-out with Remaining 120 and 6 free: Remaining falls to 114 on acceptance (S2) — the line counts like any other against Remaining.
- Actuals: a free line contributes €0.00 while its quantity still leaves the warehouse (S6).

---

### T-19.2.1 — Add discontinuing products free of charge within the rep's monthly allowance

**Parent story**

> As a Field Salesperson, I want to add a discontinuing product at no charge, within my monthly allowance, so that I can move the last of it.
>
> Acceptance criteria:
> - The picker lists only products in the Discontinuing state and states that only discontinuing products can be given free (PR009-A)
> - It shows my remaining free-of-charge allowance for this calendar month (PR009-B)
> - A quantity that would exceed the remaining allowance can't be added (PR009-C)
> - Within the allowance, the line is applied with no head office decision (PR009-D)
> - "Kids SPF50 Spray 150ml" in Run-out with Remaining 120: adding 6 free leaves Remaining 114 on acceptance (S2)
> - An FOC line with no reason is rejected with "Give a reason for the free goods" (S4 — see MI-60)
> - An accepted FOC line contributes €0.00 to actuals while its quantity leaves the warehouse (S6)

**Slice** — From Review, the rep opens the free-of-charge picker, which lists only eligible Discontinuing products and the units they have left this month; a quantity within that is added as an ordinary €0.00 line, anything over is refused, and removing or losing the line gives the units back.
**Spec source** — Pricing US-009 S2, S4, S6, PR009-A–D, superseded note; BR-NEW-003 rules 1–5; EC-NEW-003; uxdocs 01 T-07 free-of-charge picker, T7.2, T7.10
**Depends on** — T-19.1.1, T-10.2.1, T-10.3.1, T-8.4.1, T-5.1.1
**Pattern to follow** — T-19.1.2 (Review sheets), T-8.4.1 (removal at the cut-off)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — an allowance counted across offline orders and returned by server-side removal (Edge-Case Discovery High); the line itself is ordinary.

**Provisional commit message**

```
feat(pricing): give discontinuing stock free within a monthly allowance

- Free goods exist to clear discontinuing stock, so only those products
  are offered, and the cap is enforced at capture instead of approved later
- A free item is an ordinary line at zero price, so stock, availability
  and despatch need no special case
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A bounded feature with a human oracle for the balance.

**Agent prompt**

```
Role: You are adding free-of-charge lines to the tablet's Review screen in the
Field Sales Management System.

Context:
- Slice: from Review, the rep opens the free-of-charge picker, which lists
  only eligible Discontinuing products and the units they have left this
  month; a quantity within that is added as an ordinary €0.00 line, anything
  over is refused, and removing or losing the line gives the units back.
- Specs: plan_docs/stories/pricing.md US-009 (S2, S4, S6, PR009-A–D and the
  superseded note); plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-003
  and EC-NEW-003; plan_docs/uxdocs/01-tablet-day.md T-07 free-of-charge
  picker, T7.2, T7.10.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — policy and month-to-date
  use in the snapshot (T-19.1.1), Discontinuing state (T-10.2.1), Run-out
  Remaining (T-10.3.1), removal at the cut-off (T-8.4.1), Review (T-5.1.1),
  override sheet (T-19.1.2).
- Pattern to follow: T-19.1.2's Review sheets.

Acceptance criteria:
1. "Add free-of-charge line" on Review opens the picker headed "Only products
   being discontinued can be given free.", listing only Discontinuing products
   in one of the rep's policies.
2. The picker shows the remaining monthly units, e.g. "12 free units left this
   month - 0 used today", derived from the snapshot plus local lines.
3. A quantity over the remaining units can't be added.
4. Within it, the line is added at €0.00, marked free of charge, with no head
   office decision.
5. Reducing or deleting the line releases its units at once; a line removed
   at the cut-off returns its units at the next sync.
6. The line behaves like any other for stock, availability, Remaining and
   despatch; it contributes €0.00 to actuals.
7. Reason: as agreed for MI-60 in T-19.2.1-S.

Constraints:
- Use the project's existing conventions and test framework.
- A free line is an ordinary order line whose price is €0.00; add no separate
  stock or fulfilment path.
- The server re-checks the allowance as captured (BR-NEW-006).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond marking
  a line free of charge.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the balance calculation passes every agreed scenario
(offline, several orders, release, return at sync), stop and show it. Resume
only on "Continue T-19.2.1".

Steps: 1. balance from snapshot plus local lines; 2. picker; 3. add and
refuse; 4. release and return; 5. server check; 6. tests.

Test expectations: implement exactly the scenarios agreed in T-19.2.1-S. You
are forbidden from designing your own test cases.

Definition of done: from Review, the rep opens the free-of-charge picker,
which lists only eligible Discontinuing products and the units they have left
this month; a quantity within that is added as an ordinary €0.00 line,
anything over is refused, and removing or losing the line gives the units
back.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: head office reporting of free goods by rep or period (not
designed; Pricing Requires Clarification 8, MI-27).
```

**Checkpoint**

Produces before pausing — the balance calculation passing every agreed scenario.
Human reviews — Does the remaining allowance always match what the server will count, across offline orders, removals and the month boundary?
Resume trigger — `Continue T-19.2.1`

---

### T-19.3.1 — Show drift on fixed tier prices against the current base

**Parent story**

> As a Head Office User, I want each fixed tier price shown against the current base so that I can see where a base rise has quietly made a deal more generous.
>
> Acceptance criteria:
> - Tier B prices SPF30 at a fixed €10.50 set when base was €12.50; base rises to €13.20; the row shows "€10.50 — now 20% off (was 16% when set)" (S1)
> - Percentage-based rows show no drift indicator, since they track base (S2)
> - Sorting the tier's products by drift puts the most divergent fixed prices first (S3)

**Slice** — On a tier's detail page, every fixed price shows as a sentence how far below the current base it now sits against when it was set, percentage rows show nothing, and the list sorts by drift, most first.
**Spec source** — Pricing US-002 S1–S3; uxdocs 02 H-20 (H20.2 drift shown only on fixed prices, as a sentence, with sort by drift most-first)
**Depends on** — T-6.1.1, T-6.1.2, T-1.3.1
**Pattern to follow** — T-6.1.2 (tier detail rows)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — read-only derived display with fully specified criteria; Blast Radius Low.

**Provisional commit message**

```
feat(pricing): show drift on fixed tier prices

- A base rise quietly deepens every fixed deal; showing the gap as it is
  now against when it was set makes that visible without an audit
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A derived display.

**Agent prompt**

```
Role: You are adding drift indicators to the price tier detail page (H-20) of
the Field Sales Management System's head office website.

Context:
- Slice: on a tier's detail page, every fixed price shows as a sentence how
  far below the current base it now sits against when it was set, percentage
  rows show nothing, and the list sorts by drift, most first.
- Specs: plan_docs/stories/pricing.md US-002 S1–S3;
  plan_docs/uxdocs/02-head-office.md H-20 (H20.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — tier detail (T-6.1.1,
  T-6.1.2), dated base prices (T-1.3.1).
- Pattern to follow: T-6.1.2's tier detail rows.

Acceptance criteria:
1. A fixed €10.50 set when base was €12.50, with base now €13.20, reads
   "€10.50 — now 20% off (was 16% when set)".
2. Percentage rows show no drift indicator.
3. "Sort: Drift, most first" puts the most divergent fixed prices first.

Constraints:
- Use the project's existing conventions and test framework.
- Take the base when set from the product's dated base price history at the
  fixed price's effective date. If that history isn't kept, stop and ask
  before adding any storage.
- Percentages round to whole numbers as in the example.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. drift calculation; 2. row sentence; 3. sort; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: on a tier's detail page, every fixed price shows as a
sentence how far below the current base it now sits against when it was
set, percentage rows show nothing, and the list sorts by drift, most first.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: alerts on drift, drift on customer pages.
```

---

### T-19.5.1-S — Test scenarios for rep prices and free lines as facts of the order

**Owner** — Scenario Review
**Gates** — T-19.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing rep prices and free-of-charge lines
as facts on H-02 and the tablet's sent order (task T-19.5.1). Read
plan_docs/stories/rep-at-a-location-tablet.md US-014 A1014-B and its
superseded note, plan_docs/uxdocs/02-head-office.md H-02 (H2.8), and
plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-002. Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning T-7.1.2's H-02 lines and
T-7.5.1's sent item. Then cover a rep price stacked on a promotion, a rep
price on the bulk price, a free line, a free line removed at the cut-off.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-19.5.1 — Show rep prices and free lines as facts on H-02 and the sent order

**Parent story**

> As a Field Salesperson, I want to open anything I've sent and see its status and details so that I know it arrived and what happened to it.
>
> Acceptance criteria:
> - A rep price or free-of-charge line on the sent item is shown as a fact of the order ("SPF30 at your price €9.25"), not as the outcome of a request (A1014-B)
> - On H-02, an override line shows its working: offer price, rep discount and reason on one line (H2.8)

**Slice** — On head office's order record and on the rep's sent order, a line with a rep price shows its working (base or promotion price, discount, reason) and a free line shows as free, both as facts of the order with no approval status.
**Spec source** — Rep at a Location US-014 A1014-B and superseded note; uxdocs 02 H-02 (H2.8); Pricing US-008 (superseded — nothing to decide)
**Depends on** — T-19.1.2, T-19.2.1, T-7.1.2, T-7.5.1
**Pattern to follow** — T-7.1.2 (H-02 lines), T-7.5.1 (sent item)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies two existing record views (characterisation first); wording is specified.

**Provisional commit message**

```
feat(orders): show rep prices and free lines as facts of the order

- Nothing is requested any more, so the record states what was agreed
  and how, for anyone querying it later
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Two record views behind a characterisation pass.

**Agent prompt**

```
Role: You are showing rep prices and free-of-charge lines on order records in
the Field Sales Management System.

Context:
- Slice: on head office's order record and on the rep's sent order, a line
  with a rep price shows its working (base or promotion price, discount,
  reason) and a free line shows as free, both as facts of the order with no
  approval status.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-014 A1014-B and its
  superseded note; plan_docs/uxdocs/02-head-office.md H-02 (H2.8).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — rep price and free line
  records (T-19.1.2, T-19.2.1), H-02 (T-7.1.2), sent item (T-7.5.1).
- Pattern to follow: T-7.1.2's H-02 lines.

Acceptance criteria:
1. On the tablet's sent item, a rep price reads as a fact, e.g. "SPF30 at your
   price €9.25", with no request or outcome status.
2. A free line reads as free of charge on the sent item.
3. On H-02, a rep-priced line shows offer or base price, rep discount and
   reason on one line.
4. On H-02, a free line shows as free of charge with its quantity.
5. Nothing on either view offers to accept or decline a price.

Constraints:
- Use the project's existing conventions and test framework.
- Read the stored line; compute nothing.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
H-02's lines and the sent item; stop and show them passing. Resume only on
"Continue T-19.5.1".

Steps: 1. characterisation tests; 2. sent item; 3. H-02 line; 4. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-19.5.1-S; do not design
your own.

Definition of done: on head office's order record and on the rep's sent
order, a line with a rep price shows its working (base or promotion price,
discount, reason) and a free line shows as free, both as facts of the order
with no approval status.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: free-goods reporting (MI-27).
```

**Checkpoint**

Produces before pausing — characterisation tests of H-02's lines and the sent item, passing.
Human reviews — Do the tests pin current behaviour, including lines without rep prices?
Resume trigger — `Continue T-19.5.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | PR-007 (T-19.1.2), PR-009 (T-19.2.1), PR-002 (T-19.3.1), PR-008 (superseded, no tasks), A1-014 A1014-B (T-19.5.1); policy setup with no story (T-19.1.1) |
| Every task satisfies the three slice criteria | Pass | 5 of 5 |
| Every task carries a tier with a rationale citing dimensions | Pass | 5 of 5 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-19.1.1 (MI-24, RC-NEW-009), T-19.2.1 (MI-60), rounding (MI-48), month boundary (MI-53) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-19.1.2 (increment 3), T-19.5.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-19.3.1 only |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-19.1.1, T-19.1.2, T-19.2.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-19.2.1, T-19.5.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-19.1.1, T-19.1.2 |
| Every scenario task precedes the task it gates | Pass | 4 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, `{{NEEDS ACCEPTANCE CRITERIA}}` (T-19.1.1), MI-24, 27, 48, 53, 60 |
