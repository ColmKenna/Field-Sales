# E7 — Order processing and fulfilment

**Iteration** — 1, MVP: orders from the field reach the warehouse
**Outcome** — Every synced order waits as Pending, can be held or rejected by hand before the cut-off, is accepted and released to the warehouse at the cut-off without a person, and its despatches are recorded and reach the rep.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Head Office US-008 — Orders within policy go through without acceptance | Must | S1, S5, S6, S7 with BR-NEW-009 (S3 → E8; S4 → E22; S2 → E29) |
| 2 | Head Office US-006 — Work Held Orders | Should | S4 and AC-HO006-A–C; S1 without the allocation link (→ E29); S3's "Location closed" flag → E17 |
| 3 | Head Office US-004 — Record despatches from the warehouse report | Must | S1–S5 |
| 4 | Head Office US-002 — See why an order is flagged | Must (superseded in part) | S1, S2 as annotations (S5 → E10; S6 → E21; S3, S4 → E28; S6a superseded) |
| 5 | Rep at a Location US-014 — Check a Sent Call or Order | Must | S1, S3, S4, S4b, S5 (S2 and A1014-C → E13; A1014-A → E8; A1014-B → E19; S4c superseded) |
| 6 | Head Office US-001 — Process routine orders in bulk | Must (superseded) | No tasks: replaced by US-008; its read-only remainder is H-31 in T-7.1.2 |
| 7 | Head Office US-003 — Decide a flagged order | Must (superseded) | No tasks: Order Detail is a record (T-7.1.2); Hold and Reject return by hand in T-7.2.1 |

**Exit criterion** — An order uploaded from a tablet is Pending until the next weekday cut-off; head office can see it on H-31 and H-02 and hold or reject it before then; at the cut-off it is accepted automatically with its annotations and released to the warehouse; head office records what the warehouse sent, partly or fully, over several despatches; and at the rep's next sync the tablet shows each sent order's status and per-line despatch.

**Capability-class stamp** — Frontier + extended reasoning for the acceptance and release slivers (T-7.1.1); Frontier workhorse for the other tight-loop and assisted tasks and scenario drafting; Fast mid-tier for T-7.2.2. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [head-office-order-processing.md](../../stories/head-office-order-processing.md), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-014), [02-head-office.md](../../uxdocs/02-head-office.md) (H-01, H-02, H-03, H-06, H-31), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-08), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-001, BR-NEW-007, BR-NEW-009, Head Office US-006 amendment).

---

### T-7.1.1-S — Test scenarios for acceptance at the cut-off and release

**Owner** — Human-Led
**Gates** — T-7.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Cut-off times per weekday (for example 4pm Monday to Thursday, 1pm Friday, none at weekends): an order synced at 15:59, 16:00 and 16:01 on a Thursday — when is each accepted?
- An order synced on Saturday: accepted at Monday's cut-off?
- What happens if the acceptance job is down at 16:00 — accepted late, or at the next cut-off?
- Daylight-saving changes and the business time zone (MI-53).
- Release to the warehouse (MI-04): what is sent, in what format, and what proves it was received? What happens if the hand-off fails?
- Changing a cut-off time with orders already waiting.

---

### T-7.1.1 — Accept orders automatically at the cut-off and release them to the warehouse

**Parent story**

> As a Head Office User, I want orders that are within policy to go through without my acceptance so that my worklist only holds things that genuinely need a decision.
>
> Acceptance criteria:
> - A synced order with no flags or only annotate flags is accepted and released to the warehouse without a human step at the next order cut-off, with its flags recorded (S1; BR-NEW-009)
> - A synced or placed order is Pending until the next cut-off (BR-NEW-009 rule 1)
> - The company sets a cut-off time for each day of the week, and a day may have none; an order after a day's cut-off or on a day without one waits for the next (BR-NEW-009 rule 4)

**Slice** — Every uploaded order is Pending until the next weekday cut-off, when it is accepted without a person and its quantities are released to the warehouse; a day with no cut-off rolls the order to the next one.
**Spec source** — Head Office US-008 S1 and its 26 Sep 2026 amendment; glossary (Accept, Release, Order states); uxdocs 04 BR-NEW-001, BR-NEW-009; uxdocs 02 H-01 (H1.6)
**Depends on** — T-4.1.2
**Pattern to follow** — novel — see design notes
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — the order lifecycle and an external hand-off: the release mechanism and where the cut-off is set are undefined (MI-04, MI-05); time edge cases need a human oracle (MI-53).

**Provisional commit message**

