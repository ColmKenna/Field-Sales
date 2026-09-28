# E24 — Multi-branch ordering

**Iteration** — 7, Chains
**Outcome** — A rep sitting with a chain's buyer orders for every branch in one conversation — on the tablet offline, product by product, or on a laptop grid — reviews what each branch will get, and saves one ordinary order per branch that counts at the branch and flows through the cut-off like any other.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Master & Branch US-005 — Build a Multi-Branch Order on the tablet | Must | S1–S5 |
| 2 | Master & Branch US-007 — Review the split and save | Must | S1–S5 |
| 3 | Master & Branch US-006 — Build a Multi-Branch Order on the laptop | Should | S1, S2 (as amended by MB006-D, E), S3, S4, MB006-A–F |
| 4 | Master & Branch US-008 — Master that is also a shop | Should | S1–S3 |
| 5 | Rep at a Location US-023 — Work at a Master Location (part) | Must | S3, S4; the rest is in E23 |

**Exit criterion** — From a chain's master, a rep can build a multi-branch session on the tablet, offline. They pick branches (Closed excluded, Temporarily Closed flagged but ticked), enter one quantity per product for all branches and adjust the exceptions, and see a running summary; the session survives closing the app. On the rep website they can build or continue the same session as a grid of recently ordered products against branches, adding Ranges from a picker, where changing "All" never overwrites hand edits. A Split Review shows each branch's lines and units. Saving creates one ordinary In Progress order per branch, with lines, Ordered By the master, For the branch, the rep as Capturing Rep, and linked to any open call. At a master that is also a shop, one call covers the shop's own order and the chain's.

**Capability-class stamp** — Frontier + extended reasoning for the split and cross-surface sessions (T-24.2.1, T-24.3.2); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [master-branch-ordering.md](../../stories/master-branch-ordering.md) (US-005–US-008, glossary, Requires Clarification 4, 7), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-023 S3, S4), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-11 T11.1; T-13 T13.1–T13.5 and T-14 T14.1–T14.4 — drafts, MI-32), [05-manager.md](../../uxdocs/05-manager.md) (M-11 M11.1–M11.4; M11.2 deferred, MI-32).

---

### T-24.1.1-S — Test scenarios for the tablet multi-branch session

**Owner** — Scenario Review
**Gates** — T-24.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for building a multi-branch session on the
tablet (task T-24.1.1). Read plan_docs/stories/master-branch-ordering.md
US-005 S1–S5 and Requires Clarification 4,
plan_docs/stories/rep-at-a-location-tablet.md US-023 S3, and
plan_docs/uxdocs/01-tablet-day.md T-13 (T13.1–T13.5). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: changing "Same for all"
after adjustments (T13.3), unticking a branch with quantities (T13.4), a
product already in the session picked again (T13.2), an Unavailable product,
a session left unfinished (assumed open until saved or discarded, with a
count on Home). Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-24.1.1 — Build a multi-branch session on the tablet, product by product

**Parent story**

> As a Field Salesperson sitting with a chain's buyer, I want to enter one quantity per product for all branches and adjust the exceptions, offline, so that a twelve-shop order takes one conversation.
>
> Acceptance criteria:
> - Starting at Hickey's Head Office ticks all 12 branches; Hickey's Bray (Closed) isn't listed; Hickey's Arklow shows "Closed until 14 Oct" and stays ticked; I untick 2 (S1)
> - "SPF30 Sun Lotion v2 200ml", 24 for all, Wicklow Town 36: "24 × 9 branches, 1 adjusted (Wicklow Town 36)", expanding to the per-branch list (S2; Rep at a Location US-023 S3)
> - Rathdrum set to 0 excludes it from that product only: "24 × 8, 1 adjusted, 1 none" (S3)
> - 6 products entered: the header reads "6 products · 10 branches · 1,380 units" (S4)
> - With no signal, the session is saved as I go and survives closing the app (S5)

