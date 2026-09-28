# E28 — Seeing performance

**Iteration** — 9, Targets and performance
**Outcome** — Every accepted order counts, once and permanently, for the rep who held its shop at the moment of acceptance; a rep sees how they're doing against their own targets and nothing else; a manager sees who is behind first, and who won business that counts elsewhere; and head office's Large flag compares an order with what that shop is expected to buy.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Targets & Performance US-004 — See my targets and how I'm doing | Must | S1, S2, S3b, S4–S6, TP004-A–F (S3's ordering superseded) |
| 2 | Targets & Performance US-005 — Watch the team against target | Must | S1–S5, TP005-A–E |
| 3 | Targets & Performance US-006 — See what a rep captured, not just what counts to them | Should | S1–S3 |
| 4 | Targets & Performance US-007 — Supply the Large Baseline | Should (Draft) | S1–S3 |
| 5 | Head Office US-002 — See why an order is flagged (part) | Must | S1's Large sentence, S3, S4; the rest is in E7, E10 and E21 |

Also measured here: Targets & Performance US-003 S3, S4 (chain roll-up as branches join or close). Not delivered: Coverage Management US-006 S4's "performance reporting is flagged that brand totals overlap" — no brand performance report is designed (MI-41).

**Exit criterion** — Actuals are the gross line value and units of accepted orders, per Location. Each is attributed to the For Location's Primary Rep at acceptance and never moves; spend-threshold discounts are tracked apart; rejected and cancelled orders never count; and a chain target rolls up its branches over time. On R-03, a rep sees "€26,400 of €40,000 · 66% · 5 weeks left" with a neutral pace marker and the unfulfilled note under it, their Range targets, and a Location breakdown with lowest attainment first and untargeted Locations apart. On M-13, a manager sees reps furthest behind pace first (or by € behind), a By Range view, reps without targets listed apart, and Attributed and Captured labelled as different measures. An order much larger than its Location's or Range's expected pace is annotated Large, and nothing is flagged where there's no baseline.

**Capability-class stamp** — Frontier + extended reasoning for actuals and attribution (T-28.1.1); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [targets-and-performance.md](../../stories/targets-and-performance.md) (US-003–US-007, glossary, design decisions, Requires Clarification 1–4, 7), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-002 S1, S3, S4), [03-rep-planner.md](../../uxdocs/03-rep-planner.md) (R-03 R3.1–R3.5), [05-manager.md](../../uxdocs/05-manager.md) (M-13 M13.1–M13.3), [02-head-office.md](../../uxdocs/02-head-office.md) (H-01 disposition table: Large is Annotate).

---

### T-28.1.1-S — Test scenarios for actuals and attribution

**Owner** — Human-Led
**Gates** — T-28.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Actuals = gross line value and units on Accepted orders in the period, per Location; which date places an order in a period — acceptance (the cut-off) or capture?
- Attribution: the For Location's Primary Rep at acceptance, from Assignment History; never moves. An order accepted at 4pm on the day a reassignment takes effect?
- A master's order for a branch counts at the branch (US-004 S6); self-service orders have no Capturing Rep but still attribute (US-006 S3).
- Spend threshold: €520.00 gross with a €26.00 discount counts €520.00 (US-004 S3b).
- Lines removed at the cut-off (Unavailable) don't count; free-of-charge lines count €0.00 and their units? Rep prices count at the captured price.
- Staff changes to accepted orders (MI-10), rejections and cancellations: never count.
- Unfulfilled note: the value on orders not fully despatched, never subtracted (US-004 S4).
- Chain roll-up: a branch added in November counts from then; a branch Closed keeps earlier orders and shows labelled Closed (US-003 S3, S4).
- Inherited visits are excluded from the new rep's performance until they close (Requires Clarification 7) — does that affect sales actuals at all (it says attribution is unchanged)?

---

### T-28.1.1 — Count accepted orders for the rep who held the shop at acceptance

**Parent story**

