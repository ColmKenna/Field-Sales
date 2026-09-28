# E15 — Absence, cover and conflicts

**Iteration** — 4, Team changes and location upkeep
**Outcome** — A rep's leave blocks their calendar at once; the manager decides per visit whether it waits, keeps its window or is covered by another rep — from any team — for its window only; and when a manager's change overtakes a rep's offline move, both see both versions until one of them settles it.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Visit Planning US-006 — Record a Planned Absence | Must | S1–S3, VP006-A–F (S4's merge wording superseded); also US-004 S3, VP004-E and US-005 S3 |
| 2 | Visit Planning US-007 — Decide how an absence affects visits | Must | S1–S4, VP007-A–D |
| 3 | Visit Planning US-008 — Cover a visit with another rep | Should | S1–S4, S3b (also Rep at a Location US-003 S7) |
| 4 | Visit Planning US-014 — See and clear a Schedule Conflict | Should | S1–S4, VP014-A–G (also Rep at a Location US-005 S6 and US-003 S6's conflict count) |
| 5 | Coverage Management US-010 — See cross-team cover on my reps | Could | S1–S3 (also Visit Planning US-010 S1b) |

**Exit criterion** — A manager or rep records an absence and those days are blocked immediately, with scheduled visits returned to the panel; the manager decides Extend, Keep or Cover per affected visit, or in bulk with exceptions; a covering rep — even from another team, whose manager just sees a line — can call and order at the shop for the window only; and a manager change that overtakes a rep's offline move shows as a conflict on the tablet and the overview until either amends the visit, with a resolved history kept.

**Capability-class stamp** — Frontier + extended reasoning for the cover and conflict slivers; Frontier workhorse for other tasks and scenario drafting; Fast mid-tier for T-15.4.1. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [visit-planning.md](../../stories/visit-planning.md) (US-004–US-008, US-010, US-014), [coverage-management.md](../../stories/coverage-management.md) (US-010), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-003, US-005), [05-manager.md](../../uxdocs/05-manager.md) (M-01, M-02, M-05), [03-rep-planner.md](../../uxdocs/03-rep-planner.md) (R-01).

---

### T-15.1.1-S — Test scenarios for recording a planned absence

**Owner** — Scenario Review
**Gates** — T-15.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for recording a planned absence (task T-15.1.1).
Read plan_docs/stories/visit-planning.md US-006 S1–S3, VP006-A–F, US-004 S3,
VP004-E and US-005 S3, and plan_docs/uxdocs/05-manager.md M-02. Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover them, then derivable edges: an absence already started, a
half-day on a day with visits, a rep recording their own absence. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-15.1.1 — Record a planned absence that blocks the calendar at once

**Parent story**

> As a Sales Manager or Field Salesperson, I want to record days a rep is off in advance so that the schedule blocks them and the manager can decide what happens to affected visits.
>
> Acceptance criteria:
> - Recording Colm off 5–9 October, "Annual leave", blocks those days and shows "Needs a decision — 14 visits affected" (S1, VP006-A)
> - 3 visits scheduled on 6 October return to unscheduled and are listed as affected (S2)
> - With no overlapping windows it saves with "No visits affected" and no decision step (S3, VP006-D)
> - After saving, the affected count offers "Decide affected visits" or "Return to overview"; undecided visits keep their windows and show "Absence — decision pending" (VP006-B, C)
> - A genuine overlap shows the existing absence, the combined range and "Extend existing absence"; adjacent absences stay separate (VP006-E, F)
> - Dropping a visit on an absence day is refused with "You're off on 2 Oct" (US-004 S3, VP004-E); a half-day absence reduces that day's Working Day (US-005 S3)

**Slice** — A manager or rep records days a rep is off; those days are blocked in the planner immediately, visits already scheduled on them return to unscheduled, and the absence reports how many visits need a decision.
**Spec source** — Visit Planning US-006 S1–S3, VP006-A–F; US-004 S3, VP004-E; US-005 S3; uxdocs 05 M-02 (M2.1–M2.4)
**Depends on** — T-13.3.1, T-13.4.1, T-14.1.1
**Pattern to follow** — T-13.3.1 (planner scheduling)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled design; modifies scheduling and day load to respect blocked days.

**Provisional commit message**

```
feat(visit-planning): record planned absences that block the calendar

- Blocking leave days is urgent and factual, so it takes effect on save;
  deciding what happens to affected visits is a separate, later step
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Settled flow touching the planner, with a review pause.

**Agent prompt**

```
Role: You are building Planned Absence entry (M-02) for the Field Sales
Management System.

