# E10 — Product lifecycle and replacements

**Iteration** — 2, Catalogue lifecycle
**Outcome** — Head office moves products through Discontinuing and Run-out to Unavailable, links replacements from either end, and marks products to watch; reps see honest flags with successors and reorder onto current stock.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Range Lifecycle US-010 — Link Replacements from either end | Must | S1–S5 (also US-009 S1's "can set Replaced by") |
| 2 | Range Lifecycle US-007 — Mark a product Discontinuing | Should | S1–S3 |
| 3 | Range Lifecycle US-008 — Put a product into Run-out with a quantity | Must | S1–S6 |
| 4 | Rep at a Location US-011 — See and order Replacements for an unavailable or discontinuing product | Should | S1–S5 (also US-012 S10 REPLACED) |
| 5 | Head Office US-007 — Mark a Watched Product | Could | S1–S2 |
| 6 | Rep at a Location US-010 (part) — See availability states and count unavailable stock | Should | S3's and S6's Replacements, S4, S5 |
| 7 | Head Office US-002 (part) — See why an order is flagged | Must | S5 Oversold, as an annotation |

**Exit criterion** — A Head Office User can link replacements from either product, mark a product Discontinuing, put it into Run-out with a quantity whose Remaining counts accepted and pending orders and turns Unavailable at zero, and mark products as Watched. On the tablet, Discontinuing and Run-out products stay orderable with their flags and successors, Remaining is shown as an estimate, and a Low item that can't be ordered can be replaced from its successors. Oversold and Watched products are annotated on accepted orders.

**Capability-class stamp** — Frontier + extended reasoning for the Remaining calculation (T-10.3.1); Frontier workhorse for Agent-Assisted tasks and scenario drafting; Fast mid-tier for T-10.7.1. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [range-lifecycle.md](../../stories/range-lifecycle.md), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-010, US-011, US-012), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-002, US-007), [02-head-office.md](../../uxdocs/02-head-office.md) (H-13, H-14), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§1c, §2).

---

### T-10.1.1-S — Test scenarios for replacement links

**Owner** — Scenario Review
**Gates** — T-10.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for linking replacements from either end (task
T-10.1.1). Read plan_docs/stories/range-lifecycle.md US-010 S1–S5 and design
decision "Replacements from either end, any time". Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover S1–S5, then derivable edges: a cycle (A replaces B replaces A),
removing a link, a Restricted replacement, a replacement in an archived
category. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-10.1.1 — Link replacements from either product

**Parent story**

> As a Head Office User, I want to record that one product replaces another from whichever product I'm looking at, and see it on both, so that reps are always shown the right successor.
>
> Acceptance criteria:
> - Setting "SPF30 Sun Lotion v2 200ml" Replaces "SPF30 Sun Lotion 200ml" shows the old product Replaced by the new (S1)
> - Setting "Kids SPF50 Spray 150ml" Replaced by "v2 100ml" and "v2 250ml" shows both new products Replaces the old (S2)
> - A link between two Active products is saved; reps see nothing until the old one leaves Active (S3)
> - If "v2 100ml" later becomes Unavailable with its own Replaced by "v3 100ml", the old product shows "v2 100ml (Unavailable — replaced by v3 100ml)" (S4)
> - A product can't be its own Replacement (S5)

**Slice** — A Head Office User links a product to one or more successors from either product's record, each record shows the link both ways, and a successor that itself goes unavailable shows its own successor.
**Spec source** — Range Lifecycle US-010 S1–S5; US-009 S1 ("can set Replaced by"); uxdocs 02 H-13 (replacements section)
**Depends on** — T-1.6.1
**Pattern to follow** — T-1.6.1 (record sections)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — a product-to-product graph that the tablet, availability panel and archive screen read; checkpoint on the link model.

**Provisional commit message**

```
feat(catalogue): link replacements from either product

- A new product often arrives before the old one goes, so links can be set
  any time and read both ways; a link visible from one end looks missing
  from the other
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared link model with a design pause.

**Agent prompt**

```
Role: You are adding replacement links to products in the Field Sales
Management System.

Context:
- Slice: a Head Office User links a product to one or more successors from
  either product's record; each record shows the link both ways; a successor
  that itself goes unavailable shows its own successor.
