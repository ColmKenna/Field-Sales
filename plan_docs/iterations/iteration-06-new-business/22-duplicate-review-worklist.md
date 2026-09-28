# E22 — Duplicate review and the Worklist

**Iteration** — 6, New business
**Outcome** — When a rep's new prospect looks like a shop we already have, head office sees both side by side with the existing shop's own ordering pattern, decides what it is, and the rep hears the answer at their next sync; the Worklist becomes the typed list of things that need a person.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Prospecting US-006 — Decide a duplicate | Must | S1–S6 |
| 2 | Head Office US-005b — Decide a Duplicate Match | Must | S1–S3 |
| 3 | Head Office US-008 — Orders within policy go through without acceptance (part) | Must | S4, as amended 26 Sep 2026 (range proposals and duplicate matches only); the rest is in E7, E8 and E29 |
| 4 | Rep at a Location US-022 — Create and complete a prospect (part) | Must | S5; S1–S4 are in E21 |

**Exit criterion** — At sync, the server compares each new prospect with existing Locations and other new prospects, and raises a likely match as a Duplicate match item on the Worklist, labelled by type with how long it has waited. Head office opens it to see both records and the existing Location's owner, last order and usual frequency, with no stale flag. They decide: keep with the current rep, hand to the cold-calling rep, merge prospects, or dismiss. The rep sees the outcome, in plain words, on the prospect and on Home after their next sync.

**Capability-class stamp** — Frontier + extended reasoning for matching and the outcomes (T-22.1.1, T-22.3.2); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [prospecting-and-leads.md](../../stories/prospecting-and-leads.md) (US-006, glossary, Requires Clarification 2), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-005b, US-008 S4), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-022 S5), [02-head-office.md](../../uxdocs/02-head-office.md) (H-01 H1.1; H-05 H5.1–H5.4 — draft, MI-61), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-10 T10.5), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (Head Office US-008 S4 amendment).

---

### T-22.1.1-S — Test scenarios for matching new prospects

**Owner** — Human-Led
**Gates** — T-22.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-29: what counts as a likely match — name plus Town (assumed), address, spelling tolerance ("Byrne's Chemist" vs "Byrne's Pharmacy", "Byrnes")? Agree examples from real messy Location names.
- "Byrne's Chemist, Rathdrum" matching an existing Location on name and Town raises one item (S1).
- Two reps create the same prospect the same day: both match each other (S6). One item or two?
- A prospect matching several existing Locations: one item listing them, or one per match?
- A prospect edited after sync (completed, T-21.5.1): re-matched?
- Archived or permanently closed Locations: candidates?
- The rep is never blocked and can't know in advance (design decision).

---

### T-22.1.1 — Match each new prospect against existing Locations at sync

**Parent story**

> As a Sales Manager, I want to see when a new prospect matches an existing Location, with that Location's ordering history, so that I can tell a live account from a lapsed one and reply to the rep.
>
> Acceptance criteria:
> - A synced prospect "Byrne's Chemist, Rathdrum" matching an existing Location on name and Town raises a Duplicate Match item with both records side by side (S1; Head Office US-005b S1)
> - Two reps creating the same prospect both match each other at sync (S6)

**Slice** — When a new prospect uploads, the server compares it with existing Locations and other prospects by name, address and Town, and a likely match raises a Duplicate match item for head office, without ever blocking the rep.
**Spec source** — Prospecting US-006 S1, S6; glossary (Duplicate Match); design decision "Cold-calling is never blocked; duplicates go to head office"; Requires Clarification 2; Head Office US-005b S1
**Depends on** — T-21.3.1, T-4.1.2
**Pattern to follow** — T-4.1.2 (server-side processing of uploaded work)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — the matching rule is unagreed (MI-29) and fuzzy (Oracle Ambiguity High); false negatives cost two reps chasing one shop.

**Provisional commit message**