Context:
- Slice: a manager or rep records days a rep is off; those days are blocked
  in the planner immediately, visits already scheduled on them return to
  unscheduled, and the absence reports how many visits need a decision.
- Specs: plan_docs/stories/visit-planning.md US-006 S1–S3, VP006-A–F, US-004
  S3, VP004-E, US-005 S3, glossary (Planned Absence, Working Day);
  plan_docs/uxdocs/05-manager.md M-02.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — planner and
  scheduling (T-13.2.1, T-13.3.1), day load (T-13.4.1), overview (T-14.1.1).
- Pattern to follow: T-13.3.1.

Acceptance criteria:
1. Recording Colm off 5–9 October with reason "Annual leave" blocks those
   days in his planner at once.
2. 3 visits scheduled on 6 October return to unscheduled and are listed on
   the absence as affected.
3. After saving, the affected count is shown with "Decide affected visits"
   and "Return to overview"; the overview shows "Needs a decision — 14 visits
   affected".
4. Undecided affected visits keep their windows and show "Absence — decision
   pending".
5. With no affected dues: "No visits affected", no decision step.
6. A genuine overlap shows the existing absence, the combined range and
   "Extend existing absence", reason editable; adjacent absences stay
   separate.
7. Dropping a visit on an absence day is refused with "You're off on 2 Oct".
8. A half-day absence (afternoon of Thu 1 October) makes that day's Working
   Day 4h.

Constraints:
- Use the project's existing conventions and test framework.
- Absence is entered as fact; no approval workflow.
- No tests of framework internals or trivial members.
- This task opts in to the absence table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the absence model (full and part days) and its effect on
scheduling and load are written, stop and show them. Resume only on
"Continue T-15.1.1".

Steps: 1. absence model; 2. M-02 entry; 3. blocked days in scheduling and
load; 4. affected visits; 5. overlap handling; 6. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-15.1.1-S; do not design
your own.

Definition of done: a manager or rep records days a rep is off; those days
are blocked in the planner immediately, visits already scheduled on them
return to unscheduled, and the absence reports how many visits need a
decision.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–8 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Extend/Keep/Cover decisions (T-15.2.1, T-15.3.1).
```

**Checkpoint**

Produces before pausing — the absence model and its effect on scheduling and day load.
Human reviews — Are blocked days enforced on every scheduling route, including the tablet agenda?
Resume trigger — `Continue T-15.1.1`

---

### T-15.2.1-S — Test scenarios for absence decisions

**Owner** — Human-Led
**Gates** — T-15.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Extend: "push the window out by the absence length" — a 5-day absence (Mon 5 – Fri 9 Oct) inside a window ending 25 October makes it 30 October (S2). Calendar or working days? A half-day absence? An absence overlapping only the start of a window?
- Keep: the window is unchanged; the rep has fewer days (S3).
- A decision changed after the absence has started (edge case).
- Which visits are "affected": any Due Window overlapping the absence?
- The 2 visits with Due Reasons are marked "Deadline" in the list (S1).

---

### T-15.2.1 — Decide Extend or Keep for each visit an absence affects

**Parent story**

> As a Sales Manager, I want to choose Extend, Keep or Cover for each visit affected by a rep's absence, with one choice applicable to all, so that deadline-critical visits get cover while routine ones simply wait.
>
> Acceptance criteria:
> - Colm's absence affects 14 visits, 2 with Due Reasons; Apply to all: Extend marks all 14 and flags the 2 "Deadline"; changing those 2 to Cover and confirming extends 12 windows by 5 days and covers 2 (S1)
> - A window 28 Sep – 25 Oct with a 5-day absence inside, Extended, ends 30 October (S2)
> - Keep leaves the window unchanged and the planner shows the reduced days (S3)
> - Undecided visits show "Absence — decision pending" with windows unchanged (S4)
> - Any decision can be changed at any time, including after the absence starts (edge case)

**Slice** — For each visit an absence affects, the manager chooses Extend (push the window out by the absence) or Keep, singly or with apply-to-all and exceptions, deadline visits flagged, and can change the decision later.
**Spec source** — Visit Planning US-007 S1–S4, edge cases; glossary (Absence Decision); uxdocs 05 M-02
**Depends on** — T-15.1.1
**Pattern to follow** — T-14.5.1 (per-item decisions with apply-to-all)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — the Extend arithmetic changes due windows (which drive Overdue and performance); its edge cases need a human oracle.

**Provisional commit message**

```
feat(visit-planning): decide extend or keep per absence-affected visit