**Slice** — From a chain's master, the rep starts a session, confirms which branches take part, and adds products one at a time with a quantity for all branches and per-branch exceptions, seeing a line summary and a session total, all saved on the tablet as they go.
**Spec source** — Master & Branch US-005 S1–S5, design decision "Multi-branch entry differs by surface, shares the outcome", Requires Clarification 4; Rep at a Location US-023 S3; uxdocs 01 T-13 (T13.1–T13.5 — drafts, MI-32)
**Depends on** — T-23.2.1, T-23.5.1, T-5.1.1, T-6.4.2
**Pattern to follow** — T-5.1.1 (offline order saved as it goes), T-23.5.1 (pad layout for the picker)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: C | Confidence: M
  (inferred) — a new offline session object with its own rules; the rules are stated, and T-13 is a draft (MI-32).

**Provisional commit message**

```
feat(chains): build a multi-branch session on the tablet

- The buyer talks product by product, so entry is one product at a time:
  a quantity for all, then the exceptions
- A grid of 360 cells doesn't fit a tablet; the per-branch list only
  opens for the product being entered
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A multi-part entry flow with a review pause.

**Agent prompt**

```
Role: You are building the multi-branch order session (T-13) for the Field
Sales Management System's tablet app.

Context:
- Slice: from a chain's master, the rep starts a session, confirms which
  branches take part, and adds products one at a time with a quantity for
  all branches and per-branch exceptions, seeing a line summary and a session
  total, all saved on the tablet as they go.
- Specs: plan_docs/stories/master-branch-ordering.md US-005 S1–S5 and
  Requires Clarification 4; plan_docs/stories/rep-at-a-location-tablet.md
  US-023 S3; plan_docs/uxdocs/01-tablet-day.md T-13 (T13.1–T13.5).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — master view (T-23.2.1),
  branch pad layout (T-23.5.1), order capture (T-5.1.1), resolved prices
  (T-6.4.2).
- Pattern to follow: T-5.1.1's save-as-you-go.

Acceptance criteria:
1. Starting at Hickey's Head Office ticks all eligible branches; Closed ones
   aren't listed; "Closed until 14 Oct" is flagged and ticked; selection
   collapses to "Branches (10 of 12) [ Change ]".
2. + Add product opens a picker laid out like the branch pad (chain ranges
   first, oldest first, then categories and search); a product already in
   the session opens its existing line.
3. "Same for all" 24 with Wicklow Town 36 reads "24 × 9 branches, 1 adjusted
   (Wicklow Town 36)" and expands to the per-branch list.
4. Setting Rathdrum to 0 excludes it from that product: "24 × 8, 1 adjusted,
   1 none".
5. Changing "Same for all" fills only branches not adjusted by hand.
6. Unticking a branch with quantities is blocked with "Arklow has an active
   order here. Clear its quantities before removing this branch." and opens
   its quantities.
7. The header reads e.g. "6 products · 10 branches · 1,380 units".
8. Quantities are never pre-filled.
9. The session saves as it goes, survives closing the app, and stays open
   until saved or discarded, with a count on Home.

Constraints:
- Use the project's existing conventions and test framework.
- The session is not an order; it produces orders in T-24.2.1.
- No tests of framework internals or trivial members.
- This task opts in to session storage on the tablet.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after branch selection and one product's entry work on a device,
stop and show them. Resume only on "Continue T-24.1.1".

Steps: 1. session storage; 2. branch selection; 3. product picker; 4. line
entry and summary; 5. session header; 6. recovery and Home count; 7. tests
from agreed scenarios.

Test expectations: implement the scenarios agreed in T-24.1.1-S; do not design
your own.

