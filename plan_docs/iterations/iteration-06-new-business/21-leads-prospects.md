# E21 — Leads and prospects

**Iteration** — 6, New business
**Outcome** — Reps and managers note tips in seconds, offline; reps keep a lead list worth reading; visiting a lead or cold-calling a shop creates a prospect saved as a draft and finished later; calls and orders at a prospect work as at a customer; and a prospect's first accepted order makes it a customer.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Prospecting US-001 — Capture a lead quickly | Must | S1–S5 |
| 2 | Prospecting US-002 — Work my leads | Must | S1–S5 |
| 3 | Prospecting US-003 — Convert a lead by visiting it | Must | S1–S4 |
| 4 | Prospecting US-004 — Create a prospect in the shop | Must | S1–S6 |
| 5 | Prospecting US-005 — Complete a prospect after the visit | Should | S1–S3 |
| 6 | Prospecting US-007 — Order against a prospect and convert it | Must | S1–S4, with the 23 and 26 Sep 2026 amendments |
| 7 | Rep at a Location US-021 — Capture and work leads | Must | S1–S4 (the tablet restatement of Prospecting US-001, US-002) |
| 8 | Rep at a Location US-022 — Create and complete a prospect | Must | S1–S4 (S5 → E22) |

Also delivered here: Head Office US-002 S6, the "Prospect — accepting converts to customer" annotation (deferred from E7).

**Exit criterion** — On the tablet, offline, a rep captures a lead with only a name, assigns it to themselves or another rep, and works My Leads with Scheduled and Dead behind filters, stale leads flagged against the team's or their own Stale Period, and scheduled leads waking on their date through the weekly digest. Visiting a lead, or calling on a shop cold, creates a prospect draft with a Town and a "To complete" list, finished later. Calls and orders at the prospect behave as at a customer, and the first order accepted at the cut-off makes the Location a customer, which the rep sees at their next sync.

**Capability-class stamp** — Frontier + extended reasoning for offline prospect creation and conversion (T-21.3.1, T-21.6.1); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [prospecting-and-leads.md](../../stories/prospecting-and-leads.md), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-021, US-022), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-002 S6), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-09, T-10 — drafts awaiting confirmation, MI-61), [03-rep-planner.md](../../uxdocs/03-rep-planner.md) (rep settings).

---

### T-21.1.1-S — Test scenarios for capturing a lead

**Owner** — Scenario Review
**Gates** — T-21.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for capturing a lead on the tablet (task
T-21.1.1). Read plan_docs/stories/prospecting-and-leads.md US-001 S1–S4 and
glossary (Lead, Lead Source, Actionable From, Lead states),
plan_docs/stories/rep-at-a-location-tablet.md US-021 S1, S2, and
plan_docs/uxdocs/01-tablet-day.md T-09 (T9.1, T9.2). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: an Actionable From in the
past, assigning to a rep who then leaves, a lead captured twice before sync,
deleting an unsent lead. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-21.1.1 — Capture a lead offline in seconds and see it in My Leads

**Parent story**

> As a Field Salesperson or Sales Manager, I want to note a possible opportunity with whatever I know, in seconds and offline, so that tips don't get lost between visits.
>
> Acceptance criteria:
> - Offline, "New chemist, Main Street Arklow" with source "Mentioned by Murphy's Rathdrum" is saved as Active, assigned to me, as unsent work (S1; Rep at a Location US-021 S1)
> - A name alone is accepted; no address, contact or town is required (S2)
> - Actionable From 1 April 2027 makes it Scheduled, not in My Leads, never flagged Stale (S3; US-021 S2)
> - Changing the assignee to Brian puts it in Brian's leads after sync (S4)

**Slice** — From My Leads, reached from Home's header, a rep captures a lead offline with only a name required, optionally a source, note, Actionable From and another assignee; it saves as unsent work, lists as Active (or Scheduled if dated ahead), and after sync reaches the assignee's tablet.
**Spec source** — Prospecting US-001 S1–S4, glossary (Lead, Lead Source, Actionable From, Lead states); Rep at a Location US-021 S1, S2; uxdocs 01 T-09 (T9.1, T9.2 — drafts, MI-61)
**Depends on** — T-4.1.1, T-4.1.2, T-4.5.1
**Pattern to follow** — T-5.1.1 (offline capture saved as unsent work)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — a new record through the reviewed upload; T-09 is a draft (MI-61).

**Provisional commit message**

```
feat(leads): capture a lead offline in seconds

- A tip is lost if noting it takes longer than the car park, so only a
  name is required; a lead is a note, not a Location
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new offline record with a review pause.

**Agent prompt**

```
Role: You are adding lead capture and the My Leads list to the tablet app of
the Field Sales Management System.

Context:
- Slice: from My Leads, reached from Home's header, a rep captures a lead
  offline with only a name required, optionally a source, note, Actionable
  From and another assignee; it saves as unsent work, lists as Active (or
  Scheduled if dated ahead), and after sync reaches the assignee's tablet.
- Specs: plan_docs/stories/prospecting-and-leads.md US-001 S1–S4 and glossary
  (Lead, Lead Source, Actionable From, Lead states);
  plan_docs/stories/rep-at-a-location-tablet.md US-021 S1, S2;
  plan_docs/uxdocs/01-tablet-day.md T-09 (T9.1 entry beside Search on Home's
  header; T9.2 one short form, only Name required).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — snapshot (T-4.1.1),
  upload (T-4.1.2), Home (T-4.5.1), unsent work (T-4.3.1).
