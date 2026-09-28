# E3 — Rep coverage and product permissions

**Iteration** — 1, MVP: orders from the field reach the warehouse
**Outcome** — Every shop resolves to exactly one responsible rep, with the reason shown; every coverage change previews its impact and writes history; restricted-product permissions are granted per rep with a record.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Coverage Management US-001 — Assign a territory or Location to a rep | Must | S1–S5, CV001-A–G, CV001-H's history (its handover part arrives with E14, Coverage US-003) |
| 2 | Coverage Management US-002 — See who covers a Location and why | Must | S1–S4, CV002-A–D (specialists line → E20; "Add one-off visit" → E14; "Set up online ordering" → E25) |
| 3 | Coverage Management US-007 — Find and fix Unassigned Locations | Must | S1–S4, CV007-C (the overview count CV007-A, B, D–H → E14) |
| 4 | Coverage Management US-009 — Grant or remove a Restriction Permission | Must | S1–S4, CV009-A–F (S2's tablet effect is verified in T-4.4.1) |

**Exit criterion** — A Sales Manager can assign regions, counties, towns or single shops to a rep, see every shop's owner with its source, transfer assignments between reps (push or pull, with county and region roll-ups), preview each change's impact before saving, find and fix unassigned shops, and grant or remove restricted-product permissions singly or for a cohort — with an append-only record of every change.

**Capability-class stamp** — Frontier + extended reasoning for the ownership-resolution slivers (T-3.1.1); Frontier workhorse for Agent-Assisted tasks, permission slivers and scenario drafting; Fast mid-tier for Agent-Autonomous tasks. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [coverage-management.md](../../stories/coverage-management.md), [05-manager.md](../../uxdocs/05-manager.md) (M-06, M-07, M-14, M-15), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§5 Impact preview).

---

### T-3.1.1-S — Test scenarios for territory assignment and ownership resolution

**Owner** — Human-Led
**Gates** — T-3.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- For each level pair (Location over Town, Town over County, County over Region), which rep owns a shop covered by both? Write one scenario per pair and one for all four at once.
- A new Location is created in a Town covered only by a County assignment: who owns it, and does it get a history entry at creation?
- Removing a County assignment when some Towns inside it are carved out to other reps: which shops become Unassigned and which keep their carve-out owner?
- What exactly does each history entry hold (date to the minute, actor, previous and new rep, cause, optional reason), and is one entry written per Location even for a 140-shop County?
- Which reps may a manager assign — only reps they manage (Coverage assumption) — and what does a Head Office User see?

---

### T-3.1.1 — Assign territories and resolve each shop's owner with its source

**Parent story**

> As a Sales Manager, I want to assign a Region, County, Town or single Location to a rep so that every shop in it has a responsible rep without listing shops one by one.
>
> Acceptance criteria:
> - Assigning County Wicklow (140 Locations, none assigned) to Colm makes all 140 show "Colm (via Wicklow)" (S1)
> - Assigning Town Rathdrum to Aoife inside Colm's Wicklow makes Murphy's Pharmacy "Aoife (via Rathdrum)" while Doyle's Shop (Laragh) stays "Colm (via Wicklow)" (S2)
> - Removing Aoife's Rathdrum assignment returns those 23 to Colm via Wicklow (S4)
> - Removing Wexford, assigned only to Brian, leaves 96 Locations Unassigned (S5)
> - Each Location whose owner changes gets an Assignment History entry (CV001-H)

**Slice** — A Sales Manager assigns a region, county, town or single shop to a rep, every shop resolves to the most specific assignment with its source shown, and each ownership change is written to an append-only history.
**Spec source** — Coverage Management US-001 S1, S2, S4, S5, CV001-H; glossary (Primary Rep, Territory Assignment, Most-Specific Rule, Effective Owner, Assignment History); design decisions "Most-specific wins, always shown with its source", "Append-only Assignment History"
**Depends on** — T-2.3.1
**Pattern to follow** — novel — see design notes
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — the ownership rule every later area reads (tablet snapshot, visits, attribution); Blast Radius High; Edge-Case Discovery High (overlaps, cascades, new Locations inside a territory, later Town moves).

**Provisional commit message**