```
feat(prospects): match new prospects against existing locations at sync

- The rep can't see other reps' shops and is offline anyway, so a check
  at capture is impossible; a person decides what a match means
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A fuzzy rule whose errors land on people.

**Work package**

Increments:
1. Agree the matching rule with real examples (MI-29) and write it as test vectors.
2. Normalisation (case, punctuation, common words such as Chemist/Pharmacy — per the agreed rule) and comparison on name, address and Town.
3. Run at upload for each new prospect against Locations and other unconverted prospects; record candidate matches.
4. Raise a Duplicate match item per the agreed grouping; never block the upload.
5. Re-run on edits per the agreed rule.

Decision points:
- The rule and its tolerance (MI-29).
- Grouping of several candidates.
- Which Locations are candidates (archived, closed).

Delegable slivers:
- **Name normaliser** — Implement the agreed normalisation of Location names and addresses as a pure function with the agreed vectors. No matching.
- **Match item record** — Store a Duplicate match item linking the new prospect and its candidates with creation time, for the Worklist to list. No matching logic.

---

### T-22.2.1-S — Test scenarios for the typed Worklist

**Owner** — Scenario Review
**Gates** — T-22.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Worklist of typed decision items (task
T-22.2.1). Read plan_docs/stories/head-office-order-processing.md US-008 S4,
S5 and the S4 amendment in plan_docs/uxdocs/04-user-stories-amendments.md
(range proposals and duplicate matches only), US-005 S1 (labels survive sort
and filter), and plan_docs/uxdocs/02-head-office.md H-01 (H1.1, H1.8, H2.9
line). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Start with characterisation scenarios pinning
T-7.1.2's empty state, the held-orders line and View orders. Cover the story
scenarios for duplicate matches (range proposals join in E23), then derivable
edges: sorting by waiting, an item decided by someone else while open. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-22.2.1 — List duplicate matches on the Worklist by type, with how long each has waited

**Parent story**

> As a Head Office User, I want orders that are within policy to go through without my acceptance so that my worklist only holds things that genuinely need a decision.
>
> Acceptance criteria:
> - The Worklist contains only range proposals and duplicate matches, each labelled with its type (S4, amended 26 Sep 2026)
> - With none, it reads "Nothing needs a decision." with no action (S5)
> - Items keep their type label under any sort or filter (Head Office US-005 S1; H1.1)

**Slice** — Head office's Worklist lists each item needing a decision with its Type, what it concerns, why, and how long it has waited, counts them in the header, keeps the type label under any sort, and opens each item; the empty state, held-orders line and View orders link stay as they are.
**Spec source** — Head Office US-008 S4 (amended), S5; US-005 S1 (labels); uxdocs 02 H-01 (H1.1 Type is a column, H1.8, H2.9 line)
**Depends on** — T-7.1.2, T-7.2.2, T-22.1.1
**Pattern to follow** — T-7.1.2 (H-01 landing)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the H-01 landing (characterisation first); the layout is settled.

**Provisional commit message**

```
feat(head-office): list decision items on the worklist by type

- Labels must survive re-sorting, so type is a column rather than a
  section heading
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A settled screen behind a characterisation pass.

**Agent prompt**

```
Role: You are turning head office's Worklist (H-01) into a typed list of
decision items in the Field Sales Management System.

Context:
- Slice: head office's Worklist lists each item needing a decision with its
  Type, what it concerns, why, and how long it has waited, counts them in the
  header, keeps the type label under any sort, and opens each item; the
  empty state, held-orders line and View orders link stay as they are.
- Specs: plan_docs/stories/head-office-order-processing.md US-008 S4 (see the
  amendment in plan_docs/uxdocs/04-user-stories-amendments.md: range
  proposals and duplicate matches only), S5, US-005 S1;
  plan_docs/uxdocs/02-head-office.md H-01 (H1.1, H1.8, H2.9 line).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — H-01 (T-7.1.2), held
  orders line (T-7.2.2), duplicate match items (T-22.1.1).
- Pattern to follow: T-7.1.2.

Acceptance criteria:
1. Columns TYPE, ITEM, WHY, WAITING; a duplicate row reads e.g. "Duplicate
   match | Quinn's, Rathdrum (new) | matches Quinn's Centra, last ordered 14
   Mar 2026 | 2d 1h".
2. The header counts items: "3 need a decision".
3. Type labels stay on every row under any sort or filter.
4. A row opens its decision screen (H-05 for duplicate matches).
5. A decided item leaves the list.
6. With no items, "Nothing needs a decision." and nothing else changes; the
   held-orders line and View orders link remain.
7. The item model is general so Range proposals (E23) add a type without
   changing the list.

Constraints:
- Use the project's existing conventions and test framework.
- No orders on the Worklist (H1.6).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
H-01's empty state, held-orders line and View orders link; stop and show them
passing. Resume only on "Continue T-22.2.1".

Steps: 1. characterisation tests; 2. item model; 3. list and columns;
4. count and sort; 5. open item; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-22.2.1-S; do not design
your own.

Definition of done: head office's Worklist lists each item needing a
decision with its Type, what it concerns, why, and how long it has waited,
counts them in the header, keeps the type label under any sort, and opens
each item; the empty state, held-orders line and View orders link stay as
they are.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: range proposals (E23), account requests (M-17, E25).
```

