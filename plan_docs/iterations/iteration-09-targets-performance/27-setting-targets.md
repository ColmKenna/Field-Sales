# E27 — Setting targets

**Iteration** — 9, Targets and performance
**Outcome** — A manager sets each rep's overall and per-Range figures for a period in one sitting, starting from last period's figures, and puts an occasional target on a key Location or a whole chain.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Targets & Performance US-001 — Set rep targets for a period | Must | S1–S5, TP001-A, TP001-B |
| 2 | Targets & Performance US-002 — Set rep targets for a Range | Must | S1–S4, TP002-A |
| 3 | Targets & Performance US-003 — Set a Location or chain target | Should | S1–S4 (setting here; the chain roll-up is measured in T-28.1.1) |

**Exit criterion** — On M-12, a manager picks a period and sees every rep in their team with last period's figure pre-filled (blank where there was none). They enter value, units or both, with a running total shown as feedback only, and save with a confirmation naming the reps whose figures were carried unchanged. The same screen sets figures per Range or Product for a period, never offering archived Ranges. A Location target can be set, and on a master it is marked as measuring the whole chain. Mid-period edits are dated in history.

**Capability-class stamp** — Frontier workhorse for all tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [targets-and-performance.md](../../stories/targets-and-performance.md) (US-001–US-003, glossary, design decisions, Requires Clarification 4), [05-manager.md](../../uxdocs/05-manager.md) (M-12 M12.1, M12.2).

---

### T-27.1.1-S — Test scenarios for setting rep targets for a period

**Owner** — Scenario Review
**Gates** — T-27.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for setting each rep's target for a period
(task T-27.1.1). Read plan_docs/stories/targets-and-performance.md US-001
S1–S5, TP001-A, TP001-B, the glossary (Target, No team target, Blank ≠ zero)
and assumptions (periods), and plan_docs/uxdocs/05-manager.md M-12 (M12.1,
M12.2). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Cover the story scenarios, then derivable edges:
overlapping periods for one rep (a month inside a quarter), a rep who joined
mid-period, a rep who left, carrying from a period of a different length.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-27.1.1 — Set each rep's target for a period on one screen

**Parent story**

> As a Sales Manager, I want to enter each rep's target for a period on one screen, with a running total, so that I can set figures that fit each patch in one sitting.
>
> Acceptance criteria:
> - Q4 2026 with values from €28,000 to €52,000 for 8 reps saves each as a Rep Target; the running total shows €312,000 (S1)
> - €40,000 and 4,000 units for Colm saves and tracks both (S2)
> - Brian left blank has no Rep Target; his view says "No target set for this period" (S3)
> - A row with neither value nor units: "Enter a value, units, or both" (S4)
> - Changing Colm's €40,000 to €45,000 mid-period recalculates progress and dates the change in history (S5)
> - Q1 2027 pre-fills Colm's €40,000 and leaves Brian blank; the total starts from the carried figures (TP001-A)
> - Saving with 6 of 8 unchanged confirms "6 of 8 targets were carried forward from Q4 unchanged. You can update them later." and names them (TP001-B)