```
feat(coverage): resolve each location's owner from territory assignments

- Carving a town out of a county is routine, so overlaps are allowed and
  the narrowest assignment wins, shown with its source
- Every ownership change writes one history entry per location; sales
  attribution will read owner-at-the-time from it later
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The resolver and its history are the most-read rule outside pricing.

**Work package**

Increments:
1. Territory Assignment model: rep + one of Region, County, Town, Location; at most one assignment per unit per level.
2. Resolver: given a Location, return its Effective Owner and Source by the Most-Specific Rule (Location > Town > County > Region), or Unassigned. Pure function; test against the agreed scenarios.
3. Materialise or compute owners — decide (see decision points) and implement, including Locations created later inside a territory.
4. Assignment History: on any change, diff effective owners before and after and write one append-only entry per changed Location with cause "direct Location assignment" or "territory assignment", actor and minute timestamp.
5. Add and remove handlers for the four levels, restricted to reps the manager manages (Head Office Users may assign any rep).

Decision points:
- Are effective owners computed on read or materialised and refreshed on each change? Materialised is faster for the snapshot but must never drift from the rule.
- Is a Location's creation an ownership change that writes a history entry?
- When a Town moves County (E16) the owner changes without an assignment change — does the history cause need a "geography change" value? Reserve it now.
- Where do the rep → manager reporting lines come from (MI-03)?

Delegable slivers:
- **Resolver unit tests** — Given the agreed scenario list from T-3.1.1-S, write unit tests for the pure resolver function in the project's test framework. Do not change the resolver; report failures.
- **History entry formatter** — Format a history entry as "17 Sep 2026 14:02 — Colm → Aoife — via Rathdrum assignment — by M. Byrne" from the stored fields. Read-only; do not change how entries are written.
- **Add/remove assignment handlers** — Implement handlers that call the existing resolver and history writer inside one transaction, rejecting reps outside the manager's team. Do not change the resolver or history schema.

---

### T-3.1.2-S — Test scenarios for assignment impact previews

**Owner** — Scenario Review
**Gates** — T-3.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for impact previews on territory assignment
changes (task T-3.1.2). Read plan_docs/stories/coverage-management.md US-001
S1–S5 and plan_docs/uxdocs/00-conventions-and-shared-elements.md §5. Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover every preview sentence in S1–S5, then derivable edges:
a change that moves nobody, a preview that goes stale because another manager
saved first, previews involving carve-outs. Include one property scenario:
the preview equals the state after save. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-3.1.2 — Preview every assignment change before saving

**Parent story**

> As a Sales Manager, I want to assign a Region, County, Town or single Location to a rep so that every shop in it has a responsible rep without listing shops one by one.
>
> Acceptance criteria:
> - Assigning Wicklow to Colm previews "140 Locations become Colm's" (S1)
> - Assigning Rathdrum to Aoife inside Colm's Wicklow previews "23 Locations move from Colm to Aoife" (S2)
> - Removing Aoife's Rathdrum previews "23 Locations move from Aoife to Colm" (S4)
> - Removing Wexford previews "96 Locations become Unassigned" and asks to confirm (S5)

**Slice** — Before any assignment change is saved, the manager sees how many shops change owner, from whom to whom, and confirms.
**Spec source** — Coverage Management US-001 S1–S5 (previews); uxdocs 00 §5
**Depends on** — T-3.1.1
**Pattern to follow** — T-3.1.1 (resolver used as a dry run)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — a dry run of the reviewed resolver; the checkpoint proves the preview equals the saved outcome.

**Provisional commit message**

```
feat(coverage): preview ownership changes before saving

- Ownership is derived, so removing one wide assignment can silently move
  many shops; the preview is the mitigation the design relies on
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Builds on a reviewed rule with a correctness checkpoint.

**Agent prompt**

```
Role: You are adding impact previews to territory assignment changes in the
Field Sales Management System.

Context:
- Slice: before any assignment change is saved, the manager sees how many
  shops change owner, from whom to whom, and confirms.
- Specs: plan_docs/stories/coverage-management.md US-001 S1–S5 and design
  decision "Most-specific wins, always shown with its source";
  plan_docs/uxdocs/00-conventions-and-shared-elements.md §5 (counts that
  expand to detail).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — resolver, assignment
  handlers and history from T-3.1.1.
- Pattern to follow: call T-3.1.1's resolver on a proposed state without
  saving.

Acceptance criteria:
1. Assigning Wicklow (140 Locations, none assigned) to Colm previews "140
   Locations become Colm's".
2. Assigning Rathdrum to Aoife inside Colm's Wicklow previews "23 Locations
   move from Colm to Aoife".
3. Removing Aoife's Rathdrum assignment previews "23 Locations move from
   Aoife to Colm".
4. Removing Wexford (only Brian) previews "96 Locations become Unassigned"
   and requires an explicit confirm.
5. Each count expands to the list of Locations.
6. If the data changed since the preview (another save), saving re-previews
   instead of applying a stale plan.

Constraints:
- Use the project's existing conventions and test framework.
- The preview must use the same resolver as the save; no second copy of the
  rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the dry-run function and a property test "preview equals
state after save" pass, stop and show them. Resume only on "Continue T-3.1.2".

Steps: 1. dry-run over the resolver; 2. preview sentences; 3. stale-plan
detection; 4. confirm step in the UI; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-3.1.2-S; do not design
your own.

Definition of done: before any assignment change is saved, the manager sees
how many shops change owner, from whom to whom, and confirms.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] No duplicate of the resolver rule
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: transfers (T-3.1.4), open-visit handover (E14), the rep page
layout (T-3.1.3).
```

**Checkpoint**

Produces before pausing — the dry-run function and a passing property test that the preview equals the state after save.
Human reviews — Can the preview ever show something other than what saving does?
Resume trigger — `Continue T-3.1.2`

---

### T-3.1.3 — Show a rep's territory page with counties expanding to towns

**Parent story**

> As a Sales Manager, I want to assign a Region, County, Town or single Location to a rep so that every shop in it has a responsible rep without listing shops one by one.
>
> Acceptance criteria:
> - Colm's page lists his assignments by level; Wicklow expands to its Towns, each with its Location count; Towns carved out to another rep are marked with that rep (CV001-B)

