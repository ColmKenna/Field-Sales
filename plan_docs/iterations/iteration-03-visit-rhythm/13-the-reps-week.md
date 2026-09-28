# E13 — The rep's week

**Iteration** — 3, Visit rhythm
**Outcome** — Every shop with a visit rhythm gets its recurring visits on a fixed calendar; the rep turns them into a realistic week on the website planner, sees the day on the tablet's Home, moves single visits offline, decides slipped visits in a weekly digest, and fixes a pending order or a synced call on the rep website.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Visit Planning US-002 — Generate Recurring Visit Dues per Cycle Period | Must | S1–S4 (also Rep at a Location US-006 S1's "Visit Due marked Done" and US-016 S3) |
| 2 | Visit Planning US-003 — View my planner | Must | S1–S4, VP003-A–M (S5 superseded by VP003-D) |
| 3 | Visit Planning US-004 — Schedule visits onto days | Must | S1, S2, S4–S6, VP004-A–D (S3 and VP004-E absence blocking → E15) |
| 4 | Visit Planning US-005 — See day load and over-limit warnings | Should | S1, S2, S5 for recurring visits (S3 → E15; S4 and S5's campaign duration → E20) |
| 5 | Visit Planning US-009 — Action the weekly Cycle End Digest | Must | S1, S2, S4, S5, VP009-A–O (S3 superseded) |
| 6 | Rep at a Location US-005 — Move a visit to another day | Should | S1–S5 (S6 → E15) |
| 7 | Rep at a Location US-020 — Set today's visit sequence | Could | S1–S3 |
| 8 | Rep at a Location US-002 — Be reminded to Sync | Should | S1–S5 |
| 9 | Rep at a Location US-003 (part) — See today as a record and what's at risk | Must | S1, S3, S4, S6's cycle-decision count |
| 10 | Rep at a Location US-014 (part) — Check a Sent Call or Order | Must | S2, A1014-C (R-04) |
| 11 | Rep at a Location US-015 (part) — Correct a saved Call before Sync | Should | S4's website correction |

**Exit criterion** — Recurring visits exist for every shop with a rhythm and complete on any qualifying call; the rep plans them on R-01 (Day, Week, Month; Town list or map; drag or "Schedule on…"; day load in hours); Home on the tablet shows today by Town with Overdue, Due soon and deadlines; the agenda moves a visit offline with the same-Town prompt; the weekly digest closes slipped visits as Missed or keeps them Overdue; sync reminders fire at the day's natural end; and on R-04 the rep edits or cancels their own Pending orders until the cut-off and corrects synced calls.

**Capability-class stamp** — Frontier + extended reasoning for visit generation and the R-04 Pending-order slivers; Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [visit-planning.md](../../stories/visit-planning.md), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-002, US-003, US-005, US-014, US-015, US-020), [03-rep-planner.md](../../uxdocs/03-rep-planner.md) (R-01, R-02, R-04), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-01, T-02, T-03), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-004, BR-NEW-009, Visit Planning amendments).

---

### T-13.1.1-S — Test scenarios for generating and completing recurring visits

**Owner** — Human-Led
**Gates** — T-13.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Cycle arithmetic: Cycle Start 5 January 2026, every 4 weeks → window 28 September – 25 October 2026, next 26 October – 22 November (S1). Include a frequency change mid-year and a daylight-saving boundary.
- Generation Horizon (a manager setting, MI-21): how far ahead, and what happens when it's shortened?
- "One Call clears all": a qualifying Call completes every open Recurring Visit Due at the Location, Overdue and current. What is a qualifying call — any Call by any rep at the Location, including a phone call? A Follow-up Call (US-016 S3: yes, if in window)?
- A shop that changes owner: its open dues belong to whom (handover is E14)?
- An Unassigned Location's due is created as Unassigned and counted (S4).
- A Temporarily Closed or Closed shop (E17): generation continues or stops?
- "Last visited 24 Sep — 4 days ago" (S3): computed from the last Call at the Location.

---

### T-13.1.1 — Generate recurring visits on a fixed calendar and complete them by call

**Parent story**

> As a Sales Manager, I want each Location's Recurring Visit Dues generated automatically on its fixed calendar so that coverage doesn't depend on anyone remembering to create them.
>
> Acceptance criteria:
> - Murphy's Pharmacy, Cycle Start 5 January 2026, every 4 weeks, has a Recurring Visit Due with window 28 September – 25 October 2026, and the next 26 October – 22 November (S1)
> - A Call by the responsible rep on 3 October completes the open Visit Due (S2)
> - Last visited 24 September, the rep sees "Last visited 24 Sep — 4 days ago" on the 28 Sep – 25 Oct due (S3)
> - Walsh's Shop with no responsible rep gets its due created as Unassigned and counted on the manager overview (S4)
> - A saved Call marks the Location's Visit Due Done; a Follow-up Call completes an in-window Visit Due (Rep at a Location US-006 S1, US-016 S3)

**Slice** — Recurring visits are generated for every shop with a resolved frequency, one per cycle period up to the generation horizon, owned by the shop's primary rep (or Unassigned), and any call there completes every open recurring visit.
**Spec source** — Visit Planning US-002 S1–S4 and edge cases; glossary (Recurring Visit Due, Cycle Period, Overdue, Generation Horizon, One Call clears all); design decisions "Fixed calendar cycles with human-recorded Missed", "One Call clears all Recurring Visit Dues at a Location"; Rep at a Location US-006 S1, US-016 S3
**Depends on** — T-12.2.2, T-12.3.1, T-3.1.1, T-4.1.2
**Pattern to follow** — novel — see design notes
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — the visit engine every planning, overview and performance feature reads; cycle arithmetic and completion rules need a human oracle (Edge-Case Discovery High); settings undefined (MI-21).

**Provisional commit message**

