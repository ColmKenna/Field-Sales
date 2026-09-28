# E29 — Allocating short stock

**Iteration** — 10, Stock allocation
**Outcome** — When several orders want a product there isn't enough of, the short quantity waits as Outstanding instead of blocking the order; head office records what's on hand and coming, sees a proposed split that completes as many orders as possible, adjusts it knowing who they're making wait, and releases it to the warehouse when ready.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Stock Allocation US-001 — See which products are short | Must | S1–S5 |
| 2 | Stock Allocation US-002 — Record what is on hand and what is coming | Must | S1–S6 |
| 3 | Stock Allocation US-003 — Allocate a short product across waiting orders | Must | S1–S7, SA003-A–D |
| 4 | Stock Allocation US-004 — Release an allocation | Must | S1–S6 |
| 5 | Stock Allocation US-005 — Pick up an allocation later | Should | S1–S3 |
| 6 | Head Office US-008 — Orders within policy go through without acceptance (part) | Must | S2 (route a shortfall); the rest is in E7, E8 and E22 |
| 7 | Head Office US-006 — Work Held Orders (part) | Should | S1's link from a held order to its product's allocation; the rest is in E7 and E17 |

**Exit criterion** — At the cut-off, an order line short of stock is accepted with what's available released and the rest Outstanding. H-08 lists only short products, most waiting orders first, with drafts and over-allocation noted. H-09 records On Hand and dated Incoming deliveries; a delivery can arrive, or change and flag the draft. H-10 proposes a complete-what-you-can split (sole blockers first, oldest first), updates "N orders complete, M still short" as cells change, and blocks over-allocation. It shows orders passed over, excludes closed Locations, respects Run-out Remaining, and re-proposes from chosen pools. H-11 releases one order or the whole split to the warehouse, and a draft left for tomorrow is still trustworthy.

**Capability-class stamp** — Frontier + extended reasoning for the shortfall routing, the proposal and release (T-29.1.1, T-29.3.1, T-29.4.1); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [stock-allocation.md](../../stories/stock-allocation.md), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-006 S1, US-008 S2), [02-head-office.md](../../uxdocs/02-head-office.md) (H-06; H-08 H8.1–H8.3, H-09 H9.1–H9.3 and H-11 H11.1–H11.3 — drafts, MI-61; H-10 H10.1–H10.7), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-001, BR-NEW-009).

---

### T-29.1.1-S — Test scenarios for routing a shortfall at the cut-off

**Owner** — Human-Led
**Gates** — T-29.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-38: without a warehouse feed, what does the system know at the cut-off? A product is short only where head office has entered On Hand below what's outstanding (plus this order)? A product with no On Hand entered: released in full?
- "Accepted, what's available released, the short quantity Outstanding and in allocation" (HO-008 S2): which orders get the available stock at the cut-off — oldest first across the batch, or none until allocated?
- Oversold Run-out lines (Remaining below zero): the oversold part becomes Outstanding?
- The rep and customer see "Accepted, partly sent" and outstanding quantities with no new screens.
- Several orders accepted at the same cut-off competing for the same On Hand.
- An order both short and held by hand.

---

### T-29.1.1 — Accept a short order with its shortfall left Outstanding for allocation

**Parent story**

> As a Head Office User, I want orders that are within policy to go through without my acceptance so that my worklist only holds things that genuinely need a decision.
>
> Acceptance criteria:
> - A synced order with a line flagged Stock Shortfall or Oversold is accepted at the cut-off, what's available is released, and the short quantity becomes Outstanding and appears in allocation (S2, as amended by BR-NEW-009)

**Slice** — At the cut-off, an order line asking for more of a short product than is available is accepted with the available part released and the rest recorded as Outstanding, where allocation will find it; the order shows as partly sent to the rep and customer.
**Spec source** — Head Office US-008 S2 and BR-NEW-009 amendment; uxdocs 02 H-01 disposition table (Route: Stock Shortfall, Oversold); Stock Allocation Requires Clarification 1, 5
**Depends on** — T-7.1.1, T-7.3.1, T-10.3.1, T-10.6.1
**Pattern to follow** — T-8.4.1 (a disposition applied at the cut-off)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — changes the acceptance state machine's release quantities (Blast Radius High); what "available" means without a feed is open (MI-38).

**Provisional commit message**