**Slice** — A Sales Manager opens a rep's territory page and sees their assignments by level, with each county expandable to its towns, their shop counts and any carve-outs marked.
**Spec source** — Coverage Management US-001 CV001-B; uxdocs 05 M-06 (M6.1–M6.6, settled)
**Depends on** — T-3.1.1
**Pattern to follow** — T-1.1.1 (staff site pages); first manager-area page (MI-51)
**Ownership** — Impl: Agent-Assisted | Test: Agent-Autonomous | Complexity: M | Confidence: M
  (inferred) — first manager-area page, so the build pauses on the page shell; the design is settled and the tests are mechanical.

**Provisional commit message**

```
feat(coverage): show a rep's territory with carve-outs

- The usual trigger for territory work is a new rep, so the page is
  anchored on the rep and shows what they hold and what is carved out
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Sets the manager-area page shell.

**Agent prompt**

```
Role: You are building the rep-anchored territory assignment page (M-06) for
the Field Sales Management System's manager area.

Context:
- Slice: a Sales Manager opens a rep's territory page and sees their
  assignments by level, each county expandable to its towns with shop counts
  and carve-outs marked.
- Specs: plan_docs/stories/coverage-management.md US-001 CV001-B and the UX
  amendment note; plan_docs/uxdocs/05-manager.md M-06 (settled M6.1–M6.6).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — resolver and
  assignment handlers from T-3.1.1; staff site shell from T-1.1.1. Assume one
  staff website with manager and head-office areas by role (MI-51).
- Pattern to follow: T-1.1.1's page structure.

Acceptance criteria:
1. Colm's page lists his Region, County, Town and Location assignments.
2. Wicklow (County) expands to its Towns, each with its Location count.
3. Towns carved out to another rep are marked with that rep's name.
4. Add and remove actions use T-3.1.1's handlers and T-3.1.2's preview.
5. Only reps the manager manages are editable; Head Office Users can edit
   any.

Constraints:
- Use the project's existing conventions and test framework.
- Follow M-06's layout and wording; do not invent copy.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the page renders Colm's sample territory against M-06,
stop and show it. Resume only on "Continue T-3.1.3".

Steps: 1. read model for a rep's assignments with counts and carve-outs;
2. page per M-06; 3. wire add/remove; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a Sales Manager opens a rep's territory page and sees
their assignments by level, with each county expandable to its towns, their
shop counts and any carve-outs marked.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Layout matches M-06
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: transfers (T-3.1.4, T-3.1.5), rep coverage history view
(E16, Coverage US-008), specialists (E20).
```

**Checkpoint**

Produces before pausing — the page rendered for a sample rep with a County, a carve-out and a direct Location assignment.
Human reviews — Does it match M-06, and is the manager-area shell right for every later manager page?
Resume trigger — `Continue T-3.1.3`

---

### T-3.1.4-S — Test scenarios for transferring assignments

**Owner** — Human-Led
**Gates** — T-3.1.4
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Transferring only some of a County's Towns: exactly which Town assignments are created, and what happens to the County assignment (CV001-D)?
- Transferring a County's last remaining Towns: when does the County assignment move too (CV001-E), and likewise a Region with its last Counties (CV001-E2)?
- What if a Town inside the transferred County is carved out to a third rep (CV001-C keeps carve-outs) — does it stay put?
- A transfer where some rows are excluded in review: which assignments remain with the giving rep?
- Which history entries does each transfer write, and with what cause?

---

### T-3.1.4 — Transfer assignments between reps with county and region roll-ups

**Parent story**

> As a Sales Manager, I want to assign a Region, County, Town or single Location to a rep so that every shop in it has a responsible rep without listing shops one by one.
>
> Acceptance criteria:
> - "Transfer..." from Colm's filtered assignments opens a review with every row selected; rows can be removed before choosing the receiving rep (CV001-A)
> - Transferring the whole Wicklow County to Niamh moves the County assignment; carve-outs keep their owners (CV001-C)
> - Transferring only Bray, Greystones and Wicklow Town makes each a Town assignment to Niamh; the County stays with Colm (CV001-D)
> - Transferring the remaining Towns Colm holds through Wicklow to Ciara moves the County too: "Wicklow (County) moves to Ciara with its last Towns." (CV001-E)
> - Transferring Wexford, the last County Colm holds through South East, moves the Region: "South East (Region) moves to Ciara with its last Counties." (CV001-E2)

**Slice** — A Sales Manager transfers a filtered set of a rep's assignments to another rep, partial counties become town carve-outs, and a county or region moves with its last towns or counties.
**Spec source** — Coverage Management US-001 CV001-A, C, D, E, E2; glossary (Transfer); uxdocs 05 M-06
**Depends on** — T-3.1.2, T-3.1.3
**Pattern to follow** — T-3.1.1 (resolver, history), T-3.1.2 (preview)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — Blast Radius High (can move hundreds of shops); the roll-up rules need a human oracle (Edge-Case Discovery High); an agent implements against the agreed scenarios.

**Provisional commit message**

```
feat(coverage): transfer territory assignments between reps