```
feat(visit-planning): generate recurring visits on a fixed calendar

- Cycles are anchored to a start date so they never drift, and a period
  ending without a call goes Overdue for a person to judge, never the system
- One call at a shop completes every open recurring visit there, because
  the shop's need was met by the visit
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The visit engine; slivers stay narrow.

**Work package**

Increments:
1. Visit Due model: kind (Recurring, One-off, Campaign later), Location, owner rep or Unassigned, Due Window, Due Reason (type + explanation, later), Suggested Day, Scheduled Day, duration, state (Open, Complete, Overdue, Missed, Cancelled), inherited-from marker (E14).
2. Cycle calculator: given Cycle Start and frequency, the period containing a date and the next N periods. Pure; test against the agreed scenarios.
3. Generation job up to the Generation Horizon (setting, MI-21), idempotent; re-derives future periods when frequency, cycle start or profile changes, never touching the current open due.
4. Owner at generation from T-3.1.1's resolver; Unassigned when none.
5. Completion: when a Call is received for a Location, complete every open Recurring Visit Due there (Overdue and current); a Call made for a one-off is the exception (E14).
6. Overdue when a window ends with no qualifying Call; "Last visited N days ago".

Decision points:
- What counts as a qualifying Call (any rep? phone? follow-up)?
- Does a Closed or Temporarily Closed Location keep generating (E17 says yes for temporary, no for closed)?
- Generation Horizon and who sets it (MI-21).

Delegable slivers:
- **Cycle calculator** — Implement the pure cycle calculator (period containing a date; next N periods) and its tests from the agreed scenario list. No persistence or scheduling.
- **Generation job** — Implement an idempotent job that ensures each Location with a resolved frequency has dues for every period up to the horizon, using the existing calculator, resolver and owner lookup. Do not change completion rules.
- **Last-visited line** — Show "Last visited 24 Sep — 4 days ago" on a Visit Due from the Location's most recent Call. Read-only.

---

### T-13.2.1-S — Test scenarios for the planner calendar and list

**Owner** — Scenario Review
**Gates** — T-13.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the rep planner's calendar and unscheduled
Town list (task T-13.2.1). Read plan_docs/stories/visit-planning.md US-003
S1, S4 and VP003-A–M, and plan_docs/uxdocs/03-rep-planner.md R-01 (R1.1,
R1.6–R1.12). Output one line per scenario as Should_Outcome_When_Condition,
then "→" and a one-line intent. Cover every listed scenario, then derivable
edges: an Overdue visit from months ago, a Town with one visit, a narrow
laptop window. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-13.2.1 — Plan the week on a calendar beside a Town list of unscheduled visits

**Parent story**

> As a Field Salesperson, I want a calendar of my scheduled visits beside my unscheduled Visit Dues, as a Town list or a map, so that I can see deadlines and geography together.
>
> Acceptance criteria:
> - With 14 dues in the next 4 weeks, 6 scheduled, the planner for the week of 28 September shows the 6 on their days with each day's load, and the panel lists the 8 unscheduled grouped by Town, sorted by due date within each Town (S1)
> - A due with Due Reason "Autumn range order deadline" and Suggested Day Wed 30 Sep shows "Deadline: Autumn range order deadline" and "Suggested: Wed 30 Sep" (S4)
> - Day, Week and Month views; Week on first use; the last view remembered; Month shows counts and drills to Day (VP003-A, F, G, H, I)
> - The panel holds Overdue plus dues within four weeks, in every view; empty state "Nothing unscheduled due in the next 4 weeks" (VP003-B, C, D, E)
> - The panel stays beside the calendar, collapsing to "Unscheduled (n)" when narrow (VP003-J, K)
> - Due Reasons show as the type's icon and colour in Week, with text in Day when space permits (VP003-L, M)

**Slice** — On the rep website, the planner shows scheduled visits on a Day, Week or Month calendar beside a Town-grouped list of Overdue and next-four-weeks unscheduled visits, each with its deadline reason and the manager's suggested day.
**Spec source** — Visit Planning US-003 S1, S4, VP003-A–M; uxdocs 03 R-01 (R1.1, R1.6–R1.12)
**Depends on** — T-13.1.1
**Pattern to follow** — T-3.1.3 (staff site page shell)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: C | Confidence: M
  (inferred) — the most complex single layout in the system, fully settled; checkpoint on the layout at each view.

**Provisional commit message**

```
feat(visit-planning): plan the week beside the unscheduled town list

- Deadlines live in the list and days in the calendar, so both are always
  on screen; the panel's fixed four-week horizon stops far-off visits
  crowding the week
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A complex settled layout with a review pause.

**Agent prompt**

```
Role: You are building the rep planner (R-01) calendar and unscheduled list
for the Field Sales Management System's rep website.

Context:
- Slice: the planner shows scheduled visits on a Day, Week or Month calendar
  beside a Town-grouped list of Overdue and next-four-weeks unscheduled
  visits, each with its deadline reason and the manager's suggested day.
- Specs: plan_docs/stories/visit-planning.md US-003 S1, S4, VP003-A–M and
  design decision "Planner: calendar plus a list/map panel, with a non-drag
  path"; plan_docs/uxdocs/03-rep-planner.md R-01 (R1.1 calendar always left;
  R1.6 progressive disclosure of Due Reasons; R1.7–R1.12 views, horizon,
  defaults, month counts, collapse).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Visit Dues (T-13.1.1),
  staff site shell (T-3.1.3); Due Reason Types arrive in E14 (use fixtures
  until then).
- Pattern to follow: T-3.1.3's page shell.

Acceptance criteria:
1. With 14 dues in the next 4 weeks and 6 scheduled, the Week of 28 September
   shows the 6 on their days with each day's load; the panel lists the 8
   unscheduled grouped by Town, sorted by due date within each Town.
2. Day, Week and Month are selectable; Week on first use; the last selection
   is remembered.
3. Month shows each day's visit count (towns when there's room); selecting a
   populated day opens Day view on that date.
4. The panel holds Overdue dues and those due within four weeks, whatever the
   calendar view; later dues don't appear; empty state "Nothing unscheduled
   due in the next 4 weeks".
5. A due shows "Deadline: <reason>" and "Suggested: Wed 30 Sep"; in Week a
   scheduled card shows the Reason Type's icon and colour with an accessible
   label; in Day the reason text shows when space permits; opening a visit
   always shows the type and full explanation.
6. With enough width the panel stays beside the calendar; otherwise it
   collapses to "Unscheduled (n)" and reopens without losing selection.
7. Keyboard operable; colour never the only signal.

Constraints:
- Use the project's existing conventions and test framework.
- Read-only here; scheduling is T-13.3.1, the map is T-13.2.2.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the planner renders Week, Day and Month with sample data
and the panel collapses at narrow width, stop and show each against R-01.
Resume only on "Continue T-13.2.1".

Steps: 1. planner read model; 2. calendar views; 3. Town list panel;
4. reason disclosure; 5. responsive collapse; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.2.1-S; do not design
your own.

Definition of done: on the rep website, the planner shows scheduled visits on
a Day, Week or Month calendar beside a Town-grouped list of Overdue and
next-four-weeks unscheduled visits, each with its deadline reason and the
manager's suggested day.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Each view matches R-01
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: map (T-13.2.2), scheduling (T-13.3.1), day load (T-13.4.1),
absences (E15), campaigns (E20).
```

**Checkpoint**

Produces before pausing — Week, Day and Month rendered with sample data, and the collapsed panel at narrow width.
Human reviews — Does each view match R-01, and is every action reachable without a mouse?
Resume trigger — `Continue T-13.2.1`

---

### T-13.2.2-S — Test scenarios for the planner map

**Owner** — Scenario Review
**Gates** — T-13.2.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the planner's map panel (task T-13.2.2).
Read plan_docs/stories/visit-planning.md US-003 S2, S3 and its non-functional
notes, plan_docs/uxdocs/03-rep-planner.md R-01 (R1.2), and
plan_docs/stories/customer-directory.md glossary (Coordinates, Precision).
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover S2 and S3, then derivable edges: a Town-precision pin,
two shops at the same coordinates, selecting on the list then switching to
the map. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-13.2.2 — Show unscheduled and scheduled visits on a map with shared selection

**Parent story**

> As a Field Salesperson, I want a calendar of my scheduled visits beside my unscheduled Visit Dues, as a Town list or a map, so that I can see deadlines and geography together.
>
> Acceptance criteria:
> - Switching the panel to Map shows the 8 unscheduled as pins and the 6 scheduled as pins with their day letter and colour; 2 Locations without coordinates are listed below the map with "No map position" (S2)
> - Selecting 3 pins in Rathdrum then switching to List keeps the same 3 selected (S3)
> - Every map action has a list equivalent (non-functional)

**Slice** — The planner's panel switches to a map where unscheduled visits are pins, scheduled ones carry their day's letter and colour, shops without a position are listed below, and selection carries between map and list.
**Spec source** — Visit Planning US-003 S2, S3, non-functional notes; uxdocs 03 R-01 (R1.2); Visit Planning Requires Clarification 2 and Recommended Next Steps 2 (map spike)
**Depends on** — T-13.2.1, T-2.3.2
**Pattern to follow** — T-13.2.1 (panel)
**Ownership** — Impl: Human Tight-Loop ↓ | Test: Scenario Review | Complexity: M | Confidence: L
  (inferred) — would be Agent-Assisted; downgraded one step because the map provider and real coordinate coverage are unknown (MI-20) and the stories recommend a spike first.

**Provisional commit message**