```
feat(order-processing): accept orders at the cut-off and release them

- No person accepts orders: guardrails at capture replace review, and the
  cut-off gives head office a window to hold or reject by hand
- Cut-offs are per weekday because the warehouse's pick times differ by day
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A time-driven state machine with an external hand-off.

**Work package**

Increments:
1. Server order lifecycle: Pending → Accepted → (partly sent / fully sent via T-7.3.1); Held and Rejected arrive in T-7.2.1. Every transition recorded with time and cause.
2. Cut-off configuration per weekday, with days allowed to have none (MI-05: a system setting until a screen is chosen).
3. Next-cut-off calculation in the business time zone (MI-53) as a pure function; verify with the agreed scenarios.
4. Acceptance job at each cut-off: accept every Pending, not-held order; record annotations (via T-7.4.1 once it exists); idempotent if run twice.
5. Release: hand the accepted quantities to the warehouse by the agreed mechanism (MI-04), recording what was released and when; an order is never split.
6. A failed release is visible and retried, never silently lost.

Decision points:
- What is the release mechanism — file export, API call, email report (MI-04)? Who confirms receipt?
- Where does the cut-off setting live and who may change it (MI-05)?
- If the job misses a cut-off (outage), are waiting orders accepted on recovery or at the next cut-off?
- Is acceptance of each order its own transaction, so one bad order can't block the batch?

Delegable slivers:
- **Next-cut-off function** — Implement `nextCutOff(now, weeklySchedule, timeZone)` as a pure function returning the next cut-off instant, with days that have none skipped. Given the agreed scenarios from T-7.1.1-S, write its tests. No scheduling or persistence.
- **Release adapter** — Once the mechanism is chosen, implement an adapter behind `release(order) → confirmation | failure` with a test double. Do not call it from the acceptance job.
- **Lifecycle transition log** — Record each order state transition with time and cause in an append-only log, called from existing transitions. Do not add or change transitions.

---

### T-7.1.2-S — Test scenarios for viewing orders at head office

**Owner** — Scenario Review
**Gates** — T-7.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for head office's order views: the H-01 landing
page, the H-31 order list and the H-02 order record (task T-7.1.2). Read
plan_docs/stories/head-office-order-processing.md US-008 S5–S7 and
plan_docs/uxdocs/02-head-office.md H-01 (H1.6, H1.8), H-02 (H2.3, H2.6, H2.7)
and H-31 (H31.1–H31.3). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover the
story scenarios and each drafting call, then derivable edges: an order with
no annotations, filters combined, an order placed online (rep shown as
"Online"). Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-7.1.2 — View orders on H-01, H-31 and the H-02 record

**Parent story**

> As a Head Office User, I want orders that are within policy to go through without my acceptance so that my worklist only holds things that genuinely need a decision.
>
> Acceptance criteria:
> - With no range proposals, duplicate matches or account requests, the Worklist reads "Nothing needs a decision." with no action (S5)
> - An order's detail shows the lines as captured, any line removed automatically with its reason, applied rep prices and free-of-charge lines with their working, the annotations as plain sentences, and "Total as captured" and "Total accepted" labelled separately (S6)
> - There are no Accept, Partial Release or per-line override and free-goods decisions on the detail (S7)

**Slice** — Head office lands on a Worklist that reads "Nothing needs a decision." with a quiet "View orders >" link to an order list filtered to today's Pending orders, and each order opens as a read-only record with its lines, notes and two labelled totals.
**Spec source** — Head Office US-008 S5–S7; uxdocs 02 H-01 (H1.6, H1.8), H-02 (H2.3, H2.6, H2.7), H-31 (H31.1–H31.3)
**Depends on** — T-7.1.1
**Pattern to follow** — T-1.7.1 (head office list), T-3.2.1 (record page)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — H-31 is a draft awaiting confirmation (MI-08); read-only views over the lifecycle.

**Provisional commit message**

```
feat(order-processing): view orders without putting them on the worklist

- The Worklist holds only things that need a decision, so orders are
  reached by a quiet link, not listed where they'd demand attention
- The order record shows two totals because the difference is exactly the
  conversation the rep must have with the customer
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Views on a drafted screen with a review pause.

**Agent prompt**