- A permanent replacement moves the assignments themselves, so new shops
  keep following the territory to the new rep
- Partial transfers become town carve-outs, and a county or region follows
  its last towns or counties, so no empty wide assignment is left behind
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Rule-heavy implementation against a human-owned oracle.

**Agent prompt**

```
Role: You are building assignment transfers between reps (M-06) for the Field
Sales Management System.

Context:
- Slice: a Sales Manager transfers a filtered set of a rep's assignments to
  another rep; partial counties become town carve-outs; a county or region
  moves with its last towns or counties.
- Specs: plan_docs/stories/coverage-management.md US-001 CV001-A, C, D, E,
  E2 and glossary (Transfer); plan_docs/uxdocs/05-manager.md M-06.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — resolver and history
  (T-3.1.1), preview (T-3.1.2), rep page (T-3.1.3).
- Pattern to follow: plan the transfer as a pure function returning the new
  assignment set, then preview and save it through T-3.1.2 and T-3.1.1.

Acceptance criteria:
1. On Colm's page, filtering his assignments and choosing "Transfer..."
   opens a review with every filtered row selected; rows can be removed
   before choosing the receiving rep.
2. Transferring the whole Wicklow County to Niamh moves the County
   assignment; Locations show "Niamh (via Wicklow)"; carve-outs keep their
   owners.
3. Transferring only Bray, Greystones and Wicklow Town creates a Town
   assignment to Niamh for each; the County assignment stays with Colm.
4. After criterion 3, transferring every remaining Town Colm holds through
   Wicklow to Ciara moves the County assignment to Ciara too, and the
   preview says "Wicklow (County) moves to Ciara with its last Towns."
5. Transferring Wexford, the last County Colm holds through South East,
   moves the Region too: "South East (Region) moves to Ciara with its last
   Counties."
6. Every Location whose owner changes gets a history entry.

Constraints:
- Use the project's existing conventions and test framework.
- One transaction per transfer; a failure leaves no partial transfer.
- Open visits are not handled here (E14 adds handover); do not touch visits.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the pure transfer-planning function passes every agreed
scenario, stop and show the results. Resume only on "Continue T-3.1.4".

Steps: 1. transfer-planning function; 2. review screen with exclusions;
3. preview via T-3.1.2; 4. save via T-3.1.1; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-3.1.4-S. You
are forbidden from designing your own test cases; list anything you think is
missing in your report.

Definition of done: a Sales Manager transfers a filtered set of a rep's
assignments to another rep, partial counties become town carve-outs, and a
county or region moves with its last towns or counties.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Transfer is atomic
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: pulling from the receiving rep (T-3.1.5), "Is <rep>
leaving?" and handover of open visits (E14), bulk reassign batches (E16).
```

**Checkpoint**

Produces before pausing — the pure transfer-planning function with every agreed scenario passing.
Human reviews — Do partial and last-remaining transfers produce exactly the assignments the scenarios specify?
Resume trigger — `Continue T-3.1.4`

---

### T-3.1.5 — Pull a transfer from the receiving rep's page

**Parent story**

> As a Sales Manager, I want to assign a Region, County, Town or single Location to a rep so that every shop in it has a responsible rep without listing shops one by one.
>
> Acceptance criteria:
> - On Niamh's page, "Add assignment" lists Rathdrum as "Rathdrum — Aoife's"; choosing it asks "Transfer Rathdrum from Aoife to Niamh?" and continues to the impact preview (CV001-F)
> - "Take over from another rep..." then Colm opens the transfer review on Colm's assignments with Niamh set as the receiver (CV001-G)

**Slice** — From the receiving rep's page, a manager adds an area another rep holds as a transfer, or takes over another rep's book, through the same reviewed transfer.
**Spec source** — Coverage Management US-001 CV001-F, CV001-G; supersedes S3's dead-end message
**Depends on** — T-3.1.4
**Pattern to follow** — T-3.1.4 (transfer review)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — entry points into the reviewed transfer; they change nothing themselves; settled design.

**Provisional commit message**

```
feat(coverage): pull a transfer from the receiving rep's page

- Adding an area another rep holds offers a transfer instead of a dead end,
  so the common "new rep takes over" case is one flow
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Navigation into an existing flow.

**Agent prompt**

```
Role: You are adding pull-style entry points to assignment transfers on the
rep territory page (M-06) of the Field Sales Management System.

Context:
- Slice: from the receiving rep's page, a manager adds an area another rep
  holds as a transfer, or takes over another rep's book.
- Specs: plan_docs/stories/coverage-management.md US-001 CV001-F, CV001-G and
  the "Superseded" note on S3; plan_docs/uxdocs/05-manager.md M-06.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — transfer review from
  T-3.1.4.
- Pattern to follow: open T-3.1.4's review pre-filled.

Acceptance criteria:
1. On Niamh's page, "Add assignment" lists areas held by others as, e.g.,
   "Rathdrum — Aoife's".
2. Choosing it asks "Transfer Rathdrum from Aoife to Niamh?" and continues to
   the impact preview.
3. "Take over from another rep..." and choosing Colm opens the transfer
   review on Colm's assignments with Niamh set as the receiver.
4. S3's message "remove that assignment first or assign Locations
   individually" no longer appears anywhere.

