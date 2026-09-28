# E9 — Ranges

**Iteration** — 2, Catalogue lifecycle
**Outcome** — Head office builds, copies, trims, archives and un-archives Ranges without taking anything off sale by accident, and each rep's Order Pad opens on their own Ranges plus unranged products.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Range Lifecycle US-001 — Create a Range and fill it by filter-and-select | Must | S1–S5 |
| 2 | Range Lifecycle US-004 — Remove a product from a Range | Must | S1–S3 |
| 3 | Range Lifecycle US-011 — Browse Ranges by status | Should | S1–S3 |
| 4 | Range Lifecycle US-002 — Copy a Range | Should | S1–S3 |
| 5 | Range Lifecycle US-003 — Add a product to a Range from the product screen | Should | S1–S3 (also Product Management US-001 S1's Range tick) |
| 6 | Range Lifecycle US-005 — Archive a Range with impact shown | Must | S1–S5 |
| 7 | Range Lifecycle US-006 — Un-archive a Range | Should | S1–S4 (also Range Lifecycle US-009 S2) |
| 8 | Rep at a Location US-012 (part) — Build an Order | Must | S1's "rep's Ranges plus unranged" and S2's "Outside your ranges" (also US-008 S5, US-018's Range link) |

**Missing story** — no story defines how Ranges are assigned to reps or customers (MI-17). T-9.8.1 carries it as `{{NEEDS ACCEPTANCE CRITERIA}}`; customer range assignment is needed by E26.

**Exit criterion** — A Head Office User can create a Range by filter-and-select, copy it for a new season, add a product to Ranges from its record, remove products with an inline note when that takes one off sale, archive a Range after seeing its impact ordered by how much it matters, and un-archive it; the Range list shows Active by default. Each rep's Order Pad opens on their assigned Ranges plus unranged products, and anything else they find is marked "Outside your ranges".

**Capability-class stamp** — Frontier workhorse for tight-loop slivers, Agent-Assisted tasks and scenario drafting; Fast mid-tier for Agent-Autonomous tasks. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [range-lifecycle.md](../../stories/range-lifecycle.md), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-008, US-012, US-018), [02-head-office.md](../../uxdocs/02-head-office.md) (H-13, H-18, H-19), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§5 Impact preview, §6 Filter → review → apply).

---

### T-9.1.1-S — Test scenarios for creating and filling a Range

**Owner** — Scenario Review
**Gates** — T-9.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for creating a Range and filling it by
filter-and-select (task T-9.1.1). Read plan_docs/stories/range-lifecycle.md
US-001 S1–S5 and its dependency note (archived Brands, suppliers and
Categories are not offered as filters), and
plan_docs/uxdocs/00-conventions-and-shared-elements.md §6. Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover S1–S5, then derivable edges: filtering on a category and
everything beneath it, combining brand and supplier, a Restricted product in
the filter. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-9.1.1 — Create a Range and fill it by filter-and-select

**Parent story**

> As a Head Office User, I want to create a Range and add products by filtering on Category, Brand or supplier, unticking exceptions, so that a 200-product season is set up in minutes.
>
> Acceptance criteria:
> - Creating "Summer 2027" and filtering Category "Suncare" lists 64 products, all ticked; unticking 4 and adding leaves 60, and the Range is Active (S1)
> - 10 filtered products already in the Range show "Already in range" and aren't double-added (S2)
> - 2 filtered Unavailable products show their state and are unticked by default (S3)
> - A duplicate name shows "A range with this name already exists" (S4)
> - A filter matching nothing shows "No products match" and adds nothing (S5)

**Slice** — A Head Office User creates a Range, filters the catalogue by category, brand or supplier with every match ticked, unticks exceptions and adds them, and the Range is Active with its products.
**Spec source** — Range Lifecycle US-001 S1–S5; uxdocs 02 H-18 (H18.1); uxdocs 00 §6
**Depends on** — T-1.6.1, T-1.7.1, T-8.1.1
**Pattern to follow** — T-1.7.1 (product search), uxdocs 00 §6 (filter → review → apply)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — introduces the Range and membership model that the availability rule, rep pads and chains read; checkpoint on the model; H-18 is a draft.

**Provisional commit message**