```
feat(orders): accept short orders and leave the shortfall outstanding

- A shortfall is routed, not decided: the order goes through, what can
  be sent is sent, and the rest waits in allocation
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A change inside acceptance and release.

**Work package**

Increments:
1. Resolve MI-38: the availability source at the cut-off (entered On Hand, Run-out Remaining) and the rule for competing orders in one batch.
2. Stock Shortfall and Oversold detection at acceptance.
3. Partial release to the warehouse at acceptance (T-7.1.1's release) with Outstanding recorded per line.
4. Outstanding lines visible to allocation; rep and customer status "Accepted, partly sent" with outstanding quantities (T-7.5.1, T-26.5.1).
5. Characterisation of T-7.1.1's acceptance before the change.

Decision points:
- MI-38 and the batch rule.
- Whether an order with any Outstanding line still counts fully in actuals (TP: yes, on acceptance).

Delegable slivers:
- **Shortfall annotation** — Add the Stock Shortfall and Oversold annotations as sentences through T-7.4.1's interface, once detection exists. Do not change the interface.
- **Outstanding query** — Provide the query allocation uses: Outstanding quantity per product across orders, with order age from acceptance. No release logic.

---

### T-29.2.1-S — Test scenarios for recording on hand and incoming stock

**Owner** — Scenario Review
**Gates** — T-29.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for recording On Hand and Incoming stock (task
T-29.2.1). Read plan_docs/stories/stock-allocation.md US-002 S1–S6 and
glossary (On Hand, Incoming, Feed-ready), and
plan_docs/uxdocs/02-head-office.md H-09 (H9.1–H9.3) and H-10 (H10.6). Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover the story scenarios, then derivable edges: a delivery
that arrives short (corrected on Arrived), two deliveries on one date, On
Hand reduced below a draft. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-29.2.1 — Record what's on hand and what's coming for a product

**Parent story**

> As a Head Office User, I want to enter the quantity available and any expected deliveries so that I can allocate against both.
>
> Acceptance criteria:
> - On Hand 200 for "SPF30 Sun Lotion 200ml" shows 200 available now with who entered it and when (S1)
> - Incoming 400 expected 22 October 2026 shows as a second pool with its date (S2); a second Incoming 250 on 5 November lists both in date order, both allocatable (S3)
> - When the 22 October delivery arrives and is moved to On Hand, On Hand increases, the Incoming closes, and allocations against it are marked available (S4)
> - Changing the Incoming to 300 or to 29 October flags allocations "Delivery changed — review allocation" (S5)
> - A product sold per kg is entered and shown in kg (S6)

**Slice** — On H-09, head office enters On Hand and dated Incoming deliveries for a product, marks a delivery as arrived (correcting what came), and any change to a delivery used by a draft flags that draft for review without rewriting it.
**Spec source** — Stock Allocation US-002 S1–S6; glossary (On Hand, Incoming, Feed-ready); uxdocs 02 H-09 (H9.1–H9.3 — drafts, MI-61), H-10 (H10.6)
**Depends on** — T-1.2.1, T-1.4.1, T-10.3.1
**Pattern to follow** — T-10.3.1 (a hand-entered quantity with who and when)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — straightforward entry built behind a seam a feed could replace; H-09 is a draft.

**Provisional commit message**

```
feat(allocation): record what's on hand and what's coming

- The system holds no warehouse stock, so head office types the two
  figures for short products only; a feed could replace the typing later
- A change to a delivery never rewrites a draft; it asks for review
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Stock entry with a review pause.

**Agent prompt**

```
Role: You are building stock entry (H-09) for the Field Sales Management
System's head office website.

Context:
- Slice: on H-09, head office enters On Hand and dated Incoming deliveries
  for a product, marks a delivery as arrived (correcting what came), and any
  change to a delivery used by a draft flags that draft for review without
  rewriting it.
- Specs: plan_docs/stories/stock-allocation.md US-002 S1–S6 and glossary (On
  Hand, Incoming, Feed-ready); plan_docs/uxdocs/02-head-office.md H-09
  (H9.1–H9.3), H-10 (H10.6).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — products (T-1.2.1),
  units (T-1.4.1), hand-entered quantity pattern (T-10.3.1).
- Pattern to follow: T-10.3.1.

Acceptance criteria:
1. On Hand per product with who entered it and when.
2. Incoming deliveries with quantity and expected date, listed in date order.
3. Arrived moves a delivery's quantity (correctable) into On Hand and closes
   it; allocations against it become available.
4. Changing a delivery's quantity or date flags drafts using it "Delivery
   changed — review allocation" and changes no allocation.
5. Measure-based products are entered and shown in their unit.
6. Stock figures are read through one interface a warehouse feed could
   implement later (Feed-ready).

Constraints:
- Use the project's existing conventions and test framework.
- Entered per product when needed; no catalogue-wide stock sheet.
- No tests of framework internals or trivial members.
- This task opts in to stock pool storage.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after entry, arrival and change flagging work end to end, stop
and show them. Resume only on "Continue T-29.2.1".

Steps: 1. stock pools behind an interface; 2. H-09 entry; 3. Arrived;
4. change flags; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-29.2.1-S; do not design
your own.

Definition of done: on H-09, head office enters On Hand and dated Incoming
deliveries for a product, marks a delivery as arrived (correcting what
came), and any change to a delivery used by a draft flags that draft for
review without rewriting it.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the warehouse feed (MI-38), allocation (T-29.3.1).
```

