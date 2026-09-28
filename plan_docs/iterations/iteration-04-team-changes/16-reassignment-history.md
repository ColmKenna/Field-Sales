# E16 — Reassignment batches and coverage history

**Iteration** — 4, Team changes and location upkeep
**Outcome** — A long absence is covered by moving a chosen set of shops to another rep as one named batch, which can later be reversed without anyone remembering what moved; a rep's whole book and its history are readable at a glance; and a geography fix states which shops change rep before it happens.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Coverage Management US-004 — Bulk reassign as a named batch | Must | S1–S4, CV004-A, CV004-B |
| 2 | Coverage Management US-005 — Reverse a batch by hand | Should | S1–S4, CV005-A–C |
| 3 | Coverage Management US-008 — View a rep's coverage and history | Should | S1–S4 (specialist scopes appear once E20 exists) |
| 4 | Customer Directory US-007 (part) — Maintain geography and move a Town | Should | S3 Move a Town |

**Exit criterion** — A manager can bulk-reassign a filtered set of a rep's shops to another rep as a named batch (open visits moving as inherited, with no Leave), reverse it later from the batch with anything changed since left unticked and territory exceptions flagged, read any rep's assignments, counts and gained/lost history, and move a Town to another County seeing the shops and reps it affects first, with open visits going to handover.

**Capability-class stamp** — Frontier + extended reasoning for the batch slivers (T-16.1.1); Frontier workhorse for other tasks and scenario drafting; Fast mid-tier for T-16.3.1. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [coverage-management.md](../../stories/coverage-management.md) (US-004, US-005, US-008), [customer-directory.md](../../stories/customer-directory.md) (US-007), [05-manager.md](../../uxdocs/05-manager.md) (M-06, M-08, M-09), [02-head-office.md](../../uxdocs/02-head-office.md) (H-29).

---

### T-16.1.1-S — Test scenarios for bulk reassignment batches

**Owner** — Human-Led
**Gates** — T-16.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- 23 of Colm's Locations (filtered to "Large pharmacy") move to Aoife as "Paternity cover": each gets a direct assignment "Aoife (assigned directly, batch: Paternity cover)" (S2) and a history entry citing the batch (S4).
- Open visits: all move to Aoife with Scheduled Day cleared, marked "inherited from Colm", no Leave (CV004-B). Excluded from Aoife's performance until closed?
- Short leave uses absences, not batches (CV004-A): does the screen say so?
- A batch including a Location Colm holds directly (not via territory): same result?
- Batch naming: "Colm → Aoife, Paternity cover, 5 Oct 2026, 23 Locations" — permanent; never reverts on its own.

---

### T-16.1.1 — Bulk reassign a set of shops as a named batch for a long absence

**Parent story**

> As a Sales Manager, I want to move a chosen set of Locations from one rep to another in one action, saved as a named batch, so that a rep's leave or departure is handled in minutes and can be traced later.
>
> Acceptance criteria:
> - Selecting 23 of Colm's Locations filtered to "Large pharmacy", choosing Aoife and labelling "Paternity cover" previews "23 Locations move from Colm to Aoife" and saves batch "Colm → Aoife, Paternity cover, 5 Oct 2026, 23 Locations" (S1)
> - Each shows "Aoife (assigned directly, batch: Paternity cover)" (S2)
> - Proceeding with none selected shows "Select at least one Location" (S3)
> - Each Location's history cites the batch as cause (S4)
> - Short leave is handled by absence decisions, with no ownership change or batch (CV004-A)
> - No Leave or per-visit handover; every open visit moves to Aoife with Scheduled Day cleared, marked "inherited from Colm" (CV004-B)

**Slice** — A manager filters a rep's shops, picks the receiving rep and a label, previews the move, and saves a permanent named batch of direct assignments that moves every open visit as inherited and writes history citing the batch.
**Spec source** — Coverage Management US-004 S1–S4, CV004-A, CV004-B; glossary (Reassignment Batch); uxdocs 05 M-09 (M9.1–M9.4)
**Depends on** — T-14.5.2, T-3.1.1
**Pattern to follow** — T-3.1.4 (filter, review, preview), T-14.5.2 (inherited visits)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — moves ownership of many shops and their visits at once (data integrity, Blast Radius High); the batch is the record reversal depends on.

**Provisional commit message**