**Slice** — On M-12, a manager picks a period, sees every rep in their team with last period's figure carried in, enters value, units or both per rep with a running total as feedback, and saves them all as dated Rep Targets, told which were carried unchanged.
**Spec source** — Targets & Performance US-001 S1–S5, TP001-A, TP001-B; glossary (Target, No team target, Blank ≠ zero); assumptions (calendar periods, MI-37); uxdocs 05 M-12 (M12.1, M12.2)
**Depends on** — T-14.1.1, T-1.1.1
**Pattern to follow** — T-14.1.1 (manager pages listing a team's reps)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the target model is new but its rules are stated; period definitions are assumed (MI-37).

**Provisional commit message**

```
feat(targets): set each rep's target for a period on one screen

- Reps' figures differ by design, so each is entered directly; the team
  total is feedback while setting and is never stored
- Last period's figures are carried in, because targets usually change a
  little and retyping costs more than the anchoring risk
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new model and screen with a review pause.

**Agent prompt**

```
Role: You are building target setting by period (M-12) for the Field Sales
Management System's manager website.

Context:
- Slice: on M-12, a manager picks a period, sees every rep in their team with
  last period's figure carried in, enters value, units or both per rep with a
  running total as feedback, and saves them all as dated Rep Targets, told
  which were carried unchanged.
- Specs: plan_docs/stories/targets-and-performance.md US-001 S1–S5, TP001-A,
  TP001-B, glossary (Target, No team target, Blank ≠ zero) and assumptions;
  plan_docs/uxdocs/05-manager.md M-12 (M12.1, M12.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — manager's team
  (T-14.1.1), staff website (T-1.1.1).
- Pattern to follow: T-14.1.1.

Acceptance criteria:
1. Target model: subject (rep; rep and Range or Product; Location), period
   (calendar month, quarter or year — MI-37), value and/or units, with
   dated history of who set what.
2. Choosing Q4 2026 lists the manager's reps; entering figures shows a
   running total (e.g. €312,000) that is never stored.
3. Value, units or both; a row with neither: "Enter a value, units, or
   both"; blank means no target.
4. Choosing Q1 2027 pre-fills each rep's Q4 figure, blank where none; the
   total starts from the carried figures.
5. Saving saves every figure on screen as a target, dated as set by this
   manager today, and confirms "6 of 8 targets were carried forward from Q4
   unchanged. You can update them later." naming those reps.
6. Editing a figure mid-period dates the change in history.

Constraints:
- Use the project's existing conventions and test framework.
- No team target is stored or shown to reps.
- No tests of framework internals or trivial members.
- This task opts in to the target tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the model and M-12 by period work end to end, stop and show
them. Resume only on "Continue T-27.1.1".

Steps: 1. target model and history; 2. period picker; 3. rows and total;
4. carry forward; 5. save and confirmation; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-27.1.1-S; do not design
your own.

Definition of done: on M-12, a manager picks a period, sees every rep in their
team with last period's figure carried in, enters value, units or both per
rep with a running total as feedback, and saves them all as dated Rep
Targets, told which were carried unchanged.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Range targets (T-27.2.1), Location targets (T-27.3.1),
progress (E28).
```

**Checkpoint**

Produces before pausing — the target model and M-12 by period, end to end.
Human reviews — Is blank always distinct from zero, and is the total never saved?
Resume trigger — `Continue T-27.1.1`

---

### T-27.2.1-S — Test scenarios for Range and Product targets

**Owner** — Scenario Review
**Gates** — T-27.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for setting rep targets per Range or Product
(task T-27.2.1). Read plan_docs/stories/targets-and-performance.md US-002
S1–S4, TP002-A, and plan_docs/uxdocs/05-manager.md M-12. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: a Range archived mid-period,
a Product retired mid-period, carrying a Range target when the Range was
copied for a new season. Mark undecided cases as "Needs a decision". Write
no test code; change no files.
```

---

### T-27.2.1 — Set rep targets for a Range or a Product

**Parent story**

> As a Sales Manager, I want a screen per Range and period listing every rep so that I can push a line with a figure that suits each rep's area.
>
> Acceptance criteria:
> - "Summer 2027", Q2 2027: €12,000 for Aoife, €9,000 for Colm, blank for the rest creates two Rep–Range Targets; the running total shows €21,000 (S1)
> - Choosing a single Product uses the same screen and measures that Product only (S2)
> - An archived Range isn't offered for a new target; existing targets against it run out their period (S3)
> - The total is shown while setting and is never stored, shown to reps, or reported as a team target (S4)
> - Aoife's Q2 figure for "Summer 2026" pre-fills her Q3 row (TP002-A)

**Slice** — On M-12's by-range view, a manager picks a Range or Product and a period, sees every rep with last period's figure for that subject carried in, and saves each figure as a Rep–Range Target, with archived Ranges not offered.
**Spec source** — Targets & Performance US-002 S1–S4, TP002-A; uxdocs 05 M-12 (M12.1, M12.2)
**Depends on** — T-27.1.1, T-9.1.1, T-9.6.1
**Pattern to follow** — T-27.1.1
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the same screen with a subject picker; rules stated.

**Provisional commit message**

```
feat(targets): set rep targets for a range or product

- Pushing a line across a team is one sitting, with a figure that suits
  each rep's area rather than an even split
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A variant of a reviewed screen.

**Agent prompt**

```
Role: You are adding Range and Product targets to M-12 in the Field Sales
Management System's manager website.

Context:
- Slice: on M-12's by-range view, a manager picks a Range or Product and a
  period, sees every rep with last period's figure for that subject carried
  in, and saves each figure as a Rep–Range Target, with archived Ranges not
  offered.
- Specs: plan_docs/stories/targets-and-performance.md US-002 S1–S4, TP002-A;
  plan_docs/uxdocs/05-manager.md M-12.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — targets and M-12
  (T-27.1.1), Ranges (T-9.1.1), archived Ranges (T-9.6.1).
- Pattern to follow: T-27.1.1.

Acceptance criteria:
1. A by-range view picks a Range or a single Product, and a period.
2. Every rep is listed; €12,000 for Aoife and €9,000 for Colm with the rest
   blank creates two Rep–Range Targets; the total reads €21,000 and is never
   stored.
3. Previous-period figures for the same subject are carried in.
4. Archived Ranges aren't offered; existing targets against them still show
   for their period.
5. The save confirmation follows M12.2.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-27.1.1's screen and model.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the by-range view saves targets end to end, stop and show
it. Resume only on "Continue T-27.2.1".

Steps: 1. subject picker; 2. rows and carry forward; 3. save; 4. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-27.2.1-S; do not design
your own.

Definition of done: on M-12's by-range view, a manager picks a Range or
Product and a period, sees every rep with last period's figure for that
subject carried in, and saves each figure as a Rep–Range Target, with
archived Ranges not offered.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: progress (E28).
```

**Checkpoint**

Produces before pausing — the by-range view saving targets end to end.
Human reviews — Is the subject always unambiguous (Range versus Product)?
Resume trigger — `Continue T-27.2.1`

---

### T-27.3.1-S — Test scenarios for Location and chain targets

**Owner** — Scenario Review
**Gates** — T-27.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for setting a Location or chain target (task
T-27.3.1). Read plan_docs/stories/targets-and-performance.md US-003 S1–S4 and
glossary (Location Target, Chain Roll-up). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover setting
on a master (marked as measuring the chain) and on an ordinary Location, then
derivable edges: a Location that becomes a master mid-period, a target on a
branch and on its master at once. Measurement of the roll-up is T-28.1.1's;
leave it out. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-27.3.1 — Set a target on a Location, covering the chain when it's a master

**Parent story**

> As a Sales Manager, I want to set a target on a Location, and have it cover the whole chain when it's a master, so that a key account has an expectation of its own.
>
> Acceptance criteria:
> - €50,000 for Q4 2026 on Hickey's Head Office (12 branches) measures the head office and all branches, broken down per branch (S1)
> - A target on an ordinary Location measures that Location only (S2)
> - A 13th branch added in November counts from that point (S3)
> - A branch Closed in November keeps its earlier orders and appears labelled Closed (S4)

**Slice** — From a Location's page, a manager sets a value and/or units target for a period, and on a master the target is stated as covering the head office and all its branches.
**Spec source** — Targets & Performance US-003 S1, S2 (setting); glossary (Location Target, Chain Roll-up); S3, S4 are measured in T-28.1.1
**Depends on** — T-27.1.1, T-12.2.1, T-3.2.1
**Pattern to follow** — T-27.1.1 (target model), T-14.4.1 (an action on the Location page)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — a small setting on a reviewed model; no screen is drawn for it, so placement follows M-07's pattern.

**Provisional commit message**

```
feat(targets): set a target on a location or a whole chain

- A head office holds no stock, so a target on it measures the chain it
  runs, not the building
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A small setting with a review pause.

**Agent prompt**

```
Role: You are adding Location and chain targets to the Field Sales Management
System's manager website.

Context:
- Slice: from a Location's page, a manager sets a value and/or units target
  for a period, and on a master the target is stated as covering the head
  office and all its branches.
- Specs: plan_docs/stories/targets-and-performance.md US-003 S1, S2 and
  glossary (Location Target, Chain Roll-up). No screen is drawn; propose the
  placement on the Location page (M-07) in your plan.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — target model
  (T-27.1.1), master relationship (T-12.2.1), Location page (T-3.2.1).
- Pattern to follow: T-27.1.1; T-14.4.1 for Location page actions.

Acceptance criteria:
1. A manager can set a Location Target (value, units or both, and a period)
   from the Location page.
2. On Hickey's Head Office it reads as covering "Hickey's Head Office and its
   12 branches".
3. On an ordinary Location it covers that Location only.
4. Edits are dated in history.

Constraints:
- Use the project's existing conventions and test framework.
- Store the subject as the Location; the roll-up is computed in measurement
  (T-28.1.1), not stored as branch lists.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after proposing the placement and saving a chain target, stop
and show it. Resume only on "Continue T-27.3.1".

Steps: 1. placement proposal; 2. form; 3. chain wording; 4. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-27.3.1-S; do not design
your own.

Definition of done: from a Location's page, a manager sets a value and/or
units target for a period, and on a master the target is stated as covering
the head office and all its branches.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: measuring the roll-up (T-28.1.1), the Large baseline
(T-28.4.1).
```

**Checkpoint**

Produces before pausing — the proposed placement and a saved chain target.
Human reviews — Is the placement where a manager would set a key account's target?
Resume trigger — `Continue T-27.3.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | TP-001 (T-27.1.1), TP-002 (T-27.2.1), TP-003 (T-27.3.1; S3, S4 measured in T-28.1.1) |
| Every task satisfies the three slice criteria | Pass | 3 of 3 |
| Every task carries a tier with a rationale citing dimensions | Pass | 3 of 3 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-37 (periods); undrawn Location target placement (T-27.3.1) |
| Tasks modifying existing behaviour order characterisation first | Pass | None modify existing behaviour |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | None in this epic has either High |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | All 3 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | None in this epic |
| Every scenario task precedes the task it gates | Pass | 3 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-37 |