- A launch deadline and a routine village shop need different handling, so
  each affected visit gets its own decision, with apply-to-all for speed
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Window arithmetic built against a human oracle.

**Agent prompt**

```
Role: You are building absence decisions — Extend and Keep — for the Field
Sales Management System (M-02).

Context:
- Slice: for each visit an absence affects, the manager chooses Extend (push
  the window out by the absence) or Keep, singly or with apply-to-all and
  exceptions, deadline visits flagged, and can change the decision later.
- Specs: plan_docs/stories/visit-planning.md US-007 S1–S4, edge cases,
  glossary (Absence Decision) and design decision "Absence: days always
  blocked, decision per visit, manager decides"; plan_docs/uxdocs/
  05-manager.md M-02.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — absences (T-15.1.1),
  Visit Dues (T-13.1.1), overview (T-14.1.1).
- Pattern to follow: T-14.5.1's per-item decisions.

Acceptance criteria:
1. For 14 affected visits, "Apply to all: Extend" marks all 14; the 2 with
   Due Reasons are flagged "Deadline"; each can be changed individually.
2. A window 28 Sep – 25 Oct with a 5-day absence inside, Extended, ends 30
   October; the extension follows the agreed arithmetic.
3. Keep leaves the window unchanged and the planner shows the reduced days.
4. Undecided visits show "Absence — decision pending" with windows
   unchanged; the overview counts "absence decisions pending" per rep.
5. Any decision can be changed later, including after the absence starts.
6. Cover appears as a choice but is completed by T-15.3.1.

Constraints:
- Use the project's existing conventions and test framework.
- Extend changes only the window end; everything else about the visit
  stands.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  decision fields.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the Extend arithmetic passes every agreed scenario, stop and
show it. Resume only on "Continue T-15.2.1".

Steps: 1. decision model; 2. Extend arithmetic; 3. decision list with
apply-to-all; 4. overview count; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-15.2.1-S. You
are forbidden from designing your own test cases.

Definition of done: for each visit an absence affects, the manager chooses
Extend (push the window out by the absence) or Keep, singly or with
apply-to-all and exceptions, deadline visits flagged, and can change the
decision later.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Cover (T-15.3.1), conflicts raised by window moves (T-15.5.1).
```

**Checkpoint**

Produces before pausing — the Extend arithmetic passing every agreed scenario.
Human reviews — Does Extend move each window exactly as the business expects for partial and half-day absences?
Resume trigger — `Continue T-15.2.1`

---

### T-15.3.1-S — Test scenarios for covering a visit

**Owner** — Human-Led
**Gates** — T-15.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Aoife covers Murphy's (Colm's) for 28 Sep – 25 Oct: what reaches her snapshot — the Location, its Suggested List, recent calls and orders — and when does it leave (after the window)?
- During the window Aoife can record a Call and an Order; after it, nothing (S2). An order Aoife captured on the last day and syncs the next morning: judged as captured (BR-NEW-006)?
- Aoife's Call completes the visit "for Colm's record" (S3): whose performance does the order count toward (attribution is by owner at acceptance)?
- Batch cover from the absence review with exclusions (VP007-A, B); over-capacity warning still allows (S4, VP007-C).
- Cover from another team: immediate, with a notification line for that manager (S3b, VP007-D).

---

### T-15.3.1 — Cover a visit with another rep for its window

**Parent story**

> As a Sales Manager, I want to assign a Visit Due to a covering rep for its window so that the customer is seen on time without changing who owns the Location.
>
> Acceptance criteria:
> - Setting Murphy's (28 Sep – 25 Oct) to Cover by Aoife puts it in her planner and, after her next Sync, on her tablet; Murphy's stays assigned to Colm (S1)
> - During the window Aoife can record a Call and an Order at Murphy's; after 25 October it leaves her lists (S2)
> - Aoife's Call on 7 October completes the visit for Colm's record (S3)
> - Choosing Ciara from another team sets the cover immediately; her manager sees "Covering for another team" (S3b)
> - Aoife over capacity on 3 days shows "Aoife is over capacity on 3 days that week" and can still confirm (S4, VP007-C)
> - "Apply to all: Cover" opens a review with all eligible visits selected; one covering rep covers only the selected ones (VP007-A, B)
> - Home shows "Covering for Aoife" on a covered Location (Rep at a Location US-003 S7)

