# E17 — Location upkeep

**Iteration** — 4, Team changes and location upkeep
**Outcome** — The customer base stays accurate: shops shut for a while block scheduling without disappearing, shops that close for good stop being scheduled and ordered for while keeping their history, reps fix a shop's map position standing in it, and head office works one list of every shop that's missing something.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Customer Directory US-010 — Record a temporary closure | Should | S1–S7 |
| 2 | Customer Directory US-011 — Close or reopen a Location | Must | S1–S5 |
| 3 | Customer Directory US-005 — Capture a Location's position from the field | Should | S1–S3, S5, S6 (S4 superseded by S6) |
| 4 | Customer Directory US-008 — Work the gap lists | Should | S1–S4 |
| 5 | Head Office US-006 (part) — Work Held Orders | Should | S3 "Location closed" flag |

**Exit criterion** — Head office or the rep records a temporary closure that blocks scheduling in its window (open-ended if needed), and head office closes or reopens a shop permanently with its visits cancelled, ordering stopped and branches flagged; reps capture a shop's GPS position with a distance check and head office can revert it; and one gap list shows every shop missing a profile, a rep, an active main contact or a confirmed position, fixable in place.

**Capability-class stamp** — Frontier + extended reasoning for permanent closure (T-17.2.1); Frontier workhorse for other tasks and scenario drafting; Fast mid-tier for T-17.5.1. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [customer-directory.md](../../stories/customer-directory.md) (US-005, US-008, US-010, US-011), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-006), [02-head-office.md](../../uxdocs/02-head-office.md) (H-06, H-26, H-30), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-02, T-05, T5.3).

---

### T-17.1.1-S — Test scenarios for temporary closures

**Owner** — Scenario Review
**Gates** — T-17.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for recording a temporary closure from head
office (task T-17.1.1). Read plan_docs/stories/customer-directory.md US-010
S1–S3, S5–S7, glossary (Temporarily Closed) and design decision "Temporary
closure is a window, not a state change". Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover them,
then derivable edges: a closure starting today with a visit scheduled today,
two overlapping closures, a closure on a master Location. Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-17.1.1 — Record a temporary closure that blocks scheduling in its window

**Parent story**

> As a Head Office User or Field Salesperson, I want to record that a shop is shut until a given date so that nobody drives out there and the visit is scheduled for after it reopens.
>
> Acceptance criteria:
> - Byrne's Temporarily Closed 28 September – 14 October stays active, its Visit Dues keep generating, and those days can't be scheduled (S1)
> - A due with window 28 Sep – 25 Oct shows "Closed until 14 Oct" with 15 October the earliest schedulable day (S2)
> - A due whose window ends 10 October can't be scheduled and appears for the manager as needing Extend, like an absence-affected visit (S3)
> - No reopen date blocks scheduling until one is set or the closure removed, showing "Closed — reopen date unknown" (S5)
> - Changing the date to 8 October makes days from 8 October schedulable again (S6)
> - Orders can still be created, sent and accepted throughout (S7)

**Slice** — Head office records that a shop is shut for a date range — or until further notice — so its days can't be scheduled, visits due in the window point after the reopening, visits wholly inside it need an Extend decision, and ordering carries on.
**Spec source** — Customer Directory US-010 S1–S3, S5–S7; uxdocs 02 H-26 (H26.2)
**Depends on** — T-13.3.1, T-2.3.1, T-15.2.1
**Pattern to follow** — T-15.1.1 (blocked days)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the Location-side twin of an absence; reuses the blocked-day and Extend machinery.

**Provisional commit message**

```
feat(customers): record temporary closures that block scheduling

- A refurbishing shop is still a customer, so its visits keep generating
  and are planned for after it reopens rather than disappearing
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Reuses reviewed scheduling rules with a pause.

**Agent prompt**

```
Role: You are adding temporary closures (H-26) to Locations in the Field
Sales Management System.

Context:
- Slice: head office records that a shop is shut for a date range — or until
  further notice — so its days can't be scheduled, visits due in the window
  point after the reopening, visits wholly inside it need an Extend decision,
  and ordering carries on.
- Specs: plan_docs/stories/customer-directory.md US-010 S1–S3, S5–S7,
  glossary (Temporarily Closed); plan_docs/uxdocs/02-head-office.md H-26
  (H26.2 temporary and permanent closure are separate actions).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Location record
  (T-2.3.1), scheduling and blocked days (T-13.3.1, T-15.1.1), Extend
  decisions (T-15.2.1).