```
Role: You are building head office's order views — the H-01 landing page, the
H-31 order list and the H-02 order record — for the Field Sales Management
System.

Context:
- Slice: head office lands on a Worklist that reads "Nothing needs a
  decision." with a quiet "View orders >" link to an order list filtered to
  today's Pending orders; each order opens as a read-only record with its
  lines, notes and two labelled totals.
- Specs: plan_docs/stories/head-office-order-processing.md US-008 S5–S7;
  plan_docs/uxdocs/02-head-office.md H-01 (H1.6 orders leave the worklist;
  H1.8 "View orders >" link), H-02 (frame; H2.3 two totals; H2.6 record not
  decision; H2.7 annotations as plain sentences under "Notes on this order"),
  H-31 (H31.1 opens on today's Pending; H31.2 filters status, date, Location,
  rep incl. "Online", search; H31.3 footer counts, no actions).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — order lifecycle
  (T-7.1.1), orders as received (T-4.1.2).
- Pattern to follow: T-1.7.1's list and T-3.2.1's record page.

Acceptance criteria:
1. With nothing to decide, H-01 reads "Nothing needs a decision." with no
   action, and a quiet "View orders >" link below.
2. H-31 opens filtered to today's Pending orders, showing order number,
   Location, rep (or "Online"), status with its cut-off ("Pending - 4pm") and
   value; filters for status, date, Location and rep, and a search; a footer
   such as "58 orders today · 41 Pending until 4pm · 2 Held".
3. Each row opens H-02: captured-by, capture time and acceptance time; "Notes
   on this order" as sentences; lines as captured; "Total as captured" and
   "Total accepted" labelled separately.
4. H-02 has no Accept, Partial Release or per-line decision controls.
5. Lines and quantities cannot be edited anywhere on these pages.

Constraints:
- Use the project's existing conventions and test framework.
- Read-only; follow the confirmed H-31 (MI-08) and the settled H-01 and H-02.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after H-31 and H-02 render with sample orders, stop and show them
against the frames. Resume only on "Continue T-7.1.2".

Steps: 1. order list query and filters; 2. H-01 landing with empty state and
link; 3. H-31; 4. H-02 record; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-7.1.2-S; do not design
your own.

Definition of done: head office lands on a Worklist that reads "Nothing needs
a decision." with a quiet "View orders >" link to an order list filtered to
today's Pending orders, and each order opens as a read-only record with its
lines, notes and two labelled totals.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Layouts match H-01, H-02 and the confirmed H-31
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Hold and Reject (T-7.2.1), held orders (T-7.2.2), annotations'
content (T-7.4.1), removed lines (E8), rep prices and FOC working (E19),
Worklist item types (E22, E23).
```

**Checkpoint**

Produces before pausing — H-31 and H-02 rendered with sample orders.
Human reviews — Do they match the frames, with nothing on either page that could change an order?
Resume trigger — `Continue T-7.1.2`

---

### T-7.2.1-S — Test scenarios for Hold and Reject before the cut-off

**Owner** — Human-Led
**Gates** — T-7.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Hold at 15:59:59 with a 16:00 cut-off: what guarantees the order is not accepted? And Hold at 16:00:01?
- Release: "accepted at once" — including release to the warehouse immediately, outside the cut-off?
- Can a held order be held again after release, or rejected after release?
- What do the rep and the customer see for each state ("On hold — we'll be in touch"; "Rejected — Account on hold")? The hold note is internal.
- Work is judged as captured (BR-NEW-006): what may a Hold or Reject reason never be based on?
- Can the rep or customer edit a held order (no)?

---

### T-7.2.1 — Hold or reject a pending order before the cut-off

**Parent story**

> As a Head Office User, I want a list of Held Orders with why each is held and a link to allocation so that nothing parked is forgotten.
>
> Acceptance criteria:
> - Hold at 2pm on Pending order O-10412 with the note "Credit stop - check with accounts" stops it being accepted at 4pm; it appears on Held orders with its note and time held; Colm and the customer see "On hold — we'll be in touch", not the note; Release accepts it at once (S4)
> - Reject requires a reason, which the rep and customer see (AC-HO006-A/B)
> - Once an order is accepted, neither Hold nor Reject is offered (AC-HO006-C)

**Slice** — While an order is Pending, head office can hold it with an internal note — so the cut-off skips it until someone releases or rejects it — or reject it with a reason the rep and customer see; neither is offered once accepted.
**Spec source** — Head Office US-006 S4 and its 26 Sep 2026 amendment; uxdocs 04 Head Office US-006 amendment (AC-HO006-A–C); uxdocs 02 H-02 (H2.9)
**Depends on** — T-7.1.2
**Pattern to follow** — T-7.1.1 (lifecycle transitions)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — modifies the order lifecycle (T-7.1.1), so characterisation comes first; a hold racing the cut-off job is a data-integrity risk.