```
feat(visit-planning): show visits on a map with shared selection

- Geography matters as much as deadlines, but the map is never the only
  way to act: pins carry a day letter, and shops without a position are
  listed
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A third-party map integration with unknown data quality.

**Work package**

Increments:
1. Spike (MI-20): choose a map provider allowed by the stack and licensing; load real Location coordinates and measure how many are Town precision only.
2. Map panel showing unscheduled pins and scheduled pins with day letter and colour.
3. "No map position" list below the map.
4. Selection shared with the Town list, both ways.
5. Precision shown so Town-level pins aren't trusted for navigation.

Decision points:
- Map provider and licence (MI-20).
- If most Locations are Town precision, does the map stay a Should Have (Visit Planning next step 2)?

Delegable slivers:
- **Shared selection state** — Implement one selection store used by both the Town list and the map, so switching views keeps the selection. No map rendering.
- **No-position list** — List visits whose Location has no coordinates below the map with "No map position", reading existing data. No map changes.

---

### T-13.3.1-S — Test scenarios for scheduling visits

**Owner** — Scenario Review
**Gates** — T-13.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for scheduling visits onto days (task T-13.3.1).
Read plan_docs/stories/visit-planning.md US-004 S1, S2, S4–S6 and VP004-A–D,
and plan_docs/uxdocs/03-rep-planner.md R-01 (R1.3, R1.4). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover them, then derivable edges: scheduling on a past day, rescheduling an
already scheduled visit, a due whose window starts after the chosen day.
Absence-day blocking arrives with E15. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-13.3.1 — Schedule visits by dragging or by "Schedule on…"

**Parent story**

> As a Field Salesperson, I want to place one or many Visit Dues onto a day by dragging or by selecting and choosing a day so that a Town's visits land together with little effort.
>
> Acceptance criteria:
> - Dragging Doyle's Shop onto Thursday 1 October schedules it and removes it from the panel (S1); drag uses the default duration and the load updates (VP004-A)
> - Selecting 8 Rathdrum visits and choosing Schedule on… Tuesday 29 September schedules all 8, grouped under Rathdrum in that day's sequence (S2)
> - Scheduling Kelly's Chemist (due 25 September, no reason) on 28 September shows "After the due date (25 Sep) — it will be Overdue" and can be confirmed (S4, VP004-D)
> - With a Due Reason it reads "After the due date (25 Sep). Due by then because: Autumn range order deadline." and can be confirmed (S5)
> - Scheduling on a day other than the Suggested Day raises no warning or conflict (S6)

**Slice** — A rep drags a visit onto a day, or selects several and chooses "Schedule on…", past-due placements warn — more strongly when there's a reason — but are allowed, and the manager's suggested day never gets in the way.
**Spec source** — Visit Planning US-004 S1, S2, S4–S6, VP004-A, VP004-D; uxdocs 03 R-01 (R1.3, R1.4); design decision "Suggested Day is shown, never applied — and never a conflict"
**Depends on** — T-13.2.1
**Pattern to follow** — T-13.2.1 (planner)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled design; Scheduled Day is rep-owned data the tablet reads; checkpoint on the non-drag path.

**Provisional commit message**

```
feat(visit-planning): schedule visits by drag or schedule-on

- "Schedule on…" is always present so every action works by keyboard;
  drag is only an accelerator
- Past-due placements warn and are allowed: the rep knows their day
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Interaction design on a settled screen with a review pause.

**Agent prompt**

```
Role: You are adding scheduling to the rep planner (R-01) in the Field Sales
Management System.

Context:
- Slice: a rep drags a visit onto a day, or selects several and chooses
  "Schedule on…"; past-due placements warn — more strongly with a reason — but
  are allowed; the manager's suggested day never gets in the way.
- Specs: plan_docs/stories/visit-planning.md US-004 S1, S2, S4–S6, VP004-A,
  VP004-D and design decision "Suggested Day is shown, never applied — and
  never a conflict"; plan_docs/uxdocs/03-rep-planner.md R-01 (R1.3 drag uses
  the default duration; R1.4 "Schedule on..." is always present).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — planner (T-13.2.1),
  Visit Dues (T-13.1.1).
- Pattern to follow: T-13.2.1's selection model.

Acceptance criteria:
1. Dragging Doyle's Shop onto Thursday 1 October schedules it with its
   default duration; it leaves the panel; the day's load updates.
2. Selecting 8 Rathdrum visits and choosing "Schedule on…" Tuesday 29
   September schedules all 8, grouped under Rathdrum in that day's sequence.
3. Scheduling Kelly's Chemist (due 25 Sep, no reason) on 28 Sep shows "After
   the due date (25 Sep) — it will be Overdue" and can be confirmed.
4. With Due Reason "Autumn range order deadline" it shows "After the due date
   (25 Sep). Due by then because: Autumn range order deadline." and can be
   confirmed.
5. Scheduling on a day other than the Suggested Day raises no warning.
6. "Schedule on…" works entirely by keyboard.
7. The Scheduled Day reaches the tablet at the rep's next sync.

Constraints:
- Use the project's existing conventions and test framework.
- Scheduled Day is owned by the rep; nothing here changes Suggested Day.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after drag and "Schedule on…" work on sample data with the
warnings, stop and show them. Resume only on "Continue T-13.3.1".

Steps: 1. schedule command; 2. drag; 3. Schedule on… with multi-select;
4. past-due confirmations; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.3.1-S; do not design
your own.

Definition of done: a rep drags a visit onto a day, or selects several and
chooses "Schedule on…", past-due placements warn — more strongly when there's
a reason — but are allowed, and the manager's suggested day never gets in the
way.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: day load and duration editing (T-13.4.1), absence days (E15),
tablet moves (T-13.6.1).
```

**Checkpoint**

Produces before pausing — drag and "Schedule on…" working on sample data, with both past-due warnings.
Human reviews — Does the non-drag path do everything drag does?
Resume trigger — `Continue T-13.3.1`

---

### T-13.4.1-S — Test scenarios for day load

**Owner** — Scenario Review
**Gates** — T-13.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for day load and visit duration (task
T-13.4.1). Read plan_docs/stories/visit-planning.md US-005 S1, S2, S5 (for
recurring visits), VP004-B, VP004-C, glossary (Travel Allowance, Working Day,
Day Load) and plan_docs/uxdocs/03-rep-planner.md R-01 (R1.5). Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover them, then derivable edges: a day with no visits, a day exactly
at capacity, rounding of minutes. Half-day absence arrives with E15 and
campaign durations with E20. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-13.4.1 — Show each day's load in hours and let the rep set a visit's duration

**Parent story**

> As a Field Salesperson, I want each day to show scheduled time against my Working Day so that I don't build a day I can't do.
>
> Acceptance criteria:
> - Working Day 8h, Travel Allowance 20 min, 6 visits of 45 min on Tuesday shows "6h 30m of 8h" (S1)
> - Adding a 90 min visit shows "Over by 20m" in text and the visit is scheduled (S2, VP004-C)
> - Selecting a scheduled visit and amending its duration updates the visit and the day's load (VP004-B)
> - A duration set when scheduling is used for the load (S5, for recurring visits)

**Slice** — Each day shows scheduled time plus travel allowance against the rep's working day in hours, going "Over by 20m" without blocking, and the rep can change a scheduled visit's duration.
**Spec source** — Visit Planning US-005 S1, S2, S5; VP004-B, VP004-C; glossary (Travel Allowance, Working Day, Day Load); uxdocs 03 R-01 (R1.5)
**Depends on** — T-13.3.1, T-12.3.1
**Pattern to follow** — T-13.3.1 (planner)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled; Working Day and Travel Allowance are settings with no screen (MI-21).

**Provisional commit message**