- Pattern to follow: T-15.1.1's blocked days, applied per Location.

Acceptance criteria:
1. Recording Byrne's Temporarily Closed 28 September – 14 October keeps it
   active; its Visit Dues keep generating; those days can't be scheduled for
   Byrne's.
2. A due with window 28 Sep – 25 Oct shows "Closed until 14 Oct"; the
   earliest schedulable day is 15 October.
3. A due whose window ends 10 October can't be scheduled and appears for the
   manager as needing Extend, like an absence-affected visit.
4. With no reopen date, scheduling is blocked until one is set or the
   closure is removed; the Location shows "Closed — reopen date unknown".
5. Changing the date to 8 October makes days from 8 October schedulable.
6. Orders for Byrne's can still be created, sent and accepted.

Constraints:
- Use the project's existing conventions and test framework; reuse the
  blocked-day and Extend machinery.
- No tests of framework internals or trivial members.
- This task opts in to a temporary-closure record on Locations.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after closures block scheduling and route wholly-inside dues to
Extend on sample data, stop and show it. Resume only on "Continue T-17.1.1".

Steps: 1. closure record; 2. H-26 action; 3. scheduling block; 4. Extend
routing; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-17.1.1-S; do not design
your own.

Definition of done: head office records that a shop is shut for a date range
— or until further notice — so its days can't be scheduled, visits due in the
window point after the reopening, visits wholly inside it need an Extend
decision, and ordering carries on.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: changing the date from the tablet (T-17.1.2), permanent closure
(T-17.2.1).
```

**Checkpoint**

Produces before pausing — scheduling blocked by a closure and wholly-inside dues routed to Extend, on sample data.
Human reviews — Does a closure behave exactly like an absence for the shop's visits, and never block ordering?
Resume trigger — `Continue T-17.1.1`

---

### T-17.1.2-S — Test scenarios for changing a closure from the tablet

**Owner** — Scenario Review
**Gates** — T-17.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for extending or shortening a temporary closure
from the tablet (task T-17.1.2). Read plan_docs/stories/customer-directory.md
US-010 S4 and Requires Clarification 6 ("Closed until" on Home and the
Location, rep able to extend or shorten it). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S4, then
derivable edges: head office changing the date while the rep's change is
unsent, shortening to today, a rep at a shop with no closure recorded. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-17.1.2 — Extend or shorten a closure from the tablet

**Parent story**

> As a Head Office User or Field Salesperson, I want to record that a shop is shut until a given date so that nobody drives out there and the visit is scheduled for after it reopens.
>
> Acceptance criteria:
> - A rep arriving on 15 October to a shop still shut until month end changes the reopen date to 31 October; it's saved as unsent, uploads at next Sync, and the planner and Home update after it syncs (S4)
> - "Closed until 14 Oct" shows on Home and the Location (Requires Clarification 6)

**Slice** — On the tablet, a shop's temporary closure shows as "Closed until 14 Oct" on Home and the Location, and a rep standing at a still-shut shop changes the reopen date, which uploads at the next sync.
**Spec source** — Customer Directory US-010 S4; Requires Clarification 6
**Depends on** — T-17.1.1, T-13.9.1
**Pattern to follow** — T-13.6.1 (offline changes uploaded at sync)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — a new upload item on the sync contract.

**Provisional commit message**

```
feat(tablet): extend or shorten a shop's closure from the tablet

- The rep at the door usually learns the reopening date first, so they can
  update it on the spot and it reaches the planner at the next sync
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A sync contract addition with a review pause.

**Agent prompt**

