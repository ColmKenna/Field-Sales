# E8 — Taking products off sale

**Iteration** — 1, MVP: orders from the field reach the warehouse
**Outcome** — Head office can take a product off sale temporarily or for good; reps see why they can't order it but can still count it; orders already captured lose the line at the cut-off, and the rep is prompted to tell the shop.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Range Lifecycle US-008b — Mark a product Temporarily Unavailable | Must | S1–S6 (S5's "not listed among archive-made Unavailable" becomes observable with E9) |
| 2 | Range Lifecycle US-009 — Retire a product directly | Must | S1 without "Replaced by" (→ E10), S3 (S2 → E9, verified by Range Lifecycle US-006) |
| 3 | Rep at a Location US-010 — See availability states and count unavailable stock | Should | S2, S3 (replacements → E10), S5b, S6 (replacements → E10); S1's "no longer in an active range" reason arrives with E9; S4 and S5 → E10 |
| 4 | Head Office US-008 (part) — Orders within policy go through without acceptance | Must | S3 Auto-resolve an unavailable line |
| 5 | Rep at a Location US-003 (part) — See today as a record and what's at risk | Must | S8–S16 "Not supplied" |
| 6 | Rep at a Location US-014 (part) — Check a Sent Call or Order | Must | A1014-A removed-line wording |

Also delivered here: Rep at a Location US-007 S2 (unorderable products in a stock check, retired reason) and US-012 S11 (Low tab CAN'T ADD).

**Exit criterion** — Head office can mark a product Temporarily Unavailable with an expected-back date or retire it after seeing its impact, and reinstate either. After syncing, the rep sees each such product labelled in words and an icon, can count it but not order it, and sees any line captured earlier marked "Unavailable since you added it — will still be sent". At the cut-off such lines are removed with their reason, H-02 shows both totals, and the rep's Home counts orders with lines not supplied until the rep marks them "Told them" or calls the shop.

**Capability-class stamp** — Frontier workhorse for the tight-loop auto-resolve slivers, Agent-Assisted tasks and scenario drafting; Fast mid-tier for Agent-Autonomous tasks. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [range-lifecycle.md](../../stories/range-lifecycle.md), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-003, US-007, US-010, US-012, US-014), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-008), [02-head-office.md](../../uxdocs/02-head-office.md) (H-02, H-13, H-14), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-02 T2.5–T2.6, T-08 T8.3), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§2 Availability labels), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-001, BR-NEW-007, US-NEW-003, EC-NEW-007).

---

### T-8.1.1-S — Test scenarios for Temporarily Unavailable

**Owner** — Scenario Review
**Gates** — T-8.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for marking a product Temporarily Unavailable
(task T-8.1.1). Read plan_docs/stories/range-lifecycle.md US-008b S1–S4,
glossary (Product Availability State, Availability Rule) and design decision
"Temporarily Unavailable is separate from Unavailable", and
plan_docs/uxdocs/02-head-office.md H-14 (H14.1). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S1–S4,
then derivable edges: an Expected Back date in the past, removing the date,
a product that is also directly Unavailable. The passed-date behaviour is
assumed silent (MI-14). Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-8.1.1 — Mark a product temporarily unavailable with an expected-back date

**Parent story**

> As a Head Office User, I want to mark a product as out of stock but coming back, with the date I expect it, so that reps and customers wait for it instead of buying elsewhere.
>
> Acceptance criteria:
> - Setting "SPF30 Sun Lotion 200ml" Temporarily Unavailable, Expected Back 25 October 2026, makes it unorderable; customers see "Back in stock around 25 October" and reps the same with their sync date when stale (S1)
> - With no date it shows "Temporarily out of stock" (S2)
> - Changing the date to 8 November 2026 shows the new date with no escalation (S3)
> - Returning it to Active makes it orderable at its normal price, in its existing Ranges (S4)

**Slice** — A Head Office User marks a product Temporarily Unavailable with or without an expected-back date, moves the date when it slips, and returns it to Active, from the product's availability panel.
**Spec source** — Range Lifecycle US-008b S1–S4; glossary (Availability State, Availability Rule); uxdocs 02 H-14 (H14.1)
**Depends on** — T-1.2.1
**Pattern to follow** — T-1.6.1 (product record sections)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — introduces the availability model that E10 extends to five states and the range rule; checkpoint on the model; H-14 is a draft.