> As a Field Salesperson, I want my own targets with progress and a per-Location breakdown so that I know where I stand and where to push.
>
> Acceptance criteria:
> - An order with gross line value €520.00 and a 5% spend-threshold discount of €26.00 counts €520.00 in my actuals (S3b)
> - €1,240 on orders not fully despatched gives "€1,240 of this is on orders not yet fully despatched" and the figure is unchanged (S4)
> - Another rep's chain order for a Location I hold counts towards my actuals against that Location (S6)
> - A self-service order for a Location Colm holds counts in his attributed actuals with no Capturing Rep (US-006 S3)
> - A chain target measures the head office and all branches; a branch added mid-period counts from then; a closed branch keeps its earlier orders, labelled Closed (US-003 S1, S3, S4)

**Slice** — Each accepted order's gross line value and units are recorded per For Location and attributed to the rep who was its Primary Rep at acceptance, permanently, with order-level discounts kept apart, unfulfilled amounts known, and chain targets measured across the branches that belonged to the chain at each order's date.
**Spec source** — Targets & Performance glossary (Gross line value, Actuals, Unfulfilled Note, Attributed Rep, Capturing Rep, Chain Roll-up), design decisions "Actuals on acceptance…", "Attribution is historical…"; US-003 S1, S3, S4; US-004 S3b, S4, S6; US-006 S3; Requires Clarification 7
**Depends on** — T-7.1.1, T-7.2.1, T-7.3.1, T-3.1.1, T-18.6.1, T-24.2.1, T-26.4.2, T-27.1.1, T-27.3.1
**Pattern to follow** — T-3.1.1 (resolving ownership from Assignment History at a point in time)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — every performance figure rests on it, and money counted wrong is invisible until someone reconciles (Blast Radius High, Edge-Case Discovery High).

**Provisional commit message**

```
feat(performance): count accepted orders for the rep who held the shop

- A sale belongs to the shop's rep at acceptance and never moves, so a
  rep taking over a shop neither inherits nor loses history
- Actuals count on acceptance because that's what the rep controls; the
  unfulfilled amount is stated, never subtracted
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The measurement every view depends on.

**Work package**

Increments:
1. Agree the period date (acceptance assumed) and the treatment of removed lines, free-of-charge lines and staff changes (MI-10).
2. Actuals record written at acceptance (T-7.1.1): per order line, For Location, gross value, units, Attributed Rep (from Assignment History at that moment), Capturing Rep (or none), Ordered By; order-level discount kept separately (T-18.6.1).
3. Exclusions: rejected and cancelled orders never produce actuals.
4. Unfulfilled amount per subject from despatch state (T-7.3.1).
5. Target measurement: Rep, Rep–Range (Range membership at order date, or product), Location, and chain roll-up with branch membership over time.
6. A reconciliation query tying actuals to accepted orders for audit.

Decision points:
- Period date.
- Range membership for Rep–Range targets: at order date or now?
- Whether actuals are materialised or computed.

Delegable slivers:
- **Unfulfilled note query** — Given actuals and despatch state, compute the value on orders not yet fully despatched for any subject and period. No attribution logic.
- **Reconciliation report** — Produce a query listing accepted orders in a period whose actuals don't sum to their gross line value, for audit. No production writes.

---

### T-28.2.1-S — Test scenarios for the rep's performance view

**Owner** — Scenario Review
**Gates** — T-28.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the rep's performance view (task T-28.2.1).
Read plan_docs/stories/targets-and-performance.md US-004 S1, S2, S4, S5 and
TP004-A–F, and plan_docs/uxdocs/03-rep-planner.md R-03 (R3.1–R3.5). Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover the story scenarios, then derivable edges: a units-only
target, both value and units, a period not yet started, a Location target on
a chain master in the breakdown. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-28.2.1 — Show a rep their own targets, progress and Location breakdown

**Parent story**

> As a Field Salesperson, I want my own targets with progress and a per-Location breakdown so that I know where I stand and where to push.
>
> Acceptance criteria:
> - Q4 target €40,000 with €26,400 attributed: "€26,400 of €40,000 · 66% · 5 weeks left" (S1)
> - A Summer 2027 target of €12,000 is listed under my own targets with its own progress, with no team figure anywhere (S2)
> - The unfulfilled note sits under the headline and the figure is unchanged (S4; R3.1)
> - No target: "No target set for this period" with actuals still shown (S5)
> - Targeted Locations lowest attainment first; untargeted Locations below under "No target set" with actual and order count; filters All, With targets, No target (TP004-A–C)
> - A labelled straight-line pace marker with exact percentage and time remaining; no "off track", warning colour or reordering from it; no pacing model (TP004-D–F)

**Slice** — On the rep website, R-03 shows the rep their own period targets with a progress bar, a neutral pace marker and the unfulfilled note under the headline, each Range target separately, and a Location breakdown ordered by lowest attainment with untargeted Locations apart — and nothing about the team.
**Spec source** — Targets & Performance US-004 S1, S2, S4, S5, TP004-A–F; design decision "Reps see only their own targets"; uxdocs 03 R-03 (R3.1–R3.5); Requires Clarification 2 (website only, MI-37)
**Depends on** — T-28.1.1, T-27.1.1, T-27.2.1, T-13.2.1
**Pattern to follow** — T-13.2.1 (rep website pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — reads reviewed actuals; R-03 is settled; restrictions (no team figure) are stated.

**Provisional commit message**

```
feat(performance): show a rep their own targets and progress