**Checkpoint**

Produces before pausing — entry, arrival and change flagging, end to end.
Human reviews — Is the stock interface one a feed could implement without screen changes?
Resume trigger — `Continue T-29.2.1`

---

### T-29.2.2-S — Test scenarios for the Short Products list

**Owner** — Scenario Review
**Gates** — T-29.2.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Short Products list (task T-29.2.2).
Read plan_docs/stories/stock-allocation.md US-001 S1–S5 and US-005 S1,
plan_docs/stories/head-office-order-processing.md US-006 S1, and
plan_docs/uxdocs/02-head-office.md H-08 (H8.1–H8.3) and H-06. Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover the story scenarios, then derivable edges: a product short only
against an Incoming delivery, a product whose waiting orders are all
cancelled, a held order short on two products. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-29.2.2 — List only the products that are short, most pressing first

**Parent story**

> As a Head Office User, I want a list of products where outstanding orders exceed what is available so that I only spend time on the ones that need deciding.
>
> Acceptance criteria:
> - "SPF30 Sun Lotion 200ml" with 340 outstanding across 7 orders and 200 On Hand reads "340 outstanding · 200 on hand · 7 orders waiting · 2 held" (S1)
> - Products are ordered by how many orders are waiting, with the oldest waiting order's age shown (S2)
> - A short product with no On Hand shows "On hand not entered" and an entry action (S3)
> - Nothing short: "No products are short" (S4)
> - On Hand raised above outstanding takes the product off the list (S5)
> - An unreleased draft shows "Allocation drafted, not released" with its age (US-005 S1)
> - A held order short on a product links to that product's allocation (Head Office US-006 S1)

**Slice** — H-08 lists only products where Outstanding exceeds what's available, most waiting orders first with the oldest wait, notes unentered stock, drafts and over-allocation under each row, and each row opens its allocation; held orders link to the same place.
**Spec source** — Stock Allocation US-001 S1–S5, US-005 S1; Head Office US-006 S1 (allocation link); uxdocs 02 H-08 (H8.1–H8.3 — drafts, MI-61), H-06
**Depends on** — T-29.1.1, T-29.2.1, T-7.2.2
**Pattern to follow** — T-7.2.2 (held orders list)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — a list over reviewed figures; H-08 is a draft.

**Provisional commit message**

```
feat(allocation): list the products that are short, most pressing first

- Allocation is for exceptions; only products that need deciding appear,
  and an unreleased draft stays in view until it's dealt with
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A list with a review pause.

**Agent prompt**

```
Role: You are building the Short Products list (H-08) for the Field Sales
Management System's head office website.

Context:
- Slice: H-08 lists only products where Outstanding exceeds what's
  available, most waiting orders first with the oldest wait, notes unentered
  stock, drafts and over-allocation under each row, and each row opens its
  allocation; held orders link to the same place.
- Specs: plan_docs/stories/stock-allocation.md US-001 S1–S5, US-005 S1;
  plan_docs/stories/head-office-order-processing.md US-006 S1;
  plan_docs/uxdocs/02-head-office.md H-08 (H8.1–H8.3), H-06.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Outstanding
  (T-29.1.1), stock pools (T-29.2.1), held orders (T-7.2.2).
- Pattern to follow: T-7.2.2.

Acceptance criteria:
1. A row reads "340 outstanding · 200 on hand · 7 orders waiting · 2 held".
2. Ordered by waiting orders, with the oldest wait shown.
3. "On hand not entered" with "Enter on hand" opening H-09 in place.
4. "Allocation drafted, not released" with its age, and over-allocation,
   under the product's row.