Constraints:
- Use the project's existing conventions and test framework.
- No new transfer logic; reuse T-3.1.4.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. "Add assignment" list with holder labels; 2. transfer prompt;
3. "Take over" entry; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: from the receiving rep's page, a manager adds an area
another rep holds as a transfer, or takes over another rep's book, through
the same reviewed transfer.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: transfer rules (T-3.1.4), handover (E14).
```

---

### T-3.2.1 — Show who covers a shop and why on its Location page

**Parent story**

> As a Sales Manager, I want each Location to show its Effective Owner with the assignment that produced it, plus any Specialists, so that I never have to work the rule out in my head.
>
> Acceptance criteria:
> - Murphy's Pharmacy shows "Primary: Aoife (via Rathdrum)" (S1; the Specialists line arrives with E20)
> - Byrne's Chemist assigned directly shows "Primary: Colm (assigned directly)" (S2)
> - Walsh's Shop with no assignment shows "Primary: Unassigned" with an Assign action (S3)
> - History lists entries newest first, e.g. "17 Sep 2026 14:02 — Colm → Aoife — via Rathdrum assignment — by M. Byrne" (S4)
> - The page leads with the Primary Rep and source, opens History, and shows no list of open Visit Dues and no last-Call line (CV002-A, CV002-D)

**Slice** — A Sales Manager opens any shop's Location page and sees its primary rep with the source, or Unassigned, and its assignment history newest first.
**Spec source** — Coverage Management US-002 S1–S4, CV002-A, CV002-D; uxdocs 05 M-07 (M7.1–M7.6)
**Depends on** — T-3.1.1
**Pattern to follow** — T-3.1.3 (manager page)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: M | Confidence: M
  (inferred) — read-only view over the reviewed resolver and history; settled design; Blast Radius Low.

**Provisional commit message**

```
feat(coverage): show who covers a location and why

- A computed owner must be visible with its source, or managers will guess
  and fix the wrong assignment
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Read-only page on a settled design.

**Agent prompt**

```
Role: You are building the manager's Location page (M-07) coverage view for
the Field Sales Management System.

Context:
- Slice: a Sales Manager opens any shop's Location page and sees its primary
  rep with the source, or Unassigned, and its assignment history newest first.
- Specs: plan_docs/stories/coverage-management.md US-002 S1–S4, CV002-A,
  CV002-D; plan_docs/uxdocs/05-manager.md M-07 (settled; M7.5 History as its
  own view; M7.6 layout).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — resolver and history
  from T-3.1.1; manager area shell from T-3.1.3.
- Pattern to follow: T-3.1.3.

Acceptance criteria:
1. Murphy's Pharmacy shows "Primary: Aoife (via Rathdrum)".
2. Byrne's Chemist, assigned directly, shows "Primary: Colm (assigned
   directly)".
3. Walsh's Shop, with no assignment reaching it, shows "Primary: Unassigned"
   with an Assign action (the action itself is T-3.2.2).
4. History shows entries newest first in the form "17 Sep 2026 14:02 — Colm
   → Aoife — via Rathdrum assignment — by M. Byrne".
5. The page shows no list of open Visit Dues and no last-Call line.
6. The Specialists line is present but empty until E20.

Constraints:
- Use the project's existing conventions and test framework.
- Follow M-07's layout and wording.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. read model; 2. page per M-07; 3. History view; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a Sales Manager opens any shop's Location page and sees
its primary rep with the source, or Unassigned, and its assignment history
newest first.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: changing the owner from this page (T-3.2.2), "Add one-off
visit" (E14), "Set up online ordering" (E25), specialists (E20).
```

---

### T-3.2.2-S — Test scenarios for changing a shop's coverage from its page

**Owner** — Scenario Review
**Gates** — T-3.2.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for changing a shop's coverage from its Location
page (task T-3.2.2). Read plan_docs/stories/coverage-management.md US-002
CV002-B, CV002-C and plan_docs/uxdocs/05-manager.md M-07. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover CV002-B and CV002-C, then derivable edges: the Town is already held by
another rep, the manager does not manage the chosen rep, the only unassigned
shop in its Town. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-3.2.2 — Change a shop's coverage from its Location page

**Parent story**

> As a Sales Manager, I want each Location to show its Effective Owner with the assignment that produced it, plus any Specialists, so that I never have to work the rule out in my head.
>
> Acceptance criteria:
> - Walsh's Shop, unassigned with 4 others in Laragh, offers "Assign Laragh (Town) to..." first with "4 other Locations in Laragh are unassigned", and "Assign just this shop to..." beside it (CV002-B)
> - Murphy's Pharmacy, "Aoife (via Rathdrum)", offers "Change just this shop to..." first (a direct Location assignment) and "Transfer Rathdrum (Town, 23 Locations) to..." beside it, continuing to the transfer's impact preview (CV002-C)

**Slice** — From a shop's Location page, a manager assigns an unassigned shop's town or just the shop, or moves just this shop to another rep, through the impact preview.
**Spec source** — Coverage Management US-002 CV002-B, CV002-C; uxdocs 05 M-07
**Depends on** — T-3.2.1, T-3.1.4
**Pattern to follow** — T-3.1.2 (preview), T-3.1.4 (transfer)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — changes ownership, though only through reviewed flows; Blast Radius Medium.