- Specs: plan_docs/stories/range-lifecycle.md US-010 S1–S5, US-009 S1,
  glossary (Replacement) and design decision "Replacements from either end,
  any time"; plan_docs/uxdocs/02-head-office.md H-13 (replacements section:
  "Replaces" and "Replaced by").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — product record
  (T-1.6.1), availability function (T-9.2.1), retire flow (T-8.2.1).
- Pattern to follow: T-1.6.1's record sections.

Acceptance criteria:
1. On "SPF30 Sun Lotion v2 200ml", setting Replaces "SPF30 Sun Lotion 200ml"
   makes the old product show Replaced by "SPF30 Sun Lotion v2 200ml".
2. On "Kids SPF50 Spray 150ml", setting Replaced by "v2 100ml" and "v2 250ml"
   makes both show Replaces "Kids SPF50 Spray 150ml".
3. A link between two Active products saves; nothing changes for reps yet.
4. If "v2 100ml" becomes Unavailable with Replaced by "v3 100ml", the old
   product shows "v2 100ml (Unavailable — replaced by v3 100ml)".
5. Setting a product as its own Replacement is rejected.
6. The retire flow (T-8.2.1) can set Replaced by before confirming.

Constraints:
- Use the project's existing conventions and test framework.
- One stored link read both ways; never two copies.
- Decide at the checkpoint whether cycles are allowed; reject self-links.
- No tests of framework internals or trivial members.
- This task opts in to a replacement-link table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the link model and the cycle rule are designed, stop and
show them. Resume only on "Continue T-10.1.1".

Steps: 1. link model; 2. record sections both ways; 3. chained display;
4. hook into the retire flow; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-10.1.1-S; do not design
your own.

Definition of done: a Head Office User links a product to one or more
successors from either product's record, each record shows the link both
ways, and a successor that itself goes unavailable shows its own successor.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: tablet display (T-10.4.1), the tablet replacement picker
(T-10.5.1), setting replacements during a Range archive (T-9.6.1 uses this).
```

**Checkpoint**

Produces before pausing — the replacement link model and the rule on cycles.
Human reviews — Can a chain of replacements ever loop, and does every surface read the same links?
Resume trigger — `Continue T-10.1.1`

---

### T-10.2.1-S — Test scenarios for Discontinuing

**Owner** — Scenario Review
**Gates** — T-10.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for marking a product Discontinuing (task
T-10.2.1). Read plan_docs/stories/range-lifecycle.md US-007 S1–S3 and
glossary (Product Availability State). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning the availability function (T-8.1.1,
T-8.2.1, T-9.2.1). Cover S1–S3, then derivable edges: Discontinuing a product
in an archived-only range, moving Discontinuing to Run-out. Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-10.2.1 — Mark a product Discontinuing while it stays orderable

**Parent story**

> As a Head Office User, I want to flag a product as going, with its replacement, while it is still orderable so that reps run stock down and pitch the new line in advance.
>
> Acceptance criteria:
> - Setting "SPF30 Sun Lotion 200ml" (Replaced by "SPF30 Sun Lotion v2 200ml") Discontinuing keeps it orderable, and reps' pads show "Discontinuing — replaced by SPF30 Sun Lotion v2 200ml" at next Sync (S1; tablet display in T-10.4.1)
> - Discontinuing with no Replacement is accepted; reps see "Discontinuing" (S2)
> - Returning it to Active removes the flag at next Sync (S3)

**Slice** — A Head Office User marks a product Discontinuing from its availability panel, it stays orderable with its successor named, and returning it to Active clears the flag.
**Spec source** — Range Lifecycle US-007 S1–S3; uxdocs 02 H-14 (H14.1)
**Depends on** — T-10.1.1, T-8.1.1
**Pattern to follow** — T-8.1.1 (availability panel)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — extends the shared availability function, so characterisation comes first.

**Provisional commit message**

```
feat(availability): mark products discontinuing while still orderable

- Reps run stock down and pitch the successor before the product goes, so
  Discontinuing is orderable and names its replacement
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Extends a shared rule behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the Discontinuing state to product availability in the
Field Sales Management System.

Context:
- Slice: a Head Office User marks a product Discontinuing from its
  availability panel; it stays orderable with its successor named; returning
  it to Active clears the flag.
- Specs: plan_docs/stories/range-lifecycle.md US-007 S1–S3, glossary;
  plan_docs/uxdocs/02-head-office.md H-14 (H14.1).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — availability model and
  function (T-8.1.1, T-9.2.1), replacements (T-10.1.1).