- One clear answer to "how am I doing?", with the unfulfilled amount
  right under the number it qualifies
- The pace marker is a neutral reference, never a verdict, because some
  targets only move near the end of the period
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A settled read screen with a review pause.

**Agent prompt**

```
Role: You are building the rep performance page (R-03) on the Field Sales
Management System's rep website.

Context:
- Slice: on the rep website, R-03 shows the rep their own period targets with
  a progress bar, a neutral pace marker and the unfulfilled note under the
  headline, each Range target separately, and a Location breakdown ordered by
  lowest attainment with untargeted Locations apart — and nothing about the
  team.
- Specs: plan_docs/stories/targets-and-performance.md US-004 S1, S2, S4, S5,
  TP004-A–F and design decision "Reps see only their own targets";
  plan_docs/uxdocs/03-rep-planner.md R-03 (R3.1–R3.5).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — actuals and
  measurement (T-28.1.1), targets (T-27.1.1, T-27.2.1), rep website
  (T-13.2.1).
- Pattern to follow: T-13.2.1.

Acceptance criteria:
1. Headline: "€26,400 of €40,000 · 66% · 5 weeks left", a progress bar with
   a labelled straight-line pace marker, and the unfulfilled note directly
   beneath.
2. Being behind the marker changes no colour, wording or order.
3. Each Range or Product target is listed with its own progress.
4. No target: "No target set for this period" with actuals shown.
5. Location breakdown: targeted Locations by lowest attainment first;
   untargeted under "No target set" with actual and accepted order count, no
   percentage; filters All, With targets, No target.
6. No team figure and no Captured figure anywhere.

Constraints:
- Use the project's existing conventions and test framework.
- Read measurements from T-28.1.1; compute none here.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the page renders against seeded actuals, stop and show it.
Resume only on "Continue T-28.2.1".

Steps: 1. headline and bar; 2. Range targets; 3. breakdown and filters;
4. empty states; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-28.2.1-S; do not design
your own.

Definition of done: on the rep website, R-03 shows the rep their own period
targets with a progress bar, a neutral pace marker and the unfulfilled note
under the headline, each Range target separately, and a Location breakdown
ordered by lowest attainment with untargeted Locations apart — and nothing
about the team.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: progress on the tablet (MI-37, assumed website only).
```

**Checkpoint**

Produces before pausing — R-03 rendering against seeded actuals.
Human reviews — Could a rep read anything on the page as a team comparison or a verdict?
Resume trigger — `Continue T-28.2.1`

---

### T-28.3.1-S — Test scenarios for the manager's performance overview