```
feat(visit-planning): show day load in hours and edit visit durations

- Capacity is measured in time, not visit counts, and warns rather than
  blocks, because the rep knows their day better than a flat estimate
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Settled calculation with a review pause.

**Agent prompt**

```
Role: You are adding day load and duration editing to the rep planner (R-01)
in the Field Sales Management System.

Context:
- Slice: each day shows scheduled time plus travel allowance against the
  rep's working day in hours, going "Over by 20m" without blocking, and the
  rep can change a scheduled visit's duration.
- Specs: plan_docs/stories/visit-planning.md US-005 S1, S2, S5, VP004-B,
  VP004-C, glossary (Travel Allowance, Working Day, Day Load) and design
  decision "Capacity in time, warned not blocked";
  plan_docs/uxdocs/03-rep-planner.md R-01 (R1.5 load stated in hours, not a
  bar).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — scheduling (T-13.3.1),
  resolved durations (T-12.2.2, T-12.3.1).
- Pattern to follow: T-13.3.1.

Acceptance criteria:
1. Working Day 8h, Travel Allowance 20 min, 6 visits of 45 min: the day
   shows "6h 30m of 8h".
2. Adding a 90 min visit shows "Over by 20m" in text; the visit is scheduled.
3. Selecting a scheduled visit and changing its duration updates the visit
   and the day's load at once.
4. A visit's duration defaults to its resolved duration.
5. Travel Allowance and Working Day come from settings (MI-21) until a
   screen exists.

Constraints:
- Use the project's existing conventions and test framework.
- One load function used by the planner and the manager overview's "Over
  days".
- No tests of framework internals or trivial members.
- This task opts in to a duration field on scheduled visits.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the load function and duration editing work on sample
data, stop and show them. Resume only on "Continue T-13.4.1".

Steps: 1. load function; 2. day header text; 3. duration editing;
4. settings; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.4.1-S; do not design
your own.

Definition of done: each day shows scheduled time plus travel allowance
against the rep's working day in hours, going "Over by 20m" without blocking,
and the rep can change a scheduled visit's duration.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: half-day absences (E15), campaign visit durations and combined
visits (E20).
```

**Checkpoint**

Produces before pausing — the load function and duration editing working on sample data.
Human reviews — Is load always shown in words and hours, never only as colour?
Resume trigger — `Continue T-13.4.1`

---

### T-13.9.1-S — Test scenarios for visits on the tablet Home

**Owner** — Scenario Review
**Gates** — T-13.9.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing visits on the tablet's Home (task
T-13.9.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-003 S1, S3,
S4 and edge cases, and plan_docs/uxdocs/01-tablet-day.md T-02 (T2.1–T2.4).
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Start with characterisation scenarios pinning Home's current
record-of-the-day view (T-4.5.1). Cover S1, S3, S4, then derivable edges: a
visit done by a follow-up call, a due with a window under 14 days, a visit on
the Location screen (T-05). Mark undecided cases as "Needs a decision". Write
no test code; change no files.
```

---

### T-13.9.1 — Show today's visits, Overdue and Due soon on the tablet Home

**Parent story**

> As a Field Salesperson, I want Home to show today's visits, what I've done, Overdue and Due soon items so that I know where I'm going, what I've captured and what I might miss.
>
> Acceptance criteria:
> - On Tuesday 22 September, scheduled visits at Murphy's and Byrne's (Rathdrum) and Doyle's (Laragh) show under Today grouped by Town, with the Unsent count (S1)
> - Kelly's Chemist (window ended 15 September, no Call) shows under Overdue with "Due 15 Sep"; Walsh's Shop (due 3 October, no Scheduled Day) under Due soon with "Due 3 Oct" (S3)
> - Murphy's Due Reason "Autumn range order deadline" shows "Deadline: Autumn range order deadline" on Home and the agenda (S4)
> - A due window under 14 days is Due soon immediately; past days show the same record in the agenda (edge cases)

**Slice** — After a sync, Home lists today's scheduled visits grouped by Town — marked Done when a call is saved — with Overdue below, visits due within 14 days and not yet scheduled under Due soon, and deadline reasons on the card; the Location screen shows its visit due.
**Spec source** — Rep at a Location US-003 S1, S3, S4, edge cases; US-004 S2's Visit Due line; uxdocs 01 T-02 (T2.1 Today stays at the top; T2.3 done stays in place; T2.4 deadline on the card)
**Depends on** — T-13.3.1, T-4.5.1
**Pattern to follow** — T-4.5.1 (Home extension points)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies Home and the Location screen and adds visit dues to the snapshot, so characterisation comes first.

**Provisional commit message**

```
feat(tablet): show today's visits, overdue and due soon on Home

- Home answers "where am I going, what have I done, and what might I
  miss", so today's plan, overdue visits and unscheduled deadlines sit
  together, with the reason for a deadline on the card
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Extends a shared screen behind a characterisation pass.

**Agent prompt**

```
Role: You are adding visits to the tablet's Home (T-02) and Location screen
(T-05) in the Field Sales Management System.

Context:
- Slice: after a sync, Home lists today's scheduled visits grouped by Town —
  marked Done when a call is saved — with Overdue below, visits due within 14
  days and not yet scheduled under Due soon, and deadline reasons on the
  card; the Location screen shows its visit due.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-003 S1, S3, S4 and
  edge cases, US-004 S2 (Visit Due with Due Reason on the Location);
  plan_docs/uxdocs/01-tablet-day.md T-02 (T2.1 Today stays on top even when
  Overdue has items; T2.3 done visits stay in place; T2.4 deadline reason on
  the card).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Home (T-4.5.1),
  Location screen (T-4.2.2), snapshot (T-4.1.1), Visit Dues (T-13.1.1),
  Scheduled Days (T-13.3.1).
- Pattern to follow: T-4.5.1's extension points.

Acceptance criteria:
1. On Tuesday 22 September, visits scheduled at Murphy's Pharmacy and Byrne's
   Chemist (Rathdrum) and Doyle's Shop (Laragh) show under Today grouped by
   Town, with the Unsent count.
2. Saving a Call at Murphy's marks it Done in place.
3. Kelly's Chemist, whose window ended 15 September with no Call, shows
   under Overdue with "Due 15 Sep", below Today.
4. Walsh's Shop, due 3 October with no Scheduled Day, shows under Due soon
   with "Due 3 Oct"; a window under 14 days is Due soon at once.
5. A due with Due Reason "Autumn range order deadline" shows "Deadline:
   Autumn range order deadline" on its card and on the Location screen.
6. Unplanned calls and orders from E4's record of the day still show.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions (the rep's open dues with window, reason, suggested and
  scheduled day) go through T-4.1.1's versioning and its owner's review.
- "Due soon" (14 days) is a setting.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
Home and the Location screen; stop and show them passing with the snapshot
additions. Resume only on "Continue T-13.9.1".

Steps: 1. characterisation tests; 2. snapshot fields; 3. Today by Town with
Done; 4. Overdue and Due soon; 5. deadline markers; 6. Location visit due;
7. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.9.1-S; do not design
your own.