**Provisional commit message**

```
feat(order-processing): hold or reject a pending order before the cut-off

- A credit stop or a suspected mistake needs a person, so the window
  before the cut-off lets head office step in without reviewing every order
- A hold stays internal; the rep and customer are told it's on hold
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Lifecycle change with a concurrency risk; slivers stay narrow.

**Work package**

Increments:
1. Characterisation: pin T-7.1.1's lifecycle and acceptance job behaviour with tests before changing it.
2. Held and Rejected states; Hold (note required, internal), Reject (reason required, shown), Release (accept at once and release to the warehouse).
3. Make Hold and the acceptance job mutually exclusive per order (lock or state check inside the job's transaction), so no order is accepted after a successful Hold.
4. H-02 controls: "Pending - accepted at 4pm unless held" with Hold… and Reject…; inline note field; none once accepted.
5. Status wording for rep and customer: "On hold — we'll be in touch"; "Rejected — <reason>".

Decision points:
- Does Release also send the order to the warehouse immediately, or at the next release run?
- Can Reject follow a Hold, and Hold follow a Release?
- Is the rep told by the tablet at next sync only, or also another way?

Delegable slivers:
- **Hold and Reject dialogs** — Build the H-02 inline Hold note (required, internal) and Reject reason (required, shown) per the H2.9 frame, calling the existing transitions. Do not change the lifecycle.
- **Status wording map** — Map Held and Rejected to the rep and customer wording ("On hold — we'll be in touch", "Rejected — <reason>") in one function used by T-7.5.1. Read-only.
- **Race test** — Given the agreed scenarios, write a test that runs Hold and the acceptance job concurrently many times and asserts no held order is ever accepted. Test-only.

---

### T-7.2.2 — List held orders for release or rejection

**Parent story**

> As a Head Office User, I want a list of Held Orders with why each is held and a link to allocation so that nothing parked is forgotten.
>
> Acceptance criteria:
> - Held Orders shows each with its hold note and days held (S1; short products and "Allocate" arrive with E29)
> - Head office can Release or Reject from each row (S4; H-06)
> - H-01 shows "2 orders on hold (H-06) >" when any exist (H2.9 confirmed)

**Slice** — Head office sees every held order, oldest first, with its note and how long it has been held, releases or rejects it from the row, and H-01 counts held orders so none is forgotten.
**Spec source** — Head Office US-006 S1, S4; uxdocs 02 H-06 (first-pass settled), H-01 (H2.9 line)
**Depends on** — T-7.2.1
**Pattern to follow** — T-7.1.2 (H-31 list)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — a list over reviewed transitions; first-pass settled design; Blast Radius Low (the actions are T-7.2.1's).

**Provisional commit message**

```
feat(order-processing): list held orders so none is forgotten

- A held order is excluded from every cut-off until someone acts, so the
  list and the Worklist count keep it in front of head office daily
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A list reusing reviewed actions.

**Agent prompt**

```
Role: You are building the Held Orders list (H-06) for the Field Sales
Management System's head office website.

Context:
- Slice: head office sees every held order, oldest first, with its note and
  how long it has been held, releases or rejects it from the row, and H-01
  counts held orders.
- Specs: plan_docs/stories/head-office-order-processing.md US-006 S1, S4 and
  amendment; plan_docs/uxdocs/02-head-office.md H-06 and H-01 (the "2 orders
  on hold (H-06) >" line).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Hold, Release, Reject
  transitions (T-7.2.1); H-01 and H-31 (T-7.1.2).
- Pattern to follow: T-7.1.2's list.

Acceptance criteria:
1. H-06 lists held orders oldest first with order number, Location, hold
   note, time held ("3 days") and Release / Reject per row.
2. Release accepts the order at once (T-7.2.1's transition); Reject asks for
   the shown reason.
3. H-01 shows "N orders on hold (H-06) >" when N > 0, opening H-06.
4. With none held, H-06 reads that there are no held orders and H-01 shows no
   line.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-7.2.1's transitions; no new rules.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. held-orders query; 2. H-06 list; 3. H-01 count line; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: head office sees every held order, oldest first, with
its note and how long it has been held, releases or rejects it from the row,
and H-01 counts held orders so none is forgotten.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: short products and the "Allocate" link (E29), the "(!)
Location closed" flag (E17).
```

---

### T-7.3.1-S — Test scenarios for recording despatches