```
Role: You are adding temporary-closure display and editing to the Field Sales
Management System's tablet app.

Context:
- Slice: a shop's temporary closure shows as "Closed until 14 Oct" on Home and
  the Location, and a rep standing at a still-shut shop changes the reopen
  date, which uploads at the next sync.
- Specs: plan_docs/stories/customer-directory.md US-010 S4 and Requires
  Clarification 6.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — closures (T-17.1.1),
  Home visits (T-13.9.1), Location screen (T-4.2.2), upload items (T-13.6.1).
- Pattern to follow: T-13.6.1's upload item.

Acceptance criteria:
1. A temporarily closed shop shows "Closed until 14 Oct" (or "Closed — reopen
   date unknown") on Home and the Location.
2. The rep changes the reopen date to 31 October; it saves as unsent and
   uploads at next sync; the planner and Home update after the sync.
3. The rep can shorten it (reopen early) the same way.

Constraints:
- Use the project's existing conventions and test framework.
- The change is an upload item under T-4.1.2's protocol; the snapshot owner
  reviews the contract change.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the upload item and how the server applies it (including a
head office change made meanwhile) are designed, stop and show them. Resume
only on "Continue T-17.1.2".

Steps: 1. display on Home and Location; 2. date editor; 3. upload item;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-17.1.2-S; do not design
your own.

Definition of done: on the tablet, a shop's temporary closure shows as
"Closed until 14 Oct" on Home and the Location, and a rep standing at a
still-shut shop changes the reopen date, which uploads at the next sync.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: recording closures from head office (T-17.1.1).
```

**Checkpoint**

Produces before pausing — the closure-change upload item and the server's handling of a concurrent head office change.
Human reviews — Which change wins if head office and the rep both edit the date before the sync?
Resume trigger — `Continue T-17.1.2`

---

### T-17.2.1-S — Test scenarios for closing and reopening a shop

**Owner** — Human-Led
**Gates** — T-17.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Closing Doyle's (1 open due, 1 Pending order, 1 In Progress order on a tablet) from 30 September: the due is Cancelled (not Missed); no new dues; the Pending order unchanged; the In Progress order still sends; no new order can be started (S1). Is the Pending order accepted at the cut-off as normal?
- On the tablet after sync: findable labelled "Closed 30 Sep 2026", actions withdrawn, gone from Home and Suggested Lists (S2).
- A closed master with 12 branches: "12 branches report to this location — reassign their master?", and the branches show "Master location closed" on the gap list (S3).
- Reopen: scheduling from the next Cycle Period, back on Home, Closed Date kept in history (S4).
- Coverage: stays with its Primary Rep in history, excluded from the rep's active Location count (S5). And from targets?
- A held order at a closing Location (T-17.5.1).

---

### T-17.2.1 — Close a shop permanently, or reopen it

**Parent story**

> As a Head Office User, I want to mark a Location as permanently Closed so that it stops being scheduled and ordered for, while its history, existing orders and searchability remain.
>
> Acceptance criteria:
> - Closing Doyle's from 30 September cancels its open Visit Due (not Missed), stops new dues, leaves the Pending Order unchanged, lets the In Progress Order still send, and blocks new orders (S1)
> - After syncing, searching "Doyle" shows it labelled "Closed 30 Sep 2026" with Record Call and New Order withdrawn; it leaves Home and Suggested Lists (S2)
> - Closing Hickey's Head Office asks "12 branches report to this location — reassign their master?" and flags the 12 "Master location closed" (S3)
> - Reopening returns it to scheduling from the next Cycle Period and to Home, keeping its Closed Date in history (S4)
> - It stays with its Primary Rep in history and leaves the rep's active Location count (S5)

**Slice** — Head office closes a shop from a date after seeing the effect: its open visits are cancelled, no new visits or orders start, existing orders still go through, it stays findable labelled Closed, and it can be reopened later without losing its history.
**Spec source** — Customer Directory US-011 S1–S5, glossary (Closed), design decision "Closed, never deleted"; uxdocs 02 H-26 (H26.2 permanent close uses the impact preview)
**Depends on** — T-13.1.1, T-4.2.1, T-12.2.1
**Pattern to follow** — T-3.1.2 (impact preview)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — one state change reaching visits, ordering, the tablet, coverage and branches (Blast Radius High); many cross-area cases.

**Provisional commit message**

