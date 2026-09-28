# E23 — Master view and agreed ranges

**Iteration** — 7, Chains
**Outcome** — A chain's head office is assigned the catalogue Ranges it has agreed to carry; a rep at the chain's master sees its branches, ranges and open proposals offline and records a Range Review proposing whole Ranges to add or remove; head office confirms or rejects each Range from the Worklist; and every branch's Order Pad opens on the chain's ranges.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Master & Branch US-001 — View a master's branches and Agreed Range | Must | S1–S7 |
| 2 | Master & Branch US-002 — Record a Range Review and propose changes | Must | S2, S4, S5, S6 (S1, S3 superseded by T12.2) |
| 3 | Master & Branch US-003 — Confirm a Range Proposal per product | Must (Draft) | S1–S4, per Range as amended (duplicates Head Office US-005 — MI-31) |
| 4 | Head Office US-005 — Decide a Range Proposal | Must | S1–S3, per Range |
| 5 | Master & Branch US-004 — See agreed products on a branch Order Pad | Must | S1–S4, as amended by Rep at a Location US-023 S6–S8 |
| 6 | Rep at a Location US-023 — Work at a Master Location (part) | Must | S1, S2 (as amended), S5 (superseded marker), S6–S8; S3, S4 → E24 |

Also carried here, with no story: head office assigning Ranges to a chain directly (`{{NEEDS ACCEPTANCE CRITERIA}}`, MI-17).

**Exit criterion** — Head office can assign catalogue Ranges to a chain's Master Location. On the tablet, offline, the master view shows branches (Closed excluded, Temporarily Closed flagged, other reps' branches named), each Agreed Range with its products, availability and price, and open proposals, with "Start multi-branch order" as the primary action. A Range Review on a call proposes adding or removing whole Ranges, subject to head office confirmation. Head office confirms or rejects each Range from a labelled Worklist item, and the rep sees the outcome on the call. At every branch, the Order Pad opens with the chain's ranges as stacked sections, oldest first, each product once.

**Capability-class stamp** — Frontier workhorse for all tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [master-branch-ordering.md](../../stories/master-branch-ordering.md) (US-001–US-004, glossary, Requires Clarification), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-005), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-023), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-008), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-11 T11.1; T-12 T12.2; T-07 T7.14–T7.18), [02-head-office.md](../../uxdocs/02-head-office.md) (H-01 H1.1; H-04 H4.1–H4.4 — draft, MI-32; H-26 H26.3 — draft).

---

### T-23.1.1-S — Test scenarios for assigning Ranges to a chain