**Owner** — Scenario Review
**Gates** — T-7.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for recording despatches from the warehouse
report (task T-7.3.1). Read
plan_docs/stories/head-office-order-processing.md US-004 S1–S5 and glossary
(Release, Outstanding, Despatch, Order states), and
plan_docs/uxdocs/02-head-office.md H-03 (H3.1, H3.2). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover S1–S5, then derivable edges: a zero despatch for a line, a despatch
dated in the past, a measure-based line, a correction to a despatch entered
wrongly. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-7.3.1 — Record what the warehouse despatched, in one or several goes

**Parent story**

> As a Head Office User, I want to record what the warehouse actually sent against an order's lines, as many times as needed, so that reps and customers can see what has gone and what is still outstanding.
>
> Acceptance criteria:
> - For an Accepted Order with 6 lines all Outstanding, Record Despatch pre-fills each at its Outstanding quantity with today's date; confirming makes the Order Accepted (all Sent) (S1)
> - Changing SPF30 from 36 to 24 and confirming shows "24 sent 12 Oct, 12 outstanding" and the Order is Accepted — partly sent (S2)
> - Recording the remaining 12 on 19 Oct shows "24 sent 12 Oct · 12 sent 19 Oct" and the Order is Accepted (S3)
> - Entering 40 for a line with 36 outstanding is rejected with "Only 36 outstanding" (S4)
> - After the rep syncs, the Order shows "Accepted, partly sent — 12 of 36 SPF30 outstanding" (S5; delivered on the tablet by T-7.5.1)

**Slice** — Head office records a despatch against an accepted order with every outstanding line pre-filled at its remaining quantity and today's date, adjusts short lines, and the order's lines show cumulative sent and outstanding quantities.
**Spec source** — Head Office US-004 S1–S5; uxdocs 02 H-03 (H3.1, H3.2)
**Depends on** — T-7.1.1
**Pattern to follow** — T-7.1.2 (H-02 record)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — outstanding quantities feed the rep, the customer and allocation later (Blast Radius High, so not autonomous); checkpoint on the outstanding model; H-03 is a draft.

**Provisional commit message**

```
feat(order-processing): record despatches with outstanding per line

- Reps must be able to answer "has it gone?", so despatches are recorded
  from the warehouse report, per line, as many times as needed
- "Everything shipped" is one action: outstanding lines arrive pre-filled
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Fulfilment quantities with a design pause.

**Agent prompt**

```
Role: You are building despatch recording (H-03) for the Field Sales
Management System's head office website.

Context:
- Slice: head office records a despatch against an accepted order with every
  outstanding line pre-filled at its remaining quantity and today's date,
  adjusts short lines, and the order's lines show cumulative sent and
  outstanding quantities.
- Specs: plan_docs/stories/head-office-order-processing.md US-004 S1–S5,
  glossary (Outstanding, Despatch, Order states) and design decisions "One
  Order, never split; fulfilment tracked per line", "Despatch is entered at
  head office, feed-ready"; plan_docs/uxdocs/02-head-office.md H-03 (H3.1
  "Sending now" pre-filled at the full remaining quantity; H3.2 cumulative
  history in its own column).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — order lifecycle
  (T-7.1.1), order record (T-7.1.2).
- Pattern to follow: T-7.1.2's record page.

Acceptance criteria:
1. For an Accepted Order with 6 lines all outstanding, Record Despatch
   pre-fills each line's "Sending now" at its outstanding quantity with
   today's date; confirming makes the Order Accepted with every line sent.
2. Changing SPF30 from 36 to 24 and confirming shows "24 sent 12 Oct, 12
   outstanding" and the Order is "Accepted — partly sent".
3. Recording the remaining 12 on 19 Oct shows "24 sent 12 Oct · 12 sent 19
   Oct" and the Order is Accepted.
4. Entering 40 for a line with 36 outstanding is rejected with "Only 36
   outstanding".
5. Despatch history per line is kept, never overwritten.
6. The model accepts despatches from a future warehouse feed without change
   (feed-ready): each despatch is a dated record of quantities per line.

Constraints:
- Use the project's existing conventions and test framework.
- An order is never split; lines are never edited; only despatch records
  are added.
- No tests of framework internals or trivial members.
- This task opts in to a despatch-record table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the despatch and outstanding model is designed, stop and
show it with the S1–S3 sequence worked through. Resume only on
"Continue T-7.3.1".

Steps: 1. despatch records and derived outstanding; 2. state derivation
(Accepted / partly sent); 3. H-03 page; 4. validation; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-7.3.1-S; do not design
your own.

