# E14 — Manager's view, one-off visits and handover

**Iteration** — 3, Visit rhythm
**Outcome** — Managers see who or where needs attention by rep or region, with uncovered shops impossible to miss; they add one-off visits with a structured reason, sent to whichever rep fits; and when a shop changes owner its open visits are handed over deliberately, never stranded.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Visit Planning US-010 — Manager overview by rep or region | Must | S1, S1c, S2–S5, VP010-A–C (S1b cross-team cover → E15; absence and conflict counts show zero until E15) |
| 2 | Coverage Management US-007 (part) — Find and fix Unassigned Locations | Must | CV007-A, B, D–H |
| 3 | Visit Planning US-016 — Maintain Visit Due Reason Types | Should | S1–S5 |
| 4 | Visit Planning US-015 — Add a single One-off Visit Due with a reason | Should | S1–S3, VP015-A–G, F2 (also Coverage US-002's "Add one-off visit") |
| 5 | Coverage Management US-003 — Decide the handover of open visits | Must | S1–S5, CV003-A–H (also Coverage US-001 CV001-H's handover and Rep at a Location US-003 S7a) |

**Exit criterion** — The Visit Planning overview opens By rep (or the last view), each rep row counting scheduled of due, Overdue, Missed, unscheduled within 14 days, Over days and handover pending with a coverage reminder; By region groups dues by Town with the responsible rep; the header shows how many shops in the manager's area are unassigned. Managers maintain Reason Types and add one-off visits from a shop's page. A transfer asks whether the previous rep is leaving and either moves every open visit as inherited, or offers Move or Leave per visit, with undecided ones counted as handover pending.

**Capability-class stamp** — Frontier + extended reasoning for the handover slivers (T-14.5.1); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [visit-planning.md](../../stories/visit-planning.md) (US-010, US-015, US-016), [coverage-management.md](../../stories/coverage-management.md) (US-001, US-003, US-007), [05-manager.md](../../uxdocs/05-manager.md) (M-01, M-07, M-08, M-15, M-16), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-004, EC-NEW-006).

---

### T-14.3.1-S — Test scenarios for Visit Due Reason Types

**Owner** — Scenario Review
**Gates** — T-14.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for maintaining Visit Due Reason Types (task
T-14.3.1). Read plan_docs/stories/visit-planning.md US-016 S1–S5 and edge
cases, and plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-004. Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover S1–S5, then derivable edges: two types with the same
icon and colour, a user without the authority, retiring a type used by an
open visit. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-14.3.1 — Maintain Visit Due Reason Types with a controlled icon and colour

**Parent story**

> As an authorised manager or administrator, I want to maintain Visit Due Reason Types so that planners use consistent reasons that can be recognised, filtered and reported.
>
> Acceptance criteria:
> - An authorised user creates a type with a unique name, and it becomes available when a manager supplies a Due Reason (S1)
> - Renaming a type used by existing dues shows the new name there while keeping each visit's explanation (S2)
> - Retiring a used type archives it: still visible on those visits, unavailable for new reasons (S3)
> - A duplicate active name is rejected with a message naming the existing type (S4)
> - Icon and colour are chosen from a controlled set and previewed together with the name (S5)

**Slice** — An authorised manager or administrator maintains the list of Visit Due Reason Types, each with a unique name and an icon and colour from a fixed set, renaming or archiving them without breaking visits that use them.
**Spec source** — Visit Planning US-016 S1–S5, edge cases; BR-NEW-004
**Depends on** — T-1.5.1
**Pattern to follow** — T-1.5.1 (archive-not-delete list)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — a pattern list, but "authorised manager or administrator" is an undefined role (MI-59).

**Provisional commit message**

```
feat(visit-planning): maintain structured visit due reason types

- A structured type gives reasons a stable icon, colour and filter, while
  the free-text explanation carries the visit-specific detail
- Colour comes from a controlled set and is never the only signal
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Pattern list with an authorization question.

**Agent prompt**

```
Role: You are building Visit Due Reason Type maintenance for the Field Sales
Management System's manager website.

Context:
- Slice: an authorised manager or administrator maintains the list of Visit
  Due Reason Types, each with a unique name and an icon and colour from a
  fixed set, renaming or archiving them without breaking visits that use
  them.
- Specs: plan_docs/stories/visit-planning.md US-016 S1–S5, edge cases,
  glossary (Due Reason); plan_docs/uxdocs/04-user-stories-amendments.md
  BR-NEW-004.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — archive-not-delete
  list (T-1.5.1), roles (T-1.1.1).
- Pattern to follow: T-1.5.1.

Acceptance criteria:
1. An authorised user creates a type with a unique name; it becomes
   available when a manager supplies a Due Reason.
2. Renaming a type used by existing dues shows the new name on them and
   keeps each visit's explanation.
3. Retiring a used type archives it: visible on existing visits, not offered
   for new reasons.
4. A duplicate active name is rejected with a message naming the existing
   type.
5. Icon and colour come from a controlled system set and are previewed with
   the name; colour is never free-form.
6. Only authorised users can maintain types (the authority per MI-59).

Constraints:
- Use the project's existing conventions and test framework; reuse
  T-1.5.1's component.
- No tests of framework internals or trivial members.
- This task opts in to the reason-type table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the type model, the controlled icon and colour set, and the
authority check are designed, stop and show them. Resume only on
"Continue T-14.3.1".

Steps: 1. type model; 2. controlled set; 3. list page; 4. authority check;
5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-14.3.1-S; do not design
your own.

Definition of done: an authorised manager or administrator maintains the list
of Visit Due Reason Types, each with a unique name and an icon and colour from
a fixed set, renaming or archiving them without breaking visits that use
them.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: adding reasons to visits (T-14.4.1), campaigns (E20).
```

**Checkpoint**

Produces before pausing — the type model, the controlled icon and colour set, and the authority check.
Human reviews — Who exactly may maintain the list (MI-59), and is every icon-colour pair distinguishable without colour?
Resume trigger — `Continue T-14.3.1`

---

### T-14.4.1-S — Test scenarios for adding a one-off visit

**Owner** — Scenario Review
**Gates** — T-14.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for adding a single one-off visit from a
Location (task T-14.4.1). Read plan_docs/stories/visit-planning.md US-015 S1,
S3, VP015-A–E, G and plan_docs/uxdocs/05-manager.md M-16 (M16.1–M16.5).
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover them, then derivable edges: an Unassigned Location, a
window in the past, a reason with no explanation, a rep outside the
manager's team. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-14.4.1 — Add a one-off visit from a shop's page, sent to the rep who fits

**Parent story**

> As a Sales Manager, I want to ask for one extra visit at a Location with a reason and window so that a problem account or a request from the customer gets seen.
>
> Acceptance criteria:
> - A one-off for Byrne's Chemist, window 21–25 September, reason "Customer complaint follow-up", suggested Tue 22 Sep, appears in the rep's planner and on their tablet after Sync with the reason and suggestion (S1)
> - Saving without a window is rejected with "Set a due window" (S3)
> - From a Location, the form opens with it filled in and its Primary Rep selected; the picker lists reps with any assignment there first (VP015-A, B, C)
> - Choosing Aoife over Colm states "Aoife will make this visit. Colm remains Byrne's Primary Rep." and changes no assignment or history (VP015-D)
> - With Colm scheduled at Byrne's on Thu 24 Sep, the form shows "Colm is scheduled at Byrne's on Thu 24 Sep." without blocking (VP015-E)
> - No tablet delivery status is shown (VP015-G)

**Slice** — From a shop's Location page, a manager adds a one-off visit with a due window, a structured reason and a suggested day, sent to the primary rep or any other rep for this visit only, and it reaches that rep's planner and tablet.
**Spec source** — Visit Planning US-015 S1, S3, VP015-A–E, G; Coverage US-002 CV002-A ("add a one-off visit"); uxdocs 05 M-16 (M16.1–M16.5), M-07; BR-NEW-004
**Depends on** — T-14.3.1, T-13.1.1, T-3.2.1
**Pattern to follow** — T-3.2.1 (Location page), T-13.1.1 (Visit Due model)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled design; adds a Visit Due kind that the planner, tablet and overview read.

**Provisional commit message**

```
feat(visit-planning): add one-off visits from a location's page

- One-off visits start from something at a shop, so the flow starts there;
  the manager chooses who goes, for this visit only, without touching
  ownership
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new visit kind with a review pause.

**Agent prompt**

```
Role: You are building "Add a one-off visit" (M-16) for the Field Sales
Management System's manager website.

Context:
- Slice: from a shop's Location page, a manager adds a one-off visit with a
  due window, a structured reason and a suggested day, sent to the primary
  rep or any other rep for this visit only; it reaches that rep's planner and
  tablet.
- Specs: plan_docs/stories/visit-planning.md US-015 S1, S3, VP015-A–E, G,
  glossary (One-off Visit Due, Due Reason, Suggested Day);
  plan_docs/uxdocs/05-manager.md M-16 (M16.1 anchored on the Location; M16.2
  Primary Rep default; M16.3 plain picker; M16.4 no delivery status; M16.5
  nearby visit is information only) and M-07 (footer action);
  plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-004.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Location page
  (T-3.2.1), Visit Due model (T-13.1.1), reason types (T-14.3.1), planner
  (T-13.2.1), tablet visits (T-13.9.1).
- Pattern to follow: T-3.2.1's page actions.

Acceptance criteria:
1. "Add one-off visit" on the Location page opens the form with the Location
   filled in and its Primary Rep selected.
2. The rep picker lists reps with any assignment at the Location first, then
   the rest of the team.
3. Choosing Aoife when Colm is Primary states "Aoife will make this visit.
   Colm remains Byrne's Primary Rep."; saving changes no assignment or
   history.
4. With Colm scheduled at Byrne's on Thu 24 Sep, a window of 21–25 Sep shows
   "Colm is scheduled at Byrne's on Thu 24 Sep." without asking anything or
   blocking.
5. Saving without a window is rejected with "Set a due window".
6. A due reason is a Reason Type plus optional explanation; a one-off may
   have no reason.
7. The visit appears in the chosen rep's planner and, after sync, on their
   tablet with the reason and suggested day.
8. Neither the confirmation nor the saved visit shows tablet delivery
   status.

Constraints:
- Use the project's existing conventions and test framework.
- One-off is a kind of Visit Due in T-13.1.1's model.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  one-off fields.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the form works end to end to the planner with sample data,
stop and show it against M-16. Resume only on "Continue T-14.4.1".

Steps: 1. one-off kind; 2. M-16 form; 3. rep picker; 4. nearby notice;
5. planner and snapshot; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-14.4.1-S; do not design
your own.

Definition of done: from a shop's Location page, a manager adds a one-off
visit with a due window, a structured reason and a suggested day, sent to the
primary rep or any other rep for this visit only, and it reaches that rep's
planner and tablet.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–8 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: completion rules for one-offs (T-14.4.2), campaigns (E20).
```

**Checkpoint**

Produces before pausing — the one-off form working end to end to the rep's planner.
Human reviews — Does choosing another rep leave ownership and history untouched, as M-16 says?
Resume trigger — `Continue T-14.4.1`

---

### T-14.4.2-S — Test scenarios for completing a one-off visit

**Owner** — Human-Led
**Gates** — T-14.4.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Aoife holds a one-off at Byrne's and Colm the recurring visit: Aoife's Call completes only the one-off (VP015-F). Straightforward.
- Colm holds both: his Call "for the one-off" completes only the one-off (VP015-F2). How does the system know which visit a Call is for (MI-57)? A choice on the Call? The one-off always wins when open? Two Calls needed to clear both?
- A one-off with a campaign outcome list? (No — campaigns are E20.)
- The manager then decides the recurring visit (EC-NEW-006): nothing automatic.

---

### T-14.4.2 — Complete a one-off visit without clearing the recurring one

**Parent story**

> As a Sales Manager, I want to ask for one extra visit at a Location with a reason and window so that a problem account or a request from the customer gets seen.
>
> Acceptance criteria:
> - A Call at Byrne's on 23 September completes the one-off (S2)
> - Aoife's Call at Byrne's completes her one-off; Colm's recurring visit there stays open and unchanged (VP015-F)
> - Colm holding both, his Call for the one-off completes the one-off; the recurring visit stays open (VP015-F2)

**Slice** — A call made for a one-off visit completes that one-off only, even when the same rep also holds the shop's recurring visit, which stays open for the manager and reps to decide.
**Spec source** — Visit Planning US-015 S2, VP015-F, VP015-F2 and the "Superseded" note; EC-NEW-006; uxdocs 05 M-16 (M16.7)
**Depends on** — T-14.4.1, T-13.1.1
**Pattern to follow** — T-13.1.1 (completion)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: S | Confidence: M
  (inferred) — modifies "one call clears all" (T-13.1.1), so characterisation comes first; how a call is attributed to the one-off is undefined (MI-57), so Oracle Ambiguity is High.

**Provisional commit message**

```
feat(visit-planning): complete one-off visits without clearing recurring ones

- The same rep may need two visits with different contacts, so a call for
  a one-off completes only the one-off; the manager decides the rest
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A rule exception behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the one-off completion exception to visit completion in
the Field Sales Management System.

Context:
- Slice: a call made for a one-off visit completes that one-off only, even
  when the same rep also holds the shop's recurring visit, which stays open
  for the manager and reps to decide.
- Specs: plan_docs/stories/visit-planning.md US-015 S2, VP015-F, VP015-F2 and
  the "Superseded" note ("One Call clears all" gains an exception);
  plan_docs/uxdocs/04-user-stories-amendments.md EC-NEW-006;
  plan_docs/uxdocs/05-manager.md M-16 (M16.7).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — completion rule
  (T-13.1.1), one-offs (T-14.4.1), Call model and upload (T-5.3.1, T-4.1.2).
- Pattern to follow: T-13.1.1's completion.

Acceptance criteria:
1. A Call at Byrne's on 23 September completes the open one-off there.
2. Aoife holds a one-off at Byrne's; Colm holds an open recurring visit
   there. Aoife's Call completes her one-off; Colm's recurring visit stays
   open and unchanged.
3. Colm holds both; his Call for the one-off completes the one-off and the
   recurring visit stays open.
4. How a Call is identified as "for the one-off" follows the rule agreed in
   T-14.4.2-S (MI-57).
5. Nothing automatic happens to the recurring visit afterwards.

Constraints:
- Use the project's existing conventions and test framework.
- One completion function; add the exception there, not in callers.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond any
  field the MI-57 rule needs on the Call.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-13.1.1's completion; stop and show them passing. Resume only on
"Continue T-14.4.2".

Steps: 1. characterisation tests; 2. exception in the completion function;
3. any tablet field MI-57 requires; 4. tests.

Test expectations: implement exactly the scenarios agreed in T-14.4.2-S. You
are forbidden from designing your own test cases.

Definition of done: a call made for a one-off visit completes that one-off
only, even when the same rep also holds the shop's recurring visit, which
stays open for the manager and reps to decide.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Every agreed scenario passes
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: campaign outcomes (E20).
```

**Checkpoint**

Produces before pausing — characterisation tests of "one call clears all", passing.
Human reviews — Is the MI-57 rule implemented so a rep can never clear the wrong visit?
Resume trigger — `Continue T-14.4.2`

---

### T-14.5.1-S — Test scenarios for handing over open visits

**Owner** — Human-Led
**Gates** — T-14.5.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- 23 Rathdrum Locations move from Colm to Aoife; 9 have open visits with Colm (2 Overdue, 3 scheduled): what exactly does the preview list (S1)?
- Apply to all: Move, then Murphy's to Leave (S2): 8 move with Scheduled Day cleared; Murphy's stays as a Left Visit.
- Undecided visits become Handover Pending and count on Colm's overview row (S3); deciding later from the list (S4).
- A Left Visit stays on Colm's tablet "Handover — finish visit" until he records a Call, it is Missed, or it's moved (S5; A1-003 S7a). Which snapshot rules keep the Location on Colm's tablet?
- A carve-out Move marks the visit "inherited from Colm" and excludes it from Aoife's performance until it closes (CV003-H).
- Which transfers trigger handover: every ownership change from T-3.1.1, T-3.1.4 and T-3.2.2 (and later E16's batches)?

---

### T-14.5.1 — Hand over open visits when a shop changes owner

**Parent story**

> As a Sales Manager, I want to choose, per open visit, whether it moves to the new Primary Rep or stays with the previous one, with one choice applicable to all, so that a reassignment never strands a visit.
>
> Acceptance criteria:
> - Moving 23 Rathdrum Locations from Colm to Aoife, the preview shows "9 open visits with Colm: 2 Overdue, 3 scheduled" and a Handover list (S1)
> - Apply to all: Move, then Murphy's to Leave, moves 8 to Aoife with Scheduled Day cleared and leaves Murphy's with Colm as a Left Visit (S2)
> - Saving 4 undecided makes them Handover Pending, shown as "4 handover pending" on Colm's overview row (S3); deciding them later moves them and clears the count (S4)
> - A Left Visit appears on Colm's tablet as "Handover — finish visit" until a Call, Missed or a move (S5; Rep at a Location US-003 S7a)
> - Not leaving: Move or Leave per visit, Apply to all, undecided become Handover Pending (CV003-D); a carve-out Move marks the visit "inherited from Colm" (CV003-H)
> - Every transfer's open visits go to Handover (Coverage US-001 CV001-H)

**Slice** — When a shop changes owner and the previous rep is staying, the change's preview lists their open visits, the manager moves or leaves each (with apply-to-all), undecided ones are counted as handover pending, and a left visit stays on the previous rep's tablet until it's done.
**Spec source** — Coverage Management US-003 S1–S5, CV003-D, CV003-H; US-001 CV001-H; Rep at a Location US-003 S7a; uxdocs 05 M-08 (M8.1–M8.2)
**Depends on** — T-3.1.4, T-13.1.1, T-13.9.1
**Pattern to follow** — T-3.1.2 (preview), T-3.1.1 (ownership changes)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — moves visit ownership across reps and extends tablet access (data integrity and authorization of what a rep sees); many interacting cases.

**Provisional commit message**

```
feat(coverage): hand over open visits when a location changes owner

- Nothing moves automatically: the manager decides each open visit, and an
  undecided one is counted rather than stranded
- A visit left with the previous rep keeps that shop on their tablet until
  it's finished, reusing the cover mechanism
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Ownership of visits and tablet access across reps.

**Work package**

Increments:
1. Handover step in every ownership-changing flow (T-3.1.1, T-3.1.4, T-3.2.2): find open visits held by the previous owner at Locations changing owner.
2. Preview line "9 open visits with Colm: 2 Overdue, 3 scheduled" and the Handover list with Move / Leave per visit and Apply to all.
3. Move: owner changes, Scheduled Day cleared, window kept, marked "inherited from <rep>" (CV003-H, feeding performance exclusion later).
4. Leave: stays with the previous rep as a Left Visit; the snapshot builder keeps that Location on their tablet until the visit completes, is Missed or moved; Home shows "Handover — finish visit".
5. Undecided: Handover Pending, with a list to decide later and a count for the overview (T-14.1.1).

Decision points:
- Can a Left Visit's rep still take new orders at that Location (they're no longer its owner)?
- What happens to a Left Visit if the previous rep is later marked leaving?
- Does a handover decision write to Assignment History (it's visit ownership, not Location ownership)?

Delegable slivers:
- **Handover list UI** — Build the M-08 handover list with Move / Leave per visit and Apply to all, calling existing handover commands. No ownership logic.
- **Handover Pending list** — List undecided handovers with Move / Leave to decide later, calling existing commands. Read plus actions only.
- **Tablet left-visit marker** — Show "Handover — finish visit" on Home and the Location for Left Visits present in the snapshot. No snapshot logic.

---

### T-14.5.2-S — Test scenarios for a leaving rep and inherited visits

**Owner** — Human-Led
**Gates** — T-14.5.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "Is Colm leaving?" asked once at the start of a transfer (CV003-A). Asked for Location-level changes too?
- Leaving: no Leave option and no per-visit list; the preview states how many open visits move as visits needed, how many Overdue (CV003-B); all move with Scheduled Day cleared and windows unchanged; none Handover Pending (CV003-C).
- Inherited marker (CV003-E): shown on planner, overview and visit detail; counts in Niamh's operational Overdue.
- Performance exclusion (CV003-F): what exactly is excluded — the visit's completion or miss from Niamh's visit metrics; sales attribution is unchanged (Targets RC 7)?
- New dues generated after the handover aren't inherited (CV003-G).

---

### T-14.5.2 — Move every open visit as inherited when the previous rep is leaving

**Parent story**

> As a Sales Manager, I want to choose, per open visit, whether it moves to the new Primary Rep or stays with the previous one, with one choice applicable to all, so that a reassignment never strands a visit.
>
> Acceptance criteria:
> - Starting a transfer of Colm's assignments asks "Is Colm leaving?" (CV003-A)
> - Leaving: no Leave, no per-visit list; the preview states how many open visits move as visits needed, including how many are Overdue (CV003-B)
> - On save, every open Visit Due at a Location changing owner moves to its new Primary Rep with Scheduled Day cleared and window unchanged; none become Handover Pending (CV003-C)
> - Byrne's Overdue visit moved from Colm to Niamh shows "inherited from Colm" and counts in Niamh's Overdue (CV003-E); its outcome is excluded from Niamh's performance and the marker ends when it closes (CV003-F); new dues after the handover aren't inherited (CV003-G)

**Slice** — A transfer first asks whether the previous rep is leaving; if so, every open visit at the moving shops goes to the new owner unscheduled, marked "inherited from <rep>", counted operationally but kept out of the new rep's performance until it closes.
**Spec source** — Coverage Management US-003 CV003-A, B, C, E, F, G; uxdocs 05 M-08 (M8.1, M8.2)
**Depends on** — T-14.5.1
**Pattern to follow** — T-14.5.1 (handover)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — modifies T-14.5.1's handover, so characterisation comes first; the performance exclusion needs a human oracle because E28 reads it.

**Provisional commit message**

```
feat(coverage): move all open visits as inherited when a rep leaves

- "Leave with Colm" is meaningless once Colm has gone, so a leaving rep's
  open visits all move, unscheduled, as visits needed
- Late visits handed over don't count against the rep who inherits them
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A variant of a tight-loop flow, built against a human oracle.

**Agent prompt**

```
Role: You are adding the leaving-rep path and inherited visits to handover in
the Field Sales Management System.

Context:
- Slice: a transfer first asks whether the previous rep is leaving; if so,
  every open visit at the moving shops goes to the new owner unscheduled,
  marked "inherited from <rep>", counted operationally but kept out of the new
  rep's performance until it closes.
- Specs: plan_docs/stories/coverage-management.md US-003 CV003-A, B, C, E, F,
  G and glossary (Leaving rep, Inherited visit);
  plan_docs/uxdocs/05-manager.md M-08 (M8.1, M8.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — handover (T-14.5.1),
  transfer flow (T-3.1.4), Visit Dues (T-13.1.1).
- Pattern to follow: T-14.5.1.

Acceptance criteria:
1. Starting a transfer of Colm's assignments asks "Is Colm leaving?".
2. Yes: the preview has no Leave and no per-visit list, and states how many
   open visits move to the new owner as visits needed, including how many are
   Overdue.
3. On save every open Visit Due at a Location changing owner moves to its new
   Primary Rep with Scheduled Day cleared and window unchanged; none become
   Handover Pending.
4. Byrne's Overdue recurring visit moved from Colm to Niamh shows "inherited
   from Colm" on Niamh's planner, the overview and the visit detail, and
   counts in Niamh's Overdue.
5. When it completes or is marked Missed, its outcome is flagged as excluded
   from Niamh's performance and the marker ends.
6. Dues generated after the handover are not inherited.
7. No: the per-visit handover of T-14.5.1 applies unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- The performance-exclusion flag is stored on the visit for E28 to read; no
  performance views here.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  inherited marker and exclusion flag.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-14.5.1's handover; stop and show them passing. Resume only on
"Continue T-14.5.2".

Steps: 1. characterisation tests; 2. leaving question; 3. move-all path;
4. inherited marker and exclusion flag; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-14.5.2-S. You
are forbidden from designing your own test cases.

Definition of done: a transfer first asks whether the previous rep is
leaving; if so, every open visit at the moving shops goes to the new owner
unscheduled, marked "inherited from <rep>", counted operationally but kept out
of the new rep's performance until it closes.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Every agreed scenario passes
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: bulk reassign batches (E16), performance views (E28).
```

**Checkpoint**

Produces before pausing — characterisation tests of the per-visit handover, passing.
Human reviews — Does "leaving" move every open visit, and is the exclusion flag exactly what performance will need?
Resume trigger — `Continue T-14.5.2`

---

### T-14.1.1-S — Test scenarios for the overview by rep

**Owner** — Scenario Review
**Gates** — T-14.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Visit Planning overview by rep (task
T-14.1.1). Read plan_docs/stories/visit-planning.md US-010 S1, S1c, S5,
VP010-A–C and plan_docs/uxdocs/05-manager.md M-01 (M1.1, M1.2). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover them, then derivable edges: a rep with inherited Overdue
visits, a rep with no coverage, a manager with one rep. Absence decisions,
conflicts and cross-team cover count zero until E15. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-14.1.1 — See each rep's exceptions on the Visit Planning overview

**Parent story**

> As a Sales Manager, I want to see coverage by rep or by region with exceptions counted on each row so that I find who or where needs attention without reading every visit.
>
> Acceptance criteria:
> - For 8 reps, each row shows scheduled of due, Overdue, Missed, absence decisions pending, unscheduled due within 14 days, Over days, conflicts and handover pending (S1)
> - Colm's row shows "4 handover pending" linking to the Handover list (S1c)
> - A rep with no exceptions shows zeros, not an empty row (S5)
> - First use opens By rep; the last view is remembered (VP010-A, B)
> - Each row shows the broadest meaningful area, carve-out or overflow count and Location count; fragmented coverage shows up to two areas then "+ N areas" (VP010-C)

**Slice** — A manager opens the overview By rep and sees, per rep, scheduled of due, Overdue, Missed, unscheduled due within 14 days, Over days and handover pending — zeros shown — with a one-line reminder of the rep's patch.
**Spec source** — Visit Planning US-010 S1, S1c, S5, VP010-A–C; uxdocs 05 M-01 (M1.1, M1.2)
**Depends on** — T-13.1.1, T-13.4.1, T-13.5.1, T-14.5.1
**Pattern to follow** — T-3.1.3 (manager pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — aggregates several sources; settled design; checkpoint on the counts.

**Provisional commit message**

```
feat(visit-planning): overview each rep's exceptions

- Managers find problems before they go Overdue by reading counts per rep,
  not every visit; zeros are shown so an all-clear is visible
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Aggregations across areas with a review pause.

**Agent prompt**

```
Role: You are building the Visit Planning overview By rep (M-01) for the
Field Sales Management System's manager website.

Context:
- Slice: a manager opens the overview By rep and sees, per rep, scheduled of
  due, Overdue, Missed, unscheduled due within 14 days, Over days and handover
  pending — zeros shown — with a one-line reminder of the rep's patch.
- Specs: plan_docs/stories/visit-planning.md US-010 S1, S1c, S5, VP010-A–C
  and design decision "Manager overview by rep or by region, exceptions
  first"; plan_docs/uxdocs/05-manager.md M-01 (M1.1 By rep default and
  remembered; M1.2 coverage reminder).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Visit Dues (T-13.1.1),
  day load (T-13.4.1), Missed (T-13.5.1), handover (T-14.5.1), coverage
  resolver (T-3.1.1).
- Pattern to follow: T-3.1.3's pages.

Acceptance criteria:
1. For each rep the manager manages, a row shows scheduled of due, Overdue,
   Missed, absence decisions pending, unscheduled due within 14 days, Over
   days, conflicts and handover pending (absence and conflict counts are zero
   until E15).
2. "4 handover pending" on Colm's row links to the Handover Pending list.
3. A rep with no exceptions shows zeros.
4. First use opens By rep; the last-selected view is remembered.
5. Each row shows the broadest meaningful effective area, any carve-out or
   overflow count, and the effective Location count; fragmented coverage
   shows up to two areas then "+ N areas".
6. Inherited Overdue visits count in the new rep's Overdue.

Constraints:
- Use the project's existing conventions and test framework.
- Counts come from the owning functions (load, resolver, dues); no second
  rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the rep rows render with sample data for 8 reps, stop and
show them against M-01. Resume only on "Continue T-14.1.1".

Steps: 1. per-rep counts; 2. coverage reminder; 3. M-01 layout; 4. view
memory; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-14.1.1-S; do not design
your own.

Definition of done: a manager opens the overview By rep and sees, per rep,
scheduled of due, Overdue, Missed, unscheduled due within 14 days, Over days
and handover pending — zeros shown — with a one-line reminder of the rep's
patch.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: By region and drill-down (T-14.1.2), the unassigned header
count (T-14.2.1), cross-team cover (E15).
```

**Checkpoint**

Produces before pausing — rep rows for 8 sample reps with every count.
Human reviews — Does each count agree with its source screen, and do zeros read as all-clear?
Resume trigger — `Continue T-14.1.1`

---

### T-14.1.2-S — Test scenarios for the overview by region

**Owner** — Scenario Review
**Gates** — T-14.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Visit Planning overview by region with
drill-down (task T-14.1.2). Read plan_docs/stories/visit-planning.md US-010
S2, S3, S4 and plan_docs/uxdocs/05-manager.md M-01. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover them, then derivable edges: a Town covered by several reps, marking
Missed from the drill-down, a region with no dues. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-14.1.2 — See a region's visits by Town and drill into a visit

**Parent story**

> As a Sales Manager, I want to see coverage by rep or by region with exceptions counted on each row so that I find who or where needs attention without reading every visit.
>
> Acceptance criteria:
> - By region, opening Wicklow groups its Visit Dues by Town with the responsible rep; Rathdrum shows "3 Overdue" (S2)
> - Laragh shows "1 Unassigned" and Walsh's Shop links to assignment in Coverage Management (S3)
> - Opening an Overdue visit shows its window, Due Reason, last Call date and any Scheduled Day, and it can be marked Missed with an optional reason (S4)

**Slice** — Switching the overview to By region groups a region's visits by Town with the responsible rep and exception counts, unassigned shops link to assignment, and a manager opens any visit to see its detail and mark it Missed.
**Spec source** — Visit Planning US-010 S2–S4; uxdocs 05 M-01
**Depends on** — T-14.1.1
**Pattern to follow** — T-14.1.1
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled; Mark Missed is a closing state.

**Provisional commit message**

```
feat(visit-planning): overview by region with visit drill-down

- Assignments can be by region, town or shop, so a region may have several
  reps; grouping by Town with the rep beside each shows who is responsible
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A settled view with a closing action, with a review pause.

**Agent prompt**

```
Role: You are adding By region and visit drill-down to the Visit Planning
overview (M-01) of the Field Sales Management System.

Context:
- Slice: switching the overview to By region groups a region's visits by Town
  with the responsible rep and exception counts; unassigned shops link to
  assignment; a manager opens any visit to see its detail and mark it Missed.
- Specs: plan_docs/stories/visit-planning.md US-010 S2–S4;
  plan_docs/uxdocs/05-manager.md M-01.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — overview (T-14.1.1),
  Missed (T-13.5.1), Location page and assignment (T-3.2.2).
- Pattern to follow: T-14.1.1.

Acceptance criteria:
1. By region, opening Wicklow groups its Visit Dues by Town with the
   responsible rep beside each; Rathdrum shows its counts, e.g. "3 Overdue".
2. Laragh shows "1 Unassigned", and Walsh's Shop links to its Location page
   to assign it.
3. Opening an Overdue visit shows its window, Due Reason, last Call date and
   the rep's Scheduled Day if any, and offers Mark Missed with an optional
   reason.
4. The view choice is remembered (shared with T-14.1.1).

Constraints:
- Use the project's existing conventions and test framework; reuse
  T-13.5.1's Missed operation.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after By region and the drill-down render with sample data, stop
and show them. Resume only on "Continue T-14.1.2".

Steps: 1. region grouping; 2. unassigned links; 3. visit detail; 4. Mark
Missed; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-14.1.2-S; do not design
your own.

Definition of done: switching the overview to By region groups a region's
visits by Town with the responsible rep and exception counts, unassigned
shops link to assignment, and a manager opens any visit to see its detail and
mark it Missed.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the header unassigned count (T-14.2.1).
```

**Checkpoint**

Produces before pausing — By region and the visit drill-down rendered with sample data.
Human reviews — Is each Town's responsible rep correct where several reps share a region?
Resume trigger — `Continue T-14.1.2`

---

### T-14.2.1-S — Test scenarios for the unassigned count on the overview

**Owner** — Human-Led
**Gates** — T-14.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Scoping by nearest held geography (CV007-D, E, F, G): a Location counts on a manager's line when one of their reps holds a Town in its County; else a Territory Assignment in its Region; else it's "with no nearby team" on Head Office's view only. Write one scenario per rule.
- Two managers' reps both hold Towns in Wicklow: it counts on both (CV007-G).
- The link opens the Unassigned list showing only "my" Locations, with "All unassigned" showing all (CV007-H; M15.3).
- Count above zero only; no line at zero (CV007-A, B).

---

### T-14.2.1 — Show how many shops in my area are unassigned on the overview

**Parent story**

> As a Sales Manager, I want a list of Locations with no Primary Rep so that no shop is silently uncovered.
>
> Acceptance criteria:
> - With 6 Locations in my area unassigned, the overview header shows "6 Locations unassigned in your area" linking to the Unassigned list (CV007-A); none, no line (CV007-B)
> - My area is derived from the nearest geography my reps hold: same County, then Region; otherwise the Location counts "with no nearby team" for Head Office only (CV007-D, E, F)
> - A Location in a County where two managers' reps hold Towns counts on both overviews (CV007-G)
> - Following the link shows only my Locations, with "All unassigned" showing every one (CV007-H)

**Slice** — Whenever any shop in a manager's area has no rep, the overview header says how many and links to the Unassigned list filtered to that area, with a way to see all.
**Spec source** — Coverage Management US-007 CV007-A, B, D–H; uxdocs 05 M-15 (M15.1–M15.3), M-01
**Depends on** — T-14.1.1, T-3.3.1
**Pattern to follow** — T-14.1.1 (overview header), T-3.3.1 (Unassigned list)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — the scoping rule has several fall-backs a human must pin down (Edge-Case Discovery High); an agent builds against the agreed scenarios.

**Provisional commit message**

```
feat(coverage): count unassigned shops on each manager's overview

- An unassigned shop generates no visits and so never shows as an
  exception; a standing count makes it impossible to miss
- Each count is scoped to the nearest held geography, so every shop has a
  manager who owns the problem
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A scoping rule built against a human oracle.

**Agent prompt**

```
Role: You are adding the scoped unassigned count to the Visit Planning
overview (M-01, M-15) of the Field Sales Management System.

Context:
- Slice: whenever any shop in a manager's area has no rep, the overview
  header says how many and links to the Unassigned list filtered to that
  area, with a way to see all.
- Specs: plan_docs/stories/coverage-management.md US-007 CV007-A, B, D–H;
  plan_docs/uxdocs/05-manager.md M-15 (M15.1 standing count; M15.2 scoped
  to nearest held geography; M15.3 opens on the count clicked).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — overview (T-14.1.1),
  Unassigned list (T-3.3.1), resolver and reporting lines (T-3.1.1).
- Pattern to follow: T-14.1.1's header.

Acceptance criteria:
1. With 6 Locations in my area unassigned, the header shows "6 Locations
   unassigned in your area" linking to the Unassigned list; with none, no
   line.
2. A Location counts in my area when one of my reps holds a Town in its
   County; otherwise when one holds an assignment in its Region.
3. A Location with no held geography in its County or Region counts "with no
   nearby team" for a Head Office User and on no manager's line.
4. A Location in a County where two managers' reps hold Towns counts on both.
5. The link opens the Unassigned list showing only my Locations; "All
   unassigned" shows all.

Constraints:
- Use the project's existing conventions and test framework.
- The scoping rule lives in one function used by the count and the list
  filter.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the scoping function passes every agreed scenario, stop and
show it. Resume only on "Continue T-14.2.1".

Steps: 1. scoping function; 2. header count; 3. list filter and "All
unassigned"; 4. tests.

Test expectations: implement exactly the scenarios agreed in T-14.2.1-S. You
are forbidden from designing your own test cases.

Definition of done: whenever any shop in a manager's area has no rep, the
overview header says how many and links to the Unassigned list filtered to
that area, with a way to see all.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: assigning from the list (T-3.3.1 exists).
```

**Checkpoint**

Produces before pausing — the scoping function passing every agreed scenario.
Human reviews — Does every unassigned shop land on at least one responsible line, with none double-hidden?
Resume trigger — `Continue T-14.2.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | VP-010 (T-14.1.1, T-14.1.2), CV-007 part (T-14.2.1), VP-016 (T-14.3.1), VP-015 (T-14.4.1, T-14.4.2), CV-003 (T-14.5.1, T-14.5.2) |
| Every task satisfies the three slice criteria | Pass | 8 of 8 |
| Every task carries a tier with a rationale citing dimensions | Pass | 8 of 8 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-14.3.1 (MI-59), T-14.4.2 (MI-57) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-14.4.2, T-14.5.2 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | No Agent-Autonomous task in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-14.4.2, T-14.5.1, T-14.5.2, T-14.2.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | 7 of 7 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-14.5.1 |
| Every scenario task precedes the task it gates | Pass | 8 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-57, 59 |