```
feat(customers): close or reopen a location without deleting it

- Orders, calls and performance reference the shop, so closing stops
  scheduling and new orders but keeps history, existing orders and search
- Open visits are cancelled rather than left to go Missed
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A state change with effects across many areas.

**Work package**

Increments:
1. Closed state with Closed Date on Locations; H-26 "Close…" with impact preview (open visits cancelled, orders unaffected, branches to reassign).
2. Visit Planning: cancel open dues as Cancelled; stop generation from the Closed Date.
3. Ordering: block new orders on every surface; existing Pending and unsent orders go through.
4. Tablet snapshot: closed Locations stay searchable labelled "Closed 30 Sep 2026" with actions withdrawn; removed from Home and Suggested Lists.
5. Closed master: prompt to reassign branches; flag branches "Master location closed".
6. Reopen: restore scheduling from the next Cycle Period; keep the Closed Date in history.
7. Coverage: keep in history, exclude from the active Location count.

Decision points:
- Customer online ordering for a closed shop (E26): blocked the same way?
- Does a closed shop still count toward chain targets for the period (E27)?
- May an order dated before the Closed Date but synced after it be started? (Valid when captured says yes.)

Delegable slivers:
- **Closed label on the tablet** — Show "Closed <date>" on search results and the Location for closed shops in the snapshot, withdrawing Record Call and New Order. No snapshot logic.
- **Close impact preview** — Build the H-26 preview listing open visits to be cancelled, orders unaffected and branches to reassign, from existing queries. No state changes.
- **Reopen action** — Add Reopen to a Closed Location's record calling the existing reopen command, showing the kept Closed Date in history. No rule changes.

---

### T-17.5.1 — Flag a held order when its shop closes

**Parent story**

> As a Head Office User, I want a list of Held Orders with why each is held and a link to allocation so that nothing parked is forgotten.
>
> Acceptance criteria:
> - When its Location is marked Closed, a Held Order is flagged "Location closed" for a Reject or Release decision (S3; H-06 "(!) Location closed")

**Slice** — On the Held Orders list, an order whose shop has since closed is flagged "(!) Location closed" so head office decides whether to release or reject it.
**Spec source** — Head Office US-006 S3; uxdocs 02 H-06
**Depends on** — T-17.2.1, T-7.2.2
**Pattern to follow** — T-7.2.2 (held orders list)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — one flag on a reviewed list; Blast Radius Low (the decision stays with a person).

**Provisional commit message**

```
feat(order-processing): flag held orders whose shop has closed

- A parked order for a shop that no longer trades needs a person's
  decision, so the list says so rather than letting it wait silently
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
One flag on an existing list.

**Agent prompt**

```
Role: You are adding the "Location closed" flag to the Held Orders list
(H-06) of the Field Sales Management System.

Context:
- Slice: on the Held Orders list, an order whose shop has since closed is
  flagged "(!) Location closed" so head office decides whether to release or
  reject it.
- Specs: plan_docs/stories/head-office-order-processing.md US-006 S3;
  plan_docs/uxdocs/02-head-office.md H-06.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Held Orders list
  (T-7.2.2), Closed state (T-17.2.1).
- Pattern to follow: T-7.2.2.

Acceptance criteria:
1. A Held Order whose Location is Closed shows "(!) Location closed" on its
   row, with Release and Reject as usual.
2. The flag disappears if the Location is reopened.
3. Orders for open Locations show no flag.

Constraints:
- Use the project's existing conventions and test framework.
- Read the Closed state; decide nothing automatically.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. flag in the list query; 2. row display; 3. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: on the Held Orders list, an order whose shop has since
closed is flagged "(!) Location closed" so head office decides whether to
release or reject it.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: automatic rejection (never done).
```

---

### T-17.3.1-S — Test scenarios for capturing a shop's GPS position

**Owner** — Scenario Review
**Gates** — T-17.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for capturing a Location's position from the
tablet's GPS (task T-17.3.1). Read plan_docs/stories/customer-directory.md
US-005 S1–S3, S5, S6 and non-functional notes, glossary (Coordinates, GPS
Capture) and assumptions (2 km distance threshold), and
plan_docs/uxdocs/01-tablet-day.md T-05 (T5.3). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Cover them,
then derivable edges: no GPS fix available, a capture exactly 2 km away, head
office reverting a capture that hasn't synced yet. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-17.3.1 — Capture a shop's position from the tablet's GPS

**Parent story**

> As a Field Salesperson, I want to set a shop's map position from my device while I'm standing in it so that the planner map and sat-nav are right next time, without anyone typing coordinates.
>
> Acceptance criteria:
> - At Rathdrum Pharmacy (Precision "Town"), Set location from GPS 300 m from the Town position saves as unsent with Precision "Confirmed on site", no confirmation (S1)
> - A reading 4 km away asks "This is 4 km from Rathdrum — save anyway?" (S2)
> - Offline it saves as unsent and uploads at next Sync; the Location shows "Confirmed on site (not yet sent)" (S3)
> - Head office reverting a capture restores the previous Eircode or Town position with its Precision (S5)
> - With Precision "Confirmed on site" the action isn't shown; after a revert it shows again (S6)