```
feat(ranges): create a range and fill it by filter-and-select

- Ranges are built in bulk, so the catalogue is filtered and every match
  starts ticked, with exceptions unticked before adding
- Unavailable products start unticked so a season never quietly revives
  something head office took off sale
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared model with a design pause.

**Agent prompt**

```
Role: You are building Range creation with filter-and-select (H-18) for the
Field Sales Management System's head office website.

Context:
- Slice: a Head Office User creates a Range, filters the catalogue by
  category, brand or supplier with every match ticked, unticks exceptions and
  adds them; the Range is Active with its products.
- Specs: plan_docs/stories/range-lifecycle.md US-001 S1–S5, glossary (Range,
  Archive, Availability Rule) and design decision "Three routes into a Range;
  removal is routine"; plan_docs/uxdocs/02-head-office.md H-18 (H18.1 "+ Add
  products" opens filter-and-select with every match ticked);
  plan_docs/uxdocs/00-conventions-and-shared-elements.md §6.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — product search
  (T-1.7.1), classification (T-1.6.1), availability function (T-8.1.1).
- Pattern to follow: T-1.7.1's search for the filter.

Acceptance criteria:
1. Creating "Summer 2027" and filtering Category "Suncare" (and beneath)
   lists 64 products, all ticked; unticking 4 and adding leaves 60; the Range
   is Active.
2. 10 filtered products already in the Range show "Already in range" and are
   not added twice.
3. 2 filtered Unavailable products show their state and start unticked.
4. Creating a Range named "Summer 2026" when one exists shows "A range with
   this name already exists".
5. A filter matching nothing shows "No products match" and adds nothing.
6. Archived Brands, suppliers and Categories are not offered as filters.
7. Restricted products appear in the filter like any other (restriction is
   about reps, not Ranges).

Constraints:
- Use the project's existing conventions and test framework.
- Design the Range model for later use: archive and un-archive (T-9.6.1,
  T-9.7.1), copy (T-9.4.1), assignment to reps, customers and chains (T-9.8.1,
  E23, E26), and the any-active-range availability rule (T-9.2.1). Do not
  implement them.
- No tests of framework internals or trivial members.
- This task opts in to the range and range-membership tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the Range and membership model is designed, stop and show
how each later use fits. Resume only on "Continue T-9.1.1".

Steps: 1. Range and membership model; 2. create with unique name;
3. filter-and-select review; 4. H-18 detail; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-9.1.1-S; do not design
your own.

Definition of done: a Head Office User creates a Range, filters the catalogue
by category, brand or supplier with every match ticked, unticks exceptions
and adds them, and the Range is Active with its products.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: removing products (T-9.2.1), the Range list (T-9.3.1), copy
(T-9.4.1), archive (T-9.6.1), rep assignment (T-9.8.1).
```

**Checkpoint**

Produces before pausing — the Range and membership model with notes on archive, copy, assignment and availability.
Human reviews — Can archive history, copies and assignments to reps, customers and chains all hang off this model unchanged?
Resume trigger — `Continue T-9.1.1`

---

### T-9.2.1-S — Test scenarios for removing a product from a Range and the any-active-range rule

**Owner** — Human-Led
**Gates** — T-9.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- The conflict (MI-56): the Availability Rule says a product in no Range is "unranged" and orderable, yet US-004 S2 says removing a product from its only Range makes it Unavailable. Is a product that has ever been ranged and now has none Unavailable, while one never ranged stays orderable? Or does removal from the last Range leave it unranged and orderable?
- A product in "Summer 2026" (archived) and "Core Stock" (active): orderable. In only archived Ranges: Unavailable. Directly retired: Unavailable regardless.
- Adding a removed product back (S3): orderable again at once.
- When does the tablet learn of it — at the next sync only?

---

### T-9.2.1 — Remove a product from a Range, applying the any-active-range rule

**Parent story**

> As a Head Office User, I want to remove a product from a Range as a simple edit, with a note if that leaves it unavailable, so that small corrections don't need a ceremony.
>
> Acceptance criteria:
> - Removing "SPF30 Sun Lotion 200ml" from "Summer 2026" while it's also in "Core Stock" removes it with no confirmation, and it stays orderable (S1)
> - Removing "Kids SPF50 Spray 150ml", only in "Summer 2026", shows the inline note "Kids SPF50 Spray 150ml is now in no active range and is Unavailable" (S2)
> - Adding it back makes it orderable again (S3)

**Slice** — Removing a product from a Range is an immediate edit; if that leaves the product in no active Range it becomes Unavailable with an inline note and Undo, and it stays orderable while any other active Range holds it.
**Spec source** — Range Lifecycle US-004 S1–S3; glossary (Availability Rule); design decision "Availability follows any Active Range, not every Range"; uxdocs 02 H-18 (H18.2)
**Depends on** — T-9.1.1, T-8.1.1
**Pattern to follow** — T-8.1.1 (availability function)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — changes the availability rule every surface reads (Blast Radius High), and the story conflicts with the glossary on unranged products (MI-56).

**Provisional commit message**

```
feat(ranges): remove products from ranges under the any-active-range rule