Definition of done: head office records a despatch against an accepted order
with every outstanding line pre-filled at its remaining quantity and today's
date, adjusts short lines, and the order's lines show cumulative sent and
outstanding quantities.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Outstanding is derived, never stored twice
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: tablet display of despatch (T-7.5.1), allocation of short stock
(E29), a warehouse feed integration.
```

**Checkpoint**

Produces before pausing — the despatch and outstanding model with S1–S3 worked through.
Human reviews — Can outstanding ever go negative or disagree with the despatch history, and will a warehouse feed fit without change?
Resume trigger — `Continue T-7.3.1`

---

### T-7.4.1-S — Test scenarios for order annotations

**Owner** — Scenario Review
**Gates** — T-7.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for order annotations shown as plain sentences
(task T-7.4.1). Read plan_docs/stories/head-office-order-processing.md US-002
S1, S2, glossary (Flag, Disposition) and assumptions ("New Location" until 3
Orders have been Accepted), and plan_docs/uxdocs/02-head-office.md H-02
(H2.7). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Cover S1 (as annotations, not a Flagged section)
and S2, then derivable edges: a Location's 1st, 3rd and 4th orders, a
rejected order in the count, several annotations on one order ordered most
consequential first. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-7.4.1 — Record order annotations as plain sentences, starting with New location

**Parent story**

> As a Head Office User, I want each flagged order to state its flags in plain terms so that I know what to look at before opening it.
>
> Acceptance criteria:
> - An order's flags read as plain sentences, for example "Large — 3.2× target for October · Unavailable line — Autumn Cough Syrup 100ml · Rep-flagged" (S1; each flag arrives with its owning task)
> - Walsh's Shop with 1 Accepted Order: its next Order is annotated "New location — 2nd order" (S2)

**Slice** — When an order is accepted, the system records each annotation that applies as a plain sentence under "Notes on this order", beginning with "New location — 2nd order" for a shop with fewer than three accepted orders.
**Spec source** — Head Office US-002 S1, S2 and its "Superseded in part" status; assumptions (New Location threshold); uxdocs 02 H-02 (H2.7); uxdocs 04 BR-NEW-001 (Annotate)
**Depends on** — T-7.1.2
**Pattern to follow** — T-7.1.1 (acceptance job)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — establishes the annotation list later flags join (Large, Watched, Oversold, Prospect); the threshold is a setting (MI-12).

**Provisional commit message**

```
feat(order-processing): annotate orders in plain sentences

- Flags no longer stop an order; they are recorded as sentences so anyone
  reading the order later knows what was notable about it
- New location lasts until three orders are accepted
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Sets the annotation pattern with a review pause.

**Agent prompt**

```
Role: You are adding order annotations to the Field Sales Management System's
order processing.

Context:
- Slice: when an order is accepted, the system records each annotation that
  applies as a plain sentence under "Notes on this order", beginning with
  "New location — 2nd order" for a shop with fewer than three accepted
  orders.
- Specs: plan_docs/stories/head-office-order-processing.md US-002 S1, S2,
  glossary (Flag, Disposition), assumptions; plan_docs/uxdocs/02-head-office.md
  H-02 (H2.7 sentences, most consequential first);
  plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-001.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — acceptance job
  (T-7.1.1), H-02 (T-7.1.2).
- Pattern to follow: add a pluggable annotation step to the acceptance job.

Acceptance criteria:
1. A Location with 1 Accepted Order: its next order is annotated "New
   location — 2nd order" at acceptance.
2. Annotations appear on H-02 under "Notes on this order" as sentences, most
   consequential first.
3. An annotation never stops, holds or changes an order.
4. The New Location threshold (3 accepted orders) is a setting.
5. Other annotations (Large, Watched Product, Oversold, Prospect Conversion,
   Rep-flagged) can be added later as separate annotators without changing
   this code.

Constraints:
- Use the project's existing conventions and test framework.
- Annotations are recorded with the order at acceptance and never
  recalculated (valid when captured).
- No tests of framework internals or trivial members.
- This task opts in to an order-annotation table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the annotator interface and the New Location annotator are
written, stop and show them. Resume only on "Continue T-7.4.1".

Steps: 1. annotator interface; 2. New Location annotator; 3. storage;
4. H-02 display; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-7.4.1-S; do not design
your own.

Definition of done: when an order is accepted, the system records each
annotation that applies as a plain sentence under "Notes on this order",
beginning with "New location — 2nd order" for a shop with fewer than three
accepted orders.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Rep-flagged (T-7.4.2), Oversold (E10), Watched Product (E10),
Prospect Conversion (E21), Large (E28).
```