- Pattern to follow: T-8.1.1's panel.

Acceptance criteria:
1. Setting "SPF30 Sun Lotion 200ml" Discontinuing keeps it orderable; its
   state reads "Discontinuing — replaced by SPF30 Sun Lotion v2 200ml".
2. With no Replacement it reads "Discontinuing".
3. Returning it to Active removes the state.
4. The availability function reports Discontinuing as orderable, with its
   replacements, for every surface.

Constraints:
- Use the project's existing conventions and test framework.
- Extend the one availability function; no second rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the availability function; stop and show them passing. Resume only on
"Continue T-10.2.1".

Steps: 1. characterisation tests; 2. Discontinuing in the model and panel;
3. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-10.2.1-S; do not design
your own.

Definition of done: a Head Office User marks a product Discontinuing from its
availability panel, it stays orderable with its successor named, and
returning it to Active clears the flag.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: tablet labels (T-10.4.1), free-of-charge on Discontinuing
products (E19).
```

**Checkpoint**

Produces before pausing — characterisation tests of the availability function, passing.
Human reviews — Do all earlier states still resolve exactly as before?
Resume trigger — `Continue T-10.2.1`

---

### T-10.3.1-S — Test scenarios for Run-out and Remaining

**Owner** — Human-Led
**Gates** — T-10.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Remaining = Run-out Quantity − quantity on Accepted and Pending orders since the run-out began (S2). Which orders count exactly: from which moment, and do Held orders count?
- Rejection releases its quantity (S3); does a cancelled order, or a line removed at the cut-off, release too?
- At exactly zero the product becomes Unavailable (S4). What about a Pending order that takes it from 10 to −20 (S5): accepted and flagged Oversold?
- Adjusting the quantity (S6): recalculated from the same orders — including when that makes Remaining positive again after Unavailable?
- Measure-based run-out ("340 kg").
- What if two run-outs happen for one product over time (the "since run-out began" window)?

---

### T-10.3.1 — Put a product into Run-out with a quantity that counts down

**Parent story**

> As a Head Office User, I want to enter the quantity left for a product with no restock so that reps can sell it while it lasts and it stops being orderable when it's gone.
>
> Acceptance criteria:
> - Setting "Kids SPF50 Spray 150ml" to Run-out with 340 shows "340 units" ("340 kg" for measures); Remaining is 340 (S1)
> - With 120 Accepted and 60 Pending since run-out began, Remaining is 160 (S2)
> - A Pending order of 20 Rejected makes Remaining 180 (S3)
> - When Accepted plus Pending reaches 340 the product becomes Unavailable and leaves Order Pads at next Sync (S4)
> - Two reps each capturing 25 offline against Remaining 30 both send (valid when captured), and Remaining shows −20 for head office (S5; H-14 shows "(oversold by 20)")
> - Changing the quantity to 400 recalculates Remaining from the same orders (S6)

**Slice** — A Head Office User puts a product into Run-out with a quantity, Remaining counts down live as orders are pending or accepted and back up when they're rejected, the product goes Unavailable at zero, and oversold stock shows as "oversold by N".
**Spec source** — Range Lifecycle US-008 S1–S6; glossary (Run-out Quantity, Remaining, Availability Rule); design decision "Product lifecycle with a manager-entered run-out"; uxdocs 02 H-14 (H14.3)
**Depends on** — T-8.1.1, T-7.1.1, T-7.2.1
**Pattern to follow** — T-8.1.1 (availability panel)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — Remaining depends on order state transitions across the system (data integrity); a spike is recommended before build (MI-19); Edge-Case Discovery High.

**Provisional commit message**

```
feat(availability): run products out against a quantity with remaining

- Unsold stock is worth selling, and the system holds no warehouse stock,
  so head office enters what's left and orders count it down
- Pending orders count too, to reduce overselling; offline reps can still
  oversell, which is shown rather than blocked
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A derived quantity across the order lifecycle.

**Work package**

Increments:
1. Spike (MI-19): prototype Remaining against the order lifecycle (Pending, Held, Accepted, Rejected, lines removed at the cut-off) and decide computed-on-read versus maintained counter.
2. Run-out state with quantity in the product's unit and a run-out start time.
3. Remaining = quantity − Σ(Pending + Accepted since start), releasing on rejection; one function.
4. At Remaining ≤ 0 the product is Unavailable (the availability function includes it); negative values are shown as "(oversold by 20)".
5. Adjusting the quantity recalculates.
6. Remaining and the Run-out state go in the snapshot (display is T-10.4.1).