- Archiving a seasonal range must not take core products off sale, so a
  product stays orderable while any active range holds it
- Removal is routine, with an inline note and Undo when it takes a
  product off sale
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared rule change held by a human.

**Work package**

Increments:
1. Characterisation: pin T-8.1.1's availability function (direct states only) with tests.
2. Resolve MI-56 and write the rule: directly Unavailable; or ranged and every Range archived (and, per the decision, "de-ranged"); or Run-out at zero (E10); Temporarily Unavailable is not orderable; otherwise orderable in its own state.
3. Extend the one availability function; every surface keeps calling it.
4. Removal handler: immediate, no confirmation; inline note and Undo when the product becomes Unavailable (H18.2).
5. Re-adding restores orderability.

Decision points:
- MI-56 (never-ranged versus de-ranged).
- Is "reason: no longer in an active range" distinct from "retired" everywhere (tablet label, customer wording)?

Delegable slivers:
- **Inline note and Undo** — On H-18, after a removal that leaves the product Unavailable, show "<product> is now in no active range and is Unavailable" with Undo that re-adds it, using the existing handler. No rule changes.
- **Rule tests** — Given the agreed scenarios from T-9.2.1-S, write tests of the availability function for every combination of direct state and range membership. Test-only.

---

### T-9.3.1 — Browse Ranges by status

**Parent story**

> As a Head Office User, I want the Range list to show Active Ranges by default with counts of available and unavailable products so that I see the live catalogue without last year's clutter.
>
> Acceptance criteria:
> - Ranges shows Active Ranges with "58 products · 6 unavailable" per row, and an Archived filter (S1)
> - Filtering Archived shows archive date and the number of products still Unavailable because of each (S2)
> - An Active Range with 0 products shows "0 products — not visible to reps" (S3)

**Slice** — The Range list opens on Active Ranges with product and unavailable counts, filters to Archived Ranges with their archive date and knock-on count, and flags empty Ranges as invisible to reps.
**Spec source** — Range Lifecycle US-011 S1–S3; uxdocs 02 H-18 (frame)
**Depends on** — T-9.2.1
**Pattern to follow** — T-1.7.1 (head office list)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — a read-only list; Blast Radius Low; Taste Medium (H-18 draft) — treat as Agent-Assisted if H-18 is unconfirmed (MI-08).

**Provisional commit message**

```
feat(ranges): browse ranges active by default with counts

- Ranges accumulate one per season, so the list hides archived ones and
  shows what each live range actually offers reps
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A read-only list.

**Agent prompt**

```
Role: You are building the Range list (H-18) for the Field Sales Management
System's head office website.

Context:
- Slice: the Range list opens on Active Ranges with product and unavailable
  counts, filters to Archived Ranges with their archive date and knock-on
  count, and flags empty Ranges as invisible to reps.
- Specs: plan_docs/stories/range-lifecycle.md US-011 S1–S3;
  plan_docs/uxdocs/02-head-office.md H-18 (list frame).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Ranges (T-9.1.1),
  availability function (T-9.2.1).
- Pattern to follow: T-1.7.1's list.

Acceptance criteria:
1. Ranges opens on Active Ranges, each row "58 products · 6 unavailable".
2. An Archived filter lists archived Ranges with archive date and how many
   products are still Unavailable because of that archive.
3. An Active Range with 0 products reads "0 products — not visible to reps".
4. Each row opens the Range's detail.