**Owner** — Scenario Review
**Gates** — T-28.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the manager performance overview (task
T-28.3.1). Read plan_docs/stories/targets-and-performance.md US-005 S1–S5 and
TP005-A–E, and plan_docs/uxdocs/05-manager.md M-13 (M13.1–M13.3). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover the story scenarios with their figures (Aoife 25 pts behind,
Colm 9 pts; Colm €3,600, Aoife €3,000), then derivable edges: ties, a rep
ahead of pace, the first day of the period (pace 0%), a period already
ended. Mark undecided cases as "Needs a decision". Write no test code; change
no files.
```

---

### T-28.3.1 — Show the manager who is furthest behind pace first

**Parent story**

> As a Sales Manager, I want a team view ordered so that whoever is behind appears first so that I act before the period ends.
>
> Acceptance criteria:
> - For Q4 2026 each rep shows actual, target, percentage and expected pace, ordered furthest behind first (S1)
> - Opening Colm shows his Rep Target, his Rep–Range Targets and his per-Location breakdown (S2)
> - Range view shows every rep's target and actual per Range, with the sum labelled as a sum, not a team target (S3)
> - The unfulfilled note appears aggregated for the team (S4)
> - Reps with no target are listed separately with actuals only (S5)
> - 75% through Q4, Colm 66% of €40,000 and Aoife 50% of €12,000: Aoife (25 pts behind) before Colm (9 pts) (TP005-A); "Sort by € behind pace" puts Colm (€3,600) before Aoife (€3,000), everyone still listed (TP005-B)
> - Value decides the order when a rep has both; units-only ranks by units; under the € sort, units-only reps sit under their own "Units targets" heading (TP005-C–E)

**Slice** — On M-13, a manager sees each rep's actual, target, percentage and straight-line pace, ordered by points behind pace (or by € behind), drills into any rep, switches to a By Range view, and sees reps without targets and the team's unfulfilled amount apart.
**Spec source** — Targets & Performance US-005 S1–S5, TP005-A–E; uxdocs 05 M-13 (M13.1–M13.3)
**Depends on** — T-28.1.1, T-27.1.1, T-27.2.1, T-14.1.1
**Pattern to follow** — T-14.1.1 (manager overview by rep)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — sorting rules are worked through with figures; reads reviewed actuals.

**Provisional commit message**

```
feat(performance): show the manager who is furthest behind pace first

- Points behind pace ranks each rep against their own expectation; the
  € sort shows the biggest holes in the team's number
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A settled overview with worked examples.

**Agent prompt**

```
Role: You are building the manager performance overview (M-13) on the Field
Sales Management System's manager website.

Context:
- Slice: on M-13, a manager sees each rep's actual, target, percentage and
  straight-line pace, ordered by points behind pace (or by € behind), drills
  into any rep, switches to a By Range view, and sees reps without targets
  and the team's unfulfilled amount apart.
- Specs: plan_docs/stories/targets-and-performance.md US-005 S1–S5,
  TP005-A–E; plan_docs/uxdocs/05-manager.md M-13 (M13.1–M13.3).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — actuals and
  measurement (T-28.1.1), targets (T-27.1.1, T-27.2.1), manager website
  (T-14.1.1).
- Pattern to follow: T-14.1.1.

Acceptance criteria:
1. Each rep row: actual, target, percentage, expected pace for today, and
   points behind; default order furthest behind first (Aoife 25 pts before
   Colm 9 pts at 75% through Q4).
2. "Sort by € behind pace" reorders (Colm €3,600 before Aoife €3,000); every
   rep stays listed; units-only reps sit under "Units targets" ordered by
   points.
3. With value and units targets, value decides the order; the units figure
   and its gap still show.
4. Reps with no target are listed separately with actuals only.
5. The team's unfulfilled amount is stated.
6. Drilling into a rep shows their Rep Target, Rep–Range Targets and
   Location breakdown.
7. By Range: each Range with every rep's target and actual; the sum is
   labelled as a sum, not a team target.

Constraints:
- Use the project's existing conventions and test framework.
- Read measurements from T-28.1.1.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after both sorts match the worked examples on seeded data, stop
and show them. Resume only on "Continue T-28.3.1".

Steps: 1. pace and gaps; 2. default sort; 3. € sort and units group;
4. no-target list and unfulfilled; 5. drill-down; 6. By Range; 7. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-28.3.1-S; do not design
your own.

Definition of done: on M-13, a manager sees each rep's actual, target,
percentage and straight-line pace, ordered by points behind pace (or by €
behind), drills into any rep, switches to a By Range view, and sees reps
without targets and the team's unfulfilled amount apart.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Captured figures (T-28.3.2), brand reporting (MI-41).
```