- Pattern to follow: T-5.1.1's offline capture.

Acceptance criteria:
1. My Leads opens from Home's header beside Search; "+ Lead" opens the
   capture form.
2. Name is the only required field; Source, Note, Actionable From and
   Assigned to (default me) are optional.
3. Offline, "New chemist, Main Street Arklow" with source "Mentioned by
   Murphy's Rathdrum" saves as Active, assigned to me, as unsent work.
4. Actionable From 1 April 2027 saves it as Scheduled; it is not in the
   Active list.
5. Assigned to Brian, it leaves my list after sync and reaches Brian's
   tablet at his next sync.
6. My Leads lists Active leads newest first.

Constraints:
- Use the project's existing conventions and test framework.
- A lead is not a Location: no address, Town or Customer is required or
  created.
- Leads travel in the snapshot and upload through T-4.1.2; snapshot changes
  go through T-4.1.1's versioning.
- No tests of framework internals or trivial members.
- This task opts in to the lead tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after a lead captured offline reaches another rep's tablet
through two syncs, stop and show it. Resume only on "Continue T-21.1.1".

Steps: 1. lead storage and states; 2. capture form; 3. My Leads Active list;
4. upload and snapshot; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-21.1.1-S; do not design
your own.

Definition of done: from My Leads, reached from Home's header, a rep
captures a lead offline with only a name required, optionally a source,
note, Actionable From and another assignee; it saves as unsent work, lists
as Active (or Scheduled if dated ahead), and after sync reaches the
assignee's tablet.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: filters, stale flags and closing (T-21.2.1), manager capture
(T-21.1.2), visiting a lead (T-21.4.1).
```

**Checkpoint**

Produces before pausing — a lead captured offline reaching another rep's tablet through two syncs.
Human reviews — Does the form stay a thirty-second task, and does the lead land with the right rep?
Resume trigger — `Continue T-21.1.1`

---

### T-21.1.2-S — Test scenarios for a manager capturing a lead

**Owner** — Scenario Review
**Gates** — T-21.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a manager capturing and assigning a lead on
the website (task T-21.1.2). Read plan_docs/stories/prospecting-and-leads.md
US-001 S5 and glossary (Lead). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S5, then
derivable edges: assigning to a rep outside the manager's team, a lead with an
Actionable From, the assignee's tablet offline for days. Mark undecided cases
as "Needs a decision". Write no test code; change no files.
```

---

### T-21.1.2 — Let a manager capture a lead and assign it to a rep

**Parent story**

> As a Field Salesperson or Sales Manager, I want to note a possible opportunity with whatever I know, in seconds and offline, so that tips don't get lost between visits.
>
> Acceptance criteria:
> - A manager creates a lead and assigns it to Aoife; it appears in Aoife's leads with the manager as creator (S5)

**Slice** — On the manager website, a manager captures a lead with the same fields as the tablet and assigns it to a rep, who finds it in My Leads after their next sync with the manager named as creator.
**Spec source** — Prospecting US-001 S5; glossary (Lead)
**Depends on** — T-21.1.1, T-1.1.1
**Pattern to follow** — T-14.4.1 (manager adds work for a rep from the website)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: L
  (inferred) — no screen is designed for manager capture (MI-61), so Taste is High; the record and sync exist.

**Provisional commit message**

```
feat(leads): let a manager capture a lead for a rep

- Managers hear of openings too; the lead goes to whoever should call,
  with the manager kept as its source
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
An undesigned surface with a review pause.

**Agent prompt**

```
Role: You are adding manager lead capture to the Field Sales Management
System's manager website.

Context:
- Slice: on the manager website, a manager captures a lead with the same
  fields as the tablet and assigns it to a rep, who finds it in My Leads
  after their next sync with the manager named as creator.