**Provisional commit message**

```
feat(availability): mark products temporarily unavailable with a date

- "Back around 25 October" tells a customer to wait; "no longer available"
  tells them to buy elsewhere, so the two states are kept apart
- The system holds no warehouse stock, so head office sets and clears it
  by hand, as with run-out quantities
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Introduces a shared state model with a design pause.

**Agent prompt**

```
Role: You are adding product availability, starting with Temporarily
Unavailable, to the Field Sales Management System's head office website.

Context:
- Slice: a Head Office User marks a product Temporarily Unavailable with or
  without an expected-back date, moves the date when it slips, and returns
  it to Active, from the product's availability panel.
- Specs: plan_docs/stories/range-lifecycle.md US-008b S1–S4, glossary
  (Product Availability State, Availability Rule, Direct Retirement) and
  design decisions "Product lifecycle with a manager-entered run-out",
  "Temporarily Unavailable is separate from Unavailable";
  plan_docs/uxdocs/02-head-office.md H-14 (H14.1 a panel over the product
  record, one choice per state, extra fields only when chosen) and H-13
  (H13.3 state sits top right and opens H-14).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — product record
  (T-1.2.1, T-1.6.1).
- Pattern to follow: T-1.6.1's record sections.

Acceptance criteria:
1. Setting "SPF30 Sun Lotion 200ml" Temporarily Unavailable with Expected
   Back 25 October 2026 makes it not orderable, and the product shows "Back
   in stock around 25 October".
2. With no date it shows "Temporarily out of stock".
3. Changing the date to 8 November 2026 shows the new date; nothing else
   happens.
4. Returning it to Active makes it orderable again, unchanged otherwise
   (price, ranges, replacements stand).
5. The state appears in product search (T-1.7.1) and on the record's top
   right, opening the H-14 panel.
6. The availability model is designed for all five states (Active,
   Discontinuing, Run-out, Temporarily Unavailable, Unavailable) and the
   range-derived Unavailable rule, which E10 and E9 add.

Constraints:
- Use the project's existing conventions and test framework.
- A passed Expected Back date changes nothing and prompts no one (assumed,
  MI-14).
- Keep one function that answers "is this product orderable, and why not?"
  for every surface.
- No tests of framework internals or trivial members.
- This task opts in to availability columns on products.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the availability model and the "orderable and why not"
function are designed for all five states, stop and show them. Resume only on
"Continue T-8.1.1".

Steps: 1. availability model and function; 2. persistence; 3. H-14 panel
with Temporarily Unavailable; 4. record and search display; 5. tests from
agreed scenarios.

Test expectations: implement the scenarios agreed in T-8.1.1-S; do not design
your own.

Definition of done: a Head Office User marks a product Temporarily
Unavailable with or without an expected-back date, moves the date when it
slips, and returns it to Active, from the product's availability panel.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: retiring (T-8.2.1), tablet labels (T-8.3.1), Discontinuing and
Run-out (E10), range-derived availability (E9), customer wording (E26).
```

**Checkpoint**

Produces before pausing — the availability model and the single "orderable and why not" function covering all five states.
Human reviews — Will Discontinuing, Run-out and the any-active-range rule slot in without changing this function's callers?
Resume trigger — `Continue T-8.1.1`

---

### T-8.2.1-S — Test scenarios for retiring a product

**Owner** — Scenario Review
**Gates** — T-8.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for retiring a product directly with its impact
shown (task T-8.2.1). Read plan_docs/stories/range-lifecycle.md US-009 S1, S3
and glossary (Impact, Direct Retirement, Open orders go through), and
plan_docs/uxdocs/02-head-office.md H-14 (H14.2). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning T-8.1.1's availability function, then
cover S1 (without Replaced by) and S3, then derivable edges: a product
stocked nowhere, retiring a Temporarily Unavailable product, reinstating a
retired one. The story's "open orders" include In Progress and Ready to Send
orders that exist only on tablets (MI-44) — mark that "Needs a decision".
Write no test code; change no files.
```

---

### T-8.2.1 — Retire a product after seeing its impact

**Parent story**