```
feat(coverage): bulk reassign shops as a named batch

- A long absence is handled in minutes, and the named batch is the record
  that makes a later reversal possible without memory
- Open visits move with the shops as inherited, so nothing is stranded with
  a rep who isn't there
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Mass ownership changes with history.

**Work package**

Increments:
1. Batch record: label, from rep, to rep, date, member Locations, created by.
2. Filter and review of the giving rep's Locations (by Location Profile, Town, Customer), all selected, exclusions; "Select at least one Location".
3. Preview via T-3.1.2's dry run: "23 Locations move from Colm to Aoife".
4. Save: a direct Location assignment per member citing the batch; history entry per Location with cause "batch"; every open visit moves as inherited (reuse T-14.5.2's move-all path).
5. Guidance that short leave belongs to absences (M9.1).

Decision points:
- Can a Location be in two active batches?
- Does the batch record which open visits moved, for the reversal preview?

Delegable slivers:
- **Batch list** — List batches with label, reps, date and Location count, reading batch records. Read-only.
- **Filter and review UI** — Build the Location filter and review with every match selected and exclusions, returning the chosen set to the existing batch command. No ownership logic.

---

### T-16.2.1-S — Test scenarios for reversing a batch

**Owner** — Human-Led
**Gates** — T-16.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Reverse "Paternity cover": the 23 pre-ticked, target Colm, label "Reversal of: Paternity cover" (S1); 3 moved on to Brian since show under "Changed since", unticked (S2).
- Partial reversal: unticking Hickey's leaves it with Aoife; the batch shows "22 of 23 reversed" (S3).
- Restore derived ownership: the direct assignment is removed and the shop shows "Colm (via Wicklow)" (S4, CV005-B).
- Territory changed meanwhile: "3 Locations would resolve to Niamh via Wicklow", each with "Assign to Colm directly" or "Leave with Niamh" (CV005-C).
- Aoife's open visits at returning shops go through the per-visit Move/Leave handover; moved ones are "inherited from Aoife" (CV005-A).
- Reversing twice, or reversing a partially reversed batch.

---

### T-16.2.1 — Reverse a batch from its record

**Parent story**

> As a Sales Manager, I want to open a past batch and send its Locations back to the original rep in one action, with any reassigned since left out, so that undoing a leave reassignment doesn't rely on memory.
>
> Acceptance criteria:
> - Reverse on "Paternity cover" opens Bulk Reassign with the 23 ticked, target Colm, label "Reversal of: Paternity cover" (S1)
> - 3 later moved from Aoife to Brian appear under "Changed since", unticked, and can be ticked (S2)
> - Unticking Hickey's Pharmacy Rathdrum and saving moves 22 back; the batch shows "22 of 23 reversed" (S3)
> - With Colm still holding Wicklow, a reversed Location loses its direct assignment and shows "Colm (via Wicklow)" (S4, CV005-B)
> - Aoife's open visits at the returning Locations are listed for Move or Leave with Apply to all; undecided become Handover Pending; moved ones are "inherited from Aoife" (CV005-A)
> - With Wicklow transferred to Niamh meanwhile, the preview states "3 Locations would resolve to Niamh via Wicklow", each offering "Assign to Colm directly" or "Leave with Niamh" (CV005-C)

**Slice** — Reversing a past batch opens a pre-filled reassignment back to the original rep, with shops moved on since left unticked, restores territory-derived ownership, flags shops whose territory now resolves to someone else, and hands open visits over per visit.
**Spec source** — Coverage Management US-005 S1–S4, CV005-A–C; glossary (Batch Reversal); uxdocs 05 M-09
**Depends on** — T-16.1.1, T-14.5.1
**Pattern to follow** — T-16.1.1 (batch), T-14.5.1 (per-visit handover)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — Blast Radius High; the reversal rules need a human oracle; an agent builds against agreed scenarios on reviewed batch and handover code.

**Provisional commit message**

```
feat(coverage): reverse a reassignment batch from its record

- Nobody reverses a leave reassignment from memory: the batch pre-fills
  the reversal, and anything changed since is left for a deliberate choice
- Ownership is restored to the territory, with exceptions flagged where
  the territory itself has moved on
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Rule-heavy change built against a human oracle.

**Agent prompt**