**Checkpoint**

Produces before pausing — both sorts matching the worked examples on seeded data.
Human reviews — Does the order put the right person first in every example?
Resume trigger — `Continue T-28.3.1`

---

### T-28.3.2-S — Test scenarios for captured and attributed figures

**Owner** — Scenario Review
**Gates** — T-28.3.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing captured alongside attributed
figures to managers (task T-28.3.2). Read
plan_docs/stories/targets-and-performance.md US-006 S1–S3, glossary
(Capturing Rep, Captured By) and design decision "Capturing Rep recorded,
shown to managers only". Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover the
story scenarios, then derivable edges: a rep who captured orders only at
their own Locations (captured equals attributed), a rejected chain order.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-28.3.2 — Show managers what a rep captured beside what counts to them

**Parent story**

> As a Sales Manager, I want to see the orders a rep actually took, including chain orders that count at other Locations, so that a rep who wins central business is visible.
>
> Acceptance criteria:
> - Colm captured a €18,000 multi-branch order attributed across 12 branches held by others: opening Colm shows "Attributed: €26,400" and "Captured: €44,400", clearly labelled as different measures (S1)
> - Colm's own performance shows €26,400 against target and no captured figure (S2)
> - A self-service order for a Location Colm holds counts in his attributed actuals with no Capturing Rep (S3)

**Slice** — In the manager's view of a rep, a Captured figure — everything the rep took, wherever it counts — sits beside the Attributed figure, each labelled as a different measure, and never appears in the rep's own view.
**Spec source** — Targets & Performance US-006 S1–S3; glossary (Capturing Rep, Captured By); design decision "Capturing Rep recorded, shown to managers only"
**Depends on** — T-28.3.1, T-28.1.1
**Pattern to follow** — T-28.3.1
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — one labelled figure over reviewed data.

**Provisional commit message**

```
feat(performance): show what a rep captured beside what counts to them

- A rep who wins central business should be visible, without the same
  sale counting twice in two reps' progress
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A small addition with a review pause.

**Agent prompt**

```
Role: You are adding Captured figures to the manager's rep view on M-13 in the
Field Sales Management System.

Context:
- Slice: in the manager's view of a rep, a Captured figure — everything the
  rep took, wherever it counts — sits beside the Attributed figure, each
  labelled as a different measure, and never appears in the rep's own view.
- Specs: plan_docs/stories/targets-and-performance.md US-006 S1–S3, glossary
  (Capturing Rep, Captured By), design decision "Capturing Rep recorded,
  shown to managers only".
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — actuals with Capturing
  Rep (T-28.1.1), manager overview (T-28.3.1), rep view (T-28.2.1).
- Pattern to follow: T-28.3.1.

Acceptance criteria:
1. Opening Colm shows "Attributed: €26,400" and "Captured: €44,400", labelled
   as different measures.
2. Colm's own R-03 shows no captured figure.
3. Self-service orders have no Capturing Rep and add to attributed only.

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after both figures show on seeded chain orders, stop and show
them. Resume only on "Continue T-28.3.2".

Steps: 1. captured measure; 2. manager rep view; 3. rep view check; 4. tests
from agreed scenarios.

Test expectations: implement the scenarios agreed in T-28.3.2-S; do not design
your own.