**Slice** — Standing in a shop whose position isn't confirmed, a rep sets it from the tablet's GPS — confirming only if it's far from where the shop was thought to be — it uploads at the next sync as "Confirmed on site", and head office can revert a bad capture.
**Spec source** — Customer Directory US-005 S1–S3, S5, S6, non-functional notes; uxdocs 01 T-05 (T5.3 shown only while unconfirmed, quiet, highlighted exactly once)
**Depends on** — T-2.3.2, T-4.2.2
**Pattern to follow** — T-13.6.1 (offline changes uploaded at sync)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — device GPS and a new upload item; the distance threshold is assumed (MI-23).

**Provisional commit message**

```
feat(tablet): capture a shop's position from the tablet's GPS

- The rep standing in the shop is the only person who knows the pin is
  right, so capture is one tap, checked only when it's far off, and
  reversible by head office
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Device capability and a sync addition with a review pause.

**Agent prompt**

```
Role: You are adding GPS position capture to the tablet Location screen
(T-05) in the Field Sales Management System.

Context:
- Slice: standing in a shop whose position isn't confirmed, a rep sets it
  from the tablet's GPS — confirming only if it's far from where the shop was
  thought to be — it uploads at the next sync as "Confirmed on site", and head
  office can revert a bad capture.
- Specs: plan_docs/stories/customer-directory.md US-005 S1–S3, S5, S6,
  non-functional notes, glossary (Coordinates, GPS Capture), assumptions
  (2 km threshold); plan_docs/uxdocs/01-tablet-day.md T-05 (T5.3).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — coordinates with
  history (T-2.3.2), Location screen (T-4.2.2), upload items (T-13.6.1).
- Pattern to follow: T-13.6.1's upload item.

Acceptance criteria:
1. At Rathdrum Pharmacy (Precision "Town"), "Set location from GPS" 300 m
   from the expected position saves as unsent with Precision "Confirmed on
   site", without asking.
2. A reading 4 km away asks "This is 4 km from Rathdrum — save anyway?" with
   confirm and cancel.
3. Offline it saves as unsent and uploads at next sync; meanwhile the
   Location shows "Confirmed on site (not yet sent)".
4. Head office reverting a capture restores the previous position and
   Precision.
5. The action shows only while the position is unconfirmed — quiet, and
   highlighted once for an unconfirmed Location; after a revert it shows
   again.