5. A product leaves the list as soon as everything outstanding can be
   filled; "No products are short" when empty.
6. A row opens H-10 for the product; a held order short on a product links
   there from H-06.

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the list renders against seeded shortfalls, stop and show
it. Resume only on "Continue T-29.2.2".

Steps: 1. short query; 2. list and order; 3. notices; 4. links; 5. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-29.2.2-S; do not design
your own.

Definition of done: H-08 lists only products where Outstanding exceeds
what's available, most waiting orders first with the oldest wait, notes
unentered stock, drafts and over-allocation under each row, and each row
opens its allocation; held orders link to the same place.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: allocating (T-29.3.1).
```

**Checkpoint**

Produces before pausing — the list against seeded shortfalls.
Human reviews — Does a product appear exactly when it needs deciding and not otherwise?
Resume trigger — `Continue T-29.2.2`

---

### T-29.3.1-S — Test scenarios for the proposed split

**Owner** — Human-Led
**Gates** — T-29.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- 200 On Hand, 7 waiting, 4 sole blockers: the 4 filled first, oldest first, then the rest; "4 orders complete, 3 still short" (S2). "The rest" — oldest first, or smallest first to complete more?
- Moving units between orders updates live to "3 orders complete, 4 still short" (S3).
- Over-allocated: "60 more allocated than available", release blocked (S4).
- Allocated from the 22 October delivery: "Allocated from 22 Oct delivery", not released until it arrives (S5). Does the proposal use Incoming by default, and in which order of pools?
- A cancelled order drops out and its allocation returns to the pool (S7).
- A Closed Location's order is flagged "Location closed" and excluded from the proposal (SA-004 S5).
- Run-out: allocation can't exceed Remaining (SA-004 S6).
- Sole Blocker computed against what, when an order is short on another product that is also being allocated?
- Measure-based quantities and step sizes in the split.

---

### T-29.3.1 — Propose a complete-what-you-can split and let head office adjust it

**Parent story**

> As a Head Office User, I want the screen to propose how to divide short stock, showing how many orders each split would complete, so that I can get the most out of the door and see who I'm making wait.
>
> Acceptance criteria:
> - Each waiting order shows its Location, outstanding quantity, age since acceptance and whether this product alone is holding it up (S1)
> - 200 On Hand, 7 waiting, 4 sole blockers: the proposal fills those 4 first, oldest first, then allocates the rest; "4 orders complete, 3 still short" (S2)
> - Moving units updates the header live to "3 orders complete, 4 still short" (S3)
> - Over On Hand plus Incoming: "60 more allocated than available", can't release (S4)
> - Allocated against the 22 October delivery: "Allocated from 22 Oct delivery", not released until it arrives (S5)
> - A cancelled order drops out and its allocation returns to the pool (S7)
> - A Closed Location's order is flagged "Location closed" and excluded from the proposal (US-004 S5); allocation can't exceed Run-out Remaining (US-004 S6)

**Slice** — H-10 shows every order waiting on a short product with its age and whether this product alone holds it up, pre-fills a complete-what-you-can split across On Hand and Incoming, updates "N orders complete, M still short" on every change, and refuses anything that exceeds stock or Remaining.
**Spec source** — Stock Allocation US-003 S1–S5, S7; US-004 S5, S6; glossary (Sole Blocker, Proposed Split, Orders Complete); design decision "Complete-what-you-can is proposed, never imposed"; uxdocs 02 H-10 (H10.1–H10.4)
**Depends on** — T-29.2.1, T-29.2.2, T-17.2.1, T-10.3.1
**Pattern to follow** — T-6.4.1 (a pure rule with shared test vectors)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — the allocation rule decides who gets stock (Blast Radius High) and has many interacting cases (Edge-Case Discovery High).

**Provisional commit message**

```
feat(allocation): propose a complete-what-you-can split

- Getting the most orders out of the door is the usual intent, so orders
  this product alone holds up go first; every figure stays editable
- The header counts completed orders, because that's the decision, not
  the quantities
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The rule that decides who gets stock.

**Work package**