**Slice** — A manager sets an affected visit, or a selected batch, to be covered by another rep — from any team — who then sees that shop on their planner and tablet for the window only and can call and order there, while ownership stays put.
**Spec source** — Visit Planning US-008 S1–S4, S3b, edge cases; VP007-A–D; Rep at a Location US-003 S7; design decision "Cross-team cover by notification" (Coverage)
**Depends on** — T-15.2.1, T-13.9.1, T-4.1.1
**Pattern to follow** — T-14.5.1 (Left Visits keep temporary tablet access)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — grants temporary tablet access to another rep's shop (authorization) and changes who completes visits; many time-bounded cases.

**Provisional commit message**

```
feat(visit-planning): cover a visit with another rep for its window

- Cover keeps ownership and moves only the visit for its window, so the
  customer is seen on time without reshaping anyone's territory
- Any rep may cover; their manager is told, not asked, because cover is
  rare and short and managers already talk
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Time-bounded access across reps' tablets.

**Work package**

Increments:
1. Cover on a Visit Due: covering rep and window; ownership unchanged.
2. Planner: the visit appears in the covering rep's planner.
3. Snapshot builder: include covered Locations for the window with their Suggested List and recent Calls and Orders; remove them afterwards.
4. Tablet: "Covering for Aoife" on Home and the Location; Call and New Order allowed during the window.
5. Completion: the covering rep's Call completes the visit for the owner's record.
6. From absence decisions: single Cover and "Apply to all: Cover" with a review (all eligible selected, exclusions), capacity warning, cross-team immediate effect and notification.

Decision points:
- Performance: the covering rep's order is attributed to the Location's owner at acceptance (Targets rule). Confirm.
- What if the owner (Colm) also calls at the shop during cover?
- A covered visit whose window is later Extended: does the cover extend too?

Delegable slivers:
- **Cover review UI** — Build the "Apply to all: Cover" review with all eligible visits selected, Select all, Clear all, per-row exclusion and the capacity warning, calling existing cover commands. No access logic.
- **Covering marker on the tablet** — Show "Covering for <owner>" on Home and the Location for covered visits in the snapshot. No snapshot logic.
- **Snapshot window tests** — Given the agreed scenarios, write tests proving a covered Location enters the covering rep's snapshot at the window start and leaves after it ends. Test-only.

---

### T-15.4.1 — Show cross-team cover on the covering rep's manager's overview

**Parent story**

> As a Sales Manager, I want to see when one of my reps has been asked to cover a visit for another team so that the extra load is visible when I look at their week.
>
> Acceptance criteria:
> - When M. Byrne sets my rep Aoife to cover Murphy's 5–9 October, Aoife's row shows "Covering for another team: Murphy's Pharmacy, 5–9 Oct, requested by M. Byrne" (S1; Visit Planning US-010 S1b)
> - There is no Approve or Decline; the cover already applies (S2)
> - After 9 October the line no longer appears (S3)

**Slice** — When another manager sets one of my reps to cover a visit, my overview shows a line on that rep's row naming the shop, window and requesting manager — no approval asked — until the window ends.
**Spec source** — Coverage Management US-010 S1–S3; Visit Planning US-010 S1b; uxdocs 05 M-01
**Depends on** — T-15.3.1, T-14.1.1
**Pattern to follow** — T-14.1.1 (overview rows)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — a read-only line over reviewed data; settled; Blast Radius Low.

**Provisional commit message**

```
feat(coverage): show cross-team cover on the covering rep's row

- Cover isn't approved across teams, so the covering rep's own manager
  needs the extra load visible where they plan that rep's week
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A read-only line on a settled screen.

**Agent prompt**

```
Role: You are adding cross-team cover lines to the Visit Planning overview
(M-01) of the Field Sales Management System.

Context:
- Slice: when another manager sets one of my reps to cover a visit, my
  overview shows a line on that rep's row naming the shop, window and
  requesting manager — no approval asked — until the window ends.
- Specs: plan_docs/stories/coverage-management.md US-010 S1–S3;
  plan_docs/stories/visit-planning.md US-010 S1b.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — cover (T-15.3.1),
  overview rows (T-14.1.1), reporting lines (T-1.1.1).
- Pattern to follow: T-14.1.1's rows.

Acceptance criteria:
1. When M. Byrne sets my rep Aoife to cover Murphy's Pharmacy 5–9 October,
   Aoife's row shows "Covering for another team: Murphy's Pharmacy, 5–9 Oct,
   requested by M. Byrne".
2. No Approve or Decline action exists.
3. After 9 October the line no longer appears.
4. Cover set by my own team's manager (me) shows no such line.

Constraints:
- Use the project's existing conventions and test framework.
- Read-only.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. query of cross-team covers for my reps; 2. row line; 3. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: when another manager sets one of my reps to cover a
visit, my overview shows a line on that rep's row naming the shop, window and
requesting manager — no approval asked — until the window ends.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: setting cover (T-15.3.1).
```