Definition of done: after a sync, Home lists today's scheduled visits grouped
by Town — marked Done when a call is saved — with Overdue below, visits due
within 14 days and not yet scheduled under Due soon, and deadline reasons on
the card; the Location screen shows its visit due.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the agenda (T-13.6.1), sequence (T-13.7.1), the cycle-decision
count (T-13.5.1), conflicts and cover (E15), left visits (E14).
```

**Checkpoint**

Produces before pausing — characterisation tests of Home and the Location screen, passing, and the snapshot additions.
Human reviews — Does Home still read as the record of the day, with today on top?
Resume trigger — `Continue T-13.9.1`

---

### T-13.6.1-S — Test scenarios for moving a visit on the tablet agenda

**Owner** — Scenario Review
**Gates** — T-13.6.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for moving a visit on the tablet's week agenda
(task T-13.6.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-005
S1–S5 and edge cases, and plan_docs/uxdocs/01-tablet-day.md T-03 (T3.1,
T3.2). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Cover S1–S5, then derivable edges: moving a visit
twice before syncing, moving to today, moving the last visit out of a Town
group. Conflicts arrive with E15. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-13.6.1 — Move a visit to another day on the tablet, offline

**Parent story**

> As a Field Salesperson, I want to move a scheduled visit and optionally nearby visits with it so that a closed shop doesn't send me back to the same Town twice.
>
> Acceptance criteria:
> - Moving Doyle's Shop (Laragh, alone that day) from Tuesday 22 to Thursday 24 September shows it under Thursday, and the change is Unsent (S1)
> - Moving Murphy's when Byrne's and Nolan's (Rathdrum) are also on Tuesday asks "2 other visits in Rathdrum are scheduled Tuesday. Move them too?" with Move all / Just this one (S2)
> - The manager's Suggested Day shows as "Suggested: Wed 23 Sep" beside the picker; no day is pre-selected (S3)
> - A day after the due date warns "This is after the due date (25 Sep) — the visit will become Overdue", or with a reason "…Due by then because: Autumn range order deadline.", and can be confirmed (S4, S5)
> - Moved-in visits join the end of their Town group; works offline (edge cases)

**Slice** — On the tablet's week agenda, a rep moves a visit to another day offline — offered to move the Town's other visits that day too — with the suggested day beside the picker and a past-due warning, and the change uploads at the next sync.
**Spec source** — Rep at a Location US-005 S1–S5, edge cases; uxdocs 01 T-03 (T3.1 prompt after the move; T3.2 wording escalates only with a reason)
**Depends on** — T-13.9.1, T-13.3.1
**Pattern to follow** — T-13.9.1 (tablet visits); T-4.1.2 (upload)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — schedule changes become a new upload item type (sync contract); settled design.

**Provisional commit message**

```
feat(tablet): move visits between days on the week agenda

- A closed shop shouldn't send the rep back to the same Town twice, so
  moving one visit offers to move the Town's others that day
- Week-level planning stays on the laptop; the tablet handles one change
  at a time
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new upload type with a review pause.

**Agent prompt**

```
Role: You are building the tablet's week agenda move (T-03) in the Field
Sales Management System.

Context:
- Slice: on the tablet's week agenda, a rep moves a visit to another day
  offline — offered to move the Town's other visits that day too — with the
  suggested day beside the picker and a past-due warning; the change uploads
  at the next sync.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-005 S1–S5 and edge
  cases; design decision "Rep owns scheduling; reminders follow the rep's
  day"; plan_docs/uxdocs/01-tablet-day.md T-03 (T3.1, T3.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — tablet visits
  (T-13.9.1), upload protocol (T-4.1.2), website scheduling (T-13.3.1).
- Pattern to follow: T-4.1.2's upload items.

Acceptance criteria:
1. The agenda lists the week grouped by day, each visit with Town, due date
   and any deadline reason.
2. Move to… on Doyle's Shop (Laragh, alone on Tue 22 Sep) and choosing Thu
   24 Sep shows it under Thursday; the change is Unsent.
3. Moving Murphy's when Byrne's and Nolan's (Rathdrum) are also on Tuesday
   asks "2 other visits in Rathdrum are scheduled Tuesday. Move them too?"
   after the move, with Move all and Just this one.
4. "Suggested: Wed 23 Sep" sits beside the picker; no day is pre-selected.
5. After the due date without a reason: "This is after the due date (25 Sep)
   — the visit will become Overdue"; with a reason: "This is after the due
   date (25 Sep). Due by then because: Autumn range order deadline."; both
   can be confirmed.
6. Moved-in visits join the end of their Town group; everything works
   offline; the change uploads at next sync and the website planner shows it.

Constraints:
- Use the project's existing conventions and test framework.
- Schedule changes are a new upload item using T-4.1.2's exactly-once
  protocol; the snapshot's owner reviews the contract change.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the schedule-change upload item and its server handling
are designed, stop and show them. Resume only on "Continue T-13.6.1".

Steps: 1. agenda list; 2. Move to… picker with suggested day; 3. same-Town
prompt; 4. past-due warnings; 5. upload item and server apply; 6. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.6.1-S; do not design
your own.

Definition of done: on the tablet's week agenda, a rep moves a visit to
another day offline — offered to move the Town's other visits that day too —
with the suggested day beside the picker and a past-due warning, and the
change uploads at the next sync.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: schedule conflicts with manager changes (E15), today's sequence
(T-13.7.1).
```

**Checkpoint**

Produces before pausing — the schedule-change upload item and how the server applies it.
Human reviews — Is a move made offline applied exactly once, and what happens if the rep also moved it on the website?
Resume trigger — `Continue T-13.6.1`

---

### T-13.7.1-S — Test scenarios for today's visit sequence

**Owner** — Scenario Review
**Gates** — T-13.7.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for setting today's visit sequence (task
T-13.7.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-020 S1–S3
and plan_docs/uxdocs/01-tablet-day.md T-02 (T2.7). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover S1–S3, then derivable edges: reordering then syncing twice, a visit
removed from today by the manager. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-13.7.1 — Reorder today's visits into the driving route

**Parent story**

> As a Field Salesperson, I want to rearrange today's visits into my driving route so that Home matches the order I'll actually do them in.
>
> Acceptance criteria:
> - 3 visits in Rathdrum and 1 in Laragh show grouped by Town by default (S1)
> - Moving Doyle's Shop (Laragh) to the top, by drag or Move up, keeps it first for today, including after a Sync (S2)
> - A visit moved into today joins the end of its Town group (S3)
> - Move up / Move down work as a non-drag alternative (non-functional)

**Slice** — In Reorder mode on Home, the rep moves today's visits up or down into their driving route, and the order survives a sync.
**Spec source** — Rep at a Location US-020 S1–S3; uxdocs 01 T-02 (T2.7 reordering only in Reorder mode, with ^/v buttons)
**Depends on** — T-13.9.1
**Pattern to follow** — T-13.6.1 (schedule change uploads)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — the sequence is sent to Visit Planning, so it touches the sync contract; otherwise small and settled.

**Provisional commit message**

```
feat(tablet): reorder today's visits into the driving route

- Town grouping is a good default but misses neighbours across a
  boundary, so the rep can set the day's order and it survives a sync
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Small, but it touches the sync contract.

**Agent prompt**

```
Role: You are adding today's visit sequence to the tablet Home (T-02) in the
Field Sales Management System.

Context:
- Slice: in Reorder mode on Home, the rep moves today's visits up or down into
  their driving route, and the order survives a sync.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-020 S1–S3 and
  glossary (Visit sequence); plan_docs/uxdocs/01-tablet-day.md T-02 (T2.7).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Home visits
  (T-13.9.1), schedule-change uploads (T-13.6.1).
- Pattern to follow: T-13.6.1's upload item.

Acceptance criteria:
1. Today's visits are grouped by Town by default.
2. Reorder mode offers Move up and Move down (^/v) on each visit; moving
   Doyle's Shop (Laragh) to the top keeps it first.
3. The order survives a sync (uploaded and returned in the snapshot).
4. A visit moved into today joins the end of its Town group.

Constraints:
- Use the project's existing conventions and test framework.
- Reorder only in Reorder mode (T2.7); the word "order" is reserved for
  customer orders in UI copy.
- Sequence changes go through the schedule-change upload.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond a
  sequence field on scheduled visits.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the sequence field and its upload are wired, stop and show