Definition of done: from a chain's master, the rep starts a session, confirms
which branches take part, and adds products one at a time with a quantity
for all branches and per-branch exceptions, seeing a line summary and a
session total, all saved on the tablet as they go.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–9 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Split Review and saving (T-24.2.1), the laptop grid
(T-24.3.1).
```

**Checkpoint**

Produces before pausing — branch selection and one product's entry working on a device.
Human reviews — Can a rep keep up with a buyer talking product by product?
Resume trigger — `Continue T-24.1.1`

---

### T-24.2.1-S — Test scenarios for the split and save

**Owner** — Human-Led
**Gates** — T-24.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Split Review: "Rathdrum — 6 lines, 138 units", each openable (S1); a branch with all zeros shows "No order" and gets none (S3); the button names the result, "Save 9 orders" (T14.2).
- Save: 10 In Progress orders, Ordered By "Hickey's Head Office", For the branch, me as Capturing Rep, dated today, linked to an open Call; session closed (S2). All or none if the tablet dies mid-save?
- Each order is then ordinary: edited, Ready to Send, synced, Pending until the cut-off (S4; BR-NEW-009); "Mark all ready to send" (T14.3).
- Aoife (Rathdrum's Primary Rep) sees it in history with "Ordered by Hickey's Head Office" and, once accepted, "Ordered recently" on her Suggested List (S5).
- Performance counts at For; the capturing rep's "captured" figure (glossary; E28 reads it).
- Prices: each branch's own tiers and promotions (MI-30 chain pricing open)? A break at the branch's quantity, not the chain total?
- A branch closed permanently between selection and save; a branch moved to another master.
- Upload: orders for branches outside the rep's book go through the rep's sync like any order.

---

### T-24.2.1 — Review each branch's share and save one ordinary order per branch

**Parent story**

> As a Field Salesperson, I want to see what each branch will receive before the session becomes twelve orders so that I catch a wrong quantity before it's spread across a chain.
>
> Acceptance criteria:
> - Review shows one row per branch, e.g. "Rathdrum — 6 lines, 138 units", each openable (S1)
> - Save creates 10 In Progress orders, one per branch, Ordered By "Hickey's Head Office", For the branch, me as Capturing Rep, dated today, linked to an open Call; the session closes (S2; Rep at a Location US-023 S4)
> - A branch with 0 on every product shows "No order" and gets none (S3)
> - Each order is then edited, marked Ready to Send, synced and processed as any order (S4)
> - Rathdrum's Primary Rep sees "Ordered by Hickey's Head Office" in its history and, once accepted, its products as "Ordered recently" on the next Suggested List (S5)

**Slice** — The rep reviews lines and units per branch, expanding any, and saving turns the session into one ordinary In Progress order per branch with lines, recording the master as Ordered By, the branch as For and the rep as Capturing Rep; each then goes through Ready to Send, sync and the cut-off like any order.
**Spec source** — Master & Branch US-007 S1–S5, glossary (Ordered By / For, Capturing Rep), design decision "Split orders are ordinary Orders"; Rep at a Location US-023 S4; uxdocs 01 T-14 (T14.1–T14.4 — drafts, MI-32)
**Depends on** — T-24.1.1, T-5.1.1, T-5.4.1, T-7.1.1, T-6.4.1
**Pattern to follow** — T-5.1.1 (order record with Ordered By / For and Capturing Rep)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — creates many orders at once, into other reps' books, with attribution at For (Blast Radius High); pricing per branch is open (MI-30).

**Provisional commit message**

```
feat(chains): split a multi-branch session into one order per branch

- After the split each order is ordinary, carrying the master as Ordered
  By for provenance, so nothing downstream needs a special case
- The review shows each branch's share first, because one wrong quantity
  would otherwise be copied across a chain
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Many orders, attribution and pricing in one save.

**Work package**

Increments:
1. Agree per-branch pricing (MI-30) and the all-or-none rule for the save.
2. Split: from the session, one order per branch with at least one line, lines priced for the branch, Ordered By master, For branch, Capturing Rep, date, Call link; one local transaction.
3. Split Review screen: rows per branch expanding in place, "No order" rows, "Primary: Aoife" on other reps' branches, "Save 9 orders".
4. After save: return to T-11 with "9 orders saved — In Progress", each editable; "Mark all ready to send".
5. Downstream checks: upload through T-4.1.2, Pending and the cut-off (T-7.1.1), the branch's Suggested List through the last-3-accepted rule (T-5.4.1), and history showing "Ordered by Hickey's Head Office".

Decision points:
- MI-30: per-branch prices or a chain price; breaks at branch or chain quantity.
- Atomicity if the save is interrupted.
- Whether "Mark all ready to send" also marks orders the rep has since edited.