Decision points:
- Held orders: count or not? Lines removed at the cut-off: released?
- When Remaining goes from 0 back above zero (quantity raised, or a rejection), does the product become orderable again automatically?
- Computed on read versus stored counter (consistency versus speed).

Delegable slivers:
- **Remaining function tests** — Given the agreed scenarios from T-10.3.1-S, write tests of the Remaining function across order transitions. Test-only.
- **Run-out panel fields** — Add the Run-out choice with its quantity (in the product's unit) to the H-14 panel and show live Remaining with "(oversold by N)" for negatives, reading the existing function. No rule changes.

---

### T-10.4.1-S — Test scenarios for Discontinuing and Run-out on the tablet

**Owner** — Scenario Review
**Gates** — T-10.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for Discontinuing and Run-out labels with
replacements on the tablet (task T-10.4.1). Read
plan_docs/stories/rep-at-a-location-tablet.md US-010 S3, S4, S5, S6 and
US-011 S1, S2, plus plan_docs/uxdocs/00-conventions-and-shared-elements.md
§1c and §2 and plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-007.
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Start with characterisation scenarios pinning T-8.3.1's
labels. Cover the story scenarios, then derivable edges: Active product with a
replacement link (nothing shown), Remaining negative in the snapshot, last
sync not today. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-10.4.1 — Show Discontinuing and Run-out on the tablet with their successors

**Parent story**

> As a Field Salesperson, I want each product's availability shown clearly — unavailable stock countable but not orderable, discontinuing and run-out stock orderable with an honest flag — so that I count what's on the shelf and never promise what head office can't supply.
>
> Acceptance criteria:
> - "Unavailable — retired" shows with its Replacements (US-010 S3)
> - "Kids SPF50 Spray 150ml", Discontinuing with Replacement "Kids SPF50 Spray v2 100ml", is orderable and shows "Discontinuing — replaced by Kids SPF50 Spray v2 100ml" (US-010 S4)
> - "Throat Lozenges 36s" in Run-out with Remaining 120 shows "Limited stock, no restock — not guaranteed until confirmed by head office. About 120 left" (with "as of" only when stale); any quantity can be entered (US-010 S5)
> - A line added before a product became Unavailable lists its Replacements (US-010 S6)
> - One Replacement shows "Replaced by SPF30 Sun Lotion v2 200ml"; several show "Replaced by 2 products", expanding in place (US-011 S1, S2)

**Slice** — On the tablet, Discontinuing and Run-out products stay orderable with their flag in words — Run-out with an estimate of what's left — and unavailable or discontinuing products show their replacements, expanding in place when there are several.
**Spec source** — Rep at a Location US-010 S3, S4, S5, S6 and non-functional notes; US-011 S1, S2; uxdocs 00 §1c, §2; uxdocs 04 BR-NEW-007
**Depends on** — T-10.2.1, T-10.3.1, T-8.3.1
**Pattern to follow** — T-8.3.1 (tablet labels)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies T-8.3.1's labels and adds snapshot fields, so characterisation comes first.

**Provisional commit message**

```
feat(tablet): show discontinuing and run-out with their successors

- Reps must never promise what head office can't supply, so run-out says
  in full that it isn't guaranteed and shows Remaining as an estimate
- Successors appear only once a product leaves Active, so early links
  don't confuse the pad
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies shared tablet labels behind a characterisation pass.

**Agent prompt**

```
Role: You are adding Discontinuing and Run-out labels and replacement display
to the Field Sales Management System's tablet app.

Context:
- Slice: on the tablet, Discontinuing and Run-out products stay orderable
  with their flag in words — Run-out with an estimate of what's left — and
  unavailable or discontinuing products show their replacements, expanding in
  place when there are several.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-010 S3–S6 and
  non-functional notes (Run-out wording shown in full), US-011 S1, S2 and
  design decision "Replacements as ordinary products";
  plan_docs/uxdocs/00-conventions-and-shared-elements.md §1c, §2;
  plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-007.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — tablet labels
  (T-8.3.1), snapshot (T-4.1.1), replacement links (T-10.1.1), Run-out
  (T-10.3.1).
- Pattern to follow: T-8.3.1.

Acceptance criteria:
1. "Kids SPF50 Spray 150ml", Discontinuing with Replacement "Kids SPF50 Spray
   v2 100ml", is orderable and reads "Discontinuing — replaced by Kids SPF50
   Spray v2 100ml".
2. "Throat Lozenges 36s" in Run-out with Remaining 120 reads "Limited stock,
   no restock — not guaranteed until confirmed by head office. About 120
   left" in full on the order line, plus "as of <date>" only when the last
   sync wasn't today; any quantity can be entered.
3. "Unavailable — retired" products show their Replacements.
4. One Replacement reads "Replaced by SPF30 Sun Lotion v2 200ml"; several read
   "Replaced by 2 products" and expand in place.
5. A line added before its product became Unavailable lists the Replacements
   under "Unavailable since you added it — will still be sent".
6. Replacements show only once the product leaves Active; restricted
   replacements never appear.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions (Run-out Remaining, replacement links) go through
  T-4.1.1's versioning and its owner's review.
- The replacement flag has no add action here (T-10.5.1 adds picking).
- Never colour alone.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-8.3.1's labels; stop and show them passing with the snapshot additions.
Resume only on "Continue T-10.4.1".

Steps: 1. characterisation tests; 2. snapshot fields; 3. Discontinuing and
Run-out labels; 4. replacement display; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-10.4.1-S; do not design
your own.

Definition of done: on the tablet, Discontinuing and Run-out products stay
orderable with their flag in words — Run-out with an estimate of what's left —
and unavailable or discontinuing products show their replacements, expanding
in place when there are several.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: picking replacements (T-10.5.1), customer wording (E26).
```

**Checkpoint**

Produces before pausing — characterisation tests of the tablet labels, passing, and the snapshot additions.
Human reviews — Is the Run-out caveat shown in full wherever the product can be ordered?
Resume trigger — `Continue T-10.4.1`

---

### T-10.5.1-S — Test scenarios for picking replacements from a Low line

**Owner** — Scenario Review
**Gates** — T-10.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for picking replacements for an unavailable Low
item (task T-10.5.1). Read plan_docs/stories/rep-at-a-location-tablet.md
US-011 S3–S5 and US-012 S10, S11. Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning the Low tab (T-5.1.4, T-8.3.2) and the
popover (T-5.5.1). Cover the story scenarios, then derivable edges: picking
one of two replacements, removing an added replacement line, a replacement
outside the rep's ranges. Mark undecided cases as "Needs a decision". Write
no test code; change no files.
```

---

### T-10.5.1 — Order replacements for an unavailable Low item

**Parent story**

> As a Field Salesperson, I want to see what replaces a product that is Unavailable, Discontinuing or in Run-out, and pick Replacements when I reorder so that the customer moves onto current stock.
>
> Acceptance criteria:
> - On a Low "SPF50 Kids Spray 150ml", Add to order lets me select the 100ml with 12 and the 250ml with 6; both lines are added and the unavailable product is not (S3)
> - A replacement "SPF30 Sun Lotion v2 200ml" in a Range not assigned to me is selectable and marked "Outside your ranges" (S4)
> - With v1 replaced by v2 and v2 Unavailable and replaced by v3, v1's picker shows v2 unselectable as "Unavailable — replaced by v3", and opening it offers v3 (S5)
> - The Low tab shows REPLACED with the replacement and its quantity (US-012 S10); CAN'T ADD offers "Find replacement" (US-012 S11)

**Slice** — Marking an unavailable product Low offers its replacements instead of Add, the rep adds any of them with quantities, and the Low tab shows the item as REPLACED naming what was added.
**Spec source** — Rep at a Location US-011 S3–S5; US-012 S10, S11; design decision "Replacements as ordinary products"
**Depends on** — T-10.4.1, T-5.1.4, T-8.3.2
**Pattern to follow** — T-5.5.1 (popover), T-5.1.4 (Low states)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the Low tab states and popover, so characterisation comes first.

**Provisional commit message**

```
feat(orders): reorder replacements for unavailable low items

- A Low mark on a gone product is still a reorder need, so the rep picks
  its successors and the Low tab records it as replaced, not lost
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies established order-entry behaviour behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the replacement picker for unavailable Low items to the
Field Sales Management System's tablet app.

Context:
- Slice: marking an unavailable product Low offers its replacements instead
  of Add; the rep adds any of them with quantities; the Low tab shows the
  item as REPLACED naming what was added.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-011 S3–S5, US-012
  S10, S11; design decision "Replacements as ordinary products" (a two-step
  chain takes two taps; unavailable replacements unselectable with their own
  link).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Low tab (T-5.1.4,
  T-8.3.2), popover (T-5.5.1), replacement display (T-10.4.1).
- Pattern to follow: T-5.5.1's popover.

Acceptance criteria:
1. On a Low "SPF50 Kids Spray 150ml" (Unavailable), the action opens a picker
   of its direct Replacements; selecting "v2 100ml" with 12 and "v2 250ml"
   with 6 adds both lines; the unavailable product is not added.
2. A replacement in a Range not assigned to the rep is selectable and marked
   "Outside your ranges".
3. With v2 Unavailable and replaced by v3, v1's picker shows v2 unselectable
   as "Unavailable — replaced by v3", and opening it offers v3.
4. The Low tab shows REPLACED with the replacement product(s) and
   quantities; REPLACED does not count in the tab label.
5. CAN'T ADD items offer "Find replacement", opening the same picker.
6. Quantities are entered with the popover's rules (empty, validated).

Constraints:
- Use the project's existing conventions and test framework.
- Derive REPLACED from the Low mark and the order's lines; store nothing
  that can drift.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond a link
  from a Low mark to its replacement lines, if the derivation needs it.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the Low tab and popover; stop and show them passing with the REPLACED
derivation. Resume only on "Continue T-10.5.1".

Steps: 1. characterisation tests; 2. picker; 3. REPLACED derivation;
4. Find replacement on CAN'T ADD; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-10.5.1-S; do not design
your own.

Definition of done: marking an unavailable product Low offers its
replacements instead of Add, the rep adds any of them with quantities, and
the Low tab shows the item as REPLACED naming what was added.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: customer-side replacements (E26).
```

**Checkpoint**

Produces before pausing — characterisation tests of the Low tab and popover, passing, and the REPLACED derivation.
Human reviews — Can an item show REPLACED without a replacement line on the order, or vice versa?
Resume trigger — `Continue T-10.5.1`

---

### T-10.6.1-S — Test scenarios for the Oversold annotation

**Owner** — Scenario Review
**Gates** — T-10.6.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for annotating oversold orders (task T-10.6.1).
Read plan_docs/stories/head-office-order-processing.md US-002 S5 and glossary
(Flag: Oversold), plan_docs/stories/range-lifecycle.md US-008 S5, and
plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-001 (Oversold routes
to allocation — routing arrives with E29; this task only annotates). Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover S5, then derivable edges: two orders taking Remaining
negative in turn, a line that brings it to exactly zero. Mark undecided cases
as "Needs a decision". Write no test code; change no files.
```

---

### T-10.6.1 — Annotate orders that oversell a run-out product

**Parent story**

> As a Head Office User, I want each flagged order to state its flags in plain terms so that I know what to look at before opening it.
>
> Acceptance criteria:
> - A Run-out product with Remaining −20 after this order annotates it "Oversold — Kids SPF50 Spray 150ml, 20 over" (S5)

**Slice** — When an accepted order takes a run-out product's Remaining below zero, the order records "Oversold — <product>, N over" among its notes.
**Spec source** — Head Office US-002 S5; Range Lifecycle US-008 S5; uxdocs 04 BR-NEW-001
**Depends on** — T-10.3.1, T-7.4.1
**Pattern to follow** — T-7.4.1 (annotator)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — a new annotator on the reviewed interface; depends on Remaining semantics from T-10.3.1.

**Provisional commit message**

```
feat(order-processing): annotate orders that oversell run-out stock

- Offline reps can sell past Remaining; the order still goes through and
  says by how much, for head office and later allocation to act on
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new annotator on a reviewed interface.

**Agent prompt**

```
Role: You are adding the Oversold annotation to order processing in the Field
Sales Management System.

Context:
- Slice: when an accepted order takes a run-out product's Remaining below
  zero, the order records "Oversold — <product>, N over" among its notes.
- Specs: plan_docs/stories/head-office-order-processing.md US-002 S5;
  plan_docs/stories/range-lifecycle.md US-008 S5;
  plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-001.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — annotator interface
  (T-7.4.1), Remaining (T-10.3.1).
- Pattern to follow: T-7.4.1's New Location annotator.

Acceptance criteria:
1. An order that takes "Kids SPF50 Spray 150ml" Remaining to −20 is annotated
   "Oversold — Kids SPF50 Spray 150ml, 20 over" at acceptance.
2. An order that takes it exactly to zero is not annotated.
3. Each oversold product on an order gets its own sentence.
4. The order is accepted as normal; nothing is held or removed.

Constraints:
- Use the project's existing conventions and test framework; add an
  annotator, do not change the interface.
- "N over" is how far this order took Remaining below zero, per the agreed
  scenarios.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the annotator passes the agreed scenarios, stop and show
it. Resume only on "Continue T-10.6.1".

Steps: 1. Oversold annotator; 2. registration; 3. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-10.6.1-S; do not design
your own.

Definition of done: when an accepted order takes a run-out product's
Remaining below zero, the order records "Oversold — <product>, N over" among
its notes.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: routing the short quantity to allocation (E29, Head Office
US-008 S2).
```

**Checkpoint**

Produces before pausing — the Oversold annotator passing the agreed scenarios.
Human reviews — Is "N over" attributed to the right order when several orders cross zero?
Resume trigger — `Continue T-10.6.1`

---

### T-10.7.1 — Mark a product as watched so its orders are annotated

**Parent story**

> As a Head Office User, I want to mark a product so that any order containing it is flagged so that controlled or problem lines always get a look.
>
> Acceptance criteria:
> - Marking "Controlled Pain Relief 30s" Watched with note "Check licence" annotates every new order containing it "Watched product — Check licence" (S1)
> - Removing the mark stops new orders being annotated; already-annotated ones keep it (S2)

**Slice** — A Head Office User marks a product as watched with a note, every order containing it accepted afterwards reads "Watched product — <note>", and removing the mark stops new annotations.
**Spec source** — Head Office US-007 S1, S2; Head Office Requires Clarification 7 (Watched Product field on the product record)
**Depends on** — T-7.4.1, T-1.6.1
**Pattern to follow** — T-7.4.1 (annotator), T-1.6.1 (record field)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — a field and an annotator on reviewed patterns; Annotate changes nothing about the order; Blast Radius Low.

**Provisional commit message**

```
feat(order-processing): annotate orders containing watched products

- Controlled or problem lines always get a look, without stopping the order
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Pattern repetition.

**Agent prompt**

```
Role: You are adding Watched Products to the Field Sales Management System.

Context:
- Slice: a Head Office User marks a product as watched with a note; every
  order containing it accepted afterwards reads "Watched product — <note>";
  removing the mark stops new annotations.
- Specs: plan_docs/stories/head-office-order-processing.md US-007 S1, S2 and
  assumptions ("A Watched Product is a head-office mark on the product").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — product record
  (T-1.6.1), annotator interface (T-7.4.1).
- Pattern to follow: T-7.4.1's annotator; T-1.6.1's record field.

Acceptance criteria:
1. Marking "Controlled Pain Relief 30s" Watched with note "Check licence"
   annotates every order containing it accepted afterwards "Watched product —
   Check licence".
2. Removing the mark stops new annotations; orders already annotated keep
   theirs.
3. The mark and note are shown on the product record.

Constraints:
- Use the project's existing conventions and test framework.
- Annotations never stop or change an order.
- No tests of framework internals or trivial members.
- This task opts in to watched fields on products.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. watched field and note; 2. annotator; 3. record display; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a Head Office User marks a product as watched with a
note, every order containing it accepted afterwards reads "Watched product —
<note>", and removing the mark stops new annotations.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: any hold or review workflow (orders are never diverted).
```

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | RL-010 (T-10.1.1), RL-007 (T-10.2.1), RL-008 (T-10.3.1), A1-011 (T-10.4.1, T-10.5.1), HO-007 (T-10.7.1), A1-010 part (T-10.4.1), HO-002 S5 (T-10.6.1) |
| Every task satisfies the three slice criteria | Pass | 7 of 7 |
| Every task carries a tier with a rationale citing dimensions | Pass | 7 of 7 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-10.3.1 (MI-19) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-10.2.1, T-10.4.1, T-10.5.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-10.7.1 only |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-10.3.1 is Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-10.1.1, T-10.2.1, T-10.4.1, T-10.5.1, T-10.6.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-10.3.1 |
| Every scenario task precedes the task it gates | Pass | 6 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-19 |