**Provisional commit message**

```
feat(coverage): change a shop's owner from its location page

- The manager's single home for a shop leads with the likely fix: assign
  the whole Town when neighbours are unassigned too, or just this shop
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Ownership changes through established flows, with a review pause.

**Agent prompt**

```
Role: You are adding coverage-change actions to the manager's Location page
(M-07) of the Field Sales Management System.

Context:
- Slice: from a shop's Location page, a manager assigns an unassigned shop's
  town or just the shop, or moves just this shop to another rep, through the
  impact preview.
- Specs: plan_docs/stories/coverage-management.md US-002 CV002-B, CV002-C;
  plan_docs/uxdocs/05-manager.md M-07.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Location page
  (T-3.2.1), preview (T-3.1.2), transfer (T-3.1.4), handlers (T-3.1.1).
- Pattern to follow: reuse the preview and transfer flows.

Acceptance criteria:
1. Walsh's Shop in Laragh, unassigned with 4 other Laragh Locations, shows
   "Assign Laragh (Town) to..." first, with "4 other Locations in Laragh are
   unassigned", and "Assign just this shop to..." beside it.
2. Murphy's Pharmacy, "Aoife (via Rathdrum)", shows "Change just this shop
   to..." first, creating a direct Location assignment, and "Transfer
   Rathdrum (Town, 23 Locations) to..." beside it, continuing to the
   transfer's impact preview.
3. Every change goes through T-3.1.2's preview and writes history.

Constraints:
- Use the project's existing conventions and test framework.
- No new ownership logic; reuse T-3.1.1–T-3.1.4.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after both action sets render and route into the preview with
sample data, stop and show them. Resume only on "Continue T-3.2.2".

Steps: 1. action selection by state; 2. unassigned-in-Town count; 3. route
to preview and transfer; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-3.2.2-S; do not design
your own.

Definition of done: from a shop's Location page, a manager assigns an
unassigned shop's town or just the shop, or moves just this shop to another
rep, through the impact preview.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the Unassigned list (T-3.3.1), handover of open visits (E14).
```

**Checkpoint**

Produces before pausing — both action sets rendered for an unassigned and a covered shop, each routing to the preview.
Human reviews — Does the leading action match M-07 for each state, and does every path end in the preview?
Resume trigger — `Continue T-3.2.2`

---

### T-3.3.1 — List unassigned shops and fix them in place

**Parent story**

> As a Sales Manager, I want a list of Locations with no Primary Rep so that no shop is silently uncovered.
>
> Acceptance criteria:
> - 5 unassigned Locations appear grouped by Town, each with its Customer and Location Profile and an Assign action (S1; profiles exist from E12)
> - Assigning Walsh's Shop to Colm shows "Colm (assigned directly)" and removes it from the list (S2)
> - "Assign Laragh to…" from Walsh's row resolves all unassigned Laragh Locations to Colm via Laragh (S3)
> - With every Location assigned, the list reads "All Locations have a responsible rep" (S4)
> - Walsh's row leads with "Assign Laragh (Town) to..." and the Town's unassigned count, with "Assign just this shop to..." beside it (CV007-C)

**Slice** — A Sales Manager opens the Unassigned list grouped by town and assigns a shop or its whole town from the row, and an empty list reads "All Locations have a responsible rep".
**Spec source** — Coverage Management US-007 S1–S4, CV007-C; uxdocs 05 M-15
**Depends on** — T-3.2.2
**Pattern to follow** — T-3.2.2 (row actions follow M7.2)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — reuses T-3.2.2's reviewed actions; settled design; Blast Radius Low.

**Provisional commit message**

```
feat(coverage): list unassigned locations with in-place fixes

- An unassigned shop is invisible to every rep's tablet, so the list is the
  only place it surfaces until the overview count arrives with visits
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A list reusing reviewed actions.

**Agent prompt**

```
Role: You are building the Unassigned Locations list (M-15) for the Field
Sales Management System.

Context:
- Slice: a Sales Manager opens the Unassigned list grouped by town and
  assigns a shop or its whole town from the row; an empty list reads "All
  Locations have a responsible rep".
- Specs: plan_docs/stories/coverage-management.md US-007 S1–S4, CV007-C;
  plan_docs/uxdocs/05-manager.md M-15.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — row actions from
  T-3.2.2; resolver from T-3.1.1.
- Pattern to follow: T-3.2.2's action pair.

Acceptance criteria:
1. Unassigned Locations are grouped by Town, each with its Customer (and
   Location Profiles once E12 exists).
2. Each row leads with "Assign <Town> (Town) to..." and the Town's
   unassigned count, with "Assign just this shop to..." beside it.
3. Assigning Walsh's Shop to Colm shows "Colm (assigned directly)" and it
   leaves the list.
4. Assigning Laragh to Colm resolves every unassigned Laragh Location to
   "Colm (via Laragh)".
5. With none unassigned the list reads "All Locations have a responsible
   rep".

Constraints:
- Use the project's existing conventions and test framework; reuse T-3.2.2.
- This list shows all unassigned Locations; scoping by manager area comes
  with the overview count (E14).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. unassigned query; 2. grouped list per M-15; 3. row actions;
4. empty state; 5. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a Sales Manager opens the Unassigned list grouped by
town and assigns a shop or its whole town from the row, and an empty list
reads "All Locations have a responsible rep".

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the overview count and area scoping (E14), gap lists (E17).
```