Delegable slivers:
- **Split Review screen** — Given the computed per-branch split, render T-14's rows ("Rathdrum — 6 lines, 138 units"), expanding in place, "No order" rows and "Save 9 orders". No saving.
- **Ordered by line** — Show "Ordered by Hickey's Head Office" on an order in the tablet history and on H-02 when Ordered By differs from For. Characterisation tests of both views first.

---

### T-24.3.1-S — Test scenarios for the laptop grid

**Owner** — Scenario Review
**Gates** — T-24.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the multi-branch grid on the rep website
(task T-24.3.1). Read plan_docs/stories/master-branch-ordering.md US-006 S1,
S2, S4 and MB006-A–F, and plan_docs/uxdocs/05-manager.md M-11 (M11.1, M11.3,
M11.4). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Cover the story scenarios, then derivable edges: a
chain with no accepted orders (empty opening), a product in two ranges picked
twice, keyboard-only entry of an "All" value, a branch column deselected
with quantities. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-24.3.1 — Build a multi-branch order on a laptop grid

**Parent story**

> As a Field Salesperson at a desk, I want a grid of products against branches so that I can see and adjust a whole chain's order at once.
>
> Acceptance criteria:
> - Selected branches across, products down, quantity cells, row and column totals (S1)
> - Rows open as the products on any branch's last 3 accepted orders, every cell empty (MB006-A); search adds a row (MB006-B)
> - Several Agreed Ranges: a picker lists them oldest first and adds a chosen range's products as empty rows (MB006-F, replacing MB006-C's "Show full Agreed Range")
> - "All" 24 fills the row; with Rathdrum 12 and Arklow 0 set by hand, changing "All" to 36 sets the other 8 and reads "36 × 8 branches, 2 adjusted" (S2 as amended by MB006-D); "Clear adjustments" returns every cell to "All" (MB006-E)
> - Every cell and the "All" entry are reachable by keyboard (S4)

**Slice** — On the rep website, a rep opens a chain's order as a grid of recently ordered products against selected branches, adds products by search or from a range picker, fills rows with "All" without overwriting hand edits, and ends in the same Split Review and save as the tablet.
**Spec source** — Master & Branch US-006 S1, S2, S4, MB006-A–F; uxdocs 05 M-11 (M11.1, M11.3, M11.4; M11.2 layout deferred, MI-32)
**Depends on** — T-24.2.1, T-23.1.1, T-13.10.1
**Pattern to follow** — T-24.1.1 (same selection and fill rules), T-13.10.1 (orders on the rep website)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: C | Confidence: M
  (inferred) — a dense accessible grid on a settled design; it reuses the split.

**Provisional commit message**

```
feat(chains): build a multi-branch order on a laptop grid

- The grid opens near the size of a typical session, not the whole
  range, and grows one range at a time
- Filling a row never destroys a deliberate exception
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A dense grid with a review pause.

**Agent prompt**

```
Role: You are building the multi-branch order grid (M-11) on the Field Sales
Management System's rep website.

Context:
- Slice: on the rep website, a rep opens a chain's order as a grid of
  recently ordered products against selected branches, adds products by
  search or from a range picker, fills rows with "All" without overwriting
  hand edits, and ends in the same Split Review and save as the tablet.
- Specs: plan_docs/stories/master-branch-ordering.md US-006 S1, S2, S4,
  MB006-A–F; plan_docs/uxdocs/05-manager.md M-11 (M11.1, M11.3, M11.4;
  M11.2 keeps the flat layout for version 1).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — session rules
  (T-24.1.1), split and save (T-24.2.1), chain ranges (T-23.1.1), rep website
  orders (T-13.10.1).
- Pattern to follow: T-24.1.1's selection and fill rules.

Acceptance criteria:
1. Branches across (same selection rules as the tablet), products down,
   quantity cells, row and column totals.
2. Rows open as products on any branch's last 3 accepted orders, all cells
   empty; search adds a row.
3. A range picker, first option "Recently ordered", then the chain's Ranges
   oldest first, adds the chosen range's products not already present as
   empty rows; rows with quantities stay at the top; a product shows once.