> As a Head Office User, I want to make a single product unavailable regardless of its Ranges, seeing its impact first, so that a superseded line stops being ordered without archiving anything.
>
> Acceptance criteria:
> - Setting "SPF30 Sun Lotion 200ml" Unavailable shows "Stocked in 87 locations · 14 orders in 30 days · 2 open orders will still be processed" before confirming; on confirm it is Unavailable (S1; setting Replaced by → E10; "Core Stock stays Active" → E9)
> - Returning it to Active makes it orderable again (S3)

**Slice** — A Head Office User makes a product Unavailable after seeing how many shops stock it, how many recent orders had it and how many open orders will still be processed, and can reinstate it later.
**Spec source** — Range Lifecycle US-009 S1, S3; glossary (Impact, Direct Retirement); uxdocs 02 H-14 (H14.2)
**Depends on** — T-8.1.1, T-5.4.1, T-7.1.1
**Pattern to follow** — T-8.1.1 (availability panel), T-3.1.2 (impact preview)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the impact reads stock checks and orders; "open orders" can't include tablet-only ones (MI-44); modifies T-8.1.1's function, so characterisation comes first.

**Provisional commit message**

```
feat(availability): retire a product after seeing its impact

- A superseded line must stop being ordered without archiving a range, and
  the impact shows what that takes off sale before it happens
- Open orders still go through, as with any change after capture
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Impact computed across areas, with a review pause.

**Agent prompt**

```
Role: You are adding direct retirement with an impact preview (H-14) to the
Field Sales Management System.

Context:
- Slice: a Head Office User makes a product Unavailable after seeing how many
  shops stock it, how many recent orders had it and how many open orders will
  still be processed, and can reinstate it later.
- Specs: plan_docs/stories/range-lifecycle.md US-009 S1, S3, glossary
  (Impact: Locations stocking it = last Stock Check > 0; Accepted orders in
  the last 30 days; open orders) and design decision "Archive always shows
  impact, never escalates"; plan_docs/uxdocs/02-head-office.md H-14 (H14.2
  impact only for Unavailable; the button names the change "Make
  unavailable").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — availability model and
  function (T-8.1.1), stock checks (T-5.4.1), orders (T-7.1.1).
- Pattern to follow: T-3.1.2's preview-then-confirm.

Acceptance criteria:
1. Choosing Unavailable for "SPF30 Sun Lotion 200ml" shows "Stocked in 87
   locations · 14 orders in 30 days · 2 open orders will still be processed"
   before confirming; the button reads "Make unavailable".
2. On confirm the product is Unavailable (reason "retired") and not
   orderable.
3. Returning it to Active makes it orderable again.
4. "Open orders" counts orders the server knows (synced, Pending or Held)
   per the MI-44 decision; the wording follows that decision.
5. The impact's 30-day window is a setting (MI-12).

Constraints:
- Use the project's existing conventions and test framework.
- Extend T-8.1.1's "orderable and why not" function; no second rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-8.1.1's availability function; stop and show them passing with the impact
queries. Resume only on "Continue T-8.2.1".

Steps: 1. characterisation tests; 2. Unavailable (retired) in the model;
3. impact queries; 4. H-14 flow; 5. reinstate; 6. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-8.2.1-S; do not design
your own.