Increments:
1. Agree the rule's open points (rest-of-pool order, pool order, sole-blocker definition across products) as test vectors.
2. Pure proposal function: waiting orders, pools, Remaining and exclusions in; per-order, per-pool allocations and the completion count out.
3. Draft storage per product: allocations per order and pool, editable, with Reset to the proposal.
4. H-10: the waiting list with age and "only short on this product"; the live header as the largest text; over-allocation message and release block; "Allocated from 22 Oct delivery".
5. Exclusions: cancelled orders drop out; Closed Locations flagged and excluded; Run-out Remaining caps the total.

Decision points:
- The open rule points above.
- Whether a draft is per product or spans products (an order short on two products).

Delegable slivers:
- **Orders Complete counter** — Given a draft, compute "N orders complete, M still short" and the over-allocation amount. Pure function with the agreed vectors.
- **Waiting list rows** — Render H-10's rows (Location, outstanding, age, "only short on this product") from the waiting-orders query. No allocation logic.

---

### T-29.3.3-S — Test scenarios for re-proposing and keeping a draft current

**Owner** — Human-Led
**Gates** — T-29.3.3
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "Re-propose..." shows each pool with quantity and date, the draft's pools preselected (SA003-A); excluding a far delivery re-proposes from selected pools only (SA003-B); the page states it replaces the whole draft (SA003-C); Cancel leaves it unchanged (SA003-D).
- 2 new orders waiting since the draft: shown unallocated, with the offer to re-propose (SA-005 S2).
- On Hand reduced below the drafted total: flagged over-allocated, release blocked until adjusted (SA-005 S3); a delivery changed: "Delivery changed — review allocation" (SA-002 S5; H10.6).
- A delivery arrived: allocations against it become available (SA-002 S4).
- A draft left for days: any expiry?

---

### T-29.3.3 — Re-propose from chosen stock pools and keep an old draft trustworthy

**Parent story**

> As a Head Office User, I want unreleased allocations to be visible and current so that a draft I left yesterday is still trustworthy today.
>
> Acceptance criteria:
> - 2 orders that began waiting since the draft appear unallocated, and the proposal offers to re-propose across all (S2)
> - On Hand reduced below the drafted total flags the draft over-allocated; it can't be released until adjusted (S3)
> - "Re-propose..." shows each pool with quantity and date, the draft's pools preselected (Stock Allocation US-003 SA003-A)
> - Excluding a far delivery re-proposes from the selected pools only (SA003-B); the page says it replaces the draft across all waiting orders (SA003-C); Cancel leaves the draft unchanged (SA003-D)

**Slice** — A draft on H-10 stays as the manager left it: new waiting orders appear unallocated, stock changes flag it (and block release if over-allocated), and Re-propose lets the manager pick which pools are timely before replacing the whole draft.
**Spec source** — Stock Allocation US-005 S2, S3; US-003 SA003-A–D; US-002 S4, S5; uxdocs 02 H-10 (H10.6, H10.7)
**Depends on** — T-29.3.1, T-29.2.1
**Pattern to follow** — T-29.3.1
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — draft integrity as stock and orders change (Edge-Case Discovery High); decisions are settled.

**Provisional commit message**

```
feat(allocation): re-propose from chosen pools and keep drafts honest

- Stock changes never silently rewrite the manager's choices; they flag
  the draft, and a fresh proposal is always an explicit act
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Draft integrity against a human oracle.

**Agent prompt**

```
Role: You are adding Re-propose and draft upkeep to the allocation view
(H-10) of the Field Sales Management System.

Context:
- Slice: a draft on H-10 stays as the manager left it: new waiting orders
  appear unallocated, stock changes flag it (and block release if
  over-allocated), and Re-propose lets the manager pick which pools are
  timely before replacing the whole draft.
- Specs: plan_docs/stories/stock-allocation.md US-005 S2, S3, US-003
  SA003-A–D, US-002 S4, S5; plan_docs/uxdocs/02-head-office.md H-10 (H10.6,
  H10.7).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — proposal and drafts
  (T-29.3.1), stock pools (T-29.2.1).
- Pattern to follow: T-29.3.1.

Acceptance criteria:
1. New waiting orders since the draft appear unallocated, with an offer to
   re-propose.
2. A stock change flags "Delivery changed — review allocation" and marks the
   affected pool and rows; the draft is not rewritten.
3. A draft above available stock is flagged over-allocated and can't be
   released until adjusted.
4. "Re-propose..." lists On Hand and each Incoming with quantity and date,
   the draft's pools preselected, and states it replaces current draft
   allocations across all waiting orders.