---

### T-15.5.1-S — Test scenarios for raising and clearing schedule conflicts

**Owner** — Human-Led
**Gates** — T-15.5.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Which manager changes overtake a rep's offline day change: cover, cancel, and a window moved so the day falls outside it (glossary). Anything else?
- Colm moves Murphy's to Thu 8 Oct offline; his manager covers it by Aoife before he syncs: the cover applies, and Colm's tablet shows "Conflict — your change: Thu 8 Oct; manager's change: covered by Aoife" (S1). What if the manager's change came after Colm's sync?
- Cleared when either amends the visit (S2, S3): which amendments count?
- A Suggested Day change never conflicts (S4).
- Two offline moves of the same visit before a sync.

---

### T-15.5.1 — Raise a schedule conflict when a manager's change overtakes an offline move

**Parent story**

> As a Field Salesperson or Sales Manager, I want to be told when a manager's change has overtaken my offline day change, with both versions shown, so that we can settle it directly.
>
> Acceptance criteria:
> - Colm moved Murphy's to Thu 8 Oct offline and his manager set Cover by Aoife before he synced: after the sync Colm's tablet shows "Conflict — your change: Thu 8 Oct; manager's change: covered by Aoife", the cover applies, and the overview shows "1 conflict" on Colm's row (S1; Rep at a Location US-005 S6)
> - The manager removing the cover clears it for both and Thu 8 Oct stands (S2)
> - Colm rescheduling clears it for both (S3)
> - A Suggested Day change raises no conflict (S4)
> - Home shows "1 schedule conflict" beside the Unsent count (Rep at a Location US-003 S6)

**Slice** — When a rep's offline move reaches the server after a manager has covered, cancelled or re-windowed that visit, the manager's change applies, both see a conflict naming both versions — on the tablet, Home's strip and the overview — and it clears when either of them amends the visit.
**Spec source** — Visit Planning US-014 S1–S4, glossary (Schedule Conflict), design decision "Schedule Conflict as a notice, not a workflow"; Rep at a Location US-005 S6, US-003 S6
**Depends on** — T-13.6.1, T-15.3.1, T-15.2.1
**Pattern to follow** — T-4.1.2 (judging uploads as captured)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — merge semantics between offline and online edits (data integrity); the conflict rules need a human oracle.

**Provisional commit message**

```
feat(visit-planning): raise a notice when a manager change overtakes a move

- Another rep may already be on the way, so the manager's change applies
  in the gap; both people see both versions and settle it by talking
- No resolve workflow: amending the visit is what clears it
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Offline–online merge semantics.

**Work package**

Increments:
1. On receiving a schedule-change upload, compare against manager changes made since the rep's snapshot; if a cover, cancel or window move makes the rep's day impossible or pointless, record a conflict with both versions and apply the manager's version.
2. Tablet: the visit shows "Conflict — your change: Thu 8 Oct; manager's change: covered by Aoife"; Home's strip counts conflicts.
3. Overview: "1 conflict" on the rep's row.
4. Clearing: any amendment of the visit by either party clears it and records who, what and when.
5. Suggested Day changes never raise a conflict.

Decision points:
- The full list of manager changes that count (cancel arrives with campaigns in E20).
- What counts as an amendment that clears (a reschedule, a cover removal, a new window?).

Delegable slivers:
- **Conflict wording on the tablet** — Show "Conflict — your change: <day>; manager's change: <change>" on the visit and a conflict count in Home's strip, reading conflicts from the snapshot. No merge logic.
- **Overview conflict count** — Add the per-rep conflict count to the overview rows from the conflict records. Read-only.

---

### T-15.5.2-S — Test scenarios for the Schedule Conflicts page

**Owner** — Scenario Review
**Gates** — T-15.5.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Schedule Conflicts page (task
T-15.5.2). Read plan_docs/stories/visit-planning.md VP014-A–G and
plan_docs/uxdocs/05-manager.md M-05. Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover
VP014-A–G, then derivable edges: a conflict cleared and a new one raised on
the same visit, a rep with no conflicts filtered. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-15.5.2 — List open and resolved schedule conflicts for managers

**Parent story**

> As a Field Salesperson or Sales Manager, I want to be told when a manager's change has overtaken my offline day change, with both versions shown, so that we can settle it directly.
>
> Acceptance criteria:
> - Open conflicts list overdue affected visits first, then today, then future by affected date (VP014-A)
> - From a rep's conflict count the page opens filtered to that rep, the filter named, with "View all conflicts" (VP014-B); opened directly it shows all reps (VP014-C)
> - Cleared conflicts move to a Resolved view keeping both versions, the clearing amendment, who and when (VP014-D); Open is the default and Resolved adjacent (VP014-E); the rep filter carries between them (VP014-F); Resolved is most recently cleared first, showing affected date and clearing time separately (VP014-G)

**Slice** — Managers open a Schedule Conflicts page — optionally filtered to one rep — with open conflicts ordered by the affected visit's date and a separate Resolved view showing both versions and how each was cleared.
**Spec source** — Visit Planning VP014-A–G; uxdocs 05 M-05 (M5.1–M5.5)
**Depends on** — T-15.5.1, T-14.1.1
**Pattern to follow** — T-14.1.1 (manager pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — read views over reviewed conflict records; settled design.

**Provisional commit message**

```
feat(visit-planning): list open and resolved schedule conflicts