**Checkpoint**

Produces before pausing — characterisation tests of H-01, passing.
Human reviews — Do the tests pin today's landing exactly, including the held-orders line?
Resume trigger — `Continue T-22.2.1`

---

### T-22.3.1-S — Test scenarios for the duplicate review screen

**Owner** — Human-Led
**Gates** — T-22.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "previously ordered roughly every 5 weeks": how is the frequency computed (median gap between accepted orders over what window)? How is it rounded into words ("roughly every 5 weeks", "roughly monthly")?
- A Location with one order, or none: what does the line say?
- "Primary: Colm · Last order 14 Mar 2026" — the Primary Rep with its source? An unassigned Location?
- No stale flag is ever applied by the system (HO-005b S2; design decision "Stale is judged, not calculated").
- A match against another prospect: the right-hand side shows the prospect's creator and date instead of ordering history?
- Several candidates: one column each, or a chooser?

---

### T-22.3.1 — Show a duplicate match side by side with the shop's own ordering pattern

**Parent story**

> As a Head Office User, I want a rep's new prospect shown beside the Location it matched, with that Location's ordering pattern, so that I can tell a live account from a lapsed one and reply to the rep.
>
> Acceptance criteria:
> - A match appears labelled "Duplicate match — Byrne's Chemist, Rathdrum" with both records side by side (S1)
> - The existing Location shows "Primary: Colm · Last order 14 Mar 2026 · previously ordered roughly every 5 weeks", and no stale flag is applied by the system (S2; Prospecting US-006 S2)

**Slice** — Opening a duplicate match shows the new prospect (who, when, what they noted) beside the existing Location with its owner, last order and usual ordering frequency, so head office judges lapsed or live without the system calling it stale.
**Spec source** — Head Office US-005b S1, S2; Prospecting US-006 S2; design decision "Stale is judged, not calculated — for customers"; uxdocs 02 H-05 (draft, MI-61)
**Depends on** — T-22.2.1, T-3.2.1, T-7.1.2
**Pattern to follow** — T-3.2.1 (who covers a shop and why)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — the frequency wording needs a human oracle; H-05 is a draft.

**Provisional commit message**

```
feat(head-office): show a duplicate match beside the shop's own pattern

- A yearly buyer quiet for six months is normal and a monthly buyer is
  not; the shop's own rhythm lets the manager judge at a glance
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A read screen with a human-defined summary.

**Agent prompt**

```
Role: You are building the duplicate review screen (H-05) for the Field Sales
Management System's head office website.

Context:
- Slice: opening a duplicate match shows the new prospect (who, when, what
  they noted) beside the existing Location with its owner, last order and
  usual ordering frequency, so head office judges lapsed or live without the
  system calling it stale.
- Specs: plan_docs/stories/head-office-order-processing.md US-005b S1, S2;
  plan_docs/stories/prospecting-and-leads.md US-006 S2 and design decision
  "Stale is judged, not calculated — for customers";
  plan_docs/uxdocs/02-head-office.md H-05.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — match items (T-22.1.1),
  Worklist (T-22.2.1), coverage (T-3.2.1), orders (T-7.1.2).
- Pattern to follow: T-3.2.1.

Acceptance criteria:
1. The page is titled "Duplicate match — Byrne's Chemist, Rathdrum".
2. Left: the new prospect with its rep and date, name, address, Likely to
   buy and notes.
3. Right: the existing Location with "Primary: Colm · Last order 14 Mar 2026
   · previously ordered roughly every 5 weeks", worded per the agreed rule.
4. No stale label or flag appears anywhere.
5. A match against another prospect shows that prospect's rep and date
   instead of ordering history.

Constraints:
- Use the project's existing conventions and test framework.
- Read-only; decisions are T-22.3.2's.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the frequency wording passes every agreed scenario, stop and
show the page. Resume only on "Continue T-22.3.1".

Steps: 1. frequency rule; 2. page layout; 3. prospect-to-prospect variant;
4. tests.

Test expectations: implement exactly the scenarios agreed in T-22.3.1-S. You
are forbidden from designing your own test cases.