a reorder surviving a sync. Resume only on "Continue T-13.7.1".

Steps: 1. sequence field; 2. Reorder mode; 3. upload and snapshot round
trip; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.7.1-S; do not design
your own.

Definition of done: in Reorder mode on Home, the rep moves today's visits up
or down into their driving route, and the order survives a sync.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: route optimisation (out of scope for the system).
```

**Checkpoint**

Produces before pausing — a reorder surviving a sync round trip.
Human reviews — Does the website planner respect the rep's sequence for that day?
Resume trigger — `Continue T-13.7.1`

---

### T-13.8.1-S — Test scenarios for sync reminders

**Owner** — Scenario Review
**Gates** — T-13.8.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for sync reminders (task T-13.8.1). Read
plan_docs/stories/rep-at-a-location-tablet.md US-002 S1–S5 and edge cases,
and plan_docs/uxdocs/01-tablet-day.md T-01 (T1.3). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover S1–S5, then derivable edges: signal flickering several times in an
hour, a Needs Attention item in the count, no visits scheduled today with
unsent work. The soft reminder hour is a setting (MI-21). Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-13.8.1 — Remind the rep to sync at the natural end of the day

**Parent story**

> As a Field Salesperson, I want a reminder when I have Unsent work at the natural end of my day so that Orders don't sit on the tablet overnight.
>
> Acceptance criteria:
> - With 3 Scheduled Visits and 2 Unsent items, saving the third visit's Call shows "2 items not sent to head office — 1 order still in progress" with Sync now (S1)
> - With a Ready to Send Order and no signal, regaining signal shows the reminder, not again within the hour (S2)
> - After the last-visit reminder at 15:05, saving a phone Order at 16:20 shows it again (S3)
> - With no visits today and 1 Unsent Order, at the late-day time the Home count changes to "1 not sent today" in text, with no notification (S4)
> - With nothing unsent, no reminder (S5)

**Slice** — When the rep finishes the day's last scheduled visit, regains signal, or adds work after the last visit with anything unsent, the tablet reminds them to sync — at most once an hour — and late in the day Home's unsent count says so in words.
**Spec source** — Rep at a Location US-002 S1–S5, edge cases; uxdocs 01 T-01 (T1.3 no automatic sync; reminder when signal returns)
**Depends on** — T-13.9.1
**Pattern to follow** — T-4.5.1 (Home strip)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — device notifications and connectivity events are platform-specific; the soft reminder hour is unset (MI-21).

**Provisional commit message**

```
feat(tablet): remind reps to sync at the natural end of the day

- Sync is deliberate, so work can sit on the tablet overnight; reminders
  follow the rep's day, capped hourly so they don't become noise
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Platform notification behaviour with a review pause.

**Agent prompt**

```
Role: You are adding sync reminders to the Field Sales Management System's
tablet app.

Context:
- Slice: when the rep finishes the day's last scheduled visit, regains
  signal, or adds work after the last visit with anything unsent, the tablet
  reminds them to sync — at most once an hour — and late in the day Home's
  unsent count says so in words.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-002 S1–S5 and edge
  cases; plan_docs/uxdocs/01-tablet-day.md T-01 (T1.3).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Home and today's visits
  (T-13.9.1), unsent counts (T-4.3.1), sync (T-4.1.1).
- Pattern to follow: T-4.5.1's Home strip.

Acceptance criteria:
1. With 3 Scheduled Visits today and 2 Unsent items, saving the third
   visit's Call shows "2 items not sent to head office — 1 order still in
   progress" with Sync now.
2. With a Ready to Send Order and no signal, regaining signal shows the
   reminder; it is not shown again within the hour even if signal flickers.
3. After the last-visit reminder at 15:05, saving a phone Order at 16:20
   shows it again.
4. With no visits today and 1 Unsent Order, at the late-day time the Home
   count reads "1 not sent today" in text, with no notification.
5. With nothing unsent, no reminder.
6. No reminder before the morning sync; Needs Attention items are counted
   ("1 needs attention").

Constraints:
- Use the project's existing conventions and test framework.
- Sync is never started automatically.
- The late-day hour is a setting (MI-21).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the reminder triggers and the hourly cap are implemented,
stop and show them on a device. Resume only on "Continue T-13.8.1".

Steps: 1. trigger conditions; 2. hourly cap; 3. notification; 4. soft text
on Home; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.8.1-S; do not design
your own.

Definition of done: when the rep finishes the day's last scheduled visit,
regains signal, or adds work after the last visit with anything unsent, the
tablet reminds them to sync — at most once an hour — and late in the day
Home's unsent count says so in words.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: background delivery of customer requests (E25).
```

**Checkpoint**

Produces before pausing — reminder triggers and the hourly cap demonstrated on a device.
Human reviews — Do reminders stay at most once an hour and never start a sync on their own?
Resume trigger — `Continue T-13.8.1`

---

### T-13.5.1-S — Test scenarios for the cycle end digest

**Owner** — Scenario Review
**Gates** — T-13.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the weekly Cycle End Digest with per-visit
decisions (task T-13.5.1). Read plan_docs/stories/visit-planning.md US-009
S1, S2, S4, S5, VP009-L–O and edge cases, and plan_docs/uxdocs/
03-rep-planner.md R-02 (R2.1, R2.3, R2.6, R2.7). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Cover them,
then derivable edges: a Call recorded before the digest is actioned, a visit
in "Still open" for three weeks, switching Missed back to Keep Overdue. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-13.5.1 — Decide slipped visits in the weekly cycle end digest

**Parent story**

> As a Field Salesperson, I want a weekly list of visits whose cycle ended without a Call so that I decide, in one sitting, which stay Overdue and which are Missed.
>
> Acceptance criteria:
> - With 3 windows ending this week without a Call, on Sunday I get one website notification "3 visits reached the end of their cycle" opening the list (S1)
> - Marking Kelly's Chemist Missed with reason "Closed for refurbishment" closes it and puts it in the manager's Missed list; the other 2 stay Overdue (S2)
> - Ignored visits stay Overdue and reappear next week under "Still open" (S4)
> - The tablet shows "3 cycle decisions waiting — on the website" and no notification (S5; US-003 S6)
> - Per-row Keep Overdue / Missed with nothing preselected; choosing Missed reveals an optional reason inline; switching back hides and discards it (VP009-L–O)