Constraints:
- Use the project's existing conventions and test framework.
- The distance threshold (2 km, MI-23) is a setting.
- Previous coordinates are kept (T-2.3.2's history).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  capture upload item.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after capture works on a device with the distance check and the
upload item, stop and show it. Resume only on "Continue T-17.3.1".

Steps: 1. device location read; 2. distance check; 3. upload item and
server apply; 4. revert on H-26; 5. action visibility; 6. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-17.3.1-S; do not design
your own.

Definition of done: standing in a shop whose position isn't confirmed, a rep
sets it from the tablet's GPS — confirming only if it's far from where the
shop was thought to be — it uploads at the next sync as "Confirmed on site",
and head office can revert a bad capture.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the planner map (T-13.2.2), gap lists (T-17.4.1).
```

**Checkpoint**

Produces before pausing — GPS capture on a device with the distance check and the upload item.
Human reviews — Is a bad capture both hard to make by accident and easy to revert?
Resume trigger — `Continue T-17.3.1`

---

### T-17.4.1-S — Test scenarios for the gap lists

**Owner** — Scenario Review
**Gates** — T-17.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the gap lists (task T-17.4.1). Read
plan_docs/stories/customer-directory.md US-008 S1–S4 and glossary (Gap Lists),
and plan_docs/uxdocs/02-head-office.md H-30 (H30.1, H30.2). Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover them, then derivable edges: a Location with every gap, the last
gap on a row fixed, a closed master's branches. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-17.4.1 — Work one list of shops missing something

**Parent story**

> As a Head Office User, I want one place listing Locations with no Profile, no rep, an inactive Main Contact or an unconfirmed position so that new or changed shops are made whole together.
>
> Acceptance criteria:
> - 5 with no Profile, 3 Unassigned (2 also with no Profile), 1 Replacement Needed and 60 Town-precision show each Location once with its gaps as tags, filterable by gap (S1)
> - Setting a Profile from the list clears the No Profile tag without leaving the list (S2)
> - Assign on an Unassigned row goes to Coverage Management for that Location (S3)
> - Town-precision Locations are listed under "Unconfirmed position" with no action (S4)

**Slice** — Head office opens one list of every shop with a gap — no profile, no rep, an inactive main contact, an unconfirmed position or a closed master — each shop once with its gaps as tags, fixes the profile gap in place, and follows links for the rest.
**Spec source** — Customer Directory US-008 S1–S4; glossary (Gap Lists); US-011 S3 ("Master location closed"); uxdocs 02 H-30 (H30.1, H30.2)
**Depends on** — T-12.2.1, T-2.5.1, T-17.3.1, T-3.3.1, T-17.2.1
**Pattern to follow** — T-3.3.1 (fixable list)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — aggregates several areas' states; H-30 is a draft.

**Provisional commit message**

```
feat(customers): work one list of shops missing something

- Optional fields are allowed and their gaps surfaced, so new and changed
  shops are made whole from one working list rather than several reports
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A cross-area list on a drafted screen, with a review pause.

**Agent prompt**

```
Role: You are building the gap lists (H-30) for the Field Sales Management
System's head office website.

Context:
- Slice: head office opens one list of every shop with a gap — no profile, no
  rep, an inactive main contact, an unconfirmed position or a closed master —
  each shop once with its gaps as tags, fixes the profile gap in place, and
  follows links for the rest.
- Specs: plan_docs/stories/customer-directory.md US-008 S1–S4, US-011 S3,
  glossary (Gap Lists); plan_docs/uxdocs/02-head-office.md H-30 (H30.1 a
  fixed tag disappears in place; the row leaves when its last gap clears;
  H30.2 unconfirmed position rows show only when that filter is chosen).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — profiles (T-12.2.1),
  main contact flag (T-2.5.1), precision (T-2.3.2, T-17.3.1), Unassigned
  (T-3.3.1), closed master (T-17.2.1).
- Pattern to follow: T-3.3.1's fixable list.

Acceptance criteria:
1. Each Location appears once with tags for its gaps: No profile, Unassigned,
   Replacement needed, Unconfirmed position, Master location closed;
   filterable by gap.
2. Setting a Profile from the list clears that tag in place; the row leaves
   only when its last gap clears.
3. Assign on an Unassigned row opens that Location's coverage page.
4. Unconfirmed position rows appear only when that filter is chosen and carry
   no action.
5. "No profile" means no profile supplying a Visit Frequency (No Visit
   Schedule).

Constraints:
- Use the project's existing conventions and test framework.
- Each gap comes from its owning area's query; no copies of their rules.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the list renders with every gap type on sample data, stop
and show it against H-30. Resume only on "Continue T-17.4.1".

Steps: 1. combined query; 2. tags and filters; 3. inline profile fix;
4. links; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-17.4.1-S; do not design
your own.

Definition of done: head office opens one list of every shop with a gap — no
profile, no rep, an inactive main contact, an unconfirmed position or a
closed master — each shop once with its gaps as tags, fixes the profile gap in
place, and follows links for the rest.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: prospect completion gaps (E21).
```

**Checkpoint**

Produces before pausing — the gap list with every gap type on sample data.
Human reviews — Does each shop appear once, and does fixing a gap in place feel like a working list rather than a report?
Resume trigger — `Continue T-17.4.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | CD-010 (T-17.1.1, T-17.1.2), CD-011 (T-17.2.1), CD-005 (T-17.3.1), CD-008 (T-17.4.1), HO-006 S3 (T-17.5.1) |
| Every task satisfies the three slice criteria | Pass | 6 of 6 |
| Every task carries a tier with a rationale citing dimensions | Pass | 6 of 6 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-17.3.1 (MI-23) |
| Tasks modifying existing behaviour order characterisation first | Pass | The tight-loop closure task owns its effects on scheduling; others add new states |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-17.5.1 only |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-17.2.1 is Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-17.1.1, T-17.1.2, T-17.3.1, T-17.4.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-17.2.1 |
| Every scenario task precedes the task it gates | Pass | 5 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-08, 23 |