5. Applying uses only selected pools; Cancel leaves the draft unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- Use T-29.3.1's proposal function; no second rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after draft upkeep passes every agreed scenario, stop and show
it. Resume only on "Continue T-29.3.3".

Steps: 1. new orders; 2. change flags; 3. over-allocation block;
4. Re-propose with pools; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-29.3.3-S. You
are forbidden from designing your own test cases.

Definition of done: a draft on H-10 stays as the manager left it: new waiting
orders appear unallocated, stock changes flag it (and block release if
over-allocated), and Re-propose lets the manager pick which pools are timely
before replacing the whole draft.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: release (T-29.4.1).
```

**Checkpoint**

Produces before pausing — draft upkeep passing every agreed scenario.
Human reviews — Is a draft ever changed without the manager choosing to?
Resume trigger — `Continue T-29.3.3`

---

### T-29.4.1-S — Test scenarios for releasing an allocation

**Owner** — Human-Led
**Gates** — T-29.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Releasing one order's 36: "Accepted — partly sent" with 36 released; the rest stays a draft (S1).
- Releasing the whole split: each allocated order part-released, those allocated nothing stay waiting, On Hand reduces by the released total (S2); H-11 names the count ("Release 4 orders") and each row's consequence (H11.2, H11.3).
- Leaving without releasing keeps the draft and the product on H-08 (S3).
- A full fill leaves the waiting list, Accepted pending despatch (S4).
- Allocations from Incoming aren't released until the delivery arrives.
- Released quantities reduce Run-out Remaining like any order (S6).
- How release reaches the warehouse (MI-04) and what happens if it fails.
- MI-39: is a record of who allocated what to whom kept (needed for Passed Over, T-29.3.2)?

---

### T-29.4.1 — Release allocated stock per order or all together

**Parent story**

> As a Head Office User, I want to choose when allocated stock goes to the warehouse, per order or all at once, so that I can move quickly where it's urgent and think where it's contentious.
>
> Acceptance criteria:
> - Releasing one order's 36 makes it "Accepted — partly sent" with 36 released; the rest of the split stays a draft (S1)
> - Releasing the whole split part-releases each allocated order, leaves those allocated nothing waiting, and reduces On Hand by the released total (S2)
> - Leaving without releasing keeps the draft, the orders waiting and the product on the list (S3)
> - An order whose whole outstanding quantity is released leaves the waiting list, Accepted pending despatch (S4)
> - Released quantities reduce Run-out Remaining as any order does (S6)

**Slice** — From H-10 a manager releases one order's allocation, or opens H-11 to release the whole split with each order's outcome stated, and the warehouse receives only released quantities from stock that is on hand; everything else stays a draft.
**Spec source** — Stock Allocation US-004 S1–S4, S6; design decision "The allocation is a draft until the manager releases it"; uxdocs 02 H-10 (H10.5), H-11 (H11.1–H11.3 — drafts, MI-61)
**Depends on** — T-29.3.1, T-7.1.1, T-7.3.1, T-10.3.1
**Pattern to follow** — T-7.1.1 (release to the warehouse)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — sends stock to the warehouse and changes order states and Remaining (Blast Radius High); the warehouse hand-off is open (MI-04).

**Provisional commit message**

```
feat(allocation): release allocated stock per order or all together

- Releasing as you go moves stock sooner when a customer is waiting;
  holding the split lets the manager think where it's contentious
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Release to the warehouse inside the tight loop.

**Work package**

Increments:
1. Release one order from its H-10 row: partial release through T-7.1.1's warehouse hand-off; order state and Outstanding updated.
2. H-11: every allocated order with its consequence ("completes the order", "still outstanding") and "Release 4 orders".
3. On Hand reduced by released totals; Run-out Remaining reduced; Incoming allocations held until arrival.
4. Allocation record kept per release if MI-39 says so (needed for T-29.3.2).
5. Failure handling if the warehouse hand-off fails (MI-04).

Decision points:
- MI-39 (allocation history).
- MI-04 failure behaviour.

Delegable slivers:
- **H-11 screen** — Given a draft, render H-11's rows with each order's consequence and the "Release N orders" button, per H11.1–H11.3. No release logic.
- **Consequence sentences** — Compute each order's release consequence ("completes the order", "still outstanding: 12") from its outstanding and allocated quantities. Pure function.

---

### T-29.3.2-S — Test scenarios for passed-over orders