Definition of done: opening a duplicate match shows the new prospect (who,
when, what they noted) beside the existing Location with its owner, last
order and usual ordering frequency, so head office judges lapsed or live
without the system calling it stale.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the four outcomes (T-22.3.2).
```

**Checkpoint**

Produces before pausing — the frequency wording passing every agreed scenario, and the page.
Human reviews — Would a manager read "previously ordered roughly every 5 weeks" the way the order history actually runs?
Resume trigger — `Continue T-22.3.1`

---

### T-22.3.2-S — Test scenarios for deciding a duplicate

**Owner** — Human-Led
**Gates** — T-22.3.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Lapsed, handed to the cold-calling rep: the prospect is discarded, the Location moves to that rep through Coverage Management with M-08's impact shown (H5.2), and the rep sees "Already a customer — now assigned to you, last ordered 14 Mar 2026" (S3).
- Another rep's live account: the prospect is discarded; the rep sees "Already a customer, covered by Colm" (S4).
- Genuinely new: the match is dismissed and the prospect stands (S5).
- Two reps, same prospect: merge keeps one prospect with both reps' notes (S6). Whose prospect survives? Offered only when the match is another prospect (H5.4).
- The discarded prospect's calls, orders and notes: re-pointed to the existing Location, kept on a closed record, or lost? An order Pending at the cut-off against a discarded prospect?
- The linked lead (E21): Converted to the existing Location instead?
- The note to the rep is optional; the outcome sentence is generated (H5.3).
- Hand-over when the Location is covered by a territory rule: a direct assignment overriding it, with Assignment History?

---

### T-22.3.2 — Decide a duplicate: keep, hand over, merge or dismiss

**Parent story**

> As a Sales Manager, I want to see when a new prospect matches an existing Location, with that Location's ordering history, so that I can tell a live account from a lapsed one and reply to the rep.
>
> Acceptance criteria:
> - Lapsed and handed to the cold-calling rep: the prospect is discarded, the Location moves to that rep (Coverage Management), and the rep sees "Already a customer — now assigned to you, last ordered 14 Mar 2026" (S3)
> - Stays with Colm: the prospect is discarded and the rep sees "Already a customer, covered by Colm" (S4)
> - Dismissed: the prospect stands as a new Location (S5)
> - Two reps' same prospect: merged, keeping one prospect with both reps' notes (S6)
> - Head office can choose any of the four outcomes, and the rep sees the outcome and its reason at next sync (Head Office US-005b S3)

**Slice** — On a duplicate match, head office chooses what it is — the current rep's live customer, a lapsed customer to hand to the cold-calling rep, the same as another prospect, or genuinely new — with an optional note, and the system discards, reassigns, merges or keeps the prospect accordingly, carrying its work across, and queues the outcome for the rep.
**Spec source** — Prospecting US-006 S3–S6; Head Office US-005b S3; uxdocs 02 H-05 (H5.1–H5.4 — draft, MI-61); Coverage Management (direct assignment and Assignment History)
**Depends on** — T-22.3.1, T-3.1.1, T-3.1.2, T-14.5.1, T-21.4.1
**Pattern to follow** — T-3.2.2 (changing a shop's coverage from its page, with preview)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — discarding and merging Locations that may carry calls and Pending orders, plus an ownership change (Blast Radius High); Edge-Case Discovery High.

**Provisional commit message**

```
feat(head-office): decide a duplicate and tell the rep

- A match is often an opportunity, not an error: a lapsed customer can go
  to the rep who just knocked on the door
- The outcome is phrased as what the match is, with its consequence, so
  the choice reads as a judgement rather than an action list
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Record discards and merges with work attached.

**Work package**

Increments:
1. Decide what happens to a discarded prospect's calls, orders, notes and linked lead; write it down.
2. Keep: discard the prospect, carry its work per the rule, queue "Already a customer, covered by Colm".
3. Hand over: M-08 impact preview, a direct Primary Rep assignment with Assignment History (T-3.1.1), handover of open visits (T-14.5.1), discard the prospect, queue "Already a customer — now assigned to you, last ordered 14 Mar 2026".
4. Merge (prospect matches only): keep one, fold in the other's notes and work, queue outcomes to both reps.
5. Dismiss: the prospect stands; queue nothing or a quiet confirmation.
6. H-05 choice list phrased as H5.1, optional note, Decide; item leaves the Worklist.

Decision points:
- The fate of work on a discarded prospect (above).
- Which prospect survives a merge.
- Whether dismissing records anything the rep sees.

Delegable slivers:
- **Outcome sentences** — Generate the rep-facing sentences for each outcome ("Already a customer, covered by Colm", "Already a customer — now assigned to you, last ordered 14 Mar 2026", merge and dismiss per the agreed wording), with the optional note appended. Pure function.
- **Choice list** — Render H-05's four outcomes phrased as H5.1, offering Merge only when the match is another prospect (H5.4). No decision logic.

---

### T-22.4.1-S — Test scenarios for the duplicate outcome on the tablet