**Checkpoint**

Produces before pausing — the annotator interface and the New Location annotator.
Human reviews — Can each later flag be added as an annotator without touching the acceptance job?
Resume trigger — `Continue T-7.4.1`

---

### T-7.4.2-S — Test scenarios for rep-flagged orders

**Owner** — Human-Led
**Gates** — T-7.4.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- No story defines the tablet control (MI-09). Where does the rep set the flag — on Review before Mark Ready to Send? Can it be removed before sync?
- Is the note optional free text (HO Requires Clarification 4)? Maximum length?
- What does head office see: "Rep-flagged: <note>"? Does the flag change anything else (no — Annotate only)?

---

### T-7.4.2 — Let a rep flag an order for head office's attention

**Parent story**

> As a Head Office User, I want each flagged order to state its flags in plain terms so that I know what to look at before opening it.
>
> Acceptance criteria:
> - An order the rep flagged shows "Rep-flagged" among its annotations (S1)
> - {{NEEDS ACCEPTANCE CRITERIA}} for the tablet control that sets the flag (MI-09)

**Slice** — A rep marks an order "please look at this" with an optional note before sending it, and head office reads it as an annotation on the order.
**Spec source** — Head Office US-002 S1; glossary (Rep-flagged); design decision "Rep-flagging is real but informal"; Requires Clarification 4, 5
**Depends on** — T-7.4.1, T-5.1.1
**Pattern to follow** — T-7.4.1 (annotator)
**Ownership** — Impl: Human Tight-Loop ↓ | Test: Human-Led | Complexity: S | Confidence: L
  (inferred) — would be Agent-Assisted; downgraded one step because no story scenario defines the tablet control (MI-09), so Ambiguity is High.

**Provisional commit message**

```
feat(orders): let a rep flag an order for head office

- The rep sometimes knows something the data doesn't; the flag is informal
  and changes nothing on its own, it is read as an annotation
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Small, but its behaviour is undefined until decided.

**Work package**

Increments:
1. Agree the tablet control and write its acceptance criteria (MI-09), replacing `{{NEEDS ACCEPTANCE CRITERIA}}`.
2. Order field for the rep flag and optional note, captured on the tablet.
3. Rep-flagged annotator added to T-7.4.1's interface.

Decision points:
- Placement of the control (Review screen is the natural place; T7.2 puts overrides on Review too).
- Whether flagging is shown back to the rep on the sent order.

Delegable slivers:
- **Rep-flagged annotator** — Once the field exists, implement an annotator that adds "Rep-flagged" (with the note, if any) to an order's annotations at acceptance, using T-7.4.1's interface. Do not change the interface.

---

### T-7.5.1-S — Test scenarios for sent items on the tablet

**Owner** — Scenario Review
**Gates** — T-7.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for viewing sent calls and orders on the tablet
(task T-7.5.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-014 S1,
S3, S4, S4b, S5; plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-007;
plan_docs/uxdocs/01-tablet-day.md T-08 (T8.1, T8.2). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover those scenarios, then derivable edges: last sync today versus
yesterday, an On hold order, several despatches on one line. Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-7.5.1 — Show a sent call or order's status and despatch on the tablet

**Parent story**

> As a Field Salesperson, I want to see a Sent item's last known status and open it on the website so that I answer customers honestly and make changes when I have signal.
>
> Acceptance criteria:
> - An Order Pending at the last Sync shows "Pending" with read-only lines, with "as of <date>" only when that sync was before today (S1; BR-NEW-007)
> - Offline, Open on website is shown unavailable with "Needs a connection" (S3)
> - An Order Rejected with "Account on hold" shows "Rejected" and the reason (S4)
> - Accepted with 24 of 36 despatched on one line shows "Accepted, partly sent" and "24 sent to customer 12 Oct · 12 outstanding" (S4b)
> - A Sent Call is read-only with Open on website and Record follow-up call (S5)

**Slice** — On the tablet, a sent order shows its last known status — Pending, On hold, Rejected with reason, Accepted or partly sent — with per-line despatch, dated only when the last sync wasn't today, and sent calls open read-only with a follow-up action.
**Spec source** — Rep at a Location US-014 S1, S3, S4, S4b, S5; uxdocs 01 T-08 (T8.1, T8.2); uxdocs 04 BR-NEW-007; Head Office US-006 amendment (held wording)
**Depends on** — T-7.3.1, T-7.2.1, T-4.1.1
**Pattern to follow** — T-4.2.2 (tablet screen)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — adds sent-item statuses to the snapshot; "Open on website" has no target until R-04 (MI-50).

**Provisional commit message**

```
feat(tablet): show sent orders' status and despatch honestly