---

### T-3.4.1-S — Test scenarios for granting and removing restriction permissions

**Owner** — Human-Led
**Gates** — T-3.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Who may grant or remove: the rep's own Sales Manager, any Head Office User — anyone else? What does a manager outside the team see (view only, S4)?
- What does the record hold for each change (date, who, optional reason), and is it append-only?
- When does a grant or removal take effect on the tablet — only at the rep's next sync (S1, S2)?
- Can a rep hold a permission for an archived group, and what does it do (MI-45)?

---

### T-3.4.1 — Grant or remove a rep's restriction permission with a record

**Parent story**

> As a Sales Manager or Head Office User, I want to grant a rep permission for a Restriction Group, with a record of who granted it and when, so that only qualified reps see and sell those products.
>
> Acceptance criteria:
> - Granting Colm "Pharmacy-only medicines" with reason "Completed training 15 Sep 2026" records the date, my name and the reason; after Colm's next Sync the products appear on his tablet (S1)
> - Colm's permissions show each group as Granted or Not granted with its last change entry (S3)
> - A Sales Manager who doesn't manage Ciara and isn't a Head Office User can view but not change Ciara's permissions (S4)

**Slice** — A rep's manager or head office grants or removes a restriction-group permission with an optional reason, each change is recorded, and managers outside the rep's team can only view.
**Spec source** — Coverage Management US-009 S1, S3, S4; glossary (Restriction Permission); uxdocs 05 M-14
**Depends on** — T-1.5.3, T-3.1.3
**Pattern to follow** — T-3.1.3 (manager page), T-3.1.1 (append-only record)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — authorization; the reporting lines that decide who may change what are undefined (MI-03).

**Provisional commit message**

```
feat(coverage): grant restriction permissions per rep with a record

- Restrictions exist for legal qualification as well as value, so each rep
  is granted each group separately and every change is attributable
- Takes effect at the rep's next sync; lines already captured still send
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Authorization logic held by a human; narrow slivers delegated.

**Work package**

Increments:
1. Permission record: rep, Restriction Group, granted/removed, date, actor, optional reason; append-only.
2. Authorization rule: the rep's manager or any Head Office User may change; everyone else may view. Verify with the agreed scenarios.
3. Per-rep view (M-14): each group with Granted / Not granted and the last change.
4. Report permissions as a usage source for Restriction Groups (T-1.5.1's interface) so T-1.5.3's archive message counts them.
5. Expose "current permissions per rep" for the snapshot builder (T-4.4.1).

Decision points:
- Can a Head Office User change permissions for any rep, including managers who also sell?
- Is the reason optional (story) or required for removals in regulated groups?
- Does removing a permission on an archived group record anything, given MI-45?

Delegable slivers:
- **Per-rep permissions view** — Build the M-14 per-rep view from the existing permission record and authorization rule: each group with Granted / Not granted and the last change, with change controls hidden for viewers. Do not change the rule or record.
- **Permission record tests** — Given the agreed scenarios from T-3.4.1-S, write the tests for the record and authorization rule. Do not change production code; report failures.

---

### T-3.4.2-S — Test scenarios for cohort permission grants

**Owner** — Scenario Review
**Gates** — T-3.4.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for granting permissions to several reps at once
from a Restriction Group's page (task T-3.4.2). Read
plan_docs/stories/coverage-management.md US-009 CV009-A–D and
plan_docs/uxdocs/05-manager.md M-14. Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover
CV009-A–D, then derivable edges: a selected rep already granted, a mix of my
reps and others', granting to zero reps. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-3.4.2 — Grant a permission to a cohort from the group's page

**Parent story**

> As a Sales Manager or Head Office User, I want to grant a rep permission for a Restriction Group, with a record of who granted it and when, so that only qualified reps see and sell those products.
>
> Acceptance criteria:
> - The group's page lists reps with Granted / Not granted and each one's last change (CV009-A)
> - Selecting five reps and granting with reason "Completed training 15 Sep 2026" creates a separate record each (CV009-B)
> - A grant from the group's page appears on the rep's own permissions page (CV009-C)
> - A rep outside my team is listed but can't be selected (CV009-D)

**Slice** — From a restriction group's page, a manager grants the permission to several reps at once with one reason, and each rep gets their own record.
**Spec source** — Coverage Management US-009 CV009-A–D; uxdocs 05 M-14 (M14.1)
**Depends on** — T-3.4.1
**Pattern to follow** — T-3.4.1
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — builds on T-3.4.1's reviewed rule; authorization, so not autonomous.

**Provisional commit message**

```
feat(coverage): grant restriction permissions to a trained cohort

- Training completes in cohorts, so the group's page grants several reps at
  once while keeping one record per rep for audit
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Authorization-adjacent, built with a review pause.

**Agent prompt**