Definition of done: in the manager's view of a rep, a Captured figure —
everything the rep took, wherever it counts — sits beside the Attributed
figure, each labelled as a different measure, and never appears in the rep's
own view.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: captured figures in any rep-facing view.
```

**Checkpoint**

Produces before pausing — both figures on seeded chain orders.
Human reviews — Could anyone confuse the two figures?
Resume trigger — `Continue T-28.3.2`

---

### T-28.4.1-S — Test scenarios for the Large baseline

**Owner** — Human-Led
**Gates** — T-28.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-36: compared against the whole-period target or the pace to date (S1 assumes pace)? The multiplier (the "configured share")?
- Hickey's €50,000 Q4 chain target: an order well above October's expected pace is annotated "Large — 3.2× expected pace for October" (US-007 S1; HO-002 S1 "3.2× target for October" — which wording?).
- No Location target but a Rep–Range target covering the order's products: that baseline (S2) — the order's matching lines only?
- No target: the same period in previous years; no history, no flag (S3; HO-002 S3).
- Byrne's €2,400 of suncare last May and €2,600 this May: no flag (HO-002 S4).
- Evaluated at the cut-off with the other annotations; it never diverts (Annotate).

---

### T-28.4.1 — Annotate orders that are large for their Location

**Parent story**

> As a Head Office User, I want an order's size compared against the relevant target so that the Large flag means something for that Location.
>
> Acceptance criteria:
> - With Hickey's €50,000 Q4 chain target, an order above the configured share is flagged Large with "3.2× expected pace for October" (S1)
> - With no Location target but a Rep–Range target covering the products, that is the baseline (S2)
> - With no target, the baseline is the same period in previous years; with no history, no Large flag (S3; Head Office US-002 S3)
> - Byrne's Chemist ordering €2,600 of suncare this May after €2,400 last May, with no target: no flag (Head Office US-002 S4)

**Slice** — At acceptance, each order is compared with its Location's target pace, else the covering Rep–Range target, else the same period in earlier years, and one well above that baseline carries a Large annotation saying by how much; with no baseline, nothing is flagged.
**Spec source** — Targets & Performance US-007 S1–S3 and open questions; glossary (Large Baseline); Requires Clarification 1; Head Office US-002 S1, S3, S4; uxdocs 02 H-01 (Large is Annotate)
**Depends on** — T-28.1.1, T-27.3.1, T-27.2.1, T-7.4.1
**Pattern to follow** — T-7.4.1 (annotators)
**Ownership** — Impl: Human Tight-Loop ↓ | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — would be Agent-Assisted; downgraded one step because the comparison rule and multiplier are unagreed (MI-36), so the oracle is a person.

**Provisional commit message**

```
feat(orders): annotate orders that are large for their location

- An order is large relative to what that shop is expected to buy, not
  against a global number; with no baseline, no flag is honest
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
An annotator with an open rule.

**Work package**

Increments:
1. Resolve MI-36 (pace or whole period; multiplier; wording) and write vectors.
2. Baseline selection: Location target (with chain roll-up), else covering Rep–Range target, else same period in previous years, else none.
3. Large annotator at acceptance through T-7.4.1, with the "N× expected pace for October" sentence.
4. Settings for the multiplier (MI-12).

Decision points:
- MI-36.
- For a Rep–Range baseline, compare the whole order or only its matching lines?
- Whether history counts only accepted orders.

Delegable slivers:
- **Large sentence** — Given the ratio and period, produce the annotation sentence per the agreed wording. Pure function.
- **Baseline selector** — Once the rule is agreed, implement baseline selection (Location, Rep–Range, history, none) against the agreed vectors. No annotation wiring.

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | TP-004 (T-28.1.1, T-28.2.1), TP-005 (T-28.3.1), TP-006 (T-28.3.2), TP-007 (T-28.4.1), HO-002 S1 Large, S3, S4 (T-28.4.1); TP-003 S3, S4 measured in T-28.1.1; CV-006 S4 reporting flag not delivered (MI-41, stated) |
| Every task satisfies the three slice criteria | Pass | 5 of 5 |
| Every task carries a tier with a rationale citing dimensions | Pass | 5 of 5 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-10, MI-36, MI-37, MI-41 |
| Tasks modifying existing behaviour order characterisation first | Pass | T-28.1.1 and T-28.4.1 extend acceptance inside tight-loop or downgraded work packages |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-28.1.1, T-28.4.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-28.2.1, T-28.3.1, T-28.3.2 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-28.1.1, T-28.4.1 (downgraded, stated) |
| Every scenario task precedes the task it gates | Pass | 5 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-10, 12, 36, 37, 41 |