- Reps must answer "has it gone through?" truthfully, so status and
  per-line despatch come from the last sync, dated only when that sync
  wasn't today
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Snapshot addition and a settled screen with a review pause.

**Agent prompt**

```
Role: You are building the tablet's sent item view (T-08) for the Field Sales
Management System.

Context:
- Slice: on the tablet, a sent order shows its last known status — Pending,
  On hold, Rejected with reason, Accepted or partly sent — with per-line
  despatch, dated only when the last sync wasn't today; sent calls open
  read-only with a follow-up action.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-014 S1, S3, S4,
  S4b, S5; plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-007;
  plan_docs/uxdocs/01-tablet-day.md T-08 (T8.1 the sync chip attaches to the
  status line only, and only when the last sync wasn't today; T8.2 "Open on
  website" shown and disabled with its reason offline);
  plan_docs/stories/head-office-order-processing.md US-006 amendment ("On
  hold — we'll be in touch").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — snapshot (T-4.1.1),
  lifecycle and holds (T-7.1.1, T-7.2.1), despatches (T-7.3.1), wording map
  (T-7.2.1), follow-up calls (T-5.7.1).
- Pattern to follow: T-4.2.2's screen.

Acceptance criteria:
1. A Pending order opened after a sync today reads "Pending" with read-only
   lines; after a sync on Mon 21 Sep (not today) it reads "Pending — as of Mon
   21 Sep".
2. An order Rejected with "Account on hold" shows "Rejected" and the reason.
3. A held order shows "On hold — we'll be in touch" (never the internal
   note).
4. An Accepted order with 24 of 36 SPF30 despatched shows "Accepted, partly
   sent" and the line "24 sent to customer 12 Oct · 12 outstanding".
5. A Sent Call is read-only with Record follow-up call.
6. Offline, "Open on website" is shown disabled with "Needs a connection".
   With signal, its behaviour follows the MI-50 decision (hidden, disabled,
   or a read-only order page) until R-04 exists in E13.
7. No edit, reopen or delete controls on any sent item.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions (per sent order: status, reason where shown, despatch
  per line) go through T-4.1.1's versioning and its owner's review.
- Wording comes verbatim from the criteria.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the snapshot additions are written and T-08 renders each
status from sample data, stop and show them. Resume only on
"Continue T-7.5.1".

Steps: 1. snapshot fields; 2. status wording with the stale-date rule;
3. per-line despatch; 4. sent call view; 5. Open on website states; 6. tests
from agreed scenarios.

Test expectations: implement the scenarios agreed in T-7.5.1-S; do not design
your own.

Definition of done: on the tablet, a sent order shows its last known status —
Pending, On hold, Rejected with reason, Accepted or partly sent — with
per-line despatch, dated only when the last sync wasn't today, and sent calls
open read-only with a follow-up action.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: removed lines "Not supplied" (T-8.6.1), rep prices and FOC as
facts (E19), R-04 website editing (E13).
```

**Checkpoint**

Produces before pausing — the snapshot additions and T-08 rendered for Pending, On hold, Rejected, Accepted and partly sent.
Human reviews — Is every status honest and dated only when stale, and is the internal hold note never shown?
Resume trigger — `Continue T-7.5.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | HO-008 (T-7.1.1, T-7.1.2), HO-006 (T-7.2.1, T-7.2.2), HO-004 (T-7.3.1), HO-002 (T-7.4.1, T-7.4.2), A1-014 (T-7.5.1); HO-001 and HO-003 listed as superseded with no tasks |
| Every task satisfies the three slice criteria | Pass | 8 of 8 |
| Every task carries a tier with a rationale citing dimensions | Pass | 8 of 8 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-7.1.1 (MI-04, 05, 53), T-7.4.2 (MI-09), T-7.5.1 (MI-50) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-7.2.1 increment 1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-7.2.2 reuses reviewed transitions |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-7.1.1, T-7.2.1, T-7.4.2 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-7.1.2, T-7.3.1, T-7.4.1, T-7.5.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-7.1.1, T-7.2.1, T-7.4.2 |
| Every scenario task precedes the task it gates | Pass | 7 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, `{{NEEDS ACCEPTANCE CRITERIA}}` (T-7.4.2), MI-04, 05, 08, 09, 12, 50, 53 |