**Owner** — Human-Led
**Gates** — T-29.3.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-40: how many allocations, or how long, before an order is marked? Assumed twice, or short on more than one product.
- "Passed over twice · short on 3 products" regardless of where the proposal placed it (SA-003 S6).
- What counts as a previous allocation that passed it over — a release that gave it nothing, or a draft?
- It sits under the order, not in a column (H10.4); the marker changes nothing on its own.
- MI-39: requires a record of past allocations per order.

---

### T-29.3.2 — Mark orders that keep being passed over

**Parent story**

> As a Head Office User, I want the screen to propose how to divide short stock, showing how many orders each split would complete, so that I can get the most out of the door and see who I'm making wait.
>
> Acceptance criteria:
> - An order short on 3 products and left short in 2 previous allocations is marked "Passed over twice · short on 3 products" wherever the proposal placed it (S6)

**Slice** — On H-10, an order short on several products, or left short by earlier releases, carries a "Passed over" note under its row saying why, so the manager can deliberately break the default rule.
**Spec source** — Stock Allocation US-003 S6; glossary (Passed Over); design decision "Orders passed over are surfaced"; Requires Clarification 3, 4; uxdocs 02 H-10 (H10.4)
**Depends on** — T-29.3.1, T-29.4.1
**Pattern to follow** — T-29.3.1 (H-10 rows)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: S | Confidence: L
  (inferred) — the threshold (MI-40) and the history it needs (MI-39) are unagreed, so the oracle is a person.

**Provisional commit message**

```
feat(allocation): mark orders that keep being passed over

- Complete-what-you-can quietly disadvantages orders short on several
  things; the marker makes that neglect a choice rather than an accident
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A marker with an unagreed threshold.

**Agent prompt**

```
Role: You are adding the Passed Over marker to the allocation view (H-10) of
the Field Sales Management System.

Context:
- Slice: on H-10, an order short on several products, or left short by
  earlier releases, carries a "Passed over" note under its row saying why,
  so the manager can deliberately break the default rule.
- Specs: plan_docs/stories/stock-allocation.md US-003 S6, glossary (Passed
  Over), design decision "Orders passed over are surfaced", Requires
  Clarification 3, 4; plan_docs/uxdocs/02-head-office.md H-10 (H10.4).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — H-10 rows (T-29.3.1),
  release records (T-29.4.1).
- Pattern to follow: T-29.3.1.

Acceptance criteria:
1. An order short on 3 products and left short in 2 previous allocations
   shows "Passed over twice · short on 3 products" under its row.
2. The marker follows the threshold agreed in T-29.3.2-S (MI-40).
3. It never changes the proposal.

Constraints:
- Use the project's existing conventions and test framework.
- Count previous allocations from T-29.4.1's records (MI-39).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the marker passes every agreed scenario, stop and show it.
Resume only on "Continue T-29.3.2".

Steps: 1. counting; 2. note under the row; 3. tests.

Test expectations: implement exactly the scenarios agreed in T-29.3.2-S. You
are forbidden from designing your own test cases.

Definition of done: on H-10, an order short on several products, or left
short by earlier releases, carries a "Passed over" note under its row saying
why, so the manager can deliberately break the default rule.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: an allocation history screen (not designed).
```

**Checkpoint**

Produces before pausing — the marker passing every agreed scenario.
Human reviews — Does the marker appear on exactly the orders the business would call neglected?
Resume trigger — `Continue T-29.3.2`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | SA-001 (T-29.2.2), SA-002 (T-29.2.1), SA-003 (T-29.3.1, T-29.3.2, T-29.3.3), SA-004 (T-29.4.1; S5, S6 in T-29.3.1), SA-005 (T-29.2.2, T-29.3.3), HO-008 S2 (T-29.1.1), HO-006 S1 link (T-29.2.2) |
| Every task satisfies the three slice criteria | Pass | 7 of 7 |
| Every task carries a tier with a rationale citing dimensions | Pass | 7 of 7 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-04, MI-38, MI-39, MI-40, MI-61 |
| Tasks modifying existing behaviour order characterisation first | Pass | T-29.1.1 (increment 5) |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-29.1.1, T-29.3.1, T-29.3.2, T-29.3.3, T-29.4.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-29.2.1, T-29.2.2, T-29.3.2, T-29.3.3 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-29.1.1, T-29.3.1, T-29.4.1 |
| Every scenario task precedes the task it gates | Pass | 7 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-04, 38, 39, 40, 61 |