```
Role: You are adding the per-group view with cohort grants (M-14) to the
Field Sales Management System.

Context:
- Slice: from a restriction group's page, a manager grants the permission to
  several reps at once with one reason; each rep gets their own record.
- Specs: plan_docs/stories/coverage-management.md US-009 CV009-A–D;
  plan_docs/uxdocs/05-manager.md M-14 (M14.1: two views over one record).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — permission record and
  authorization rule from T-3.4.1.
- Pattern to follow: T-3.4.1's per-rep view.

Acceptance criteria:
1. The group's page lists reps with Granted / Not granted and each one's last
   change.
2. Selecting Colm, Aoife, Ciara, Brian and Niamh and granting with reason
   "Completed training 15 Sep 2026" creates five separate records with the
   date, my name and that reason.
3. Each grant appears on that rep's own permissions page as Granted with the
   same record.
4. A rep I may not change is listed but can't be selected.

Constraints:
- Use the project's existing conventions and test framework.
- Use T-3.4.1's rule and record; no second authorization check.
- All grants in one action succeed or fail together.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the group page renders with selectable and locked rows and
a cohort grant writes records in a test, stop and show them. Resume only on
"Continue T-3.4.2".

Steps: 1. group view; 2. multi-select respecting the rule; 3. cohort grant;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-3.4.2-S; do not design
your own.

Definition of done: from a restriction group's page, a manager grants the
permission to several reps at once with one reason, and each rep gets their
own record.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: removal messaging (T-3.4.3), tablet hiding (T-4.4.1).
```

**Checkpoint**

Produces before pausing — the group page with selectable and locked rows, and a passing test of a five-rep grant.
Human reviews — Can a manager grant to a rep outside their team by any route?
Resume trigger — `Continue T-3.4.2`

---

### T-3.4.3-S — Test scenarios for removing a permission

**Owner** — Human-Led
**Gates** — T-3.4.3
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- The story's message counts "lines already on In Progress orders", but In Progress orders live only on the rep's tablet until sync (MI-44). Should the message count lines from the last sync, count nothing and say "any lines already on unsent orders will still send", or be dropped?
- What does a multi-rep removal say once (CV009-F)?
- After removal and sync, what exactly does the rep see on an existing line (T-4.4.1: "No longer available to you")?

---

### T-3.4.3 — Say what removing a permission will do before saving

**Parent story**

> As a Sales Manager or Head Office User, I want to grant a rep permission for a Restriction Group, with a record of who granted it and when, so that only qualified reps see and sell those products.
>
> Acceptance criteria:
> - Removing Colm's Pharmacy-only medicines first says "Colm will lose Pharmacy-only medicines at his next Sync. 2 lines already on In Progress orders will still send." (CV009-E)
> - Removing it from Colm, Aoife and Niamh says once how many reps lose it at their next Sync and how many In Progress lines will still send (CV009-F)

**Slice** — Before a permission removal is saved, the manager is told who loses the group at their next sync and that lines already taken will still send.
**Spec source** — Coverage Management US-009 CV009-E, CV009-F; uxdocs 05 M-14 (M14.2)
**Depends on** — T-3.4.2
**Pattern to follow** — T-3.1.2 (preview before save)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: S | Confidence: L
  (inferred) — Oracle Ambiguity High: the server cannot count lines that exist only on tablets (MI-44), so the message's content needs a decision first.

**Provisional commit message**

```
feat(coverage): state the consequence before removing a permission

- Removal takes effect at the rep's next sync and captured lines still
  send; saying so first stops a manager assuming stock is recalled
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Small, but the wording depends on a data decision.

**Work package**

Increments:
1. Decide what the count can honestly be based on (see decision points), then fix the message wording.
2. Single-rep removal message before save (CV009-E).
3. Multi-rep removal message shown once (CV009-F).

Decision points:
- The server only knows orders that have synced. Count lines on synced-but-Pending orders, show no count ("lines already on unsent orders will still send"), or count from the last snapshot upload? (MI-44)
- Is the message a confirmation step or feed-forward text beside the Save button (M14.2)?

Delegable slivers:
- **Removal message UI** — Once the wording is agreed, show it before saving a removal on both M-14 views, using the existing removal handler. Do not change the handler or record.

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | CV-001 (T-3.1.1–T-3.1.5), CV-002 (T-3.2.1, T-3.2.2), CV-007 (T-3.3.1; overview count → E14), CV-009 (T-3.4.1–T-3.4.3) |
| Every task satisfies the three slice criteria | Pass | 11 of 11 |
| Every task carries a tier with a rationale citing dimensions | Pass | 11 of 11 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-3.4.1 (MI-03), T-3.4.3 (MI-44) |
| Tasks modifying existing behaviour order characterisation first | Pass | None in this epic modify earlier behaviour |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-3.1.5, T-3.2.1, T-3.3.1 reuse reviewed flows; Blast Low |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-3.1.1, T-3.1.4, T-3.4.1, T-3.4.3 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-3.1.2, T-3.1.3, T-3.1.4, T-3.2.2, T-3.4.2 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-3.1.1, T-3.4.1, T-3.4.3 |
| Every scenario task precedes the task it gates | Pass | 7 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-03, 44, 45, 51 |
