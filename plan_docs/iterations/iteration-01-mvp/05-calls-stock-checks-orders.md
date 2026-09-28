# E5 — Calls, stock checks and orders

**Iteration** — 1, MVP: orders from the field reach the warehouse
**Outcome** — At a shop, offline, a rep records a call with a stock check from the suggested list, turns Low items into order lines with a confirmed quantity, reviews the order and marks it ready — or takes a phone order with no call at all.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Rep at a Location US-012 — Build an Order | Must | S1, S1b, S1c, S3–S6, S7–S9, S12–S22 (S2's "Outside your ranges" marker and S1's rep ranges → E9; S4b, S4c → E6; S4d → E18; S4e → E19; S10 → E10; S11 → E8) |
| 2 | Rep at a Location US-013 — Edit, reopen or delete an Unsent Order | Must | S1–S5 |
| 3 | Rep at a Location US-006 — Record a Call | Must | S1–S5, S8–S12 (S1's "Visit Due marked Done" → E13; S6–S7 → E20) |
| 4 | Rep at a Location US-007 — Stock Check from a Suggested List | Must | S1, S3–S6 (S2 → E8) |
| 5 | Rep at a Location US-008 — Mark a line Low and add it to the Order | Must | S1–S4, S6–S8, S10–S14 (S5 → E9; S9 → E6) |
| 6 | Rep at a Location US-015 — Correct a saved Call before Sync | Should | S1–S3, S4 on the tablet (the website correction → E13) |
| 7 | Rep at a Location US-016 — Record a Follow-up Call | Should | S1, S2 (S3's visit completion → E13) |
| 8 | Rep at a Location US-018 — Add Competitor Notes to a Call | Should | S1–S5 (the Range link offers nothing until E9) |

**Exit criterion** — Offline, a rep can: take a standalone phone order and mark it ready; record a call with channel and pitch notes; run a stock check from the suggested list (or "Stock mentioned" on a phone call); mark items Low and add them through the quantity popover; catch up on the Low tab, including gaps carried from the last call; see unusually high quantities marked on Review; edit, reopen or delete unsent orders; correct an unsynced call; record a follow-up call; and add competitor notes.

**Capability-class stamp** — Frontier + extended reasoning for T-5.1.1's order-model slivers; Frontier workhorse for Agent-Assisted tasks and scenario drafting; Fast mid-tier for T-5.8.1. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-06, T-07), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§1 The order line), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (US-NEW-001, US-NEW-002, US-NEW-004).

---

### T-5.1.1-S — Test scenarios for taking a standalone order

**Owner** — Human-Led
**Gates** — T-5.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- What does an order line capture and never change afterwards: product, quantity, unit, captured price and its source, snapshot version? (Valid when captured; PM-002 S2.)
- What else does the order record: date taken, Capturing Rep, Ordered By and For (the same Location for an ordinary order), link to a Call if any?
- Which states exist on the tablet (In Progress, Ready to Send, Sent, Needs Attention) and which transitions are allowed in this task?
- Adding the same product twice: one line with a higher quantity, or two lines?
- An order for a Location reassigned since the snapshot: can a new one be started (no — T-4.2.2)?

---

### T-5.1.1 — Take a standalone order offline and mark it ready to send

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - Opening Byrne's Chemist, choosing New Order, adding "Vitamin D 1000IU 90s" quantity 12 and saving creates an In Progress Order dated today with no Call linked (S4)
> - Marking an In Progress Order with 3 lines ready makes it Ready to Send and it appears in the Unsent count as ready (S5)
> - Marking an Order with 0 lines ready shows "Add at least one product" (S6)
> - Each Order records the Capturing Rep (glossary)

**Slice** — Offline, a rep starts a new order at a shop, adds products found by search with whole quantities, saves it In Progress, and marks it Ready to Send, which an empty order can't be.
**Spec source** — Rep at a Location US-012 S4, S5, S6; glossary (Order, Capturing Rep, Valid when captured); Master & Branch glossary (Ordered By / For); uxdocs 01 T-07 (T7.4 "Mark Ready to Send", never "Submit" or "Send")
**Depends on** — T-4.2.2
**Pattern to follow** — novel — see design notes (the order record every area reads)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — defines the order record read by head office, allocation, targets and self-service; data integrity: a captured price or quantity must never change after capture.

**Provisional commit message**

```
feat(orders): take an order offline and mark it ready to send

- A half-agreed order must never send by accident, so nothing uploads
  until the rep marks it Ready to Send
- Each line captures its price and snapshot version, and the order records
  who took it and for which shop, because later areas read them as history
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The most-read record in the system; delegated slivers stay narrow.

**Work package**

Increments:
1. Order and Order Line model on the tablet: Location For, Ordered By (= For for ordinary orders), Capturing Rep, date taken, optional Call link, state, snapshot version; line with product, quantity (T-1.4.1's type), unit, captured price and source. Verify the model against the agreed scenarios.
2. Matching server model that accepts uploaded orders unchanged (hand-off to T-4.1.2).
3. New Order from the Location screen: search the catalogue (non-restricted products from the snapshot), add with a quantity of 1 or more, save In Progress.
4. Mark Ready to Send; "Add at least one product" on an empty order; Unsent count shows it as ready.
5. Captured price for now is the base price in effect today (E6 replaces this with the full price engine without changing the stored shape).

Decision points:
- Same product added twice: merge into one line or keep two? (Affects the Low tab and the out-of-pattern marker.)
- Is the captured price stored with all candidates (for H-02's working) or winner and source only?
- Does an order line store the product's name and code at capture, so it still reads correctly if the product is later renamed or hidden (T-4.4.1)?

Delegable slivers:
- **Order entry search** — Build the product search inside Order entry over the snapshot's non-restricted products, showing name, breadcrumb and price, returning a product to add. Do not change the order model.
- **Ready-to-send control** — Add "Mark Ready to Send" with the empty-order message "Add at least one product", calling the existing state transition. Do not add other transitions.
- **Order model tests** — Given the agreed scenarios from T-5.1.1-S, write the tests for the order and line model. Do not change production code; report failures.

---

### T-5.1.2-S — Test scenarios for the Order Pad

**Owner** — Scenario Review
**Gates** — T-5.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Order Pad's category browsing (task
T-5.1.2). Read plan_docs/stories/rep-at-a-location-tablet.md US-012 S1, S1b
and the non-functional notes, and plan_docs/uxdocs/01-tablet-day.md T-07
(T7.3). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Cover S1 (all products unranged for now) and S1b,
then derivable edges: a category with only subcategories, a product on a
branch category, collapsing and expanding, 260 products searched in under a
second. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-5.1.2 — Browse the Order Pad by category with breadcrumbs

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - The Order Pad lists products by category with quantity controls and a search field, each with its breadcrumb (S1; until E9 every product is unranged, so the pad holds the whole non-restricted catalogue)
> - Opening "Suncare" shows its 4 subcategories first, then "In Suncare (12)", and "180 products" beneath; search results show breadcrumbs so the two "Lotions" are distinguishable (S1b)
> - Search within 1 second for 260 products (non-functional)

**Slice** — The Order Pad lists products under collapsible category headers with full breadcrumbs — subcategories first, then the category's own products — and search answers within a second.
**Spec source** — Rep at a Location US-012 S1, S1b, non-functional notes; uxdocs 01 T-07 (T7.3)
**Depends on** — T-5.1.1
**Pattern to follow** — T-1.1.2 (subcategories then "In <category>")
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled design; a device performance target (Test Safety Net Medium).

**Provisional commit message**

```
feat(orders): browse the order pad by category with breadcrumbs

- A five-level tree with repeated leaf names is unusable without full
  paths, so every header and result shows its breadcrumb
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Performance on the device with a review pause.

**Agent prompt**

```
Role: You are building the Order Pad's category browsing (T-07) in the Field
Sales Management System's tablet app.

Context:
- Slice: the Order Pad lists products under collapsible category headers with
  full breadcrumbs — subcategories first, then the category's own products —
  and search answers within a second.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-012 S1, S1b and
  non-functional notes (search within 1 second for 260 products; quantity
  controls usable standing; "Remove line" away from quantity controls);
  plan_docs/uxdocs/01-tablet-day.md T-07 (T7.3 categories are collapsible
  section headers with the full breadcrumb).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Order entry from
  T-5.1.1, snapshot from T-4.1.1.
- Pattern to follow: T-1.1.2's ordering of subcategories and "In <name>".

Acceptance criteria:
1. New Order opens on the Order Pad: products by category with quantity
   controls and a search field; each product shows its breadcrumb. (Every
   product is unranged until E9.)
2. Opening "Suncare" shows its 4 subcategories first, then "In Suncare (12)",
   and "180 products" as the total beneath.
3. Search results show each product's breadcrumb, so "Lotions" under
   Suncare and under Body Care are distinguishable.
4. Categories are collapsible headers.
5. Search returns within 1 second for 260 products on a target device.
6. Restricted products never appear (they aren't in the snapshot).

Constraints:
- Use the project's existing conventions and test framework.
- Price shown is whatever T-5.1.1 captures (base price until E6).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the pad renders a five-level sample catalogue and search is
timed on a device, stop and show both. Resume only on "Continue T-5.1.2".

Steps: 1. category tree from the snapshot; 2. pad layout per T-07;
3. search; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.1.2-S; do not design
your own.

Definition of done: the Order Pad lists products under collapsible category
headers with full breadcrumbs — subcategories first, then the category's own
products — and search answers within a second.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: rep ranges and "Outside your ranges" (E9), prices with source
(E6), measure-based controls (T-5.1.3), the Low tab (T-5.1.4).
```

**Checkpoint**

Produces before pausing — the pad rendering a five-level sample catalogue and a device timing for search.
Human reviews — Is the pad usable standing, one-handed, with deep paths readable?
Resume trigger — `Continue T-5.1.2`

---

### T-5.1.3-S — Test scenarios for measure-based order lines

**Owner** — Scenario Review
**Gates** — T-5.1.3
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for measure-based quantities on order lines
(task T-5.1.3). Read plan_docs/stories/rep-at-a-location-tablet.md US-012 S1c
and US-008 S6, and plan_docs/stories/product-management.md US-003. Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Start with characterisation scenarios pinning T-5.1.1's current Each
quantity handling, then cover S1c and S6, then derivable edges: below the
minimum, stepping down to the minimum, typing 1.25 with step 0.5, very large
quantities. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-5.1.3 — Order measure-based products in steps with the unit shown

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - "Loose Herbal Tea" sold per kg with step 0.5 and minimum 1.0 steps 1.0, 1.5, 2.0 kg with the unit shown; 1.2 kg is rejected with "Enter multiples of 0.5 kg"; the price shows "€4.80 per kg" (S1c)
> - 0 for an Each product shows "Enter a quantity of 1 or more"; for a kg product "Enter at least 1.0 kg in steps of 0.5 kg" (US-008 S6)

**Slice** — A measure-based product's quantity control steps by its unit from its minimum, rejects values off the step with a message, and shows the price per unit.
**Spec source** — Rep at a Location US-012 S1c; US-008 S6
**Depends on** — T-5.1.1, T-1.4.1
**Pattern to follow** — T-1.4.1 (quantity value type and messages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies T-5.1.1's quantity handling, so characterisation comes first.

**Provisional commit message**

```
feat(orders): order measure-based products in steps

- Weight and volume products are sold in steps so the warehouse never
  receives an unpickable quantity; the unit is always shown beside it
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies established behaviour behind a characterisation pass.

**Agent prompt**

```
Role: You are adding measure-based quantity controls to order lines in the
Field Sales Management System's tablet app.

Context:
- Slice: a measure-based product's quantity control steps by its unit from
  its minimum, rejects values off the step with a message, and shows the
  price per unit.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-012 S1c, US-008 S6;
  plan_docs/stories/product-management.md US-003.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — order lines (T-5.1.1),
  quantity value type and messages (T-1.4.1).
- Pattern to follow: T-1.4.1's validation API.

Acceptance criteria:
1. "Loose Herbal Tea" (kg, step 0.5, minimum 1.0) steps 1.0, 1.5, 2.0 kg with
   the unit shown.
2. Typing 1.2 kg is rejected with "Enter multiples of 0.5 kg".
3. The price reads "€4.80 per kg".
4. 0 for an Each product shows "Enter a quantity of 1 or more"; below the
   minimum for the kg product shows "Enter at least 1.0 kg in steps of 0.5
   kg".
5. The line stores the unit it was captured in.

Constraints:
- Use the project's existing conventions and test framework; use T-1.4.1's
  quantity type — no second validation rule.
- No float arithmetic.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-5.1.1's Each quantity behaviour; stop and show them passing. Resume only on
"Continue T-5.1.3".

Steps: 1. characterisation tests; 2. stepper using the product's unit;
3. validation messages; 4. per-unit price display; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-5.1.3-S; do not design
your own.

Definition of done: a measure-based product's quantity control steps by its
unit from its minimum, rejects values off the step with a message, and shows
the price per unit.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: quantity breaks on measured products (E6), stock counts in
measures (T-5.4.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of T-5.1.1's Each quantities, passing.
Human reviews — Does the change leave Each behaviour exactly as it was?
Resume trigger — `Continue T-5.1.3`

---

### T-5.2.1-S — Test scenarios for editing, reopening and deleting unsent orders

**Owner** — Scenario Review
**Gates** — T-5.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for editing, reopening and deleting unsent
orders (task T-5.2.1). Read plan_docs/stories/rep-at-a-location-tablet.md
US-013 S1–S5 and the design decision "Three Unsent states and read-only after
Sync". Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Cover S1–S5, then derivable edges: reopening an
order in Needs Attention, deleting an order linked to a Call, editing during
a sync. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-5.2.1 — Edit, reopen or delete an unsent order

**Parent story**

> As a Field Salesperson, I want to change, reopen or delete an Order before I Sync so that I fix mistakes before head office sees them.
>
> Acceptance criteria:
> - Changing "Cold & Flu Relief 16s" from 24 to 36 on an In Progress Order and saving shows 36 (S1)
> - Reopen on an unsynced Ready to Send Order returns it to In Progress for editing (S2)
> - Delete order and confirm on an In Progress Order removes it; nothing is sent (S3)
> - Removing the only line asks "Remove the last line and delete this order?" (S4)
> - A Sent Order shows no edit, reopen or delete controls (S5)

**Slice** — Before syncing, a rep changes quantities on an order, reopens a Ready to Send order, or deletes an order; once sent, none of these are offered.
**Spec source** — Rep at a Location US-013 S1–S5
**Depends on** — T-5.1.1
**Pattern to follow** — T-5.1.1 (order state transitions)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — adds transitions to the order state machine; the orders live only on the tablet until sync, so Blast Radius is Medium.

**Provisional commit message**

```
feat(orders): edit, reopen or delete orders before sync

- Mistakes are fixed before head office sees them; after sync the order is
  read-only on the tablet so offline edits never race head office
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
State-machine additions with a review pause.

**Agent prompt**

```
Role: You are adding edit, reopen and delete for unsent orders in the Field
Sales Management System's tablet app.

Context:
- Slice: before syncing, a rep changes quantities on an order, reopens a
  Ready to Send order, or deletes an order; once sent, none of these are
  offered.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-013 S1–S5 and
  design decision "Three Unsent states and read-only after Sync".
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — order model and states
  from T-5.1.1.
- Pattern to follow: T-5.1.1's transitions.

Acceptance criteria:
1. On an In Progress Order, changing "Cold & Flu Relief 16s" from 24 to 36
   and saving shows 36.
2. On an unsynced Ready to Send Order, Reopen returns it to In Progress.
3. "Delete order" and confirm removes an In Progress Order; nothing is sent.
4. Removing the last line asks "Remove the last line and delete this order?".
5. A Sent Order shows no edit, reopen or delete controls.
6. A Ready to Send order being uploaded by an in-flight sync can't be edited
   until the sync finishes.

Constraints:
- Use the project's existing conventions and test framework.
- Allowed transitions only: In Progress ↔ Ready to Send (before upload),
  delete from In Progress or Ready to Send; no transitions from Sent.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the transition table is written with its guards, stop and
show it. Resume only on "Continue T-5.2.1".

Steps: 1. transition table; 2. guards against in-flight upload; 3. controls
on the order screen; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.2.1-S; do not design
your own.

Definition of done: before syncing, a rep changes quantities on an order,
reopens a Ready to Send order, or deletes an order; once sent, none of these
are offered.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: editing Pending orders on the website (E13, R-04), Needs
Attention handling (T-4.3.1).
```

**Checkpoint**

Produces before pausing — the order state transition table with its guards.
Human reviews — Is there any path that edits an order after it has started uploading?
Resume trigger — `Continue T-5.2.1`

---

### T-5.3.1-S — Test scenarios for recording a call

**Owner** — Scenario Review
**Gates** — T-5.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for recording a Call with channel, pitch notes
and a save review (task T-5.3.1). Read
plan_docs/stories/rep-at-a-location-tablet.md US-006 S1, S3–S5 and glossary
(Call, Call Purposes), and plan_docs/uxdocs/01-tablet-day.md T-06 (T6.1,
T6.2, T6.4). Output one line per scenario as Should_Outcome_When_Condition,
then "→" and a one-line intent. Cover S1, S3–S5, then derivable edges: going
Back from the review, a Call at a Location reassigned since the snapshot,
leaving the screen unsaved. Visit completion is out of scope. Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-5.3.1 — Record a call with channel, pitch notes and a save review

**Parent story**

> As a Field Salesperson, I want to record a Call with its Channel and what I did so that my activity is logged whether I visited or phoned.
>
> Acceptance criteria:
> - At Murphy's on 22 September 2026, Channel "In person" with Pitch notes "Presented autumn cold & flu range", confirmed in the save review, saves the Call with date, my name, Channel and notes (S1; marking the Visit Due Done arrives with E13)
> - Save shows a summary such as "In person · Pitch notes · 13 counted, 3 not checked · 2 Low · 1 competitor note" with Save and Back (S3)
> - Notes but no Channel: "Choose In person or Phone" and nothing saves (S4)
> - In person with nothing recorded: "Add pitch notes, a stock check or a competitor note" (S5)

**Slice** — A rep records a call at a shop by choosing In person or Phone and adding pitch notes, confirms a one-line summary, and the call is saved with its date and rep.
**Spec source** — Rep at a Location US-006 S1, S3, S4, S5; non-functional note; uxdocs 01 T-06 (T6.1 one scrolling page; T6.2 two large unselected channel options; T6.4 save review is a dialog)
**Depends on** — T-4.2.2
**Pattern to follow** — T-5.1.1 (captured record with snapshot version)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — introduces the Call model that visits, follow-ups, corrections and campaign outcomes extend; checkpoint on the model.

**Provisional commit message**

```
feat(calls): record a call with channel, pitch notes and a save review

- A call is the record of what happened at a shop, so it is saved once and
  corrected or followed up rather than rewritten
- Channel is never preselected; a guessed channel would make phone and
  in-person activity unreliable
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared model with a design pause.

**Agent prompt**

```
Role: You are building call recording (T-06) in the Field Sales Management
System's tablet app.

Context:
- Slice: a rep records a call at a shop by choosing In person or Phone and
  adding pitch notes, confirms a one-line summary, and the call is saved with
  its date and rep.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-006 S1, S3–S5,
  glossary (Call, Correction, Follow-up Call, Competitor Note) and design
  decisions "Three Unsent states…" and "Corrections constrained by the
  interface"; plan_docs/uxdocs/01-tablet-day.md T-06 (T6.1, T6.2, T6.4).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Location screen
  (T-4.2.2), capture pattern (T-5.1.1).
- Pattern to follow: T-5.1.1's captured record with snapshot version.

Acceptance criteria:
1. "Record Call" at Murphy's Pharmacy opens one scrolling page with the
   channel as two large options, In person and Phone, neither preselected.
2. Channel "In person" with Pitch notes "Presented autumn cold & flu range",
   confirmed in the save review, saves the Call with date, rep, Channel and
   notes.
3. Save opens a review dialog summarising the call, e.g. "In person · Pitch
   notes · 13 counted, 3 not checked · 2 Low · 1 competitor note", with Save
   and Back.
4. Notes but no Channel: the Call isn't saved and "Choose In person or Phone"
   is shown.
5. In person with no notes, counts or competitor notes: "Add pitch notes, a
   stock check or a competitor note".
6. A saved Call is Unsent until the next sync (uploaded by T-4.1.2).

Constraints:
- Use the project's existing conventions and test framework.
- Design the Call model so later tasks can add: stock check entries
  (T-5.4.1), Low marks (T-5.5.1), competitor notes (T-5.8.1), a follow-up
  link (T-5.7.1), campaign outcomes (E20), a Range Review purpose (E23), and
  completion of Visit Dues (E13). Do not implement them.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  tablet's local store for calls and the server's received-call store.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the Call model is designed with the later extension points
listed above, stop and show it. Resume only on "Continue T-5.3.1".

Steps: 1. Call model (tablet and server); 2. T-06 page with channel and
notes; 3. save review dialog; 4. validation; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.3.1-S; do not design
your own.

Definition of done: a rep records a call at a shop by choosing In person or
Phone and adding pitch notes, confirms a one-line summary, and the call is
saved with its date and rep.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Each extension point named in Constraints has a place in the model
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: stock check (T-5.4.1), Stock mentioned (T-5.3.2), Low marks
(T-5.5.1), competitor notes (T-5.8.1), corrections (T-5.6.1), follow-ups
(T-5.7.1), campaign outcomes (E20), visit completion (E13).
```

**Checkpoint**

Produces before pausing — the Call model with its extension points for stock checks, Low marks, competitor notes, follow-ups, campaign outcomes and Range Review.
Human reviews — Can each later feature attach to a Call without reshaping it, and is a saved Call immutable except through the correction rules?
Resume trigger — `Continue T-5.3.1`

---

### T-5.4.1-S — Test scenarios for the suggested-list stock check

**Owner** — Scenario Review
**Gates** — T-5.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the stock check built from the suggested
list (task T-5.4.1). Read plan_docs/stories/rep-at-a-location-tablet.md
US-007 S1, S3–S6 and its edge cases, and glossary (Stock Check, Not checked).
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover the story scenarios, then derivable edges: a product
on both sources, a Not checked line that is Not checked again, an order not
yet Accepted (must not feed the list), a restricted product on an old order,
measure-based counts. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-5.4.1 — Run a stock check from the suggested list

**Parent story**

> As a Field Salesperson, I want a Stock Check to start from what this Location usually has so that I don't miss what I'd otherwise have to remember.
>
> Acceptance criteria:
> - Murphy's last Stock Check (10 counted, 2 Not checked) and last 3 Accepted Orders (5 products, 3 of them on that check) give 14 products, each once, labelled "Last visit", "Ordered recently" or both (S1)
> - Removing 2 and adding "Vitamin D 1000IU 90s" by search or browsing leaves 13 (S3)
> - With 3 uncounted, the review shows "10 counted, 3 not checked"; the 3 save as Not checked and appear on the next visit's list (S4)
> - A count of -2 is rejected with "Enter 0 or more" (S5)
> - Doyle's Shop with no history shows "No suggested products yet — search or browse to add" (S6)

**Slice** — A stock check opens on the products counted last visit plus those on the last three accepted orders, each labelled by source; the rep adds or removes rows, and uncounted lines save as Not checked for next time.
**Spec source** — Rep at a Location US-007 S1, S3–S6, edge cases; design decision "Suggested List, Not checked, and Low as the rep's judgement"
**Depends on** — T-5.3.1
**Pattern to follow** — T-5.3.1 (Call sections)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — adds snapshot fields (last stock check lines, last 3 accepted orders per Location); checkpoint on the list derivation.

**Provisional commit message**

```
feat(calls): stock check from the location's suggested list

- Recognition beats recall: the list starts from what this shop had and
  ordered, so reps don't miss products they'd have to remember
- Uncounted lines are kept as Not checked, so a gap reads as a gap next
  visit instead of vanishing
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
List derivation and snapshot additions with a design pause.

**Agent prompt**

```
Role: You are building the stock check from the suggested list (T-06) in the
Field Sales Management System's tablet app.

Context:
- Slice: a stock check opens on the products counted last visit plus those
  on the last three accepted orders, each labelled by source; the rep adds or
  removes rows; uncounted lines save as Not checked for next time.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-007 S1, S3–S6 and
  edge cases, glossary (Stock Check, Suggested List, Not checked);
  plan_docs/uxdocs/01-tablet-day.md T-06.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Call model (T-5.3.1),
  snapshot builder (T-4.1.1), order catalogue search (T-5.1.1).
- Pattern to follow: T-5.3.1's Call sections.

Acceptance criteria:
1. Murphy's last Stock Check had 10 counted and 2 Not checked lines; its last
   3 Accepted Orders contained 5 products, 3 of them on that check. Starting
   a Stock Check shows 14 products, each once, labelled "Last visit",
   "Ordered recently" or both.
2. Removing 2 and adding "Vitamin D 1000IU 90s" by search or category
   browsing leaves 13 products.
3. With 13 products and 3 uncounted, the review shows "10 counted, 3 not
   checked"; the 3 save as Not checked and appear on the next visit's list.
4. Entering -2 is rejected with "Enter 0 or more"; measure-based products
   accept decimal counts with their unit.
5. Doyle's Shop with no Stock Check and no Accepted Orders shows "No
   suggested products yet — search or browse to add".
6. The most recent not-yet-Accepted Order does not feed the list; restricted
   products never appear.

Constraints:
- Use the project's existing conventions and test framework.
- The suggested list is a pure function of the snapshot's history for the
  Location; keep it in one place.
- Snapshot additions (last stock check lines, last 3 accepted orders per
  Location) go through T-4.1.1's versioning; the snapshot's owner reviews
  them.
- "Last 3 Accepted Orders" is a system setting (MI-12).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond stock
  check entries on the Call.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the suggested-list function and the snapshot fields are
written with the S1 example passing, stop and show them. Resume only on
"Continue T-5.4.1".

Steps: 1. snapshot history fields; 2. suggested-list function; 3. stock check
section on T-06 with source labels, add and remove; 4. Not checked saving;
5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.4.1-S; do not design
your own.

Definition of done: a stock check opens on the products counted last visit
plus those on the last three accepted orders, each labelled by source; the
rep adds or removes rows, and uncounted lines save as Not checked for next
time.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Low marks and adding to the order (T-5.5.1), unavailable and
outside-range labels (E8, E9), the low-stock hint (E12), Stock mentioned on
phone calls (T-5.3.2).
```

**Checkpoint**

Produces before pausing — the suggested-list function passing the S1 example, and the snapshot field additions.
Human reviews — Does the list merge exactly as the story says, and is the snapshot addition within the size budget (MI-02)?
Resume trigger — `Continue T-5.4.1`

---

### T-5.3.2-S — Test scenarios for "Stock mentioned" on a phone call

**Owner** — Scenario Review
**Gates** — T-5.3.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for "Stock mentioned" on a phone call (task
T-5.3.2). Read plan_docs/stories/rep-at-a-location-tablet.md US-006 S2,
S8–S12 and plan_docs/uxdocs/01-tablet-day.md T-06 (T6.5). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: switching Phone → In person
→ Phone, "Add count" then Out, a filter with no match leading to catalogue
search. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-5.3.2 — Record "Stock mentioned" on a phone call

**Parent story**

> As a Field Salesperson, I want to record a Call with its Channel and what I did so that my activity is logged whether I visited or phoned.
>
> Acceptance criteria:
> - A phone call to Byrne's with four products mentioned saves with Channel "Phone" and 4 entries (S2, as amended)
> - Choosing "Phone" turns the stock section into "Stock mentioned": the same suggested products with a filter box, each row offering Low, Out, Add to order and "Add count", with no stepper (S8)
> - Typing in the filter narrows the list; with no match the rep can search the catalogue and add the product as a row (S9)
> - Marking Sudocrem 125g Out also marks it Low with a count of 0 (S10)
> - With SPF30 Low, Sudocrem Out and 14 untouched rows, the review reads "Phone · 2 marked Low (1 out)" and only the 2 are recorded (S11)
> - Switching Channel keeps everything entered (S12)

**Slice** — On a phone call the stock section becomes "Stock mentioned": the same suggested rows with a filter, marked Low, Out or counted on request, and only the rows the rep touched are saved.
**Spec source** — Rep at a Location US-006 S2, S8–S12; uxdocs 01 T-06 (T6.5)
**Depends on** — T-5.4.1
**Pattern to follow** — T-5.4.1 (suggested-list rows)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — a settled variant of T-5.4.1's list; checkpoint on the row behaviour.

**Provisional commit message**

```
feat(calls): record stock mentioned on phone calls

- A rep can't count stock down a phone line, so a phone call records what
  the shopkeeper said was low or out, and nothing is saved as Not checked
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A variant of a reviewed list with a review pause.

**Agent prompt**

```
Role: You are adding "Stock mentioned" for phone calls (T-06) in the Field
Sales Management System's tablet app.

Context:
- Slice: on a phone call the stock section becomes "Stock mentioned": the
  same suggested rows with a filter, marked Low, Out or counted on request,
  and only the rows the rep touched are saved.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-006 S2, S8–S12 and
  the 24 Sep 2026 UX amendment; plan_docs/uxdocs/01-tablet-day.md T-06
  (DECISION T6.5).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — stock check section
  (T-5.4.1), Call model (T-5.3.1).
- Pattern to follow: T-5.4.1's rows.

Acceptance criteria:
1. At Doyle's with a Suggested List of 16, choosing "Phone" shows "Stock
   mentioned" with the same 16 products and a filter box; each row offers
   Low, Out, Add to order and "Add count", with no stepper.
2. Typing in the filter narrows the list; with no match the rep can search
   the catalogue and add the product as a row.
3. Marking Sudocrem 125g Out also marks it Low and records a count of 0.
4. With SPF30 Low, Sudocrem Out and 14 rows untouched, the review reads
   "Phone · 2 marked Low (1 out)"; only the 2 are recorded; the 14 are not
   saved as Not checked.
5. A phone call with four products mentioned saves with Channel "Phone" and
   4 entries.
6. Switching Channel between In person and Phone loses nothing entered.

Constraints:
- Use the project's existing conventions and test framework.
- "Add to order" on a row uses T-5.5.1's popover once it exists; until then
  show it disabled (this task may land before T-5.5.1).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the Phone-mode rows and the save rule ("only what was
mentioned") are implemented against the S11 example, stop and show them.
Resume only on "Continue T-5.3.2".

Steps: 1. Phone-mode rendering of the list; 2. Low / Out / Add count;
3. filter and catalogue search; 4. save rule; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.3.2-S; do not design
your own.

Definition of done: on a phone call the stock section becomes "Stock
mentioned": the same suggested rows with a filter, marked Low, Out or counted
on request, and only the rows the rep touched are saved.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the quantity popover (T-5.5.1), the Low tab (T-5.1.4).
```

**Checkpoint**

Produces before pausing — Phone-mode rows and the save rule passing the S11 example.
Human reviews — Does a phone call record only what was mentioned, with Out stored as Low and count 0?
Resume trigger — `Continue T-5.3.2`

---

### T-5.5.1-S — Test scenarios for marking Low and the quantity popover

**Owner** — Scenario Review
**Gates** — T-5.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for marking a line Low and adding it through the
quantity popover (task T-5.5.1). Read
plan_docs/stories/rep-at-a-location-tablet.md US-008 S1–S4, S6–S8, S10–S12,
S14 and plan_docs/uxdocs/01-tablet-day.md T-06/T-07 (T7.6–T7.8). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover those scenarios, then derivable edges: adding a product already
on the order from another route, cancelling after typing, adding on a phone
call. Mark undecided cases as "Needs a decision". Write no test code; change
no files.
```

---

### T-5.5.1 — Mark a product Low and add it to the order through a quantity popover

**Parent story**

> As a Field Salesperson, I want to mark a product Low and add it to this visit's Order straight away so that I reorder it while I'm looking at it.
>
> Acceptance criteria:
> - Counting 3 of "Cold & Flu Relief 16s", marking Low and adding 24 creates an In Progress Order for Murphy's with that line, linked to this Call (S1)
> - With an existing In Progress Order of 2 lines, adding "Throat Lozenges 36s" 12 makes 3 lines and no second Order (S2)
> - A Low line already on the Order shows "On order (24)" instead of Add to order (S3)
> - Removing the Low mark leaves the Order line at 24 (S4)
> - Invalid quantities show "Enter a quantity of 1 or more" or the measure message (S6, S12)
> - Add to order opens a popover with the product name, the recorded count, the resolved price with its source and an empty quantity field; it is never pre-filled (S7, S8)
> - A valid quantity adds the line and the item shows as ADDED with its quantity; Cancel adds nothing; the popover closes and returns where it opened (S10, S11, S14)

**Slice** — Marking a counted product Low offers Add to order, which opens a quantity popover with an empty quantity; confirming adds the line to the call's order — creating it if needed — and the row then reads "On order (24)".
**Spec source** — Rep at a Location US-008 S1–S4, S6–S8, S10–S12, S14; uxdocs 01 T-06, T-07 (T7.6, T7.7, T7.8); uxdocs 04 US-NEW-001
**Depends on** — T-5.4.1, T-5.1.1
**Pattern to follow** — T-5.1.1 (adding lines), T-5.4.1 (stock check rows)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — settled design; links the Call and the Order, so the build pauses on that link.

**Provisional commit message**

```
feat(orders): add low items to the order through a quantity popover

- The rep confirms the quantity with the customer anyway, so the popover
  costs no time and nothing is added at a guessed quantity
- The quantity opens empty because the data holds a floor, not a target,
  and a prefilled number would anchor the rep
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Links two captured records with a review pause.

**Agent prompt**

```
Role: You are adding "mark Low and add to order" with the quantity popover
(T-06, T-07) to the Field Sales Management System's tablet app.

Context:
- Slice: marking a counted product Low offers Add to order, which opens a
  quantity popover with an empty quantity; confirming adds the line to the
  call's order — creating it if needed — and the row then reads "On order
  (24)".
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-008 S1–S4, S6–S8,
  S10–S12, S14 and design decision "Suggested List, Not checked, and Low as
  the rep's judgement"; plan_docs/uxdocs/01-tablet-day.md T-06, T-07 (T7.6
  no Add all; T7.7 quantity opens empty; T7.8 unticking Low does not remove
  an added line); plan_docs/uxdocs/04-user-stories-amendments.md US-NEW-001.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — stock check rows
  (T-5.4.1), order lines (T-5.1.1), quantity validation (T-1.4.1).
- Pattern to follow: T-5.1.1's add-line path.

Acceptance criteria:
1. After counting 3 of "Cold & Flu Relief 16s" at Murphy's and marking it
   Low, Add to order opens a popover showing the product name, the count 3,
   the resolved price with its source (base price and "list price" until E6)
   and an empty quantity field.
2. Entering 24 and tapping Add to order creates an In Progress Order for
   Murphy's with that line, linked to this Call; the popover closes and
   returns to the stock check.
3. If Murphy's already has an In Progress Order with 2 lines, adding
   "Throat Lozenges 36s" 12 makes 3 lines; no second Order is created.
4. A Low line whose product is already on the Order shows "On order (24)"
   instead of Add to order.
5. Removing the Low mark leaves the Order line at 24.
6. Cancel adds nothing and the item stays not added.
7. Invalid quantities show "Enter a quantity of 1 or more" (Each) or the
   measure message (e.g. "Enter at least 1.0 kg in steps of 0.5 kg"), and Add
   to order is unavailable until corrected.
8. The popover never opens with a quantity filled in, from any route.

Constraints:
- Use the project's existing conventions and test framework.
- Build the popover as one reusable component: the Low tab (T-5.1.4) and the
  break prompt (T-6.5.1) reuse it.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond Low
  marks on stock check entries and the Call–Order link.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the Call–Order link and the reusable popover component are
in place, stop and show them. Resume only on "Continue T-5.5.1".

Steps: 1. Low mark on stock check entries; 2. Call–Order link and
find-or-create order; 3. popover component; 4. "On order (N)" state;
5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.5.1-S; do not design
your own.

Definition of done: marking a counted product Low offers Add to order, which
opens a quantity popover with an empty quantity; confirming adds the line to
the call's order — creating it if needed — and the row then reads "On order
(24)".

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–8 demonstrated
- [ ] Popover is one component usable from the Low tab
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the Low tab (T-5.1.4), break prompts (T-6.5.1), replacements
for unavailable Low lines (E10), "Outside your ranges" (E9).
```

**Checkpoint**

Produces before pausing — the Call–Order link with find-or-create, and the reusable popover component.
Human reviews — Can a Low item ever be added twice, or an order be created twice for one call?
Resume trigger — `Continue T-5.5.1`

---

### T-5.1.4-S — Test scenarios for the Low tab

**Owner** — Scenario Review
**Gates** — T-5.1.4
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Low tab on an order that follows a call
(task T-5.1.4). Read plan_docs/stories/rep-at-a-location-tablet.md US-012 S3,
S7–S9, S12, S13 and US-008 S13, and plan_docs/uxdocs/01-tablet-day.md T-07
(T7.1, T7.6, T7.8). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning T-5.1.1's and T-5.5.1's current order and
popover behaviour. Then cover the story scenarios for the states not added,
ADDED and REMOVED (REPLACED arrives with E10 and CAN'T ADD with E8), the
count, no Add all, and unticking Low. Then derivable edges: remove then
re-add, change quantity after adding. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-5.1.4 — Catch up on Low items in the Low tab

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - During a Call with 2 Low lines, 1 already added, Order entry shows the In Progress Order with 1 line and the Low list alongside, one added and one not; the Order is linked to the Call (S3)
> - Every Low item is listed with exactly one state; this task delivers not added, ADDED with quantity, and REMOVED (S7; REPLACED → E10, CAN'T ADD → E8)
> - The Low tab label counts only not added (and later CAN'T ADD) items (S8)
> - Removing an added Low item's line shows it REMOVED with an Add action (S9)
> - There is no action that adds them all at once (S12)
> - Unticking Low on the stock-check line leaves the order line unchanged (S13)
> - Add on a not added or REMOVED item opens the same quantity popover (US-008 S13)

**Slice** — Order entry shows a Low tab beside Order pad and Search all, listing each item marked Low on this call as not added, ADDED or REMOVED, counting only the unresolved ones, with Add opening the quantity popover.
**Spec source** — Rep at a Location US-012 S3, S7–S9, S12, S13; US-008 S13; uxdocs 01 T-07 (T7.1, T7.6, T7.8); uxdocs 04 US-NEW-002
**Depends on** — T-5.5.1
**Pattern to follow** — T-5.5.1 (popover), T-5.1.2 (order entry tabs)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — a state list with enumerated states; modifies T-5.1.1's order entry screen, so characterisation comes first.

**Provisional commit message**

```
feat(orders): catch up on low items in the low tab

- Most Low items are added at tick time; the tab is where anything the
  customer is short of but not yet on the order stays visible
- Removed items are decisions, not gaps, so the count shows only what is
  still unresolved
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies established screens behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the Low tab (T-07) to order entry in the Field Sales
Management System's tablet app.

Context:
- Slice: order entry shows a Low tab beside Order pad and Search all,
  listing each item marked Low on this call as not added, ADDED or REMOVED,
  counting only the unresolved ones, with Add opening the quantity popover.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-012 S3, S7–S9, S12,
  S13 and glossary (Low tab), US-008 S13; plan_docs/uxdocs/01-tablet-day.md
  T-07 (T7.1 the Low list is a tab; T7.6 no Add all; T7.8 unticking Low keeps
  the line); plan_docs/uxdocs/04-user-stories-amendments.md US-NEW-002.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — order entry (T-5.1.1,
  T-5.1.2), popover and Call–Order link (T-5.5.1).
- Pattern to follow: T-5.5.1's popover.

Acceptance criteria:
1. During a Call with 2 Low lines, 1 already added, opening Order entry shows
   the In Progress Order with 1 line and the Low tab listing one ADDED and one
   not added; the Order is linked to the Call.
2. Each Low item has exactly one state: not added, ADDED (with quantity) or
   REMOVED. REPLACED and CAN'T ADD are reserved for E10 and E8.
3. The tab label counts only not added items (later also CAN'T ADD).
4. Removing an added item's line from the order shows it REMOVED with an Add
   action.
5. There is no action that adds several items at once.
6. Unticking Low on the stock-check line leaves the order line unchanged.
7. Add on a not added or REMOVED item opens T-5.5.1's popover.

Constraints:
- Use the project's existing conventions and test framework.
- Derive each item's state from the Call's Low marks and the order's lines;
  do not store a second copy that can drift.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-5.1.1's and T-5.5.1's order-entry and popover behaviour; stop and show
them passing with your state-derivation design. Resume only on
"Continue T-5.1.4".

Steps: 1. characterisation tests; 2. state derivation; 3. Low tab beside
Order pad and Search all; 4. count; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.1.4-S; do not design
your own.

Definition of done: order entry shows a Low tab beside Order pad and Search
all, listing each item marked Low on this call as not added, ADDED or
REMOVED, counting only the unresolved ones, with Add opening the quantity
popover.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: orders with no call (T-5.1.5), REPLACED (E10), CAN'T ADD (E8).
```

**Checkpoint**

Produces before pausing — characterisation tests of order entry and the popover, passing, and the state-derivation design.
Human reviews — Is each Low item's state derived from one source, so the tab can never disagree with the order?
Resume trigger — `Continue T-5.1.4`

---

### T-5.1.5-S — Test scenarios for gaps carried to an order with no call

**Owner** — Scenario Review
**Gates** — T-5.1.5
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Low tab on an order with no call (task
T-5.1.5). Read plan_docs/stories/rep-at-a-location-tablet.md US-012 S19–S22
and plan_docs/uxdocs/01-tablet-day.md T-07 (T7.13). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning T-5.1.4's Low tab, then cover
S19–S22, then derivable edges: a product ordered since by a customer online
or another rep, the last call at the shop was by another rep, a Call that
itself has no gaps. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-5.1.5 — Carry the last call's gaps to an order with no call

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - Carey's last Call (Tue 15 Sep) left Nappy Wipes and Sudocrem not added and Aftersun CAN'T ADD; Sudocrem was ordered on 18 Sep. Starting an Order with no Call shows "Still open from your call at Carey's - Tue 15 Sep" with Nappy Wipes (not added) and Aftersun (CAN'T ADD) only, counted in the tab label (S19)
> - A last Call 10 weeks ago still has its gaps carried, labelled with its date (S20)
> - With no earlier Call or no gaps left, the tab shows Low (0) and "No stock check with this order." with a Record call link (S21)
> - Recording a Call during the Order links it and replaces the carried items with that Call's Low items (S22)

**Slice** — An order started without a call shows, in its Low tab, the unresolved gaps from the shop's most recent call minus anything ordered since, dated, and recording a call during the order swaps in that call's items.
**Spec source** — Rep at a Location US-012 S19–S22; uxdocs 01 T-07 (T7.13); uxdocs 04 US-NEW-002 AC-8–12
**Depends on** — T-5.1.4
**Pattern to follow** — T-5.1.4 (state derivation)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies T-5.1.4; adds snapshot fields (the last call's unresolved Low items and products ordered since, by any route).

**Provisional commit message**

```
feat(orders): carry the last call's gaps to an order with no call

- Phone orders have no stock check, so the gaps from the last visit are
  carried over rather than lost; anything ordered since drops off
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Snapshot additions and a modified screen, with a review pause.

**Agent prompt**

```
Role: You are carrying the last call's gaps into the Low tab of an order with
no call, in the Field Sales Management System's tablet app.

Context:
- Slice: an order started without a call shows, in its Low tab, the
  unresolved gaps from the shop's most recent call minus anything ordered
  since, dated; recording a call during the order swaps in that call's items.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-012 S19–S22 and
  the 24 Sep 2026 UX amendment; plan_docs/uxdocs/01-tablet-day.md T-07
  (T7.13); plan_docs/uxdocs/04-user-stories-amendments.md US-NEW-002
  AC-8–12.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Low tab (T-5.1.4),
  snapshot builder (T-4.1.1).
- Pattern to follow: T-5.1.4's derived states.

Acceptance criteria:
1. Carey's most recent Call on Tue 15 Sep left Nappy Wipes and Sudocrem not
   added and Aftersun CAN'T ADD; Sudocrem was ordered at Carey's on 18 Sep.
   Starting an Order at Carey's with no Call and opening the Low tab reads
   "Still open from your call at Carey's - Tue 15 Sep" and lists Nappy Wipes
   (not added) and Aftersun (CAN'T ADD) only, counted in the tab label.
2. With the most recent Call 10 weeks ago, its gaps are still carried,
   labelled with its date.
3. With no earlier Call, or none of its gaps left, the tab shows Low (0) and
   "No stock check with this order." with a Record call link.
4. Using Record call and saving a Call at the Location links the Order to it,
   and the Low tab shows that Call's Low items in place of the carried ones.
5. "Ordered since" includes orders by any route (rep, customer online,
   chain) known to the snapshot.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions (per Location: most recent Call's unresolved Low items
  with their states and date; products ordered since) go through T-4.1.1's
  versioning and its owner's review.
- CAN'T ADD items appear only once E8 provides availability; until then
  carried items can be not added only.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-5.1.4's Low tab; stop and show them passing with the snapshot field
additions. Resume only on "Continue T-5.1.5".

Steps: 1. characterisation tests; 2. snapshot fields; 3. carried-gaps
derivation; 4. header, empty state and Record call link; 5. swap on call;
6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.1.5-S; do not design
your own.

Definition of done: an order started without a call shows, in its Low tab,
the unresolved gaps from the shop's most recent call minus anything ordered
since, dated, and recording a call during the order swaps in that call's
items.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: CAN'T ADD computation (E8), REPLACED (E10).
```

**Checkpoint**

Produces before pausing — characterisation tests of the Low tab, passing, and the snapshot field additions.
Human reviews — Is "ordered since" complete across every ordering route the snapshot knows, and within the size budget?
Resume trigger — `Continue T-5.1.5`

---

### T-5.1.6-S — Test scenarios for the out-of-pattern quantity marker

**Owner** — Human-Led
**Gates** — T-5.1.6
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- What does "well above" mean as a number (MI-49)? For example, more than 3× the median of the last 3 accepted quantities, and at least N units more. Decide and state the rule.
- If the Location ordered the product on only 1 of its last 3 orders, is there a pattern?
- How is "Usually about [N] here" rounded — to the nearest whole unit for Each, and to the step for measure products?
- Confirm: high only, never blocks, no acknowledgement (S16, S17).

---

### T-5.1.6 — Mark unusually high quantities on Review

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - A line well above what this Location ordered of that product on its last 3 accepted orders shows "Usually about [N] here" on Review order (S14)
> - No accepted order containing the product: no marker (S15)
> - A quantity well below usual: no marker (S16)
> - Marked lines never block Mark Ready to Send, with no confirmation or acknowledgement (S17)
> - Offline, markers come from the order history in the snapshot (S18)

**Slice** — On Review, a line quantity far above what this shop usually orders of that product is marked "Usually about 48 here", never blocking Mark Ready to Send, and nothing is marked without history.
**Spec source** — Rep at a Location US-012 S14–S18; uxdocs 01 T-07 (T7.11); uxdocs 04 US-NEW-004
**Depends on** — T-5.1.1, T-5.4.1
**Pattern to follow** — T-5.4.1 (history from the snapshot)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: S | Confidence: M
  (inferred) — Oracle Ambiguity High: "well above" has no value yet (MI-49), so a human sets the rule with the scenarios; the build is simple.

**Provisional commit message**

```
feat(orders): mark unusually high quantities on review

- No person reviews orders now, so Review is the last place a slip like 480
  for 48 can be caught; the marker informs and never blocks
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Simple build against a human-set rule.

**Agent prompt**

```
Role: You are adding the out-of-pattern quantity marker to Review order (T-07)
in the Field Sales Management System's tablet app.

Context:
- Slice: on Review, a line quantity far above what this shop usually orders
  of that product is marked "Usually about 48 here", never blocking Mark Ready
  to Send, and nothing is marked without history.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-012 S14–S18 and
  edge cases; plan_docs/uxdocs/01-tablet-day.md T-07 (T7.11: high only,
  non-blocking); plan_docs/uxdocs/04-user-stories-amendments.md US-NEW-004.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Review order and
  lines (T-5.1.1), last 3 accepted orders in the snapshot (T-5.4.1).
- Pattern to follow: T-5.4.1's snapshot history.

Acceptance criteria:
1. A line well above this Location's quantity of that product on its last 3
   accepted orders shows "Usually about [N] here" on Review, where "well
   above" and N follow the rule agreed in T-5.1.6-S.
2. No accepted order at the Location containing the product: no marker.
3. A quantity well below usual: no marker.
4. Marked lines never block Mark Ready to Send, with no confirmation or
   acknowledgement.
5. Works offline from the snapshot.

Constraints:
- Use the project's existing conventions and test framework.
- The rule lives in one pure function taking the line and the history.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the rule function passes every agreed scenario, stop and
show it. Resume only on "Continue T-5.1.6".

Steps: 1. rule function; 2. marker on Review lines; 3. tests.

Test expectations: implement exactly the scenarios agreed in T-5.1.6-S. You
are forbidden from designing your own test cases.

Definition of done: on Review, a line quantity far above what this shop
usually orders of that product is marked "Usually about 48 here", never
blocking Mark Ready to Send, and nothing is marked without history.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the Large annotation at head office (E28), low-quantity
markers (rejected in design).
```

**Checkpoint**

Produces before pausing — the rule function with every agreed scenario passing.
Human reviews — Does the threshold catch a 10× slip without marking ordinary seasonal peaks?
Resume trigger — `Continue T-5.1.6`

---

### T-5.6.1-S — Test scenarios for correcting an unsynced call

**Owner** — Scenario Review
**Gates** — T-5.6.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for correcting a saved, unsynced Call (task
T-5.6.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-015 S1–S4 and
edge cases, and the design decision "Corrections constrained by the
interface". Output one line per scenario as Should_Outcome_When_Condition,
then "→" and a one-line intent. Start with characterisation scenarios pinning
T-5.3.1's saved-call behaviour, then cover S1–S4 (tablet part), then
derivable edges: removing a Low line whose product is on the order, changing
Channel from In person to Phone after counting, correcting during a sync.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-5.6.1 — Correct a saved call before it syncs

**Parent story**

> As a Field Salesperson, I want to fix genuine mistakes in a Call I've saved but not synced so that head office receives accurate records.
>
> Acceptance criteria:
> - Changing a count of 3 to 13 in Edit Call saves 13 (S1)
> - Edit Call allows changing Channel, notes, counts, Low marks and Competitor Note text, and removing lines or notes; there is no Add product, Add pitch or Add competitor note — "Record a follow-up call" is offered instead (S2)
> - Removing a line counted by mistake removes it from the Call (S3)
> - A Sent Call offers no Edit Call; Record follow-up call is offered (S4, tablet part; "Open on website" → E13)
> - A Call cannot be deleted; a corrected Call still completes its Visit Due (edge cases)

**Slice** — Before syncing, a rep can change values in a saved call and remove lines, but can't add anything — the screen offers "Record a follow-up call" instead — and once synced the call is read-only.
**Spec source** — Rep at a Location US-015 S1–S4, edge cases; design decision "Corrections constrained by the interface"
**Depends on** — T-5.3.1
**Pattern to follow** — T-5.3.1 (Call page)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies T-5.3.1's saved-call behaviour, so characterisation comes first; the rule is enforced by the interface.

**Provisional commit message**

```
feat(calls): correct a saved call before sync without adding to it

- A call records what happened, so corrections fix values and additions go
  on a follow-up call; the screen enforces it instead of asking reps to
  remember a rule
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies a record's rules behind a characterisation pass.

**Agent prompt**

```
Role: You are adding Edit Call for saved, unsynced calls in the Field Sales
Management System's tablet app.

Context:
- Slice: before syncing, a rep can change values in a saved call and remove
  lines, but can't add anything — the screen offers "Record a follow-up
  call" instead — and once synced the call is read-only.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-015 S1–S4 and edge
  cases, glossary (Correction); design decision "Corrections constrained by
  the interface".
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Call model and page
  (T-5.3.1), stock check (T-5.4.1), Low marks (T-5.5.1).
- Pattern to follow: T-5.3.1's page in an edit mode.

Acceptance criteria:
1. On a saved unsynced Call with "Cold & Flu Relief 16s" counted 3, Edit Call
   and changing it to 13 saves 13.
2. Edit Call allows changing Channel, notes, counts, Low marks and
   Competitor Note text, and removing lines or notes.
3. Edit Call shows no Add product, Add pitch or Add competitor note action;
   "Record a follow-up call" is offered in their place.
4. Removing "Vitamin D 1000IU 90s", counted by mistake, removes it.
5. A Sent Call offers no Edit Call; Record follow-up call is offered.
6. A Call cannot be deleted from Edit Call.

Constraints:
- Use the project's existing conventions and test framework.
- Removing a Low mark never removes an order line (T7.8).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-5.3.1's saved-call behaviour; stop and show them passing. Resume only on
"Continue T-5.6.1".

Steps: 1. characterisation tests; 2. edit mode with allowed changes only;
3. follow-up offer in place of Add; 4. Sent-call lock; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-5.6.1-S; do not design
your own.

Definition of done: before syncing, a rep can change values in a saved call
and remove lines, but can't add anything — the screen offers "Record a
follow-up call" instead — and once synced the call is read-only.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the follow-up call itself (T-5.7.1), correcting synced calls
on the website (E13).
```

**Checkpoint**

Produces before pausing — characterisation tests of saved-call behaviour, passing.
Human reviews — Is there any path in Edit Call that adds content?
Resume trigger — `Continue T-5.6.1`

---

### T-5.7.1-S — Test scenarios for follow-up calls

**Owner** — Scenario Review
**Gates** — T-5.7.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for follow-up calls (task T-5.7.1). Read
plan_docs/stories/rep-at-a-location-tablet.md US-016 S1, S2 and edge cases,
and glossary (Follow-up Call). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S1 and
S2, then derivable edges: a follow-up to a Sent call, a follow-up to a
follow-up, two readings of the same product on the same day. Visit completion
(S3) is out of scope. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-5.7.1 — Record a follow-up call linked to the original

**Parent story**

> As a Field Salesperson, I want to record what I forgot or did later as a follow-up linked to the original Call so that the record is complete without rewriting history.
>
> Acceptance criteria:
> - Record follow-up call on a saved Call at Murphy's, Channel "Phone", counting "Nasal Spray 15ml" as 2, saves a new Call linked to the original with its own date and Channel (S1)
> - A 16:00 follow-up counting "Cold & Flu Relief 16s" 13 over the 09:40 count of 3 makes 13 the current stock level for Murphy's, keeping both readings (S2)
> - A follow-up to a Sent Call is itself a new Unsent Call (edge case)

**Slice** — From a saved or sent call, a rep records a follow-up call linked to it with its own channel and date, and a later count becomes the shop's current reading while both are kept.
**Spec source** — Rep at a Location US-016 S1, S2, edge case
**Depends on** — T-5.3.1
**Pattern to follow** — T-5.3.1 (Call recording)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — adds a link to the Call model and a "latest reading" rule.

**Provisional commit message**

```
feat(calls): record follow-up calls linked to the original

- Additions after a call is saved become a new, linked call, so the record
  stays true to what happened when
- The latest reading becomes the current stock level; earlier ones stay
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A model extension with a review pause.

**Agent prompt**

```
Role: You are adding follow-up calls in the Field Sales Management System's
tablet app.

Context:
- Slice: from a saved or sent call, a rep records a follow-up call linked to
  it with its own channel and date; a later count becomes the shop's current
  reading while both are kept.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-016 S1, S2, edge
  cases; glossary (Follow-up Call).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Call model and page
  (T-5.3.1), Edit Call's follow-up offer (T-5.6.1).
- Pattern to follow: T-5.3.1.

Acceptance criteria:
1. On a saved Call at Murphy's Pharmacy on 22 September 2026, Record
   follow-up call with Channel "Phone" counting "Nasal Spray 15ml" as 2 saves
   a new Call linked to the original, with its own date and Channel.
2. The original counted "Cold & Flu Relief 16s" as 3 at 09:40; a follow-up at
   16:00 counts 13. The current stock level for Murphy's is 13, and both
   readings are kept.
3. A follow-up to a Sent Call is a new Unsent Call.
4. The follow-up is reachable from Edit Call's offer (T-5.6.1) and from a
   Sent Call.

Constraints:
- Use the project's existing conventions and test framework.
- "Current stock level" is the latest reading by time across calls at the
  Location; implement it in one function.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  follow-up link on Calls.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the follow-up link and the latest-reading function are
written, stop and show them. Resume only on "Continue T-5.7.1".

Steps: 1. follow-up link; 2. latest-reading function; 3. entry points;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-5.7.1-S; do not design
your own.

Definition of done: from a saved or sent call, a rep records a follow-up call
linked to it with its own channel and date, and a later count becomes the
shop's current reading while both are kept.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: completing a Visit Due (E13), campaign outcomes added later
(E20).
```

**Checkpoint**

Produces before pausing — the follow-up link and the latest-reading function.
Human reviews — Does "current stock level" pick the latest reading correctly when calls are recorded out of order?
Resume trigger — `Continue T-5.7.1`

---

### T-5.8.1 — Add competitor notes to a call

**Parent story**

> As a Field Salesperson, I want to note competitor products I see, optionally linked to our products, a Range or a Category, so that head office knows where we're losing shelf space.
>
> Acceptance criteria:
> - A note "Competitor 250ml SPF30 on shelf" linked to Products, with both SPF30 sizes ticked from this Call's list, saves with 2 product links (S1)
> - A note linked to Category "Suncare" saves with 1 category link (S2)
> - A note with Link to: None saves with no links (S3)
> - With 2 products ticked, switching to Range asks "Switching will remove 2 linked products" and can be cancelled (S4)
> - 3 notes on one Call save separately and show in the save review count (S5)

**Slice** — On a call, a rep adds free-text competitor notes, each optionally linked to products (this call's counted products first), a category or a range, and they count in the save review.
**Spec source** — Rep at a Location US-018 S1–S5, edge cases; design decision "Competitor Notes: free text with one link type"
**Depends on** — T-5.3.1
**Pattern to follow** — T-5.3.1 (Call sections), T-5.4.1 (product search)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: M | Confidence: M
  (inferred) — settled design, fully specified, Blast Radius Low.

**Provisional commit message**

```
feat(calls): add competitor notes linked to products or a category

- Free text is fast to capture; one link type per note gives head office
  reporting by our product, range or category without structured fields
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Specified end to end on a settled screen.

**Agent prompt**

```
Role: You are adding competitor notes to calls in the Field Sales Management
System's tablet app.

Context:
- Slice: on a call, a rep adds free-text competitor notes, each optionally
  linked to products (this call's counted products first), a category or a
  range, and they count in the save review.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-018 S1–S5, edge
  cases, glossary (Competitor Note) and design decision "Competitor Notes:
  free text with one link type"; plan_docs/uxdocs/01-tablet-day.md T-06.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Call model and save
  review (T-5.3.1), product search (T-5.1.1).
- Pattern to follow: T-5.3.1's collapsed secondary sections.

Acceptance criteria:
1. Note "Competitor 250ml SPF30 on shelf", Link to: Products, ticking our
   SPF30 200ml and 300ml from this Call's counted list, saves with 2 product
   links.
2. Note "Rival suncare stand by the till", Link to: Category "Suncare", saves
   with 1 category link and no products.
3. Link to: None saves with no links.
4. With 2 products ticked, switching to Range asks "Switching will remove 2
   linked products" and can be cancelled.
5. 3 notes on one Call save separately and show as "3 competitor notes" in
   the save review.
6. The picker lists this Call's counted products first; unavailable and
   outside-range products are linkable; restricted products never appear.
7. The Range link type offers active Ranges from the snapshot (none until
   E9); archived Ranges are never offered for new notes.

Constraints:
- Use the project's existing conventions and test framework.
- One link type per note.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond
  competitor notes and their links on Calls.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. note model with one link type; 2. collapsed section on T-06;
3. picker; 4. switch-type confirmation; 5. save review count; 6. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: on a call, a rep adds free-text competitor notes, each
optionally linked to products (this call's counted products first), a
category or a range, and they count in the save review.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Ranges themselves (E9), competitor reporting (not designed).
```

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | A1-012 (T-5.1.1–T-5.1.6), A1-013 (T-5.2.1), A1-006 (T-5.3.1, T-5.3.2), A1-007 (T-5.4.1), A1-008 (T-5.5.1), A1-015 (T-5.6.1), A1-016 (T-5.7.1), A1-018 (T-5.8.1); deferred scenarios listed in the story table |
| Every task satisfies the three slice criteria | Pass | 14 of 14 |
| Every task carries a tier with a rationale citing dimensions | Pass | 14 of 14 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-5.1.6 (MI-49) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-5.1.3, T-5.1.4, T-5.1.5, T-5.6.1 checkpoints are characterisation passes |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-5.8.1 only; all Low or Medium |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-5.1.1 and T-5.1.6 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | 12 of 12 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-5.1.1 |
| Every scenario task precedes the task it gates | Pass | 13 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-02, 12, 49 |