```
Role: You are building batch reversal (M-09) for the Field Sales Management
System.

Context:
- Slice: reversing a past batch opens a pre-filled reassignment back to the
  original rep, with shops moved on since left unticked, restores
  territory-derived ownership, flags shops whose territory now resolves to
  someone else, and hands open visits over per visit.
- Specs: plan_docs/stories/coverage-management.md US-005 S1–S4, CV005-A–C,
  glossary (Batch Reversal, Changed Since); plan_docs/uxdocs/05-manager.md
  M-09.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — batches (T-16.1.1),
  resolver and history (T-3.1.1), preview (T-3.1.2), handover (T-14.5.1).
- Pattern to follow: T-16.1.1's batch flow with T-14.5.1's handover.

Acceptance criteria:
1. Reverse on "Paternity cover" opens the reassignment with the batch's 23
   Locations ticked, target Colm, label "Reversal of: Paternity cover".
2. Locations reassigned since the batch (3 now with Brian) appear under
   "Changed since", unticked, and can be ticked.
3. Unticking Hickey's Pharmacy Rathdrum and saving moves 22 back; the batch
   shows "22 of 23 reversed".
4. With Colm still holding Wicklow, a reversed Location's direct assignment
   is removed and it shows "Colm (via Wicklow)".
5. With Wicklow transferred to Niamh meanwhile, the preview states "3
   Locations would resolve to Niamh via Wicklow", each offering "Assign to
   Colm directly" or "Leave with Niamh".
6. Aoife's open visits at returning Locations are listed for Move or Leave
   with Apply to all; undecided become Handover Pending; moved ones are
   "inherited from Aoife".
7. History entries cite cause "batch reversal".

Constraints:
- Use the project's existing conventions and test framework.
- One transaction per reversal.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond
  reversal tracking on the batch.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the reversal planning function passes every agreed
scenario, stop and show it. Resume only on "Continue T-16.2.1".

Steps: 1. reversal planning (changed since, derived restore, territory
exceptions); 2. pre-filled screen; 3. handover step; 4. save with history;
5. tests.

Test expectations: implement exactly the scenarios agreed in T-16.2.1-S. You
are forbidden from designing your own test cases.

Definition of done: reversing a past batch opens a pre-filled reassignment
back to the original rep, with shops moved on since left unticked, restores
territory-derived ownership, flags shops whose territory now resolves to
someone else, and hands open visits over per visit.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: creating batches (T-16.1.1).
```

**Checkpoint**

Produces before pausing — the reversal planning function passing every agreed scenario.
Human reviews — Does every returning shop end with the owner the scenarios specify, including the territory exceptions?
Resume trigger — `Continue T-16.2.1`

---

### T-16.3.1 — Review a rep's book and its history at a glance

**Parent story**

> As a Sales Manager, I want to see a rep's Territory Assignments, resulting Locations, Specialist scopes, and what they gained or lost over time so that I can review a rep's book at a glance.
>
> Acceptance criteria:
> - Opening Colm shows "Wicklow (County), Byrne's Chemist (Location)", "117 Locations as Primary", "Specialist: none", "Reports to: M. Byrne" (S1)
> - Wicklow shows "140 Locations, 23 carved out (Rathdrum → Aoife)" (S2)
> - History lists gained and lost entries, e.g. "5 Oct 2026 — lost 23 to Aoife — batch: Paternity cover" (S3)
> - A new rep with nothing assigned shows "No assignments yet" with Add assignment (S4)

**Slice** — A manager opens a rep and sees their assignments, how many shops they're primary for, any specialist scopes, who they report to, carve-outs, and a gained-and-lost history.
**Spec source** — Coverage Management US-008 S1–S4
**Depends on** — T-3.1.3, T-16.1.1
**Pattern to follow** — T-3.1.3 (rep territory page)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: M | Confidence: M
  (inferred) — read-only over reviewed data; Blast Radius Low; specified.

**Provisional commit message**

```
feat(coverage): review a rep's book and what they gained or lost

- "Who had this shop and why" must be answerable later, so a rep's page
  reads their history from the append-only record
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Read-only views.

**Agent prompt**

```
Role: You are adding a rep coverage summary and history to the rep territory
page (M-06) of the Field Sales Management System.

Context:
- Slice: a manager opens a rep and sees their assignments, how many shops
  they're primary for, any specialist scopes, who they report to,
  carve-outs, and a gained-and-lost history.
- Specs: plan_docs/stories/coverage-management.md US-008 S1–S4.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — rep page (T-3.1.3),
  resolver and history (T-3.1.1), batches (T-16.1.1).
- Pattern to follow: T-3.1.3.

Acceptance criteria:
1. Opening Colm shows "Wicklow (County), Byrne's Chemist (Location)", "117
   Locations as Primary", "Specialist: none" (scopes appear once E20 exists)
   and "Reports to: M. Byrne".
2. Wicklow shows "140 Locations, 23 carved out (Rathdrum → Aoife)".
3. History lists gained and lost entries newest first, e.g. "5 Oct 2026 —
   lost 23 to Aoife — batch: Paternity cover".
4. A new rep with nothing assigned shows "No assignments yet" with Add
   assignment.

Constraints:
- Use the project's existing conventions and test framework.
- Read-only; history grouped per event (one line for a 23-shop batch), from
  per-Location entries.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. summary query; 2. carve-out counts; 3. grouped history; 4. empty