4. "All" fills only cells not edited by hand; hand-edited cells are marked;
   the row reads e.g. "36 × 8 branches, 2 adjusted"; "Clear adjustments"
   returns every cell to "All".
5. Every cell and the "All" entry are reachable and editable by keyboard.
6. Review opens the same Split Review; saving creates the same orders as
   T-24.2.1.

Constraints:
- Use the project's existing conventions and test framework.
- The flat grid only (M11.2 deferred).
- Reuse T-24.2.1's split; no second implementation.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the grid opens, fills and keyboard-navigates as specified,
stop and show it before wiring the save. Resume only on "Continue T-24.3.1".

Steps: 1. grid shell and totals; 2. opening rows; 3. search and range picker;
4. All and adjustments; 5. keyboard; 6. Split Review and save; 7. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-24.3.1-S; do not design
your own.

Definition of done: on the rep website, a rep opens a chain's order as a grid
of recently ordered products against selected branches, adds products by
search or from a range picker, fills rows with "All" without overwriting
hand edits, and ends in the same Split Review and save as the tablet.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: continuing a tablet session (T-24.3.2), the customer grid (E26).
```

**Checkpoint**

Produces before pausing — the grid opening, filling and keyboard-navigating as specified, before the save is wired.
Human reviews — Is the grid usable by keyboard alone, and does "All" behave exactly as M11.3 says?
Resume trigger — `Continue T-24.3.1`

---

### T-24.3.2-S — Test scenarios for continuing a tablet session on the laptop

**Owner** — Human-Led
**Gates** — T-24.3.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- A session started on the tablet and synced opens on the website showing what was entered; further edits save to the same session (S3).
- A session started offline is only openable on the laptop after sync (design decision trade-off).
- The rep keeps editing on the tablet after the sync and also on the laptop: which wins? Lock the session to one surface once opened elsewhere? A conflict like T-15.5.1?
- Saving (the split) on one surface while the other still has it open.
- A session started on the laptop: can it continue on the tablet at the next sync?
- Session recovery (MI-32): an abandoned session's lifetime.

---

### T-24.3.2 — Continue a tablet session on the laptop

**Parent story**

> As a Field Salesperson at a desk, I want a grid of products against branches so that I can see and adjust a whole chain's order at once.
>
> Acceptance criteria:
> - A session started on the tablet and synced opens on the website with what I entered, and further edits are saved to the same session (S3)

**Slice** — After a sync, a multi-branch session started on the tablet opens in the laptop grid with everything entered so far, and edits there save to the same session without either surface silently losing the other's work.
**Spec source** — Master & Branch US-006 S3; design decision "Multi-branch entry differs by surface, shares the outcome" (trade-off); Requires Clarification 4
**Depends on** — T-24.1.1, T-24.3.1, T-4.1.2
**Pattern to follow** — T-15.5.1 (a change that overtakes an offline one)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — one object edited on two surfaces, one offline, is a new sync shape (Blast Radius High); session rules are open (MI-32).

**Provisional commit message**

```
feat(chains): continue a tablet multi-branch session on the laptop

- A chain order often starts with the buyer and finishes at a desk; the
  session carries across rather than being re-keyed
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Two-surface editing of one offline object.

**Work package**

Increments:
1. Decide the concurrency rule (lock to one surface, last-writer with a conflict notice, or merge) and write it down.
2. Upload sessions at sync (T-4.1.2) as their own item type; download a laptop-edited session to the tablet if the rule allows.
3. Open a synced session in the grid (T-24.3.1) with its selection, lines and adjustments intact.
4. Enforce the rule on both surfaces, including a split on one while the other has it open.

Decision points:
- The concurrency rule.
- Session lifetime and discard (MI-32).

Delegable slivers:
- **Session mapping** — Map a tablet session (branches, lines, "same for all", adjusted cells) to the grid's model and back, losslessly, with round-trip tests. No sync.

---

### T-24.4.1-S — Test scenarios for a master that is also a shop