Constraints:
- Use the project's existing conventions and test framework.
- Counts use the availability function; no second rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. list query with counts; 2. H-18 list; 3. Archived filter; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: the Range list opens on Active Ranges with product and
unavailable counts, filters to Archived Ranges with their archive date and
knock-on count, and flags empty Ranges as invisible to reps.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: archive and un-archive (T-9.6.1, T-9.7.1), copy (T-9.4.1).
```

---

### T-9.4.1 — Copy a Range for a returning season

**Parent story**

> As a Head Office User, I want to create a new Range pre-filled from an existing one so that a returning season starts from last year's list without reopening last year's Range.
>
> Acceptance criteria:
> - Copying Archived "Summer 2026" (58 products) as "Summer 2027" creates an Active Range with the 58, leaving "Summer 2026" unchanged (S1)
> - 6 Unavailable products among the 58 are shown with their state so they can be removed (S2)
> - Copying an Active Range works the same way; both stay Active (S3)

**Slice** — A Head Office User copies any Range, archived or active, into a new Active Range with the same products, and unavailable ones are shown with their state to prune.
**Spec source** — Range Lifecycle US-002 S1–S3; design decision "Un-archive is undo; a season is a copy"
**Depends on** — T-9.1.1
**Pattern to follow** — T-9.1.1 (create and fill)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — a copy through reviewed creation; Blast Radius Low (the source Range is untouched).

**Provisional commit message**

```
feat(ranges): copy a range for a returning season

- An archived range is the record of what was sold under that name, so a
  new season starts from a copy rather than by reopening the old one
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Pattern repetition.

**Agent prompt**

```
Role: You are adding Copy Range (H-18) to the Field Sales Management System.

Context:
- Slice: a Head Office User copies any Range, archived or active, into a new
  Active Range with the same products; unavailable ones are shown with their
  state to prune.
- Specs: plan_docs/stories/range-lifecycle.md US-002 S1–S3 and design
  decision "Un-archive is undo; a season is a copy";
  plan_docs/uxdocs/02-head-office.md H-18 ("Copy..." action).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Ranges (T-9.1.1).
- Pattern to follow: T-9.1.1's creation.

Acceptance criteria:
1. Copying Archived "Summer 2026" (58 products) as "Summer 2027" creates an
   Active Range with the 58; "Summer 2026" is unchanged.
2. The 6 Unavailable products among them are shown with their state so they
   can be removed.
3. Copying an Active Range works the same; both stay Active.
4. The new name must be unique ("A range with this name already exists").
5. Copying doesn't copy assignments to reps, customers or chains.

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. copy handler; 2. Copy action and name prompt; 3. state display on
the new Range; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a Head Office User copies any Range, archived or active,
into a new Active Range with the same products, and unavailable ones are
shown with their state to prune.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: un-archive (T-9.7.1), assignments (T-9.8.1).
```

---

### T-9.5.1-S — Test scenarios for adding a product to Ranges from its record

**Owner** — Scenario Review
**Gates** — T-9.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for adding a product to Ranges, or creating a
Range, from the product record (task T-9.5.1). Read
plan_docs/stories/range-lifecycle.md US-003 S1–S3 and
plan_docs/stories/product-management.md US-001 S1, S3. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning T-1.2.1's minimum create (no
Range picker). Cover the story scenarios, then derivable edges: an archived
Range (not offered), a duplicate inline name, removing a Range from the
record. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-9.5.1 — Add a product to Ranges, or create one, from the product record

**Parent story**

> As a Head Office User, I want to add a product to Ranges, or create a Range, while creating or editing the product so that new products aren't left out of a Range by accident.
>
> Acceptance criteria:
> - Creating "SPF30 Sun Lotion v2 200ml" and ticking "Summer 2027" and "Core Stock" puts it in both on save (S1; also Product Management US-001 S1)
> - Create range "Autumn 2027" inline, then saving the product, creates the Range, Active, containing it (S2)
> - Saving without any Range leaves it unranged and orderable by everyone, and the screen says so (S3)