**Slice** — Each Sunday the rep is notified of visits whose cycle ended without a call; on the digest they keep each Overdue or mark it Missed with an optional reason, unactioned visits come back next week, and the tablet counts the decisions waiting.
**Spec source** — Visit Planning US-009 S1, S2, S4, S5, VP009-L–O, edge cases; Rep at a Location US-003 S6 (cycle-decision count); uxdocs 03 R-02 (R2.1, R2.3, R2.6, R2.7)
**Depends on** — T-13.1.1
**Pattern to follow** — T-13.2.1 (rep website pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled design; Missed is a closing state read by managers and performance.

**Provisional commit message**

```
feat(visit-planning): decide slipped visits in a weekly digest

- The system never closes a visit on its own; a weekly prompt puts the
  Overdue-or-Missed decision where the rep can make it in one sitting
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Settled screen with a closing state, with a review pause.

**Agent prompt**

```
Role: You are building the weekly Cycle End Digest (R-02) with per-visit
decisions for the Field Sales Management System's rep website.

Context:
- Slice: each Sunday the rep is notified of visits whose cycle ended without a
  call; on the digest they keep each Overdue or mark it Missed with an
  optional reason; unactioned visits come back next week; the tablet counts
  the decisions waiting.
- Specs: plan_docs/stories/visit-planning.md US-009 S1, S2, S4, S5,
  VP009-L–O, edge cases, glossary (Missed, Cycle End Digest);
  plan_docs/uxdocs/03-rep-planner.md R-02 (R2.1 "Still open from earlier
  weeks" on top; R2.3 no default; R2.6 per-visit controls; R2.7 inline
  reason); plan_docs/stories/rep-at-a-location-tablet.md US-003 S6.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Visit Dues (T-13.1.1),
  rep website (T-13.2.1), tablet Home strip (T-4.5.1), snapshot (T-4.1.1).
- Pattern to follow: T-13.2.1's pages.

Acceptance criteria:
1. On Sunday, with 3 dues whose windows ended this week without a Call, the
   rep gets one website notification "3 visits reached the end of their
   cycle" opening the digest.
2. The digest lists "Still open from earlier weeks" first, then "This week";
   no outcome is preselected.
3. Marking Kelly's Chemist Missed shows an optional reason field beneath
   that row; with "Closed for refurbishment" it closes as Missed and appears
   in the manager's Missed list with the reason; the other 2 stay Overdue.
4. Switching a row back to Keep Overdue hides and discards its reason.
5. Ignored visits stay Overdue and reappear next week under "Still open".
6. A Call recorded before the rep acts removes that visit from the digest.
7. The tablet Home strip shows "3 cycle decisions waiting — on the website",
   with no notification.
8. Missed closes and records only: no replacement visit or follow-up is
   created.

Constraints:
- Use the project's existing conventions and test framework.
- The digest day (Sunday) is a setting (MI-21).
- The tablet count goes in the snapshot via T-4.1.1's versioning.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  Missed state and reason.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the digest renders both sections and row decisions work on
sample data, stop and show them. Resume only on "Continue T-13.5.1".

Steps: 1. digest query and notification; 2. R-02 page with row controls;
3. Missed with reason; 4. tablet count; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.5.1-S; do not design
your own.

Definition of done: each Sunday the rep is notified of visits whose cycle
ended without a call; on the digest they keep each Overdue or mark it Missed
with an optional reason, unactioned visits come back next week, and the
tablet counts the decisions waiting.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–8 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: bulk review pages (T-13.5.2), manager marking Missed from the
overview (T-14.1.2).
```

**Checkpoint**

Produces before pausing — the digest with both sections and working row decisions on sample data.
Human reviews — Is nothing preselected, and does Missed close only what the rep chose?
Resume trigger — `Continue T-13.5.1`

---

### T-13.5.2-S — Test scenarios for bulk digest decisions

**Owner** — Scenario Review
**Gates** — T-13.5.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for bulk review pages on the Cycle End Digest
(task T-13.5.2). Read plan_docs/stories/visit-planning.md VP009-A–K and
plan_docs/uxdocs/03-rep-planner.md R-02 (R2.2, R2.4, R2.5). Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Start with characterisation scenarios pinning T-13.5.1's row
decisions. Cover VP009-A–K, then derivable edges: a shared reason changed
twice with two overrides, leaving and returning to the review. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-13.5.2 — Review and apply a digest decision to many visits at once

**Parent story**

> As a Field Salesperson, I want a weekly list of visits whose cycle ended without a Call so that I decide, in one sitting, which stay Overdue and which are Missed.
>
> Acceptance criteria:
> - "Review Keep Overdue…" or "Review Mark Missed…" opens a review listing all candidates under the digest's section headings, every one selected (VP009-A)
> - Select all, Clear all and checkboxes update the count immediately; with none selected, apply is unavailable with an explanation (VP009-B, D)
> - Only selected visits receive the outcome; excluded ones stay undecided; completion returns to the digest; leaving without applying changes nothing (VP009-C, E, F)
> - A shared optional Missed reason applies to every selected visit without its own override; overrides survive a changed shared reason (VP009-G, H, I)
> - Missed closes and records only; next cycle's due is unchanged; a retry is a manual one-off (VP009-J, K)

**Slice** — From the digest, the rep reviews every candidate for Keep Overdue or Mark Missed on its own page — all selected, exclusions allowed, one shared Missed reason with per-visit overrides — and applies it only to the visits still selected.
**Spec source** — Visit Planning VP009-A–K; uxdocs 03 R-02 (R2.2, R2.4, R2.5)
**Depends on** — T-13.5.1
**Pattern to follow** — T-3.1.4 (review with exclusions)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies T-13.5.1's decisions, so characterisation comes first; the reason-override rules are fully specified.

**Provisional commit message**

```
feat(visit-planning): review and apply digest decisions in bulk

- "Apply to all" hid what it changed; a review page shows every visit it
  will touch and lets the rep exclude exceptions first
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Bulk changes behind a characterisation pass.

**Agent prompt**

```
Role: You are adding bulk review pages to the Cycle End Digest (R-02) in the
Field Sales Management System.

Context:
- Slice: from the digest, the rep reviews every candidate for Keep Overdue or
  Mark Missed on its own page — all selected, exclusions allowed, one shared
  Missed reason with per-visit overrides — and applies it only to the visits
  still selected.
- Specs: plan_docs/stories/visit-planning.md VP009-A–K;
  plan_docs/uxdocs/03-rep-planner.md R-02 (R2.2 shared reason with overrides;
  R2.4 Missed closes and records only; R2.5 dedicated review page).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — digest (T-13.5.1).
- Pattern to follow: T-3.1.4's review with exclusions.

Acceptance criteria:
1. "Review Keep Overdue…" and "Review Mark Missed…" open a page listing all
   candidates under "Still open from earlier weeks" and "This week", every
   one selected.
2. Select all, Clear all and individual checkboxes update the selected count
   immediately; with none selected, apply is unavailable and the page says at
   least one must be selected.
3. Applying gives only the selected visits the outcome; excluded ones remain
   undecided on the digest; completion returns to the digest.
4. Leaving without applying changes nothing.
5. On Mark Missed, a shared optional reason applies to every selected visit
   without an individual override; overrides and clears are kept when the
   shared reason changes.
6. Missed closes and records only; the next cycle's due is unchanged.

Constraints:
- Use the project's existing conventions and test framework; reuse
  T-13.5.1's Missed and Keep operations.
- All selected changes apply together or not at all.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-13.5.1's row decisions; stop and show them passing. Resume only on
"Continue T-13.5.2".

Steps: 1. characterisation tests; 2. review pages; 3. selection; 4. shared
reason and overrides; 5. apply; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.5.2-S; do not design
your own.

Definition of done: from the digest, the rep reviews every candidate for Keep
Overdue or Mark Missed on its own page — all selected, exclusions allowed, one
shared Missed reason with per-visit overrides — and applies it only to the
visits still selected.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: creating replacement one-off visits (T-14.4.1 is the manual
route).
```

**Checkpoint**

Produces before pausing — characterisation tests of the digest's row decisions, passing.
Human reviews — Does bulk apply touch only the selected visits, with the right reason on each?
Resume trigger — `Continue T-13.5.2`

---

### T-13.10.1-S — Test scenarios for editing a Pending order on the rep website

**Owner** — Human-Led
**Gates** — T-13.10.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Which orders are editable: only Pending orders the rep took themselves (R4.1); customer-placed and other reps' orders read-only even while Pending.
- The cut-off race: a save at 15:59:30 with a 16:00 cut-off; a save refused after it, with the accepted order standing; the 15-minute warning shown only once editing (C4.4 applied to reps).
- Pricing of changed and added lines (MI-58): the website is live, but the order was priced from the morning snapshot. Do untouched lines keep captured prices? Do new lines price live? Are discount allowance and FOC limits enforced online (E19)?
- Cancelling: the order shows Cancelled everywhere; does a cancelled order release Run-out Remaining?
- A held order (T-7.2.1): not editable.

---

### T-13.10.1 — Edit or cancel my own Pending order on the website until the cut-off

**Parent story**

> As a Field Salesperson, I want to see a Sent item's last known status and open it on the website so that I answer customers honestly and make changes when I have signal.
>
> Acceptance criteria:
> - With signal, Open on website opens the Order on the website with its current status (S2)
> - An Order taken at Carey's Pharmacy at 10:40 with a 4pm cut-off can have its lines changed, or be cancelled, on the website at 2pm, until 4pm; an order Mary placed online for Hickey's Rathdrum opens read-only even while Pending (A1014-C)
> - An accepted order is read-only with its live status (R-04 confirmed default 2)

**Slice** — From the tablet's "Open on website", a rep opens a sent order on R-04 and, if it is still Pending and they took it, changes its lines or cancels it until the cut-off; everything else opens read-only with its live status.
**Spec source** — Rep at a Location US-014 S2, A1014-C and the 26 Sep amendment; uxdocs 03 R-04 (R4.1 and confirmed defaults 1–2); uxdocs 04 BR-NEW-009 rule 3
**Depends on** — T-7.2.1, T-7.5.1
**Pattern to follow** — T-7.2.1 (lifecycle transitions against the cut-off)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — edits orders before the cut-off (data integrity, the same race as Hold); edited-line pricing is undefined (MI-58).

**Provisional commit message**

```
feat(orders): edit or cancel own pending orders on the website

- Orders wait until the cut-off, so the rep who took one can fix it with
  signal before it's accepted; nobody edits anyone else's order
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Order edits racing an automated acceptance.

**Work package**

Increments:
1. Resolve MI-58 (pricing and limits of edited lines) and record the rule.
2. R-04 order page: Pending own orders show the T-07-style order lines, "You can change this until 4pm", Save changes and Cancel order; everything else read-only with live status.
3. Edit and cancel as lifecycle transitions that are mutually exclusive with the acceptance job (reuse T-7.2.1's locking approach).
4. The 15-minute warning once editing; a save after the cut-off is refused and the accepted order stands.
5. "Open on website" on the tablet opens this page (resolves MI-50).
6. The tablet shows the edited or cancelled order at next sync.

Decision points:
- MI-58: captured prices kept for untouched lines? New lines priced live or from the rep's snapshot? Allowance and FOC limits online?
- Does cancelling release Run-out Remaining (T-10.3.1)?
- Sign-in handover from the tablet link to the website (tablet assumption: handled by the web platform).

Delegable slivers:
- **Read-only order page** — Build the R-04 read-only view of an order with its live status and lines, used for accepted, held, customer-placed and other reps' orders. No edit controls.
- **Cut-off warning** — Show "This order locks at 4pm — 5 minutes left to save" only while editing, starting 15 minutes before the cut-off, reading the existing next-cut-off function. No save logic.
- **Tablet link target** — Make "Open on website" open the R-04 page for that order when there is signal. No other tablet changes.

---

### T-13.11.1-S — Test scenarios for correcting a synced call on the website

**Owner** — Scenario Review
**Gates** — T-13.11.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for correcting a synced call on the rep website
(task T-13.11.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-015
S2, S4 and glossary (Correction, Follow-up Call), and
plan_docs/uxdocs/03-rep-planner.md R-04 (confirmed defaults 3–4). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover them, then derivable edges: correcting a count that fed a Low
item already on an order, two corrections to one call, another rep's call.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-13.11.1 — Correct a synced call on the website

**Parent story**

> As a Field Salesperson, I want to fix genuine mistakes in a Call I've saved but not synced so that head office receives accurate records.
>
> Acceptance criteria:
> - A Sent Call offers Open on website and Record follow-up call, not Edit Call (S4)
> - On the website, values are editable and lines removable; where "Add" would be, the page explains that follow-up calls are recorded on the tablet; each correction is saved with who and when; the tablet shows the corrected version at next sync (R-04 confirmed defaults 3–4)

**Slice** — On R-04, a rep corrects values in a call they've already synced or removes a line — never adds — each correction is recorded with who and when, and the tablet shows the corrected call after the next sync.
**Spec source** — Rep at a Location US-015 S4 and the US-014 26 Sep amendment; uxdocs 03 R-04 (confirmed defaults 3–4); glossary (Correction)
**Depends on** — T-13.10.1, T-5.6.1
**Pattern to follow** — T-5.6.1 (constrained edit), T-13.10.1 (R-04 pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — changes a record after upload (audit trail needed); the constrained-edit rule is reviewed.

**Provisional commit message**

```
feat(calls): correct synced calls on the website with an audit trail

- After sync the tablet is read-only, so genuine mistakes are fixed online;
  additions still go on a follow-up call, and every correction says who
  made it and when
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Post-upload edits with an audit trail, with a review pause.

**Agent prompt**

```
Role: You are adding synced-call correction to R-04 in the Field Sales
Management System's rep website.

Context:
- Slice: on R-04, a rep corrects values in a call they've already synced or
  removes a line — never adds — each correction is recorded with who and
  when, and the tablet shows the corrected call after the next sync.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-015 S2, S4 and
  glossary (Correction, Follow-up Call); plan_docs/uxdocs/03-rep-planner.md
  R-04 (synced call frame; confirmed defaults 3–4).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — R-04 pages (T-13.10.1),
  constrained edit rule (T-5.6.1), calls as received (T-4.1.2), snapshot
  (T-4.1.1).
- Pattern to follow: T-5.6.1's constrained edit.

Acceptance criteria:
1. On R-04 a synced call's counts, Channel, Low marks and note text are
   editable and lines removable.
2. There is no Add action; "Anything forgotten? Record a follow-up call on
   the tablet." appears instead.
3. Each correction is saved with who changed it and when; earlier values are
   kept.
4. The tablet shows the corrected call after its next sync.
5. Only the rep who recorded the call can correct it (others read-only).

Constraints:
- Use the project's existing conventions and test framework; apply the same
  constrained-edit rule as T-5.6.1 (one rule, both surfaces).
- No tests of framework internals or trivial members.
- This task opts in to a call-correction history table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the correction history model and the shared constrained-
edit rule are in place, stop and show them. Resume only on
"Continue T-13.11.1".

Steps: 1. correction history; 2. R-04 synced call page; 3. snapshot update;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-13.11.1-S; do not
design your own.

Definition of done: on R-04, a rep corrects values in a call they've already
synced or removes a line — never adds — each correction is recorded with who
and when, and the tablet shows the corrected call after the next sync.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: follow-up calls on the website (they are tablet-only), order
editing (T-13.10.1).
```

**Checkpoint**

Produces before pausing — the correction history model and the constrained-edit rule shared with the tablet.
Human reviews — Is every correction attributable and reversible from history, with no way to add content?
Resume trigger — `Continue T-13.11.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | VP-002 (T-13.1.1), VP-003 (T-13.2.1, T-13.2.2), VP-004 (T-13.3.1), VP-005 (T-13.4.1), VP-009 (T-13.5.1, T-13.5.2), A1-005 (T-13.6.1), A1-020 (T-13.7.1), A1-002 (T-13.8.1), A1-003 part (T-13.9.1), A1-014 part (T-13.10.1), A1-015 part (T-13.11.1) |
| Every task satisfies the three slice criteria | Pass | 13 of 13 |
| Every task carries a tier with a rationale citing dimensions | Pass | 13 of 13 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-13.1.1 (MI-21), T-13.2.2 (MI-20), T-13.10.1 (MI-58) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-13.9.1, T-13.5.2 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | No Agent-Autonomous task in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-13.1.1, T-13.10.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | 10 of 10 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-13.1.1, T-13.2.2 (downgraded, stated), T-13.10.1 |
| Every scenario task precedes the task it gates | Pass | 13 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-20, 21, 50, 58 |