**Owner** — Scenario Review
**Gates** — T-24.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a master that is also a shop (task
T-24.4.1). Read plan_docs/stories/master-branch-ordering.md US-008 S1–S3 and
plan_docs/uxdocs/01-tablet-day.md T-11 (T11.1 "New order for this shop").
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover the story scenarios, then derivable edges: the master
selected as a branch in its own split alongside its own order (two orders for
one Location in one call), a head office Location with a Location Profile
that holds no stock. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-24.4.1 — Take a master's own order and the chain order in one call

**Parent story**

> As a Field Salesperson at a main shop that is also the chain's master, I want to do the shop's own stock check and order as normal, and the chain's Range Review and Multi-Branch Order in the same visit so that one call covers both roles.
>
> Acceptance criteria:
> - At Hickey's Wicklow Town (master and shop), a Call offers Pitch, Stock Check and Range Review (S1)
> - The shop's own order has Ordered By = For = Wicklow Town; the split orders have For = each branch; Wicklow Town may also be a selected branch (S2)
> - Hickey's Head Office (Type "Head office") still offers Stock Check, with an empty Suggested List reading "No stock held here" (S3)

**Slice** — At a master that is also a shop, one call offers all three purposes and the rep takes the shop's own order and a multi-branch order side by side; at a head office holding no stock, the stock check says so.
**Spec source** — Master & Branch US-008 S1–S3; uxdocs 01 T-11 (T11.1)
**Depends on** — T-23.3.1, T-24.2.1, T-5.4.1
**Pattern to follow** — T-5.4.1 (stock check), T-23.2.1 (master view actions)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies the stock check's empty state; rules stated.

**Provisional commit message**

```
feat(chains): take a master's own order and the chain order in one call

- A main shop is still a shop; its own order and the chain's are
  separate orders from one visit
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A small combination of reviewed flows.

**Agent prompt**

```
Role: You are supporting masters that are also shops on the tablet in the
Field Sales Management System.

Context:
- Slice: at a master that is also a shop, one call offers all three purposes
  and the rep takes the shop's own order and a multi-branch order side by
  side; at a head office holding no stock, the stock check says so.
- Specs: plan_docs/stories/master-branch-ordering.md US-008 S1–S3;
  plan_docs/uxdocs/01-tablet-day.md T-11 (T11.1).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Range Review purpose
  (T-23.3.1), split (T-24.2.1), stock check (T-5.4.1), master view
  (T-23.2.1).
- Pattern to follow: T-5.4.1.

Acceptance criteria:
1. At Hickey's Wicklow Town, the call offers Pitch, Stock Check and Range
   Review.
2. "New order for this shop" creates an order with Ordered By = For = Wicklow
   Town; the multi-branch split creates orders For each branch; Wicklow Town
   may also be selected as a branch.
3. At Hickey's Head Office (Type "Head office"), Stock Check is offered with
   an empty Suggested List reading "No stock held here".

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-5.4.1's suggested list; stop and show them passing. Resume only on
"Continue T-24.4.1".

Steps: 1. characterisation tests; 2. purposes at a master; 3. own order
alongside the split; 4. head office empty state; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-24.4.1-S; do not design
your own.

Definition of done: at a master that is also a shop, one call offers all
three purposes and the rep takes the shop's own order and a multi-branch
order side by side; at a head office holding no stock, the stock check says
so.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: chain-level invoicing.
```

**Checkpoint**

Produces before pausing — characterisation tests of the suggested list, passing.
Human reviews — Do the tests pin today's suggested list exactly?
Resume trigger — `Continue T-24.4.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | MB-005 (T-24.1.1), MB-007 (T-24.2.1), MB-006 (T-24.3.1, T-24.3.2), MB-008 (T-24.4.1), A1-023 S3, S4 (T-24.1.1, T-24.2.1) |
| Every task satisfies the three slice criteria | Pass | 5 of 5 |
| Every task carries a tier with a rationale citing dimensions | Pass | 5 of 5 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-30 (per-branch pricing), MI-32 (T-13, T-14 drafts, M11.2, session recovery) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-24.4.1; the Ordered-by sliver in T-24.2.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-24.2.1, T-24.3.2 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-24.1.1, T-24.3.1, T-24.4.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-24.2.1, T-24.3.2 |
| Every scenario task precedes the task it gates | Pass | 5 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-30, 32 |