**Slice** — While creating or editing a product, a Head Office User ticks the Ranges it belongs to or creates a new Range inline, and a product saved with none is stated to be unranged and orderable by all reps.
**Spec source** — Range Lifecycle US-003 S1–S3; Product Management US-001 S1, S3; uxdocs 02 H-13 (H13.2 ranges section)
**Depends on** — T-9.1.1, T-1.2.1
**Pattern to follow** — T-1.6.1 (record sections), T-9.1.1 (Range creation)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies product creation (T-1.2.1), so characterisation comes first.

**Provisional commit message**

```
feat(ranges): add a product to ranges from its record

- The moment of listing a product is when it gets forgotten from a range,
  so ranges are offered right there, including creating one inline
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies an established form behind a characterisation pass.

**Agent prompt**

```
Role: You are adding a Ranges section to product create and edit (H-13) in
the Field Sales Management System.

Context:
- Slice: while creating or editing a product, a Head Office User ticks the
  Ranges it belongs to or creates a new Range inline; a product saved with
  none is stated to be unranged and orderable by all reps.
- Specs: plan_docs/stories/range-lifecycle.md US-003 S1–S3;
  plan_docs/stories/product-management.md US-001 S1, S3;
  plan_docs/uxdocs/02-head-office.md H-13.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — product create and
  record (T-1.2.1), Ranges (T-9.1.1).
- Pattern to follow: T-1.6.1's record sections.

Acceptance criteria:
1. Creating "SPF30 Sun Lotion v2 200ml" and ticking "Summer 2027" and "Core
   Stock" puts it in both on save.
2. "Create range", entering "Autumn 2027", then saving the product creates
   "Autumn 2027", Active, containing the product.
3. Saving with no Range leaves it unranged with "Unranged — orderable by all
   reps".