- Specs: plan_docs/stories/prospecting-and-leads.md US-001 S5 and glossary
  (Lead). No screen is designed; propose the placement in your plan
  (for example beside "Add one-off visit" in M-16's area).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — lead record and sync
  (T-21.1.1), manager website (T-14.4.1).
- Pattern to follow: T-14.4.1.

Acceptance criteria:
1. A manager can create a lead with Name (required), Source, Note, Actionable
   From and a required assignee.
2. Assigned to Aoife, it appears in Aoife's My Leads after her next sync.
3. The lead shows the manager as creator.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-21.1.1's record; no second lead model.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after proposing the placement and building the form, stop and
show it before wiring sync. Resume only on "Continue T-21.1.2".

Steps: 1. placement proposal; 2. form; 3. save and deliver; 4. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-21.1.2-S; do not design
your own.

Definition of done: on the manager website, a manager captures a lead with
the same fields as the tablet and assigns it to a rep, who finds it in My
Leads after their next sync with the manager named as creator.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: a manager list of all leads; lead reporting (not designed).
```

**Checkpoint**

Produces before pausing — the proposed placement and the form, before sync is wired.
Human reviews — Is this where a manager would look to add a lead?
Resume trigger — `Continue T-21.1.2`

---

### T-21.2.1-S — Test scenarios for stale and dead leads

**Owner** — Human-Led
**Gates** — T-21.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- What counts as "activity" on a lead: an edit, a note, a reassignment, a visit attempt? The Stale Period runs from the Actionable From date where one exists, otherwise from creation or last activity (glossary).
- Stale Period 8 weeks, 9 weeks without activity: "No activity for 9 weeks — follow up or archive" (S2). Weeks rounded down?
- Stale is evaluated on the tablet offline against the snapshot's period — which clock and time zone (MI-53)?
- 12 Active, 3 Scheduled, 4 Dead: the list shows 12, Scheduled and Dead behind filters (S1); Active lists Stale first, then newest (T9.3).
- Close as dead needs a reason (T9.4); "Unit never opened" moves it to Dead with the reason (S4). Can a Dead lead be reopened?
- A Scheduled lead is never Stale (S3 of US-001), even after its date until the period has run from that date.
- Where the manager sets the team period (MI-12).

---

### T-21.2.1 — Flag stale leads and close dead ones in My Leads

**Parent story**

> As a Field Salesperson, I want a list of the leads I can act on now, with stale ones surfaced, so that the list stays worth reading.
>
> Acceptance criteria:
> - 12 Active, 3 Scheduled and 4 Dead: My Leads shows the 12, with Scheduled and Dead behind filters (S1)
> - Stale Period 8 weeks and no activity for 9: flagged Stale with "No activity for 9 weeks — follow up or archive" (S2; Rep at a Location US-021 S3)
> - Closing with reason "Unit never opened" makes it Dead with the reason and removes it from the working list (S4; US-021 S4)

**Slice** — My Leads shows Active leads with stale ones first and flagged against the team's Stale Period, Scheduled and Dead leads behind filters with their date or reason, and lets the rep close a lead as dead with a reason; the manager sets the team's period.
**Spec source** — Prospecting US-002 S1, S2, S4, glossary (Stale Period, Lead states), design decision "Stale leads are flagged…"; Rep at a Location US-021 S3, S4; uxdocs 01 T-09 (T9.3, T9.4 — drafts, MI-61)
**Depends on** — T-21.1.1
**Pattern to follow** — T-13.9.1 (Overdue and Due soon on the tablet, computed offline)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — "activity" is undefined (Oracle Ambiguity High); the list itself is straightforward.

**Provisional commit message**

```
feat(leads): flag stale leads and close dead ones

- A lead list nobody prunes stops being read; stale leads surface with a
  prompt to follow up or archive, and dead ones leave with their reason
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A list with a human-defined staleness rule.

**Agent prompt**

```
Role: You are adding stale flags, filters and closing to My Leads on the
tablet in the Field Sales Management System.

Context:
- Slice: My Leads shows Active leads with stale ones first and flagged
  against the team's Stale Period, Scheduled and Dead leads behind filters
  with their date or reason, and lets the rep close a lead as dead with a
  reason; the manager sets the team's period.
- Specs: plan_docs/stories/prospecting-and-leads.md US-002 S1, S2, S4 and
  glossary (Stale Period, Lead states);
  plan_docs/stories/rep-at-a-location-tablet.md US-021 S3, S4;
  plan_docs/uxdocs/01-tablet-day.md T-09 (T9.3, T9.4).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — leads (T-21.1.1),
  offline date logic (T-13.9.1), settings (MI-12).
- Pattern to follow: T-13.9.1.

Acceptance criteria:
1. Filters Active (default), Scheduled and Dead, each with its count.
2. With an 8-week period, a lead with 9 weeks of no activity is flagged "No
   activity for 9 weeks — follow up or archive" and listed before other
   Active leads, which are newest first.
3. Scheduled shows each lead's Actionable From date; Dead shows the reason.
4. Close… asks for a reason; "Unit never opened" moves the lead to Dead, out
   of the Active list, as unsent work.
5. The manager can set the team's Stale Period; it reaches tablets in the
   snapshot.
6. "Activity" and the start of the period follow the rule agreed in
   T-21.2.1-S.

Constraints:
- Use the project's existing conventions and test framework.
- Staleness is computed on the tablet from the snapshot, offline.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  Dead state and reason.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the staleness rule passes every agreed scenario, stop and
show it. Resume only on "Continue T-21.2.1".

Steps: 1. staleness rule; 2. filters; 3. close as dead; 4. team setting;
5. tests.

Test expectations: implement exactly the scenarios agreed in T-21.2.1-S. You
are forbidden from designing your own test cases.

Definition of done: My Leads shows Active leads with stale ones first and
flagged against the team's Stale Period, Scheduled and Dead leads behind
filters with their date or reason, and lets the rep close a lead as dead
with a reason; the manager sets the team's period.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the rep's own period (T-21.2.2), scheduled leads waking
(T-21.2.3).
```

**Checkpoint**

Produces before pausing — the staleness rule passing every agreed scenario.
Human reviews — Is a lead flagged exactly when the agreed rule says, and never while Scheduled?
Resume trigger — `Continue T-21.2.1`

---

### T-21.2.2-S — Test scenarios for a rep's own Stale Period

**Owner** — Scenario Review
**Gates** — T-21.2.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a rep overriding the team's Stale Period
(task T-21.2.2). Read plan_docs/stories/prospecting-and-leads.md US-002 S5
and design decision "Stale leads are flagged…", and
plan_docs/uxdocs/01-tablet-day.md T-09 (T9.5). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S5,
then derivable edges: clearing the override, the team period changing after
an override, a very long period. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-21.2.2 — Let a rep set their own Stale Period

**Parent story**

> As a Field Salesperson, I want a list of the leads I can act on now, with stale ones surfaced, so that the list stays worth reading.
>
> Acceptance criteria:
> - With the team setting at 8 weeks, setting my own to 12 weeks makes my leads use 12 and leaves other reps unaffected (S5)

**Slice** — On the rep website, a rep sets their own Stale Period over the team's, and after syncing their leads are flagged against it while other reps' are not.
**Spec source** — Prospecting US-002 S5; uxdocs 01 T-09 (T9.5 the override sits on the rep website — draft, MI-61)
**Depends on** — T-21.2.1, T-13.2.1
**Pattern to follow** — T-12.3.1 (override over a default, with its source shown)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — a setting over a reviewed rule.

**Provisional commit message**

```
feat(leads): let a rep set their own stale period

- A tight urban patch and a wide rural one warrant different periods;
  the team default stays for everyone who doesn't choose
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A small setting with a review pause.

**Agent prompt**

```
Role: You are adding a rep's own Stale Period to the rep website of the Field
Sales Management System.

Context:
- Slice: on the rep website, a rep sets their own Stale Period over the
  team's, and after syncing their leads are flagged against it while other
  reps' are not.
- Specs: plan_docs/stories/prospecting-and-leads.md US-002 S5;
  plan_docs/uxdocs/01-tablet-day.md T-09 (T9.5).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — team period and
  staleness (T-21.2.1), rep website (T-13.2.1).
- Pattern to follow: T-12.3.1's override shown with its source.

Acceptance criteria:
1. The rep website shows the Stale Period as "8 weeks (team setting)" and
   lets the rep set their own.
2. Set to 12 weeks, the rep's leads use 12 after the next sync; other reps
   still use 8.
3. Clearing the override returns to the team setting.

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  override field.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the override reaches a tablet and changes its flags, stop
and show it. Resume only on "Continue T-21.2.2".

Steps: 1. override field; 2. rep website setting; 3. snapshot; 4. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-21.2.2-S; do not design
your own.

Definition of done: on the rep website, a rep sets their own Stale Period
over the team's, and after syncing their leads are flagged against it while
other reps' are not.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: other rep settings.
```

**Checkpoint**

Produces before pausing — the override reaching a tablet and changing its flags.
Human reviews — Is the source of the period always clear to the rep?
Resume trigger — `Continue T-21.2.2`

---

### T-21.2.3-S — Test scenarios for scheduled leads becoming active

**Owner** — Scenario Review
**Gates** — T-21.2.3
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for Scheduled leads becoming Active on their
date (task T-21.2.3). Read plan_docs/stories/prospecting-and-leads.md US-002
S3, US-001 S3, glossary (Actionable From, Lead states), and the weekly digest
in plan_docs/stories/visit-planning.md (as built in T-13.5.1). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Start with characterisation scenarios pinning the digest's current
content. Cover the story scenarios, then derivable edges: the date passing
while offline, several leads waking in one week, a lead waking mid-week
(before the next digest). Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-21.2.3 — Wake scheduled leads on their date and say so in the weekly digest

**Parent story**

> As a Field Salesperson, I want a list of the leads I can act on now, with stale ones surfaced, so that the list stays worth reading.
>
> Acceptance criteria:
> - A lead with Actionable From 1 April 2027 becomes Active when that date passes and appears in the weekly digest as "1 lead is now actionable" (S3)

**Slice** — A Scheduled lead becomes Active on its Actionable From date, offline, joining My Leads, and the rep's next weekly digest says how many leads have become actionable.
**Spec source** — Prospecting US-002 S3; US-001 S3; glossary (Actionable From); design decision "Stale leads are flagged…" (scheduled leads rely on the weekly digest to resurface)
**Depends on** — T-21.2.1, T-13.5.1
**Pattern to follow** — T-1.3.2 (a dated change applied on the tablet on its day)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies the digest (characterisation first); day boundary (MI-53).

**Provisional commit message**

```
feat(leads): wake scheduled leads on their date

- A shop opening in six months isn't neglected, just not due; the digest
  brings it back when it is
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A dated state change and a digest line.

**Agent prompt**

```
Role: You are making Scheduled leads become Active on their date in the Field
Sales Management System.

Context:
- Slice: a Scheduled lead becomes Active on its Actionable From date,
  offline, joining My Leads, and the rep's next weekly digest says how many
  leads have become actionable.
- Specs: plan_docs/stories/prospecting-and-leads.md US-002 S3, US-001 S3,
  glossary (Actionable From, Lead states).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — leads (T-21.1.1,
  T-21.2.1), weekly digest (T-13.5.1), dated values on the tablet (T-1.3.2).
- Pattern to follow: T-1.3.2.

Acceptance criteria:
1. On 1 April 2027 a lead with that Actionable From is Active on the tablet,
   offline, and appears in My Leads.
2. Its Stale Period starts from that date.
3. The next weekly digest reads "1 lead is now actionable" (plural for
   more), linking to My Leads.
4. Nothing else in the digest changes.

Constraints:
- Use the project's existing conventions and test framework.
- Dates use the business time zone (MI-53).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the digest's current content; stop and show them passing. Resume only on
"Continue T-21.2.3".

Steps: 1. characterisation tests; 2. dated activation; 3. digest line;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-21.2.3-S; do not design
your own.

Definition of done: a Scheduled lead becomes Active on its Actionable From
date, offline, joining My Leads, and the rep's next weekly digest says how
many leads have become actionable.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: notifications outside the digest.
```

**Checkpoint**

Produces before pausing — characterisation tests of the digest, passing.
Human reviews — Do the tests pin today's digest exactly?
Resume trigger — `Continue T-21.2.3`

---

### T-21.3.1-S — Test scenarios for creating a prospect offline

**Owner** — Human-Led
**Gates** — T-21.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Offline, name, Town, "Likely to buy" High and a note saves a draft as unsent work (S1), listing "To complete: address, contact details, location type" (S2).
- Without a Town: "Choose a town" (S5). Is anything else required (a name)?
- "What they sell" linked to Category Suncare is stored in the Competitor Note shape (S3).
- GPS stored with Precision "Confirmed on site" (S6).
- The prospect is a Location created offline: how is it identified before sync, and how do a call and an order captured against it upload in the right order (prospect first) exactly once?
- Which Customer does a prospect Location belong to, if any, before conversion (MI-29)?
- Who covers it? The creating rep implicitly (assumption); does it appear in other reps' searches, or the Unassigned list?
- Deleting an unsent prospect with a call attached.
- A sync that fails half-way: the prospect uploaded, the call not.

---

### T-21.3.1 — Create a prospect in the shop as a draft, offline

**Parent story**

> As a Field Salesperson, I want to record a place I've just called on with what I learned, saving whatever I have, so that I can finish it later without losing the visit.
>
> Acceptance criteria:
> - Offline, name, Town, susceptibility High and note "Owner keen on the suncare range, currently stocks mostly own-brand" saves the prospect as a draft, as unsent work (S1; Rep at a Location US-022 S1)
> - It shows "To complete: address, contact details, location type" (S2)
> - "Stocks Boots own-brand suncare" linked to Category Suncare is saved in the same shape as a Competitor Note (S3)
> - Saving without a Town is rejected with "Choose a town" (S5)
> - A position from the device is stored with Precision "Confirmed on site" (S6)

**Slice** — On the tablet, offline, a rep records a shop that isn't a customer with a name, a Town, how likely it is to buy, notes, what it sells and optionally its GPS position; it saves as a draft Location, lists what's still missing, and uploads at the next sync ahead of any work recorded against it.
**Spec source** — Prospecting US-004 S1–S3, S5, S6; glossary (Prospect, Susceptibility, What they sell, Prospect draft), assumptions (no Location Profile, rep may call without assignment); Rep at a Location US-022 S1; uxdocs 01 T-10 (T10.1, T10.2 — drafts, MI-61)
**Depends on** — T-4.1.2, T-2.3.1, T-2.1.1, T-5.8.1, T-17.3.1
**Pattern to follow** — T-2.3.1 (creating a Location), T-4.1.2 (upload exactly once)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — the first Location created offline, with dependent calls and orders, changes the sync contract (Blast Radius High); ownership and Customer are open (MI-29).

**Provisional commit message**

```
feat(prospects): create a prospect in the shop as a draft, offline

- A form demanding everything fails at a counter; the draft keeps what
  the rep has and lists what's missing
- A prospect is a Location from the start, so calls and orders against
  it need nothing special
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Offline-created records with dependants change the sync contract.

**Work package**

Increments:
1. Decide ownership and Customer for a prospect Location (MI-29) and record it.
2. Offline identity: a Location created on the tablet with a local identifier, mapped to a server identity at upload; dependants (calls, orders, notes) reference it and upload after it, each exactly once (extends T-4.1.2).
3. Prospect fields: name, Town (required), Susceptibility with notes (history kept), What they sell in the Competitor Note shape (T-5.8.1), GPS "Confirmed on site" (T-17.3.1).
4. Draft and "To complete" list (address, contact details, location type) updating as fields are filled.
5. T-10 screen: in-shop fields first, "Likely to buy" label, Save draft and Save and record call.
6. The rep's own prospects in their snapshot and search.

Decision points:
- MI-29: Customer and Primary Rep for a prospect before conversion.
- Whether other reps can find a prospect (duplicate risk versus privacy).
- Failure half-way through upload: retry semantics for the dependants.

Delegable slivers:
- **To complete list** — Given a prospect record, compute and show "To complete: …" naming missing address, contact details and location type, updating as fields change. No saving.
- **What they sell** — Reuse T-5.8.1's Competitor Note entry for "What they sell" on the prospect, with its optional Product, Range or Category link. No new shape.

---

### T-21.3.2-S — Test scenarios for calls and orders at a prospect

**Owner** — Human-Led
**Gates** — T-21.3.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-28: a prospect prices against Base Price plus general promotions (assumed). Which promotions are "general" — audience All customers only?
- Rep discounts and free goods at a prospect: allowed under the rep's policy?
- A call at a prospect behaves exactly as at a customer, with channel, purposes and competitor notes (US-004 S4); stock checks with no order history (no suggested list)?
- An order at a prospect: Order Pad, prices, In Progress, Ready to Send (US-007 S1); it waits for the cut-off like any other (BR-NEW-009).
- "Save and record call" from the new prospect opens the call directly (T10.3).
- An order at a prospect that hasn't synced yet: both upload in order.

---

### T-21.3.2 — Record calls and take orders at a prospect as at a customer

**Parent story**

> As a Field Salesperson, I want to take a first order at a prospect so that an accepted order turns it into a customer.
>
> Acceptance criteria:
> - An order at a prospect behaves as any order — Order Pad, prices, In Progress, Ready to Send (S1)
> - A Call at the new prospect behaves exactly as at a Customer Location, with channel, purposes and competitor notes (Prospecting US-004 S4; Rep at a Location US-022 S2)

**Slice** — At a prospect, offline, the rep records calls and builds orders exactly as at a customer, priced at base price and general promotions, and both upload and wait for the cut-off like any other.
**Spec source** — Prospecting US-007 S1, US-004 S4, Requires Clarification 4 (prospect pricing assumed Base Price plus general promotions); Rep at a Location US-022 S2; uxdocs 01 T-10 (T10.3 — draft, MI-61)
**Depends on** — T-21.3.1, T-5.3.1, T-5.1.1, T-6.4.1, T-18.2.1
**Pattern to follow** — T-5.3.1 and T-5.1.1 (call and order at a customer)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — reuses the reviewed call and order; prospect pricing is an assumption (MI-28), so the oracle needs a person.

**Provisional commit message**

```
feat(prospects): record calls and take orders at a prospect

- Calls and orders at a prospect are ordinary, so the first order is
  taken the same way as the hundredth
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Reuse of reviewed flows under a pricing assumption.

**Agent prompt**

```
Role: You are enabling calls and orders at prospects on the tablet in the
Field Sales Management System.

Context:
- Slice: at a prospect, offline, the rep records calls and builds orders
  exactly as at a customer, priced at base price and general promotions, and
  both upload and wait for the cut-off like any other.
- Specs: plan_docs/stories/prospecting-and-leads.md US-007 S1, US-004 S4,
  Requires Clarification 4; plan_docs/stories/rep-at-a-location-tablet.md
  US-022 S2; plan_docs/uxdocs/01-tablet-day.md T-10 (T10.3).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — prospects (T-21.3.1),
  call (T-5.3.1), order (T-5.1.1), price engine (T-6.4.1), promotion audience
  (T-18.2.1).
- Pattern to follow: T-5.3.1 and T-5.1.1 unchanged.

Acceptance criteria:
1. A Call at a prospect has channel, purposes and competitor notes, as at a
   customer.
2. An Order at a prospect uses the Order Pad, prices, In Progress and Ready
   to Send, as at a customer.
3. Prices resolve per the agreed prospect pricing (MI-28): base price plus
   promotions for all customers, unless decided otherwise.
4. "Save and record call" on a new prospect opens the call.
5. The call and order upload after the prospect, each exactly once; the order
   is Pending until the cut-off.

Constraints:
- Use the project's existing conventions and test framework.
- Add no prospect-specific branches to the call or order flows beyond
  pricing inputs.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after an order at a new prospect uploads and is Pending, priced as
agreed, stop and show it. Resume only on "Continue T-21.3.2".

Steps: 1. pricing inputs for prospects; 2. call; 3. order; 4. upload order;
5. tests.

Test expectations: implement exactly the scenarios agreed in T-21.3.2-S. You
are forbidden from designing your own test cases.

Definition of done: at a prospect, offline, the rep records calls and builds
orders exactly as at a customer, priced at base price and general promotions,
and both upload and wait for the cut-off like any other.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: conversion at the cut-off (T-21.6.1).
```

**Checkpoint**

Produces before pausing — an order at a new prospect uploaded and Pending, priced as agreed.
Human reviews — Is every price on a prospect's order the one the business agreed to quote?
Resume trigger — `Continue T-21.3.2`

---

### T-21.4.1-S — Test scenarios for converting a lead by visiting it

**Owner** — Scenario Review
**Gates** — T-21.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for converting a lead by visiting it (task
T-21.4.1). Read plan_docs/stories/prospecting-and-leads.md US-003 S1–S4 and
design decision "A Lead is not a Location; a Prospect is",
plan_docs/stories/rep-at-a-location-tablet.md US-022 S3, and
plan_docs/uxdocs/01-tablet-day.md T-09 (T9.4) and T-10. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: Brian closing the lead as
dead offline while I convert it, cancelling the prospect form after Visit, a
Dead lead visited. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-21.4.1 — Convert a lead into a prospect by visiting it

**Parent story**

> As a Field Salesperson, I want visiting a lead to create the prospect and close the lead against it so that the two are linked without anyone matching records.
>
> Acceptance criteria:
> - Visit on "New chemist, Main Street Arklow" creates a prospect pre-filled from the lead, and the lead becomes Converted, linked to it (S1; Rep at a Location US-022 S3)
> - The prospect shows "From lead: mentioned by Murphy's Rathdrum" and the lead shows the prospect it became (S2)
> - A lead assigned to Brian and converted by me: the prospect is mine, the lead Converted, and Brian sees who converted it (S3)
> - A Scheduled lead visited early converts normally (S4)

**Slice** — Visit on a lead opens the new prospect pre-filled from it; saving the prospect converts the lead and links the two both ways, whoever the lead was assigned to, and the assignee sees who converted it after syncing.
**Spec source** — Prospecting US-003 S1–S4; design decision "A Lead is not a Location; a Prospect is"; Rep at a Location US-022 S3; uxdocs 01 T-09 (T9.4), T-10 (drafts, MI-61)
**Depends on** — T-21.3.1, T-21.2.1
**Pattern to follow** — T-5.7.1 (a record linked to its origin)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — links two reviewed records; the concurrency case is scenario-listed.

**Provisional commit message**

```
feat(leads): convert a lead into a prospect by visiting it

- Visiting is the only way a lead converts, so the link is made by the
  act itself and nobody matches records afterwards
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A linking flow with a review pause.

**Agent prompt**

```
Role: You are adding lead conversion by visit to the tablet app of the Field
Sales Management System.

Context:
- Slice: Visit on a lead opens the new prospect pre-filled from it; saving
  the prospect converts the lead and links the two both ways, whoever the
  lead was assigned to, and the assignee sees who converted it after
  syncing.
- Specs: plan_docs/stories/prospecting-and-leads.md US-003 S1–S4;
  plan_docs/stories/rep-at-a-location-tablet.md US-022 S3;
  plan_docs/uxdocs/01-tablet-day.md T-09 (T9.4), T-10.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — leads (T-21.1.1,
  T-21.2.1), prospects (T-21.3.1).
- Pattern to follow: T-5.7.1's link to an origin record.

Acceptance criteria:
1. Visit on a lead opens T-10 pre-filled with the lead's name and notes.
2. Saving the prospect makes the lead Converted and links them.
3. The prospect shows "From lead: mentioned by Murphy's Rathdrum"; the lead
   shows the prospect it became.
4. A lead assigned to Brian and converted by me: the prospect is mine; after
   syncing, Brian's lead shows Converted and who converted it.
5. A Scheduled lead converts the same way.
6. Cancelling the prospect form leaves the lead unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- Conversion happens only through Visit.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the link.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after conversion by another rep reaches the assignee's tablet,
stop and show it. Resume only on "Continue T-21.4.1".

Steps: 1. Visit action; 2. pre-fill; 3. convert on save; 4. both-way link;
5. sync to the assignee; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-21.4.1-S; do not design
your own.

Definition of done: Visit on a lead opens the new prospect pre-filled from
it; saving the prospect converts the lead and links the two both ways,
whoever the lead was assigned to, and the assignee sees who converted it
after syncing.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: conversion reporting (not designed, Prospecting Requires
Clarification 3).
```

**Checkpoint**

Produces before pausing — a conversion by another rep reaching the assignee's tablet.
Human reviews — Is the lead–prospect link visible from both ends, for both reps?
Resume trigger — `Continue T-21.4.1`

---

### T-21.5.1-S — Test scenarios for completing a prospect later

**Owner** — Scenario Review
**Gates** — T-21.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for completing a prospect after the visit (task
T-21.5.1). Read plan_docs/stories/prospecting-and-leads.md US-005 S1–S3,
plan_docs/stories/rep-at-a-location-tablet.md US-022 S4, and
plan_docs/uxdocs/01-tablet-day.md T-10 (T10.1, T10.4). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: completing a prospect
already synced (an edit uploads), completing it after head office changed
it, a prospect discarded by duplicate review while being completed. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-21.5.1 — Finish a prospect later from a To finish list

**Parent story**

> As a Field Salesperson, I want to fill in the rest of a prospect later so that the record is usable without me typing it all at the counter.
>
> Acceptance criteria:
> - A draft missing address and contact, completed that evening, clears its missing-items list (S1; Rep at a Location US-022 S4)
> - A draft still incomplete after sync appears in my to-finish list with its missing items (S2)
> - Changing the rating from High to Medium with a note keeps the change and the note, with the earlier note retained (S3)

**Slice** — Incomplete prospects wait in a To finish filter on My Leads with their missing items; the rep fills them in later, offline, before or after sync, and each change of rating keeps its note alongside the earlier ones.
**Spec source** — Prospecting US-005 S1–S3; Rep at a Location US-022 S4; uxdocs 01 T-10 (T10.4 — draft, MI-61)
**Depends on** — T-21.3.1
**Pattern to follow** — T-17.3.1 (a tablet edit to a synced Location)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — edits to a synced record from the tablet; rules are stated.

**Provisional commit message**

```
feat(prospects): finish a prospect later from a to-finish list

- The missing-items list is the only thing driving completion, so
  unfinished prospects stay in view until they're done
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Offline edits with a review pause.

**Agent prompt**

```
Role: You are adding prospect completion to the tablet app of the Field Sales
Management System.

Context:
- Slice: incomplete prospects wait in a To finish filter on My Leads with
  their missing items; the rep fills them in later, offline, before or after
  sync, and each change of rating keeps its note alongside the earlier ones.
- Specs: plan_docs/stories/prospecting-and-leads.md US-005 S1–S3;
  plan_docs/stories/rep-at-a-location-tablet.md US-022 S4;
  plan_docs/uxdocs/01-tablet-day.md T-10 (T10.1, T10.4).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — prospects and the To
  complete list (T-21.3.1), My Leads (T-21.1.1), upload (T-4.1.2).
- Pattern to follow: T-17.3.1's tablet edit to a synced Location.

Acceptance criteria:
1. My Leads has a To finish filter listing incomplete prospects with their
   missing items.
2. Adding address and contact that evening clears them from the list; the
   prospect is complete and leaves To finish.
3. A draft still incomplete after sync stays in To finish.
4. Changing Likely to buy from High to Medium with a note keeps the change
   and note, with the earlier note retained and shown.
5. Edits to a synced prospect upload at the next sync, exactly once.

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  rating history.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after completing a synced prospect uploads correctly, stop and
show it. Resume only on "Continue T-21.5.1".

Steps: 1. To finish filter; 2. edit form; 3. rating history; 4. upload of
edits; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-21.5.1-S; do not design
your own.

Definition of done: incomplete prospects wait in a To finish filter on My
Leads with their missing items; the rep fills them in later, offline, before
or after sync, and each change of rating keeps its note alongside the
earlier ones.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: head office gap lists for prospects.
```

**Checkpoint**

Produces before pausing — completion of a synced prospect uploaded correctly.
Human reviews — Is the earlier rating note always kept?
Resume trigger — `Continue T-21.5.1`

---

### T-21.6.1-S — Test scenarios for converting a prospect on its first accepted order

**Owner** — Human-Led
**Gates** — T-21.6.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- The first order is annotated "Prospect — accepting converts to customer" (HO-002 S6; PL-007 S1).
- Accepted at the cut-off: the Location becomes a Customer; the rep sees "Now a customer" at their next sync (S2). Does conversion create a Customer record, attach the Location to one, or change a type (MI-29)?
- Rejected by hand before the cut-off with a reason: stays a Prospect; the rep sees the reason (S3).
- Held, then released: converts on release.
- Two orders at the prospect in the same cut-off: which is "first"? Both annotated?
- After conversion: no Location Profile, so no visits; appears on No Visit Schedule (S4). Who becomes Primary Rep — the creating rep by direct assignment with Assignment History (MI-29)?
- A prospect under duplicate review when its order is accepted (E22): does conversion wait?

---

### T-21.6.1 — Make a prospect a customer when its first order is accepted

**Parent story**

> As a Field Salesperson, I want to take a first order at a prospect so that an accepted order turns it into a customer.
>
> Acceptance criteria:
> - The first order at a prospect is flagged at head office "Prospect — accepting converts to customer" (S1; Head Office US-002 S6)
> - When it is accepted, the Location becomes a Customer and I see "Now a customer" at my next sync (S2)
> - Rejected with a reason, the Location stays a Prospect and I see the reason (S3)
> - After conversion it needs a Location Profile before visits are scheduled, and appears on head office's No Visit Schedule gap list (S4)
> - Accepted automatically at the next cut-off; before then head office may hold or reject it by hand (amendments 23 and 26 Sep 2026)

**Slice** — A prospect's first order carries the prospect annotation, and when the cut-off accepts it the Location becomes a customer, covered by the rep and listed as needing a Location Profile; a manual rejection leaves it a prospect; the rep learns either outcome at the next sync.
**Spec source** — Prospecting US-007 S1–S4 and amendments; glossary (Conversion Prospect → Customer); Requires Clarification 1; Head Office US-002 S6; uxdocs 02 H-01 disposition table (Prospect Conversion is Annotate)
**Depends on** — T-21.3.2, T-7.1.1, T-7.2.1, T-7.4.1, T-7.5.1, T-17.4.1, T-3.1.1
**Pattern to follow** — T-7.1.1 (acceptance at the cut-off), T-7.4.1 (annotations)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — hooks into the acceptance state machine and changes a Location's kind and ownership (Blast Radius High); the conversion model is open (MI-29).

**Provisional commit message**

```
feat(prospects): make a prospect a customer on its first accepted order

- Conversion is an annotation, not a decision: the order goes through at
  the cut-off like any other, and acceptance is what converts
- A new customer has no servicing profile yet, so it lands on the gap
  list rather than silently generating no visits
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A change inside the acceptance state machine.

**Work package**

Increments:
1. Resolve MI-29: what "becomes a Customer" means in the data, and who becomes Primary Rep at conversion (a direct assignment with Assignment History is the working assumption).
2. Prospect Conversion annotator on the first order, using T-7.4.1's interface.
3. Conversion inside T-7.1.1's acceptance transaction (also on Release of a held order); rejection leaves the prospect unchanged.
4. After conversion: no profile, so the Location appears on the No Visit Schedule gap list (T-17.4.1).
5. Tablet: "Now a customer" on the Location and as a sync outcome; a rejection's reason on the sent order (T-7.5.1).

Decision points:
- MI-29 (model and Primary Rep).
- Two orders in one cut-off.
- Interaction with an open duplicate match (E22).

Delegable slivers:
- **Prospect annotator** — Implement an annotator that adds "Prospect — accepting converts to customer" to a prospect's order using T-7.4.1's interface. Do not change the interface.
- **Now a customer** — Once conversion is in the snapshot, show "Now a customer" on the Location and as a Home sync outcome, following T-4.5.1's prompts. No conversion logic.

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | PL-001 (T-21.1.1, T-21.1.2), PL-002 (T-21.2.1, T-21.2.2, T-21.2.3), PL-003 (T-21.4.1), PL-004 (T-21.3.1, T-21.3.2), PL-005 (T-21.5.1), PL-007 (T-21.3.2, T-21.6.1), A1-021 (T-21.1.1, T-21.2.1), A1-022 S1–S4 (T-21.3.1, T-21.3.2, T-21.4.1, T-21.5.1); HO-002 S6 (T-21.6.1) |
| Every task satisfies the three slice criteria | Pass | 10 of 10 |
| Every task carries a tier with a rationale citing dimensions | Pass | 10 of 10 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-28 (T-21.3.2), MI-29 (T-21.3.1, T-21.6.1), MI-61 (drafted and undesigned screens), MI-12 (team period), MI-53 |
| Tasks modifying existing behaviour order characterisation first | Pass | T-21.2.3 (digest); T-21.6.1 extends acceptance inside the tight loop |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-21.2.1, T-21.3.1, T-21.3.2, T-21.6.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-21.1.1, T-21.1.2, T-21.2.1, T-21.2.2, T-21.2.3, T-21.3.2, T-21.4.1, T-21.5.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-21.3.1, T-21.6.1 |
| Every scenario task precedes the task it gates | Pass | 10 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-12, 28, 29, 53, 61 |