Definition of done: a Head Office User makes a product Unavailable after
seeing how many shops stock it, how many recent orders had it and how many
open orders will still be processed, and can reinstate it later.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: setting Replaced by (E10), range archive interaction (E9),
removing lines at the cut-off (T-8.4.1), tablet labels (T-8.3.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of the availability function, passing, and the three impact queries.
Human reviews — Do the impact counts mean what the wording says, given the server can't see tablet-only orders?
Resume trigger — `Continue T-8.2.1`

---

### T-8.3.1-S — Test scenarios for availability labels on the tablet

**Owner** — Scenario Review
**Gates** — T-8.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for availability labels on the tablet (task
T-8.3.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-010 S2, S3,
S5b, S6 and US-007 S2; plan_docs/uxdocs/00-conventions-and-shared-elements.md
§2 and §3; plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-007. Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Start with characterisation scenarios pinning current stock
check (T-5.4.1) and order entry (T-5.1.x) behaviour, then cover the story
scenarios (retired and temporarily unavailable only; replacements, range
reasons, Discontinuing and Run-out come later), then derivable edges: last
sync today versus yesterday, a product that became available again. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-8.3.1 — Label unorderable products on the tablet and keep them countable

**Parent story**

> As a Field Salesperson, I want each product's availability shown clearly — unavailable stock countable but not orderable, discontinuing and run-out stock orderable with an honest flag — so that I count what's on the shelf and never promise what head office can't supply.
>
> Acceptance criteria:
> - Searching an Unavailable product in Order entry shows it as unavailable with its label; it can't be added (S2)
> - "Autumn Cough Syrup 100ml", retired directly, shows "Unavailable — retired" (S3; its Replacements arrive with E10)
> - "SPF30 Sun Lotion 200ml", Temporarily Unavailable with Expected Back 25 October 2026, shows "Back in stock around 25 Oct" and can't be added, but can be counted (S5b; "as of" only when the last sync wasn't today)
> - A product on an In Progress Order from before this morning's Sync, now Unavailable, shows "Unavailable since you added it — will still be sent" and can't be added to any other Order (S6)
> - On a stock check, an Unavailable product shows its label and can still be counted (US-007 S2)

**Slice** — After syncing, the tablet labels retired and temporarily unavailable products in words and an icon wherever they appear; they stay findable and countable but can't be added, and a line added before the change says it will still be sent.
**Spec source** — Rep at a Location US-010 S2, S3, S5b, S6, non-functional notes; US-007 S2; uxdocs 00 §2 and §3; uxdocs 04 BR-NEW-007; design decision "Ranges guide; Unavailable and Restricted limit"
**Depends on** — T-8.2.1, T-5.4.1, T-5.1.2
**Pattern to follow** — T-8.1.1 (availability function)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies stock check and order entry and adds snapshot fields, so characterisation comes first.

**Provisional commit message**

```
feat(tablet): label unorderable products and keep them countable

- Old stock on the shelf is still worth counting, and a rep who can't find
  a product assumes the app is broken, so unorderable products stay
  visible with a reason in words, never colour alone
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies two core tablet screens behind a characterisation pass.

**Agent prompt**

```
Role: You are adding availability labels to the Field Sales Management
System's tablet app.

Context:
- Slice: after syncing, the tablet labels retired and temporarily
  unavailable products in words and an icon wherever they appear; they stay
  findable and countable but can't be added, and a line added before the
  change says it will still be sent.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-010 S2, S3, S5b,
  S6 and non-functional notes, US-007 S2;
  plan_docs/uxdocs/00-conventions-and-shared-elements.md §2 (labels) and §3
  ("As of sync"); plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-007.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — availability function
  (T-8.1.1, T-8.2.1), snapshot (T-4.1.1), stock check (T-5.4.1), order entry
  (T-5.1.1, T-5.1.2), popover (T-5.5.1).
- Pattern to follow: call the same availability function the server uses.

Acceptance criteria:
1. "Autumn Cough Syrup 100ml", retired directly, shows "Unavailable —
   retired" with text and an icon, in search, on the pad and on a stock
   check.
2. It can be counted on a stock check but can't be added to an order.
3. "SPF30 Sun Lotion 200ml", Temporarily Unavailable with Expected Back 25
   October 2026, shows "Back in stock around 25 Oct"; with no date,
   "Temporarily out of stock"; it can be counted but not added.
4. After a sync today no "as of" wording appears; after a sync on Mon 21 Sep
   it reads "Back in stock around 25 Oct (as of Mon 21 Sep)".
5. A product on an In Progress Order from before this morning's Sync, now
   Unavailable, shows "Unavailable since you added it — will still be sent"
   and can't be added to any other order.
6. Status is never shown by colour alone.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions (availability state, expected-back date) go through
  T-4.1.1's versioning and its owner's review.
- One availability function shared with the server (T-8.1.1); no second
  rule on the tablet.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the stock check's and order entry's current add and count behaviour; stop and
show them passing with the snapshot additions. Resume only on
"Continue T-8.3.1".

Steps: 1. characterisation tests; 2. snapshot fields; 3. labels with icons
on every product row; 4. block adding; 5. "since you added it" line state;
6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-8.3.1-S; do not design
your own.

Definition of done: after syncing, the tablet labels retired and temporarily
unavailable products in words and an icon wherever they appear; they stay
findable and countable but can't be added, and a line added before the
change says it will still be sent.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: replacements (E10), Discontinuing and Run-out labels (E10),
"no longer in an active range" (E9), the Low tab CAN'T ADD state (T-8.3.2).
```

**Checkpoint**

Produces before pausing — characterisation tests of stock check and order entry, passing, and the snapshot additions.
Human reviews — Is every unorderable product still findable and countable, with a reason in words?
Resume trigger — `Continue T-8.3.1`

---

### T-8.3.2-S — Test scenarios for CAN'T ADD on the Low tab

**Owner** — Scenario Review
**Gates** — T-8.3.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the CAN'T ADD state on the Low tab (task
T-8.3.2). Read plan_docs/stories/rep-at-a-location-tablet.md US-012 S7, S8,
S11. Output one line per scenario as Should_Outcome_When_Condition, then "→"
and a one-line intent. Start with characterisation scenarios pinning the Low
tab's current states (T-5.1.4, T-5.1.5), then cover S11 and the count rule
in S8 (CAN'T ADD counts), then derivable edges: a carried gap that became
orderable again, a Temporarily Unavailable Low item. "Find replacement"
arrives with E10. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-8.3.2 — Show CAN'T ADD on the Low tab for unorderable items

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - A Low item that is unorderable, with no replacement added, shows CAN'T ADD with the availability reason and no quantity control (S11; "Find replacement" arrives with E10)
> - CAN'T ADD items count in the Low tab label (S8)

**Slice** — On the Low tab, an item marked Low whose product can't be ordered shows CAN'T ADD with its availability reason and no quantity control, and counts toward the tab's number.
**Spec source** — Rep at a Location US-012 S8, S11; uxdocs 04 US-NEW-002
**Depends on** — T-8.3.1, T-5.1.4
**Pattern to follow** — T-5.1.4 (derived Low states)
**Ownership** — Impl: Agent-Autonomous | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — a small addition to a reviewed state list; tests held at Scenario Review because it modifies existing behaviour.

**Provisional commit message**

```
feat(orders): show can't-add items on the low tab

- A shelf gap the customer can't be sold is still a gap, so it stays on
  the tab with its reason instead of silently dropping off
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A small, specified addition.

**Agent prompt**

```
Role: You are adding the CAN'T ADD state to the Low tab in the Field Sales
Management System's tablet app.

Context:
- Slice: on the Low tab, an item marked Low whose product can't be ordered
  shows CAN'T ADD with its availability reason and no quantity control, and
  counts toward the tab's number.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-012 S7, S8, S11;
  plan_docs/uxdocs/04-user-stories-amendments.md US-NEW-002.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Low tab state
  derivation (T-5.1.4, T-5.1.5), availability function (T-8.1.1, T-8.3.1).
- Pattern to follow: T-5.1.4's derived states.

Acceptance criteria:
1. A Low item whose product is Unavailable or Temporarily Unavailable, with
   no replacement added, shows CAN'T ADD with the reason (e.g. "Unavailable —
   retired", "Back in stock around 25 Oct") and no quantity control.
2. CAN'T ADD items count in the Low tab label alongside not added items.
3. Carried gaps (T-5.1.5) show CAN'T ADD the same way.
4. If the product becomes orderable again at the next sync, the item returns
   to not added.

Constraints:
- Use the project's existing conventions and test framework.
- Derive the state from the availability function; store nothing new.
- No "Find replacement" action yet (E10).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. characterisation tests pinning current Low tab states; 2. CAN'T ADD
in the derivation; 3. display; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-8.3.2-S, starting with
their characterisation scenarios; do not design your own.

Definition of done: on the Low tab, an item marked Low whose product can't be
ordered shows CAN'T ADD with its availability reason and no quantity
control, and counts toward the tab's number.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: REPLACED and "Find replacement" (E10).
```

---

### T-8.4.1-S — Test scenarios for removing unavailable lines at the cut-off

**Owner** — Human-Led
**Gates** — T-8.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Temporarily Unavailable at the cut-off (MI-46): Range Lifecycle US-008b S6 says lines "still process", while BR-NEW-001 removes lines that are "now Unavailable". Is a temporarily unavailable line removed, kept (and short-shipped), or routed to allocation as Outstanding?
- An order whose every line is unavailable: accepted with nothing, or rejected?
- A free-of-charge line whose product became unavailable (EC-NEW-003) — removed the same way (applies once E19 exists)?
- A line on a held order: removed at release or at hold?
- What exactly is recorded: reason text, time, and the removed quantity and value?

---

### T-8.4.1 — Remove unavailable lines at the cut-off with their reason

**Parent story**

> As a Head Office User, I want orders that are within policy to go through without my acceptance so that my worklist only holds things that genuinely need a decision.
>
> Acceptance criteria:
> - A synced order with a line that is now Unavailable is accepted at the cut-off; that line is removed with its reason recorded; the capturing rep is prompted to tell the customer (S3; the prompt is T-8.5.1)
> - H-02 shows the removed line "(x) Not supplied — no longer … Removed automatically." and both totals (H-02 frame; H2.3)

**Slice** — At the cut-off, a line whose product is now unavailable is removed from the order with its reason recorded, the rest of the order is accepted, and H-02 shows the removed line and the difference between captured and accepted totals.
**Spec source** — Head Office US-008 S3; uxdocs 04 BR-NEW-001 (Auto-resolve); uxdocs 02 H-01 (H1.7), H-02 (frame, H2.3)
**Depends on** — T-8.2.1, T-7.1.1
**Pattern to follow** — T-7.1.1 (acceptance job), T-7.4.1 (annotations)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — modifies acceptance (T-7.1.1), so characterisation comes first; whether Temporarily Unavailable lines are removed is unresolved (MI-46), so Oracle Ambiguity is High.

**Provisional commit message**

```
feat(order-processing): remove unavailable lines at the cut-off

- Stopping an order for a line nobody can supply achieves nothing, so the
  line is removed with its reason and the rest goes through
- The captured total is kept beside the accepted total, because the
  difference is what the rep must explain to the customer
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Changes the acceptance job; slivers stay narrow.

**Work package**

Increments:
1. Characterisation: pin T-7.1.1's acceptance behaviour and totals before changing anything.
2. Decide MI-46 and record the rule.
3. At acceptance, for each line whose product is unorderable by the agreed rule: mark it removed with the reason from the availability function and the time; exclude it from release.
4. "Total as captured" stays; "Total accepted" excludes removed lines.
5. Record, per order, which removed lines the rep has not yet been told about (read by T-8.5.1 through the snapshot).
6. H-02 shows the removed line in the frame's wording and an annotation "1 line could not be supplied and was removed - <rep> prompted to tell the customer".

Decision points:
- MI-46 (Temporarily Unavailable).
- An order with every line removed — accept an empty order, or reject it?
- Is the release to the warehouse sent after removal only (never including the removed line)?

Delegable slivers:
- **H-02 removed-line display** — Show removed lines on H-02 as "(x) Not supplied - <reason>. Removed automatically." and the annotation sentence, reading the stored removal records. Do not change acceptance.
- **Totals test** — Given the agreed scenarios, write tests asserting "Total as captured" and "Total accepted" for orders with 0, 1 and all lines removed. Test-only.

---

### T-8.5.1-S — Test scenarios for "not supplied" on the tablet

**Owner** — Scenario Review
**Gates** — T-8.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the "not supplied" prompt on the tablet
(task T-8.5.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-003
S8–S16; plan_docs/uxdocs/01-tablet-day.md T-02 (T2.5, T2.6);
plan_docs/uxdocs/04-user-stories-amendments.md US-NEW-003 and EC-NEW-007.
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Start with characterisation scenarios pinning Home's current
strip (T-4.5.1). Cover S8–S16 and EC-NEW-007, then derivable edges: "Told
them" offline and synced later, Undo after syncing, two orders at one
Location cleared by one Call. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-8.5.1 — Prompt the rep about lines not supplied until they've told the shop

**Parent story**

> As a Field Salesperson, I want Home to show today's visits, what I've done, Overdue and Due soon items so that I know where I'm going, what I've captured and what I might miss.
>
> Acceptance criteria:
> - After a sync following a removal, Home shows a "not supplied" count in the exception strip (S8); tapping it lists each affected order with its Location, removed lines and reason (S9); no removals, no count (S11)
> - "Told them" records every untold removed line on that order as told with the time, and the order stops counting (S12)
> - A just-told order stays in the list greyed with "Told [time]" and Undo until the rep leaves the list; Undo restores the count (S13)
> - Logging a Call at the Location records its orders' untold lines as told (S14)
> - A later removal on a told order makes it count again with only the new line flagged; earlier lines keep their told record (S15; EC-NEW-007)
> - Opening an affected order without tapping "Told them" leaves it counting (S16)

**Slice** — After a sync, Home counts orders with lines that couldn't be supplied; the rep sees which lines and why, and each order stops counting when the rep taps "Told them" (with Undo) or records a call at that shop, until a further line is removed.
**Spec source** — Rep at a Location US-003 S8–S16; uxdocs 01 T-02 (T2.5, T2.6); uxdocs 04 US-NEW-003, EC-NEW-007
**Depends on** — T-8.4.1, T-4.5.1
**Pattern to follow** — T-4.5.1 (Home's exception strip)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — "told" records travel up through sync and removals come down; modifies Home's strip (T-4.5.1), so characterisation comes first.

**Provisional commit message**

```
feat(tablet): prompt reps to tell shops about lines not supplied

- For most shops the rep is the only channel to the customer, and the
  total has changed since they quoted it, so the rep is prompted until
  they declare they've told the shop
- A call at the shop is the backstop, because reps phone the same day
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Sync round-trip and a shared screen, with a review pause.

**Agent prompt**

```
Role: You are adding the "not supplied" prompt to the Field Sales Management
System's tablet Home screen.

Context:
- Slice: after a sync, Home counts orders with lines that couldn't be
  supplied; the rep sees which lines and why; each order stops counting when
  the rep taps "Told them" (with Undo) or records a call at that shop, until
  a further line is removed.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-003 S8–S16 and
  glossary (Not supplied); plan_docs/uxdocs/01-tablet-day.md T-02 (T2.5 a
  fourth counter; T2.6 "Told them" per order, Undo until leaving the list,
  the next Call as backstop); plan_docs/uxdocs/04-user-stories-amendments.md
  US-NEW-003 and EC-NEW-007.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — removal records
  (T-8.4.1), snapshot and upload (T-4.1.1, T-4.1.2), Home strip (T-4.5.1),
  calls (T-5.3.1).
- Pattern to follow: T-4.5.1's exception strip.

Acceptance criteria:
1. After a sync following a removal, Home's exception strip shows a "not
   supplied" count of affected orders; with none, no count.
2. Tapping it lists each affected order with its Location, removed lines and
   reason.
3. "Told them" on an order (in the list or on the sent order) records every
   untold removed line on it as told with the time, and it stops counting.
4. A just-told order stays in the list greyed with "Told 14:20" and Undo
   until the rep leaves the list; Undo removes the told record and it counts
   again.
5. Logging a Call at that Location, by visit or phone, records its orders'
   untold lines as told.
6. A later removal on a told order makes it count again with only the new
   line flagged; earlier lines keep their told time.
7. Opening an affected order and leaving without "Told them" keeps it
   counting.
8. Told records upload at the next sync and survive the snapshot download.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions (removed lines per order with reason and told state)
  go through T-4.1.1's versioning; told records upload through T-4.1.2's
  protocol.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond told
  records.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
Home's strip; stop and show them passing with the told-record design (tablet
store, upload, snapshot merge). Resume only on "Continue T-8.5.1".

Steps: 1. characterisation tests; 2. snapshot and upload fields; 3. count and
list; 4. Told them and Undo; 5. call backstop; 6. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-8.5.1-S; do not design
your own.

Definition of done: after a sync, Home counts orders with lines that couldn't
be supplied; the rep sees which lines and why, and each order stops counting
when the rep taps "Told them" (with Undo) or records a call at that shop,
until a further line is removed.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–8 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the sent order's removed-line wording (T-8.6.1), other Home
counters (E13, E15).
```

**Checkpoint**

Produces before pausing — characterisation tests of Home's strip, passing, and the told-record design across tablet store, upload and snapshot.
Human reviews — Can a told record be lost between an offline "Told them" and the next sync, or be overwritten by a download?
Resume trigger — `Continue T-8.5.1`

---

### T-8.6.1-S — Test scenarios for removed lines on the sent order

**Owner** — Scenario Review
**Gates** — T-8.6.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for removed-line wording on the tablet's sent
order (task T-8.6.1). Read plan_docs/stories/rep-at-a-location-tablet.md
US-014 A1014-A and plan_docs/uxdocs/01-tablet-day.md T-08 (T8.3). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Start with characterisation scenarios pinning the sent item view
(T-7.5.1). Cover A1014-A and "Told them" beside the lines, then derivable
edges: two removed lines, a told order. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-8.6.1 — Say "Let the customer know" on removed lines of a sent order

**Parent story**

> As a Field Salesperson, I want to see a Sent item's last known status and open it on the website so that I answer customers honestly and make changes when I have signal.
>
> Acceptance criteria:
> - A line removed as unavailable reads "Not supplied — [reason]. Removed from the order. Let the customer know." on the sent item (A1014-A)
> - "Told them" sits beside the removed lines (T8.3)

**Slice** — On a sent order, each removed line reads "Not supplied — [reason]. Removed from the order. Let the customer know." with "Told them" beside it.
**Spec source** — Rep at a Location US-014 A1014-A; uxdocs 01 T-08 (T8.3)
**Depends on** — T-8.5.1, T-7.5.1
**Pattern to follow** — T-7.5.1 (sent item view)
**Ownership** — Impl: Agent-Autonomous | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — settled wording on a reviewed screen; tests held at Scenario Review because it modifies existing behaviour.

**Provisional commit message**

```
feat(tablet): show not-supplied lines on the sent order

- The sent order is where a rep checks what the customer will actually
  get, so a removed line says so and what to do about it
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Settled wording on an existing screen.

**Agent prompt**

```
Role: You are adding removed-line wording to the tablet's sent order view
(T-08) in the Field Sales Management System.

Context:
- Slice: on a sent order, each removed line reads "Not supplied — [reason].
  Removed from the order. Let the customer know." with "Told them" beside it.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-014 A1014-A;
  plan_docs/uxdocs/01-tablet-day.md T-08 (DECISION T8.3: "Told them" sits
  beside the removed lines).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — sent item view
  (T-7.5.1), removed lines and told records (T-8.5.1).
- Pattern to follow: T-7.5.1.

Acceptance criteria:
1. A line removed as unavailable reads "Not supplied — [reason]. Removed from
   the order. Let the customer know."
2. "Told them" appears beside the removed lines and records the order as
   told, as in T-8.5.1, with Undo.
3. Once told, the lines show "Told [time]".

Constraints:
- Use the project's existing conventions and test framework; reuse
  T-8.5.1's told action.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. characterisation tests pinning T-7.5.1's view; 2. removed-line
wording; 3. Told them beside lines; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-8.6.1-S, starting with
their characterisation scenarios; do not design your own.

Definition of done: on a sent order, each removed line reads "Not supplied —
[reason]. Removed from the order. Let the customer know." with "Told them"
beside it.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: rep prices and FOC as facts (E19), website view (E13).
```

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | RL-008b (T-8.1.1), RL-009 (T-8.2.1), A1-010 part (T-8.3.1, T-8.3.2), HO-008 S3 (T-8.4.1), A1-003 S8–S16 (T-8.5.1), A1-014 A1014-A (T-8.6.1) |
| Every task satisfies the three slice criteria | Pass | 7 of 7 |
| Every task carries a tier with a rationale citing dimensions | Pass | 7 of 7 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-8.4.1 (MI-46), T-8.2.1 (MI-44), T-8.1.1 (MI-14) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-8.2.1, T-8.3.1, T-8.3.2, T-8.4.1, T-8.5.1, T-8.6.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-8.3.2, T-8.6.1 are small and settled |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-8.4.1 is Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-8.1.1, T-8.2.1, T-8.3.1, T-8.5.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-8.4.1 |
| Every scenario task precedes the task it gates | Pass | 7 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-12, 14, 44, 46 |