state; 5. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a manager opens a rep and sees their assignments, how
many shops they're primary for, any specialist scopes, who they report to,
carve-outs, and a gained-and-lost history.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: specialist scopes (E20).
```

---

### T-16.4.1-S — Test scenarios for moving a Town to another County

**Owner** — Human-Led
**Gates** — T-16.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Rathdrum (23 Locations) moves from Wicklow (Colm's County) to Wexford (Brian's): the preview says "23 locations move to Wexford; Primary Rep changes from Colm to Brian for 23 locations" (S3). What if Rathdrum itself is carved out to Aoife — does anything change?
- Open visits: Handover Pending on confirm (S3), or through M-08's handover preview (H29.2)?
- History cause for a geography-driven owner change (reserved in T-3.1.1).
- Moving a County to another Region (design decision "Geography edits show their reach").

---

### T-16.4.1 — Move a Town to another County with its coverage effect shown

**Parent story**

> As a Head Office User, I want to maintain Regions, Counties and Towns, and move a Town to the right County with the effect on territories shown so that a geography fix doesn't silently reassign shops.
>
> Acceptance criteria:
> - Moving Rathdrum (23 Locations, Wicklow assigned to Colm) to Wexford (assigned to Brian) shows "23 locations move to Wexford; Primary Rep changes from Colm to Brian for 23 locations", and Handover Pending is raised for their open visits on confirm (S3)

**Slice** — Moving a Town to another County first states how many shops move and whose ownership changes, then continues into the handover of their open visits, and history records the geography change as the cause.
**Spec source** — Customer Directory US-007 S3; design decision "Geography edits show their reach"; uxdocs 02 H-29 (H29.2)
**Depends on** — T-2.1.1, T-14.5.1, T-3.1.2
**Pattern to follow** — T-3.1.2 (preview), T-14.5.1 (handover)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — a geography edit silently reassigns shops (Blast Radius High); its interaction with carve-outs needs a human oracle.

**Provisional commit message**

```
feat(customers): move a town between counties with its coverage effect

- Territory is derived from Town, so fixing geography can reassign shops;
  the preview says whose shops move before anything does
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A reassigning edit built against a human oracle.

**Agent prompt**

```
Role: You are adding "Move a Town" (H-29) to geography maintenance in the
Field Sales Management System.

Context:
- Slice: moving a Town to another County first states how many shops move
  and whose ownership changes, then continues into the handover of their open
  visits, and history records the geography change as the cause.
- Specs: plan_docs/stories/customer-directory.md US-007 S3 and design
  decision "Geography edits show their reach"; plan_docs/uxdocs/
  02-head-office.md H-29 (H29.2 continues to M-08's handover preview when
  reps change).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — geography (T-2.1.1),
  resolver and preview (T-3.1.1, T-3.1.2), handover (T-14.5.1).
- Pattern to follow: T-3.1.2's preview-equals-outcome approach.

Acceptance criteria:
1. Moving Rathdrum (23 Locations) from Wicklow (Colm) to Wexford (Brian)
   shows "23 locations move to Wexford; Primary Rep changes from Colm to Brian
   for 23 locations".
2. On confirm, open visits at those Locations go to the handover step (Move /
   Leave per visit; undecided become Handover Pending).
3. Each Location whose owner changes gets a history entry with cause
   "geography change".
4. A Town carved out to its own rep keeps that owner; the preview reflects
   it.
5. Moving a County to another Region previews the same way.

Constraints:
- Use the project's existing conventions and test framework.
- Preview and outcome use the same resolver.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the move preview passes every agreed scenario, stop and
show it. Resume only on "Continue T-16.4.1".

Steps: 1. move operation; 2. preview; 3. handover step; 4. history; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-16.4.1-S. You
are forbidden from designing your own test cases.

Definition of done: moving a Town to another County first states how many
shops move and whose ownership changes, then continues into the handover of
their open visits, and history records the geography change as the cause.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: creating or archiving geography (T-2.1.1, T-2.1.2).
```

**Checkpoint**

Produces before pausing — the Town move preview passing every agreed scenario.
Human reviews — Does the preview match exactly the owners after the move, carve-outs included?
Resume trigger — `Continue T-16.4.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | CV-004 (T-16.1.1), CV-005 (T-16.2.1), CV-008 (T-16.3.1), CD-007 S3 (T-16.4.1) |
| Every task satisfies the three slice criteria | Pass | 4 of 4 |
| Every task carries a tier with a rationale citing dimensions | Pass | 4 of 4 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | Reversal and Town-move edge cases raised as decision questions |
| Tasks modifying existing behaviour order characterisation first | Pass | None modify earlier behaviour directly; T-16.4.1 extends geography through a preview |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-16.3.1 is read-only |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-16.1.1, T-16.2.1, T-16.4.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-16.2.1, T-16.4.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-16.1.1 |
| Every scenario task precedes the task it gates | Pass | 3 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}` |