- Conflicts are time-sensitive, so open ones sort by the affected visit's
  date; resolved ones keep both versions and who cleared them
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Settled views with a review pause.

**Agent prompt**

```
Role: You are building the Schedule Conflicts page (M-05) for the Field Sales
Management System's manager website.

Context:
- Slice: managers open a Schedule Conflicts page — optionally filtered to one
  rep — with open conflicts ordered by the affected visit's date and a
  separate Resolved view showing both versions and how each was cleared.
- Specs: plan_docs/stories/visit-planning.md VP014-A–G;
  plan_docs/uxdocs/05-manager.md M-05.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — conflict records
  (T-15.5.1), overview (T-14.1.1).
- Pattern to follow: T-14.1.1's pages.

Acceptance criteria:
1. Open lists overdue affected visits first, then today, then future by
   ascending affected date.
2. From a rep's conflict count the page opens filtered to that rep, naming
   the filter, with "View all conflicts" to clear it; opened directly it
   shows all reps.
3. Open is the default; Resolved is an adjacent view; the rep filter carries
   between them.
4. Resolved keeps both versions, the clearing amendment, who made it and
   when, ordered most recently cleared first, showing the affected visit
   date separately from the clearing time.
5. There is no "resolve" action.

Constraints:
- Use the project's existing conventions and test framework.
- Read-only views.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after Open and Resolved render with sample conflicts, stop and
show them against M-05. Resume only on "Continue T-15.5.2".

Steps: 1. queries and ordering; 2. Open and Resolved views; 3. rep filter;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-15.5.2-S; do not design
your own.

Definition of done: managers open a Schedule Conflicts page — optionally
filtered to one rep — with open conflicts ordered by the affected visit's
date and a separate Resolved view showing both versions and how each was
cleared.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: raising and clearing conflicts (T-15.5.1).
```

**Checkpoint**

Produces before pausing — Open and Resolved views rendered with sample conflicts.
Human reviews — Does the ordering put the most urgent affected visits first, as M-05 settled?
Resume trigger — `Continue T-15.5.2`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | VP-006 (T-15.1.1), VP-007 (T-15.2.1, with Cover in T-15.3.1), VP-008 (T-15.3.1), VP-014 (T-15.5.1, T-15.5.2), CV-010 (T-15.4.1) |
| Every task satisfies the three slice criteria | Pass | 6 of 6 |
| Every task carries a tier with a rationale citing dimensions | Pass | 6 of 6 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | Extend arithmetic and conflict triggers raised as decision questions (T-15.2.1-S, T-15.5.1-S) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-15.1.1 modifies scheduling through its checkpoint; the tight-loop tasks own their characterisation increments |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-15.4.1 only |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-15.2.1, T-15.3.1, T-15.5.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-15.1.1, T-15.2.1, T-15.5.2 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-15.3.1, T-15.5.1 |
| Every scenario task precedes the task it gates | Pass | 5 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}` |