4. Only Active Ranges are offered.
5. The same section on an existing product's record adds or removes Ranges
   (removal follows T-9.2.1's rule and note).

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-9.1.1's creation and T-9.2.1's removal; no second rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-1.2.1's create form; stop and show them passing. Resume only on
"Continue T-9.5.1".

Steps: 1. characterisation tests; 2. Ranges section on create and record;
3. inline create; 4. unranged note; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-9.5.1-S; do not design
your own.

Definition of done: while creating or editing a product, a Head Office User
ticks the Ranges it belongs to or creates a new Range inline, and a product
saved with none is stated to be unranged and orderable by all reps.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: filter-and-select (T-9.1.1), archive (T-9.6.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of the minimum create form, passing.
Human reviews — Does product creation behave exactly as before when no Range is chosen?
Resume trigger — `Continue T-9.5.1`

---

### T-9.8.1-S — Test scenarios for rep ranges on the Order Pad

**Owner** — Human-Led
**Gates** — T-9.8.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- How are Ranges assigned to a rep (MI-17) — on the rep's page, on the Range's page, both? Who may assign? Is it recorded with history?
- A rep with Ranges "Core Stock" and "Summer 2027": the pad shows those products plus unranged ones; everything else is found by search and marked "Outside your ranges", still orderable.
- A product in an archived Range the rep holds: not on the pad (Unavailable) unless in another active Range.
- Does a specialist's Brand scope (E20) add to the pad later? Leave room.
- Customer range assignment for self-service (E26): same mechanism?

---

### T-9.8.1 — Open each rep's Order Pad on their assigned Ranges

**Parent story**

> As a Field Salesperson, I want to build an Order from my Order Pad or by searching the catalogue, during a Call or on its own, and mark it ready when done so that head office only receives what I've finished.
>
> Acceptance criteria:
> - With 180 products in my Ranges and 40 unranged, New Order lists those 220 by category (S1)
> - "Pharmacy Rx Balm", in a Range not assigned to me and not Restricted, found by search and added with 6, is marked "Outside your ranges" (S2)
> - A Low product outside my ranges added from the stock check is marked "Outside your ranges" (US-008 S5)
> - {{NEEDS ACCEPTANCE CRITERIA}} for assigning Ranges to a rep (MI-17)

**Slice** — Head office assigns Ranges to a rep, and after syncing the rep's Order Pad lists only those Ranges' products plus unranged ones, while anything else found by search is orderable and marked "Outside your ranges".
**Spec source** — Rep at a Location US-012 S1, S2; US-008 S5; glossary (Order Pad, Outside your ranges, Sellability); design decision "Ranges guide; Unavailable and Restricted limit"
**Depends on** — T-9.1.1, T-5.1.2, T-4.1.1
**Pattern to follow** — T-5.1.2 (Order Pad), T-4.1.1 (snapshot)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — no story defines how Ranges are assigned (MI-17), so Ambiguity is High; the pad change modifies T-5.1.2.

**Provisional commit message**

```
feat(ranges): open each rep's order pad on their assigned ranges

- Ranges guide rather than limit: the pad shows the rep's ranges, and
  anything else stays orderable, marked, when the customer asks for it
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A missing story to settle before anything is delegated.

**Work package**

Increments:
1. Write the missing story for assigning Ranges to reps (and, if the same, to customers) and agree its acceptance criteria (MI-17), replacing `{{NEEDS ACCEPTANCE CRITERIA}}`.
2. Assignment record (rep ↔ Range) with who and when; persists through archive (MI-18).
3. Snapshot carries the rep's assigned Ranges and product membership.
4. Characterisation of T-5.1.2's pad; then the pad lists assigned Ranges' products plus unranged ones.
5. "Outside your ranges" marker in search, on lines added from search, and on Low lines (neutral, disables nothing).

Decision points:
- Where assignment lives (MI-17): M-06 rep page, H-18 Range page (H18.3 already shows assignment counts), or both.
- Whether customer range assignment (E26 curated catalogue) shares the mechanism.
- Whether assignments survive archive and return on un-archive (MI-18; Range Lifecycle US-006 S4 assumes yes).

Delegable slivers:
- **"Outside your ranges" marker** — Show the neutral marker "Outside your ranges" on search results, order lines and Low lines for products not in the rep's assigned Ranges, reading the snapshot. It must disable nothing.
- **Pad filter** — Once assignments are in the snapshot, filter the Order Pad to assigned Ranges' products plus unranged ones, keeping T-5.1.2's category layout. No changes to search.

---

### T-9.6.1-S — Test scenarios for archiving a Range

**Owner** — Human-Led
**Gates** — T-9.6.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- For "Summer 2026" (58 products: 38 only here, 20 also in Core Stock, 1 already retired): exactly which counts and lists does the confirmation show (S1, S5)?
- "Highest impact first": by Locations stocking it (H19.1 draft), then orders? What breaks ties?
- "Open orders contain these products and will still be processed": which orders can the server count (MI-44)?
- A Range with every product also elsewhere: "No products become Unavailable" and a plain Confirm (S2).
- A replacement set during the archive screen is kept even if the archive is cancelled (S3).
- A Range assigned to chains (H19.2, BR-NEW-008): what does the confirmation add, and what happens to the chains' Agreed Ranges?

---

### T-9.6.1 — Archive a Range after seeing what goes off sale

**Parent story**

> As a Head Office User, I want the archive confirmation to show exactly what will become unavailable, ordered by how much it matters, and what stays on sale so that I take a season down without surprises.
>
> Acceptance criteria:
> - For "Summer 2026", Archive shows "38 products become Unavailable · 20 stay on sale via other ranges · 3 open orders contain these products and will still be processed", the 38 listed highest impact first (e.g. "SPF30 Sun Lotion 200ml — stocked in 87 locations, 14 orders in 30 days, 2 open orders"), each count expanding (S1)
> - With every product also in an Active Range: "No products become Unavailable" and a plain Confirm (S2)
> - Setting Replaced by on a listed product saves the link whether or not the archive completes (S3)
> - Confirming archives the Range, the 38 become Unavailable, the 20 are unchanged, and reps see it at next Sync (S4)
> - A product already retired directly is listed under "Already unavailable" and not counted in the 38 (S5)

**Slice** — Archiving a Range first shows how many products go off sale, listed by impact, how many stay on sale via other Ranges and how many open orders still go through, lets head office set replacements there, and on confirm takes only the right products off sale.
**Spec source** — Range Lifecycle US-005 S1–S5; design decision "Archive always shows impact, never escalates"; uxdocs 02 H-19 (H19.1, H19.2)
**Depends on** — T-9.2.1, T-8.2.1, T-10.1.1
**Pattern to follow** — T-8.2.1 (impact), T-3.1.2 (preview equals outcome)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — Blast Radius High (can take dozens of products off sale in one click); the impact ordering and chain effects need a human oracle (Edge-Case Discovery High); the agent builds against agreed scenarios.

**Provisional commit message**

```
feat(ranges): archive a range with its impact shown first

- One confirmation for every archive, with impact ordered by how much it
  matters, instead of an escalation ladder with no meaningful threshold
- Only products left in no active range go off sale; core products in
  other ranges are untouched
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
High-impact change built against a human oracle.

**Agent prompt**

```
Role: You are building Range archiving with its impact confirmation (H-19)
for the Field Sales Management System.

Context:
- Slice: archiving a Range first shows how many products go off sale, listed
  by impact, how many stay on sale via other Ranges and how many open orders
  still go through, lets head office set replacements there, and on confirm
  takes only the right products off sale.
- Specs: plan_docs/stories/range-lifecycle.md US-005 S1–S5 and design
  decision "Archive always shows impact, never escalates";
  plan_docs/uxdocs/02-head-office.md H-19 (H19.1 highest impact = most
  Locations stocking; H19.2 chains assigned the Range are stated).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — availability rule
  (T-9.2.1), impact queries (T-8.2.1), replacements (T-10.1.1).
- Pattern to follow: compute the preview with the same availability rule the
  archive applies, as T-3.1.2 does for coverage.

Acceptance criteria:
1. Archiving "Summer 2026" (58 products: 38 only here, 20 also in Core
   Stock) shows "38 products become Unavailable · 20 stay on sale via other
   ranges · 3 open orders contain these products and will still be
   processed"; each count expands.
2. The 38 are listed highest impact first, each like "SPF30 Sun Lotion 200ml
   — stocked in 87 locations, 14 orders in 30 days, 2 open orders".
3. With every product also in an Active Range, the screen says "No products
   become Unavailable" with a plain Confirm.
4. Setting Replaced by on a listed product saves the link whether or not the
   archive completes.
5. Confirming archives the Range; the 38 become Unavailable ("no longer in an
   active range"); the 20 are unchanged; reps see it at next sync.
6. A product already retired directly is listed under "Already unavailable"
   and not counted in the 38.
7. No type-to-confirm.

Constraints:
- Use the project's existing conventions and test framework.
- The preview must equal the outcome: use the same rule for both.
- Open orders go through (valid when captured); do not touch orders.
- No tests of framework internals or trivial members.
- This task opts in to archive fields on Ranges (archived at, by).

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the preview function passes every agreed scenario and a
property test shows preview equals outcome, stop and show them. Resume only
on "Continue T-9.6.1".

Steps: 1. preview function; 2. impact ordering; 3. H-19 screen with
replacement setting; 4. archive handler; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-9.6.1-S. You
are forbidden from designing your own test cases.

Definition of done: archiving a Range first shows how many products go off
sale, listed by impact, how many stay on sale via other Ranges and how many
open orders still go through, lets head office set replacements there, and on
confirm takes only the right products off sale.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Preview equals outcome (property test)
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: un-archive (T-9.7.1), chain Agreed Range behaviour beyond the
stated line (E23).
```

**Checkpoint**

Produces before pausing — the preview function with every agreed scenario passing and a property test that preview equals outcome.
Human reviews — Is it impossible for the archive to take off sale anything the confirmation didn't list?
Resume trigger — `Continue T-9.6.1`

---

### T-9.7.1-S — Test scenarios for un-archiving a Range

**Owner** — Scenario Review
**Gates** — T-9.7.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for un-archiving a Range (task T-9.7.1). Read
plan_docs/stories/range-lifecycle.md US-006 S1–S4 and US-009 S2, and design
decision "Un-archive is undo; a season is a copy". Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning T-9.6.1's archive outcome.
Cover S1–S4 and US-009 S2, then derivable edges: a product archived in two
ranges where only one is un-archived, an un-archive after a copy was made.
Assignments persisting through archive is assumed (MI-18). "Recent" is 30
days (assumption). Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-9.7.1 — Un-archive a Range to undo a mistake

**Parent story**

> As a Head Office User, I want to undo a mistaken archive quickly so that a wrong click doesn't take a season off sale for a day.
>
> Acceptance criteria:
> - "Summer 2026" archived 2 hours ago offers Un-archive as the emphasised action with Copy to new Range secondary; confirming "Un-archive — 38 products return to sale" makes the Range Active and the 38 orderable (S1)
> - A product also retired directly stays Unavailable, and the confirmation says "1 product stays Unavailable (retired directly)" (S2; Range Lifecycle US-009 S2)
> - A Range archived 14 months ago emphasises Copy, with Un-archive secondary and "Archived 14 months ago — copy instead?" (S3)
> - Reps and customers who held it see its products on their Order Pads again at next Sync (S4)

**Slice** — Opening a recently archived Range offers Un-archive first, which returns its products to sale except any retired directly, while an old archive steers head office to copy instead.
**Spec source** — Range Lifecycle US-006 S1–S4; US-009 S2; assumptions ("Recent" is 30 days)
**Depends on** — T-9.6.1, T-9.8.1
**Pattern to follow** — T-9.6.1 (preview equals outcome)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies archive state through the reviewed rule; returns products to sale (Blast Radius Medium).

**Provisional commit message**

```
feat(ranges): un-archive a range as an undo

- A wrong click must not take a season off sale for a day, so a recent
  archive offers undo first; an old one steers to copy to keep history
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Reverses a high-impact change with a review pause.

**Agent prompt**

```
Role: You are adding Un-archive to Ranges in the Field Sales Management
System.

Context:
- Slice: opening a recently archived Range offers Un-archive first, which
  returns its products to sale except any retired directly; an old archive
  steers head office to copy instead.
- Specs: plan_docs/stories/range-lifecycle.md US-006 S1–S4, US-009 S2,
  assumptions ("Recent" for Un-archive emphasis is 30 days) and design
  decision "Un-archive is undo; a season is a copy".
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — archive (T-9.6.1),
  availability rule (T-9.2.1), copy (T-9.4.1), rep assignments (T-9.8.1).
- Pattern to follow: T-9.6.1's preview-equals-outcome approach.

Acceptance criteria:
1. A Range archived 2 hours ago shows Un-archive emphasised and Copy to new
   Range secondary; confirming "Un-archive — 38 products return to sale"
   makes it Active and the 38 orderable.
2. A product also retired directly stays Unavailable; the confirmation says
   "1 product stays Unavailable (retired directly)".
3. A Range archived 14 months ago emphasises Copy, with Un-archive secondary
   and "Archived 14 months ago — copy instead?".
4. Reps (and later customers) holding the Range see its products on their
   pads again at next sync; assignments persisted through the archive.
5. The recent threshold (30 days) is a setting.

Constraints:
- Use the project's existing conventions and test framework.
- Use the availability rule; no second rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-9.6.1's archive outcome; stop and show them passing with the un-archive
preview. Resume only on "Continue T-9.7.1".

Steps: 1. characterisation tests; 2. un-archive preview and handler;
3. emphasis by age; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-9.7.1-S; do not design
your own.

Definition of done: opening a recently archived Range offers Un-archive
first, which returns its products to sale except any retired directly, while
an old archive steers head office to copy instead.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: customer assignments (E26), chain assignments (E23).
```

**Checkpoint**

Produces before pausing — characterisation tests of archive, passing, and the un-archive preview.
Human reviews — Does un-archive restore exactly what the archive took off sale, and nothing retired directly?
Resume trigger — `Continue T-9.7.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | RL-001 (T-9.1.1), RL-004 (T-9.2.1), RL-011 (T-9.3.1), RL-002 (T-9.4.1), RL-003 (T-9.5.1), RL-005 (T-9.6.1), RL-006 (T-9.7.1), A1-012 part (T-9.8.1) |
| Every task satisfies the three slice criteria | Pass | 8 of 8 |
| Every task carries a tier with a rationale citing dimensions | Pass | 8 of 8 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-9.2.1 (MI-56), T-9.8.1 (MI-17, MI-18), T-9.6.1 (MI-44) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-9.2.1, T-9.5.1, T-9.7.1, T-9.8.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-9.3.1, T-9.4.1 are Low risk |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-9.2.1, T-9.6.1, T-9.8.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-9.1.1, T-9.5.1, T-9.6.1, T-9.7.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-9.2.1, T-9.8.1 |
| Every scenario task precedes the task it gates | Pass | 6 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, `{{NEEDS ACCEPTANCE CRITERIA}}` (T-9.8.1), MI-08, 17, 18, 44, 56 |