**Owner** — Scenario Review
**Gates** — T-22.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing head office's duplicate outcome on
the rep's tablet (task T-22.4.1). Read
plan_docs/stories/rep-at-a-location-tablet.md US-022 S5,
plan_docs/stories/prospecting-and-leads.md US-006 S3–S6, and
plan_docs/uxdocs/01-tablet-day.md T-10 (T10.5) and T-02 (sync outcome
prompts). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Start with characterisation scenarios pinning
T-4.5.1's Home prompts. Cover each outcome, then derivable edges: the rep has
unsent work on the discarded prospect, the handed-over Location arriving in
the snapshot. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-22.4.1 — Tell the rep head office's answer on a duplicate

**Parent story**

> As a Field Salesperson, I want to record a place I've just called on with what I learned, saving whatever I have, so that I can finish the record later without losing the visit.
>
> Acceptance criteria:
> - When the prospect matched an existing Location at sync, at my next sync I see head office's answer — for example "Already a customer — now assigned to you, last ordered 14 Mar 2026" (S5)

**Slice** — After the next sync, the rep sees head office's answer on a matched prospect in plain words, on the prospect and as a Home prompt, and a handed-over Location appears among their shops.
**Spec source** — Rep at a Location US-022 S5; Prospecting US-006 S3–S6; uxdocs 01 T-10 (T10.5 — draft, MI-61), T-02 (sync outcome prompts)
**Depends on** — T-22.3.2, T-4.5.1
**Pattern to follow** — T-4.5.1 (Home prompts), T-8.5.1 (a prompt kept until acknowledged)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies Home (characterisation first); sentences come from T-22.3.2.

**Provisional commit message**

```
feat(tablet): tell the rep head office's answer on a duplicate

- Being told weeks later that a shop was already someone's account is
  the pain; the answer arrives at the next sync, in words
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A tablet prompt behind a characterisation pass.

**Agent prompt**

```
Role: You are showing duplicate outcomes on the tablet in the Field Sales
Management System.

Context:
- Slice: after the next sync, the rep sees head office's answer on a matched
  prospect in plain words, on the prospect and as a Home prompt, and a
  handed-over Location appears among their shops.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-022 S5;
  plan_docs/stories/prospecting-and-leads.md US-006 S3–S6;
  plan_docs/uxdocs/01-tablet-day.md T-10 (T10.5), T-02.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — outcomes and sentences
  (T-22.3.2), Home prompts (T-4.5.1), prompt pattern (T-8.5.1).
- Pattern to follow: T-4.5.1's prompts.

Acceptance criteria:
1. After sync, a handed-over match shows "Already a customer — now assigned
   to you, last ordered 14 Mar 2026" on Home and on the prospect's record.
2. A kept match shows "Already a customer, covered by Colm".
3. Merge and dismiss outcomes show their agreed sentences.
4. A handed-over Location is in the rep's search and snapshot.
5. The prompt clears when the rep opens it.

Constraints:
- Use the project's existing conventions and test framework.
- Show the stored sentence; compose nothing on the tablet.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
Home's prompts; stop and show them passing. Resume only on
"Continue T-22.4.1".

Steps: 1. characterisation tests; 2. outcome in the snapshot; 3. Home
prompt; 4. prospect record; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-22.4.1-S; do not design
your own.

Definition of done: after the next sync, the rep sees head office's answer on
a matched prospect in plain words, on the prospect and as a Home prompt, and
a handed-over Location appears among their shops.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: notifications outside the tablet.
```

**Checkpoint**

Produces before pausing — characterisation tests of Home's prompts, passing.
Human reviews — Do the tests pin today's Home prompts exactly?
Resume trigger — `Continue T-22.4.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | PL-006 (T-22.1.1, T-22.3.1, T-22.3.2), HO-005b (T-22.1.1, T-22.3.1, T-22.3.2), HO-008 S4 (T-22.2.1), A1-022 S5 (T-22.4.1) |
| Every task satisfies the three slice criteria | Pass | 5 of 5 |
| Every task carries a tier with a rationale citing dimensions | Pass | 5 of 5 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-29 (matching), MI-61 (H-05, T-10 drafts); fate of work on a discarded prospect (T-22.3.2 decision) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-22.2.1 (H-01), T-22.4.1 (Home) |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-22.1.1, T-22.3.1, T-22.3.2 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-22.2.1, T-22.3.1, T-22.4.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-22.1.1, T-22.3.2 |
| Every scenario task precedes the task it gates | Pass | 5 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-29, 61 |