**Owner** — Human-Led
**Gates** — T-23.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-17: how does head office assign Ranges to a Master Location — on H-26, the Master Location's record (drafting call H26.3 puts an Agreed ranges section there), on H-18 (the Range), or both? Confirm H26.3 or replace it. Is it the same mechanism as a rep's assigned Ranges (T-9.8.1)?
- Only Active Ranges can be assigned (T12.2's rule for proposals); what happens to an assigned Range that is archived (MI-18)?
- Per-branch variations: every branch gets all of the chain's Ranges (MI-30 — still open)?
- Assignment history: who assigned which Range and when?
- Go-live: are chains' existing agreements loaded (MI-06)?
- Does assigning a Range to a chain change its availability anywhere (the any-active-range rule is about Range status, not assignment)?

---

### T-23.1.1 — Assign catalogue Ranges to a chain as its Agreed Ranges

**Parent story**

> As a Field Salesperson, I want the Master Location screen to show its branches, the confirmed Agreed Range and any open proposals so that I walk into the buyer's office knowing what stands.
>
> Acceptance criteria:
> - A chain's Agreed Ranges are ordinary catalogue Ranges assigned to the Master Location; a chain may be assigned several, and a product may be in several (BR-NEW-008 rules 0, 1)
> - Head office creates Ranges; reps never do (BR-NEW-008 rule 5)
> - Hickey's Head Office with "Everyday" and "2026 Christmas gift packs" shows both by name, each with its own products (US-001 S5, S6)
> - {{NEEDS ACCEPTANCE CRITERIA}} for head office assigning and removing a chain's Ranges directly (MI-17)

**Slice** — On a chain's Master Location record, head office assigns and removes catalogue Ranges as the chain's Agreed Ranges, with who and when recorded, and those assignments are what every later chain screen reads.
**Spec source** — BR-NEW-008 rules 0, 1, 5; Master & Branch US-001 S5, S6 and amendment; Requires Clarification 2, 6, 8; uxdocs 02 H-26 (H26.3 — Agreed ranges section on a master's Location record, a draft)
**Depends on** — T-9.8.1, T-9.1.1, T-12.2.1
**Pattern to follow** — T-9.8.1 (assigning Ranges)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — no story defines assignment (MI-17) and per-branch variation is open (MI-30), so Oracle Ambiguity is High; the mechanism exists from T-9.8.1.

**Provisional commit message**

```
feat(chains): assign catalogue ranges to a chain as its agreed ranges

- A chain's agreed range is an ordinary range assigned to it; nothing is
  copied, so a range change reaches every chain that takes it
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A missing story settled first, then reuse.

**Agent prompt**

```
Role: You are adding chain range assignment to the head office website of the
Field Sales Management System.

Context:
- Slice: on a chain's Master Location record, head office assigns and removes
  catalogue Ranges as the chain's Agreed Ranges, with who and when recorded,
  and those assignments are what every later chain screen reads.
- Specs: plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-008;
  plan_docs/stories/master-branch-ordering.md US-001 S5, S6 and Requires
  Clarification 2, 6, 8; plan_docs/uxdocs/02-head-office.md H-26 (H26.3). The
  acceptance criteria for assignment itself are agreed in T-23.1.1-S
  (MI-17); do not start until they exist.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — rep Range assignment
  (T-9.8.1), Ranges (T-9.1.1), master Locations (T-12.2.1).
- Pattern to follow: T-9.8.1's assignment record.

Acceptance criteria:
1. On a Master Location's record, head office can assign Active catalogue
   Ranges and remove assigned ones, per the agreed criteria.
2. A chain can hold several Ranges; a product may be in several.
3. Each assignment records who and when.
4. Assignments apply to every branch of the master (MI-30 open: no
   per-branch variation).
5. Hickey's Head Office with "Everyday" and "2026 Christmas gift packs"
   lists both by name with their product counts.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-9.8.1's assignment mechanism if the scenarios agree it is shared.
- No tests of framework internals or trivial members.
- This task opts in to chain range assignment storage.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after assignment works on the record against every agreed
scenario, stop and show it. Resume only on "Continue T-23.1.1".

Steps: 1. assignment record; 2. record page section; 3. history; 4. tests.

Test expectations: implement exactly the scenarios agreed in T-23.1.1-S. You
are forbidden from designing your own test cases.

Definition of done: on a chain's Master Location record, head office assigns
and removes catalogue Ranges as the chain's Agreed Ranges, with who and when
recorded, and those assignments are what every later chain screen reads.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: proposals (T-23.3.1, T-23.4.1), customer range assignment for
self-service (E26).
```

**Checkpoint**

Produces before pausing — assignment on the Master Location record, passing every agreed scenario.
Human reviews — Is this the one place a chain's ranges are set outside a proposal, and does it match what the business agreed for MI-17?
Resume trigger — `Continue T-23.1.1`

---

### T-23.2.1-S — Test scenarios for the master view on the tablet

**Owner** — Scenario Review
**Gates** — T-23.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the tablet's Master Location view (task
T-23.2.1). Read plan_docs/stories/master-branch-ordering.md US-001 S1–S7 and
assumptions, plan_docs/stories/rep-at-a-location-tablet.md US-023 S1, and
plan_docs/uxdocs/01-tablet-day.md T-11 (T11.1). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning T-4.2.2's Location screen for an ordinary
shop. Cover the story scenarios, then derivable edges: a master whose branches
are all closed, a branch moved to another master, a master that is also a
shop versus a head office holding no stock. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-23.2.1 — Show a chain's branches, ranges and proposals on the master view, offline

**Parent story**

> As a Field Salesperson, I want the Master Location screen to show its branches, the confirmed Agreed Range and any open proposals so that I walk into the buyer's office knowing what stands.
>
> Acceptance criteria:
> - Hickey's Head Office, 12 branches, offline: "12 branches" (Closed excluded, 1 "Closed until 14 Oct" flagged), "Agreed range: 42 products", "1 proposal awaiting head office" (S1; Rep at a Location US-023 S1)
> - The Agreed Range lists its products with availability and price; Unavailable ones are labelled (S2)
> - 4 branches with another Primary Rep show "Primary: Aoife" and can still be included in a Multi-Branch Order (S3)
> - An ordinary Location shows no branches or Agreed Range section (S4)
> - Several Agreed Ranges are each available by name with their own products (S5, S6)
> - "Start multi-branch order" is the one primary action; "Record call" is secondary (S7, T11.1)

**Slice** — Opening a chain's master on the tablet, offline, shows its branches with closures and other reps' ownership, each Agreed Range with its products' availability and price, and proposals awaiting head office, with the multi-branch order as the primary action; ordinary shops are unchanged.
**Spec source** — Master & Branch US-001 S1–S7, assumptions (branches in the snapshot even where not Primary); Rep at a Location US-023 S1; uxdocs 01 T-11 (T11.1, confirmed frame)
**Depends on** — T-23.1.1, T-4.2.2, T-4.1.1, T-12.2.1, T-17.1.1, T-17.2.1, T-6.4.2, T-8.3.1
**Pattern to follow** — T-4.2.2 (Location screen)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — extends the snapshot's scope to branches outside the rep's book (through T-4.1.1's versioning); the screen is settled.

**Provisional commit message**

```
feat(tablet): show a chain's branches, ranges and proposals at its master

- A rep at a chain's head office is there to take the chain order, so
  that action leads and the range review sits under Record call
- Branches in other reps' books are included, since a chain order can
  land in any of them
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A settled screen with a snapshot extension.

**Agent prompt**

```
Role: You are building the Master Location view (T-11) for the Field Sales
Management System's tablet app.

Context:
- Slice: opening a chain's master on the tablet, offline, shows its branches
  with closures and other reps' ownership, each Agreed Range with its
  products' availability and price, and proposals awaiting head office, with
  the multi-branch order as the primary action; ordinary shops are
  unchanged.
- Specs: plan_docs/stories/master-branch-ordering.md US-001 S1–S7 and
  assumptions; plan_docs/stories/rep-at-a-location-tablet.md US-023 S1;
  plan_docs/uxdocs/01-tablet-day.md T-11 (T11.1 and its confirmed frame).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Location screen
  (T-4.2.2), snapshot (T-4.1.1), master relationship (T-12.2.1), closures
  (T-17.1.1, T-17.2.1), resolved prices (T-6.4.2), availability labels
  (T-8.3.1), chain ranges (T-23.1.1).
- Pattern to follow: T-4.2.2.

Acceptance criteria:
1. For Hickey's Head Office, offline: "12 branches" with Closed branches
   excluded and "Closed until 14 Oct" flagged.
2. Branches with another Primary Rep show "Primary: Aoife".
3. Each Agreed Range is a summary row with its name and product count,
   opening to its products with availability label and price.
4. "1 proposal awaiting head office" shows when one is open (proposals exist
   from T-23.3.1; show none until then).
5. "Start multi-branch order" is the one primary action; "Record call" is
   secondary; "New order for this shop" appears only for a master that is
   also a shop.
6. An ordinary Location shows none of this.
7. The snapshot for a rep holding a master includes its branches even where
   the rep isn't their Primary Rep.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot changes go through T-4.1.1's versioning and its owner's review.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-4.2.2's Location screen for an ordinary shop; stop and show them passing,
then the master view on a device. Resume only on "Continue T-23.2.1".

Steps: 1. characterisation tests; 2. snapshot scope; 3. master view;
4. ranges detail; 5. actions; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-23.2.1-S; do not design
your own.

Definition of done: opening a chain's master on the tablet, offline, shows
its branches with closures and other reps' ownership, each Agreed Range with
its products' availability and price, and proposals awaiting head office,
with the multi-branch order as the primary action; ordinary shops are
unchanged.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the multi-branch order itself (E24), Range Review (T-23.3.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of the ordinary Location screen, passing, and the master view on a device.
Human reviews — Does the master view match T-11's confirmed frame, and is the ordinary shop untouched?
Resume trigger — `Continue T-23.2.1`

---

### T-23.3.1-S — Test scenarios for a Range Review on the call

**Owner** — Scenario Review
**Gates** — T-23.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for recording a Range Review on a call (task
T-23.3.1). Read plan_docs/stories/master-branch-ordering.md US-002 S2, S4, S5,
S6 and its 26 Sep 2026 amendment, plan_docs/stories/rep-at-a-location-tablet.md
US-023 S2, plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-008 rule 5,
and plan_docs/uxdocs/01-tablet-day.md T-12 (T12.2). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning T-5.3.1's purposes and save
review. Cover the story scenarios, then derivable edges: proposing to add a
Range already proposed on an unsynced call, removing the last Range, a Range
archived after the snapshot. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-23.3.1 — Propose adding or removing whole Ranges on a Range Review call

**Parent story**

> As a Field Salesperson, I want to record what the buyer agreed to add or drop from the chain's range as a proposal on my Call so that head office can confirm it and I never tell a branch something unconfirmed.
>
> Acceptance criteria:
> - Hickey's assigned "Everyday" and "Sun care 2026": adding "2026 Christmas gift packs" and removing "Sun care 2026" saves a Range Proposal of add 1, remove 1, state Proposed; the call review reads "Range review: add 1 range, remove 1 — subject to head office confirmation"; no Range's products change (S6)
> - Channel "Phone" with Purposes "Range Review" and "Pitch" saves both sections on one Call (S2)
> - Before sync, Edit Call can change or remove proposed lines; a forgotten change goes on a Follow-up Call as a new proposal (S4)
> - Aoife at Hickey's Rathdrum sees the Agreed Range without my proposed changes (S5; Rep at a Location US-023 S2)

**Slice** — On a call at a chain's master, the rep adds the Range Review purpose, marks assigned Ranges to remove and picks Active Ranges to add, sees each change on its row with Undo until sync, and saves a proposal subject to head office confirmation that no branch sees.
**Spec source** — Master & Branch US-002 S2, S4, S5, S6 and amendment; BR-NEW-008 rule 5; Rep at a Location US-023 S2; uxdocs 01 T-12 (T12.2, confirmed details)
**Depends on** — T-23.2.1, T-5.3.1, T-5.6.1, T-5.7.1
**Pattern to follow** — T-5.3.1 (call purposes and save review)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the call (characterisation first); T12.2 is settled.

**Provisional commit message**

```
feat(chains): propose whole ranges on a range review call

- A range is shared with every customer and rep assigned it, so a review
  agrees which ranges the chain takes and never edits a range's products
- The change is a proposal, so no branch is told something head office
  hasn't agreed
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new call purpose behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the Range Review purpose (T-12) to the tablet's call in
the Field Sales Management System.

Context:
- Slice: on a call at a chain's master, the rep adds the Range Review
  purpose, marks assigned Ranges to remove and picks Active Ranges to add,
  sees each change on its row with Undo until sync, and saves a proposal
  subject to head office confirmation that no branch sees.
- Specs: plan_docs/stories/master-branch-ordering.md US-002 S2, S4, S5, S6
  and amendment; plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-008
  rule 5; plan_docs/stories/rep-at-a-location-tablet.md US-023 S2;
  plan_docs/uxdocs/01-tablet-day.md T-12 (T12.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — call and save review
  (T-5.3.1), corrections (T-5.6.1), follow-up calls (T-5.7.1), master view
  and chain ranges (T-23.2.1, T-23.1.1).
- Pattern to follow: T-5.3.1.

Acceptance criteria:
1. At a master, Range Review is offered as a purpose beside Pitch and Stock
   Check, combinable with them on one call.
2. The section lists the chain's assigned Ranges, each with its product count
   and Remove, and Add a range, offering only Active Ranges not already
   assigned.
3. A proposed change shows on its row ("Proposed: add", "Proposed: remove")
   with Undo until the call syncs.
4. Saving shows "Range review: add 1 range, remove 1 — subject to head office
   confirmation" in the save review; the proposal is Proposed.
5. Before sync, Edit Call can change or remove proposed lines; after, a
   forgotten change goes on a Follow-up Call as a new proposal.
6. No branch pad and no other rep sees proposed changes.
7. No Range's products change.

Constraints:
- Use the project's existing conventions and test framework.
- Proposals upload with the call through T-4.1.2.
- No tests of framework internals or trivial members.
- This task opts in to proposal storage.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-5.3.1's purposes and save review; stop and show them passing. Resume only
on "Continue T-23.3.1".

Steps: 1. characterisation tests; 2. purpose; 3. section; 4. save review;
5. corrections and follow-ups; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-23.3.1-S; do not design
your own.

Definition of done: on a call at a chain's master, the rep adds the Range
Review purpose, marks assigned Ranges to remove and picks Active Ranges to
add, sees each change on its row with Undo until sync, and saves a proposal
subject to head office confirmation that no branch sees.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: head office's decision (T-23.4.1), the outcome on the call
(T-23.4.2).
```

**Checkpoint**

Produces before pausing — characterisation tests of the call's purposes and save review, passing.
Human reviews — Do the tests pin today's call exactly?
Resume trigger — `Continue T-23.3.1`

---

### T-23.4.1-S — Test scenarios for deciding a Range Proposal

**Owner** — Human-Led
**Gates** — T-23.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- 2 proposals among the Worklist items: first, labelled "Range proposal — Hickey's Pharmacies", label kept under sort and filter (HO-005 S1; MB-003 S1). "First" under any sort, or only by default?
- Confirm one add and reject one remove with reason "Contracted until year end": Partly Confirmed; the chain is assigned the confirmed Range and keeps the other (HO-005 S2; MB-003 S2).
- Confirm all: Confirmed; branch pads show the new Range as its own section at their next sync (MB-003 S3; H4.4).
- Reject all with reasons: Rejected, assignments unchanged (MB-003 S4). Reason per line (assumed) or per proposal (MB RC 1)?
- The proposing rep lost the master since syncing: still decided; outcome shown on the Call for whoever holds it now (HO-005 S3).
- A proposed Range archived before the decision; two open proposals for one chain that conflict; a direct assignment (T-23.1.1) made meanwhile.
- Save blocked until every line is decided; Reject needs its reason on the row; the result stated before saving (H4.2, H4.3).

---

### T-23.4.1 — Decide a Range Proposal per Range from the Worklist

**Parent story**

> As a Head Office User, I want Range Proposals at the top of my worklist, decided product by product with a reason for anything rejected, so that chain agreements are controlled without blocking their branches.
>
> Acceptance criteria:
> - With 2 Range Proposals waiting, they are first, labelled "Range proposal — Hickey's Pharmacies" and "Range proposal — Doyle Group", and keep the label under any sort or filter (S1; Master & Branch US-003 S1)
> - Confirming 2 lines and rejecting 1 with reason "Contracted until year end" makes the proposal Partly Confirmed, updates the Agreed Ranges, and the rep sees the outcome and reason on their Call (S2; US-003 S2)
> - Confirming every line makes it Confirmed; branch pads show the new Range at their next sync (US-003 S3)
> - Rejecting every line makes it Rejected with the assignments unchanged (US-003 S4)
> - A proposer who has since lost the master still has the proposal decided; the outcome shows on the Call for whoever now holds it (S3)
> - Lines are whole Ranges to assign or remove, decided per Range (amendments 26 Sep 2026, T12.2)

**Slice** — Range proposals appear first on the Worklist, labelled, and open to a decision screen showing the chain's current Ranges above the proposed changes; head office confirms or rejects each Range, a rejection with its reason, sees the result stated before saving, and the chain's assignments change accordingly.
**Spec source** — Head Office US-005 S1–S3; Master & Branch US-003 S1–S4 and amendment (MI-31 duplicate stories); uxdocs 02 H-04 (H4.1–H4.4 — draft, MI-32), H-01 (H1.1)
**Depends on** — T-23.3.1, T-23.1.1, T-22.2.1
**Pattern to follow** — T-22.3.2 (a Worklist decision with an outcome for the rep)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — changes chain assignments that every branch pad reads; H-04 is a draft; conflicting proposals need a human oracle.

**Provisional commit message**

```
feat(head-office): decide range proposals per range from the worklist

- A pending proposal holds up every branch's default, so it sorts first
  and keeps its label under any sort
- Each range is decided on its own, so one contested range doesn't hold
  up the rest of the agreement
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A decision screen against a human oracle.

**Agent prompt**

```
Role: You are adding Range proposal decisions (H-04) to head office's
Worklist in the Field Sales Management System.

Context:
- Slice: Range proposals appear first on the Worklist, labelled, and open to
  a decision screen showing the chain's current Ranges above the proposed
  changes; head office confirms or rejects each Range, a rejection with its
  reason, sees the result stated before saving, and the chain's assignments
  change accordingly.
- Specs: plan_docs/stories/head-office-order-processing.md US-005 S1–S3;
  plan_docs/stories/master-branch-ordering.md US-003 S1–S4 and amendment;
  plan_docs/uxdocs/02-head-office.md H-04 (H4.1–H4.4), H-01 (H1.1).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — proposals (T-23.3.1),
  chain assignments (T-23.1.1), Worklist items (T-22.2.1).
- Pattern to follow: T-22.3.2's decision flow.

Acceptance criteria:
1. Range proposals are Worklist items of type "Range proposal", labelled
   "Range proposal — Hickey's Pharmacies", listed first by default and
   labelled under any sort or filter.
2. H-04 shows the chain's current Ranges above the proposed changes; each
   line starts undecided.
3. Reject needs a reason on the same row; Save is blocked until every line is
   decided.
4. The result (Confirmed, Partly Confirmed, Rejected) is stated before saving.
5. Saving assigns confirmed adds and removes confirmed removes; rejected
   lines change nothing.
6. A proposer who has lost the master doesn't block the decision.
7. Conflicts (archived Range, overlapping proposals) behave as agreed in
   T-23.4.1-S.

Constraints:
- Use the project's existing conventions and test framework.
- The decision is one transaction.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond
  proposal states.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after decisions pass every agreed scenario, stop and show H-04.
Resume only on "Continue T-23.4.1".

Steps: 1. Worklist item type; 2. H-04 screen; 3. per-line decisions;
4. apply to assignments; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-23.4.1-S. You
are forbidden from designing your own test cases.

Definition of done: Range proposals appear first on the Worklist, labelled,
and open to a decision screen showing the chain's current Ranges above the
proposed changes; head office confirms or rejects each Range, a rejection
with its reason, sees the result stated before saving, and the chain's
assignments change accordingly.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the outcome on the rep's call (T-23.4.2), branch pads
(T-23.5.1).
```

**Checkpoint**

Produces before pausing — decisions passing every agreed scenario, and H-04.
Human reviews — Does every combination of decisions leave the chain's Ranges exactly as intended?
Resume trigger — `Continue T-23.4.1`

---

### T-23.4.2-S — Test scenarios for the proposal outcome on the call

**Owner** — Scenario Review
**Gates** — T-23.4.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing a Range proposal's outcome to the
rep (task T-23.4.2). Read plan_docs/stories/head-office-order-processing.md
US-005 S2, S3 and plan_docs/stories/master-branch-ordering.md US-003 S2 and
its amendment. Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning T-7.5.1's sent item for a call. Cover
Confirmed, Partly Confirmed and Rejected, the rep who lost the master, and
the master view's "proposal awaiting head office" count clearing. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-23.4.2 — Show a proposal's outcome on the rep's call and master view

**Parent story**

> As a Head Office User, I want Range Proposals at the top of my worklist, decided product by product with a reason for anything rejected, so that chain agreements are controlled without blocking their branches.
>
> Acceptance criteria:
> - The rep sees the outcome and reason on their Call, e.g. "2 confirmed, 1 rejected — Contracted until year end" (S2; Master & Branch US-003 S2)
> - A proposer who has lost the master: the outcome is shown on the Call for whoever now holds it (S3)

**Slice** — After the next sync, the Range Review call shows each Range's outcome with any rejection reason, the master view's awaiting count clears, and whoever holds the master now sees it.
**Spec source** — Head Office US-005 S2, S3; Master & Branch US-003 S2 and amendment
**Depends on** — T-23.4.1, T-7.5.1, T-23.2.1
**Pattern to follow** — T-7.5.1 (status on a sent item)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies the sent item (characterisation first); wording is specified.

**Provisional commit message**

```
feat(tablet): show a range proposal's outcome on the call

- The buyer will ask whether last month's agreement went through; the
  answer is on the call where it was proposed
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A status display behind a characterisation pass.

**Agent prompt**

```
Role: You are showing Range proposal outcomes on the tablet in the Field Sales
Management System.

Context:
- Slice: after the next sync, the Range Review call shows each Range's
  outcome with any rejection reason, the master view's awaiting count
  clears, and whoever holds the master now sees it.
- Specs: plan_docs/stories/head-office-order-processing.md US-005 S2, S3;
  plan_docs/stories/master-branch-ordering.md US-003 S2 and amendment.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — decisions (T-23.4.1),
  sent item (T-7.5.1), master view (T-23.2.1).
- Pattern to follow: T-7.5.1.

Acceptance criteria:
1. The call shows the proposal's state and a summary such as "2 confirmed,
   1 rejected — Contracted until year end", with each Range's outcome.
2. The master view's "proposal awaiting head office" count clears.
3. If the proposer no longer holds the master, the current holder sees the
   outcome on the master's call history.
4. Confirmed Ranges appear in the master view's Agreed Ranges.

Constraints:
- Use the project's existing conventions and test framework.
- Show stored outcomes; compose nothing on the tablet beyond the summary.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-7.5.1's sent call; stop and show them passing. Resume only on
"Continue T-23.4.2".

Steps: 1. characterisation tests; 2. outcome in the snapshot; 3. call
display; 4. master view; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-23.4.2-S; do not design
your own.

Definition of done: after the next sync, the Range Review call shows each
Range's outcome with any rejection reason, the master view's awaiting count
clears, and whoever holds the master now sees it.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: branch pads (T-23.5.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of the sent call, passing.
Human reviews — Do the tests pin today's sent call exactly?
Resume trigger — `Continue T-23.4.2`

---

### T-23.5.1-S — Test scenarios for agreed ranges on a branch Order Pad

**Owner** — Scenario Review
**Gates** — T-23.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the chain's Agreed Ranges on a branch Order
Pad (task T-23.5.1). Read plan_docs/stories/master-branch-ordering.md US-004
S1–S4, plan_docs/stories/rep-at-a-location-tablet.md US-023 S5–S8 and
amendments, and plan_docs/uxdocs/01-tablet-day.md T-07 (T7.14–T7.18). Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Start with characterisation scenarios pinning the pad as
built in T-9.8.1 and T-20.1.2. Cover the story scenarios, then derivable
edges: an agreed product also found by search, an agreed product outside my
ranges (is it marked "Outside your ranges"?), a Range removed at the next
sync, a restricted product in an agreed Range. Mark undecided cases as "Needs
a decision". Write no test code; change no files.
```

---

### T-23.5.1 — Open a branch's Order Pad on the chain's agreed ranges

**Parent story**

> As a Field Salesperson at a chain branch, I want the Order Pad to include every product the chain has agreed, marked as such, alongside my own ranges so that I order what the branch is meant to stock even when it's outside my ranges.
>
> Acceptance criteria:
> - My Ranges 180, unranged 40, Hickey's agreed 42 with 15 outside my Ranges: the pad at Hickey's Rathdrum lists 235 products (S1)
> - An independent pharmacy's pad has no agreed-range sections (S2)
> - An agreed product now Unavailable shows its label and can't be added (S3)
> - A product outside the agreement is added normally with no warning (S4)
> - The agreed range is a section "Hickey's agreed range (30)" above the normal pad; a product in it appears only there (Rep at a Location US-023 S6)
> - Several ranges are stacked sections, each collapsible, all expanded, oldest first; a product in two appears once, in the first (US-023 S7, S8)

**Slice** — At any branch of a chain, the Order Pad opens with each of the chain's Agreed Ranges as its own collapsible section, oldest first, above the rep's normal pad, each product appearing once across sections and categories, with availability labels and no limit on ordering outside them.
**Spec source** — Master & Branch US-004 S1–S4; Rep at a Location US-023 S5 (superseded marker), S6–S8; uxdocs 01 T-07 (T7.14–T7.18)
**Depends on** — T-23.1.1, T-9.8.1, T-20.1.2, T-5.1.2, T-8.3.1
**Pattern to follow** — T-5.1.2 (collapsible category sections)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the Order Pad's membership and layout (characterisation first); rules are settled.

**Provisional commit message**

```
feat(tablet): open a branch's order pad on the chain's agreed ranges

- What the chain agreed is what a branch most likely orders, so it leads
  the pad even when it's outside the rep's ranges
- Each product appears once, in the oldest range that holds it, so the
  pad never lists the same thing twice
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Pad layout behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the chain's Agreed Ranges to the branch Order Pad on the
tablet in the Field Sales Management System.

Context:
- Slice: at any branch of a chain, the Order Pad opens with each of the
  chain's Agreed Ranges as its own collapsible section, oldest first, above
  the rep's normal pad, each product appearing once across sections and
  categories, with availability labels and no limit on ordering outside
  them.
- Specs: plan_docs/stories/master-branch-ordering.md US-004 S1–S4;
  plan_docs/stories/rep-at-a-location-tablet.md US-023 S5–S8 and amendments;
  plan_docs/uxdocs/01-tablet-day.md T-07 (T7.14–T7.18).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — chain assignments
  (T-23.1.1), pad membership (T-9.8.1, T-20.1.2), pad layout (T-5.1.2),
  availability labels (T-8.3.1), snapshot (T-4.1.1).
- Pattern to follow: T-5.1.2's collapsible sections.

Acceptance criteria:
1. At Hickey's Rathdrum, sections "Hickey's Everyday (30)" and "Hickey's 2026
   Christmas gift packs (12)" appear above the normal pad, both expanded,
   oldest Range first.
2. A product in both ranges appears once, in the first section; the second
   section's count excludes it.
3. A product in a section doesn't appear again in its category.
4. Each section collapses and expands like a category header.
5. The pad's total covers the union: 180 in my Ranges, 40 unranged, and 15
   agreed products outside my Ranges gives 235.
6. An Unavailable agreed product shows its label and can't be added.
7. Products outside the agreement are added with no warning.
8. An independent shop's pad is unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot changes go through T-4.1.1's versioning.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the pad as built by T-9.8.1 and T-20.1.2; stop and show them passing. Resume
only on "Continue T-23.5.1".

Steps: 1. characterisation tests; 2. branch's chain ranges in the snapshot;
3. sections and ordering; 4. one-product-one-place; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-23.5.1-S; do not design
your own.

Definition of done: at any branch of a chain, the Order Pad opens with each
of the chain's Agreed Ranges as its own collapsible section, oldest first,
above the rep's normal pad, each product appearing once across sections and
categories, with availability labels and no limit on ordering outside them.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–8 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: per-branch variations (MI-30), chain pricing (MI-30).
```

**Checkpoint**

Produces before pausing — characterisation tests of the pad, passing.
Human reviews — Do the tests pin today's pad exactly, including specialist brand products?
Resume trigger — `Continue T-23.5.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | MB-001 (T-23.2.1), MB-002 (T-23.3.1), MB-003 (T-23.4.1, T-23.4.2), HO-005 (T-23.4.1, T-23.4.2), MB-004 (T-23.5.1), A1-023 S1, S2, S5–S8 (T-23.2.1, T-23.3.1, T-23.5.1); chain assignment with no story (T-23.1.1) |
| Every task satisfies the three slice criteria | Pass | 6 of 6 |
| Every task carries a tier with a rationale citing dimensions | Pass | 6 of 6 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-17 (T-23.1.1), MI-30 (per-branch, pricing), MI-31 (duplicate stories), MI-32 (H-04 draft) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-23.2.1, T-23.3.1, T-23.4.2, T-23.5.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-23.1.1, T-23.4.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | All 6 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | None in this epic |
| Every scenario task precedes the task it gates | Pass | 6 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, `{{NEEDS ACCEPTANCE CRITERIA}}` (T-23.1.1), MI-17, 30, 31, 32 |
