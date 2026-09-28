# E11 — Category restructuring

**Iteration** — 2, Catalogue lifecycle
**Outcome** — Head office reorganises a deep category tree — moving branches, splitting them, and archiving them — seeing each change's reach first, so nothing disappears from browsing without a deliberate choice.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Product Management US-006 — Move a category with its subtree | Should | S1–S3 |
| 2 | Product Management US-007 — Recategorise products in bulk | Should | S1–S3 (also US-005 S3's inline "Recategorise products?" offer) |
| 3 | Product Management US-009 — Archive a category with children | Must | S1–S6 |
| 4 | Product Management US-010 (part) — Find products | Must | S3's archived-category products |

**Exit criterion** — A Head Office User can move a category with everything beneath it after seeing the impact, split a crowded category by filter-and-select, and archive a branch through the H-16 decision — move first, or archive all with an explicit destination for its products — confirmed by typing; products left in an archived branch stay orderable and searchable, labelled, but vanish from browsing on every surface.

**Capability-class stamp** — Frontier workhorse for Agent-Assisted tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [product-management.md](../../stories/product-management.md), [02-head-office.md](../../uxdocs/02-head-office.md) (H-12, H-15, H-16), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§5 Impact preview, §6 Filter → review → apply, §7 Archive).

---

### T-11.1.1-S — Test scenarios for moving a category

**Owner** — Scenario Review
**Gates** — T-11.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for moving a category with its subtree (task
T-11.1.1). Read plan_docs/stories/product-management.md US-006 S1–S3 and
design decision "Move carries the subtree; splitting is a separate action".
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover S1–S3, then derivable edges: moving under an archived
category (not offered), moving a root with its six levels, breadcrumbs on
historic orders after a move. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-11.1.1 — Move a category with everything beneath it

**Parent story**

> As a Head Office User, I want to move a category under a new parent, taking everything beneath it, after seeing what that moves so that reorganising is one action with no surprises.
>
> Acceptance criteria:
> - Moving "Suncare" to "Health > Skincare" shows "Moving Suncare will move 4 subcategories and 180 products to Health > Skincare"; on confirm all breadcrumbs beneath update (S1)
> - "Suncare" and its subcategories aren't offered as destinations (S2)
> - Picking "Top level" makes Suncare a root category (S3)

**Slice** — A Head Office User moves a category under a new parent or to the top level after seeing how many subcategories and products move with it, its own subtree is never offered as a destination, and every breadcrumb beneath updates.
**Spec source** — Product Management US-006 S1–S3; uxdocs 02 H-15 (H15.2)
**Depends on** — T-1.1.2
**Pattern to follow** — T-1.1.1 (category tree), T-3.1.2 (impact preview)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — changes the paths of many products at once (Blast Radius Medium); H-15 is a draft.

**Provisional commit message**

```
feat(catalogue): move a category with its subtree after an impact line

- Reorganising is routine, and the scale of a move is otherwise invisible,
  so the preview states how much moves before anything does
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A tree operation with a review pause.

**Agent prompt**

```
Role: You are adding Move to the category tree (H-15) of the Field Sales
Management System.

Context:
- Slice: a Head Office User moves a category under a new parent or to the top
  level after seeing how many subcategories and products move with it; its
  own subtree is never offered as a destination; every breadcrumb beneath
  updates.
- Specs: plan_docs/stories/product-management.md US-006 S1–S3 and design
  decision "Move carries the subtree; splitting is a separate action";
  plan_docs/uxdocs/02-head-office.md H-15 (H15.2 Move uses the impact
  preview).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — category tree
  (T-1.1.1, T-1.1.2).
- Pattern to follow: T-3.1.2's preview-then-confirm.

Acceptance criteria:
1. Move on "Suncare" to "Health > Skincare" shows "Moving Suncare will move 4
   subcategories and 180 products to Health > Skincare"; on confirm every
   breadcrumb beneath reads "Health > Skincare > Suncare > …".
2. "Suncare" and its subcategories are not offered as destinations.
3. "Top level" makes Suncare a root category.
4. Archived categories are not offered as destinations.
5. Orders and calls are unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- One transaction per move.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the move operation and destination filter pass their unit
tests, stop and show them. Resume only on "Continue T-11.1.1".

Steps: 1. move operation with subtree check; 2. destination picker;
3. impact line; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-11.1.1-S; do not design
your own.

Definition of done: a Head Office User moves a category under a new parent or
to the top level after seeing how many subcategories and products move with
it, its own subtree is never offered as a destination, and every breadcrumb
beneath updates.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: recategorising products (T-11.2.1), archiving (T-11.3.1).
```

**Checkpoint**

Produces before pausing — the move operation and destination filter with passing unit tests.
Human reviews — Can a move ever create a cycle or leave a product's breadcrumb stale?
Resume trigger — `Continue T-11.1.1`

---

### T-11.2.1-S — Test scenarios for recategorising products in bulk

**Owner** — Scenario Review
**Gates** — T-11.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for recategorising products by filter-and-select
(task T-11.2.1). Read plan_docs/stories/product-management.md US-007 S1–S3 and
US-005 S3, and plan_docs/uxdocs/00-conventions-and-shared-elements.md §6.
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover the story scenarios, then derivable edges: a filter
matching products beneath as well as here, moving products into a
subcategory created in the same session. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-11.2.1 — Recategorise products in bulk to split a category

**Parent story**

> As a Head Office User, I want to select products in a category by filter and move them to another category so that I can split a branch that has grown too big.
>
> Acceptance criteria:
> - "Suncare" holds 60 products; filtering name contains "Spray" ticks 18; choosing "Suncare > Sprays" moves them and Suncare shows "42 here" (S1)
> - Unticking 2 moves 16 and leaves 2 (S2)
> - Archived categories aren't offered as destinations (S3)
> - Adding a subcategory to a category with products offers "Recategorise products?" inline (US-005 S3)

**Slice** — A Head Office User filters a category's products, unticks exceptions and moves the rest to another category in one action, reached also from the "Recategorise products?" offer after adding a subcategory.
**Spec source** — Product Management US-007 S1–S3; US-005 S3; uxdocs 00 §6
**Depends on** — T-1.1.2, T-1.7.1
**Pattern to follow** — T-9.1.1 (filter-and-select)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — a bulk change to many products' categories; reuses the filter pattern.

**Provisional commit message**

```
feat(catalogue): recategorise products in bulk by filter-and-select

- Real catalogues subdivide after products exist, so splitting a branch is
  a filter, a review and one move rather than product-by-product edits
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Bulk change with a review pause.

**Agent prompt**

```
Role: You are adding bulk Recategorise to the category tree (H-15) of the
Field Sales Management System.

Context:
- Slice: a Head Office User filters a category's products, unticks exceptions
  and moves the rest to another category in one action, reached also from the
  "Recategorise products?" offer after adding a subcategory.
- Specs: plan_docs/stories/product-management.md US-007 S1–S3, US-005 S3;
  plan_docs/uxdocs/00-conventions-and-shared-elements.md §6;
  plan_docs/uxdocs/02-head-office.md H-15 (H15.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — category tree
  (T-1.1.2), product search (T-1.7.1), filter-and-select (T-9.1.1).
- Pattern to follow: T-9.1.1's filter-and-select review.

Acceptance criteria:
1. In "Suncare" (60 products directly), filtering name contains "Spray" ticks
   18; choosing destination "Suncare > Sprays" moves them; Suncare shows "42
   here".
2. Unticking 2 before moving moves 16 and leaves 2.
3. Archived categories are not offered as destinations.
4. After adding a subcategory to a category that holds products, an inline
   "Recategorise products?" offer opens this flow pre-filtered to that
   category.
5. Orders and calls are unchanged.

Constraints:
- Use the project's existing conventions and test framework; reuse the
  filter-and-select component.
- One transaction per move.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the flow runs end to end on sample data, stop and show it.
Resume only on "Continue T-11.2.1".

Steps: 1. recategorise operation; 2. filter-and-select review; 3. inline
offer on subcategory creation; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-11.2.1-S; do not design
your own.

Definition of done: a Head Office User filters a category's products,
unticks exceptions and moves the rest to another category in one action,
reached also from the "Recategorise products?" offer after adding a
subcategory.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: moving categories (T-11.1.1), archiving (T-11.3.1).
```

**Checkpoint**

Produces before pausing — the recategorise flow run end to end on sample data.
Human reviews — Does it move exactly the ticked products and nothing beneath by accident?
Resume trigger — `Continue T-11.2.1`

---

### T-11.3.1-S — Test scenarios for archiving a category branch

**Owner** — Human-Led
**Gates** — T-11.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- For "Suncare" (4 subcategories, 180 products beneath), what exactly does each path do: Move first (returns to the decision with reduced counts, S2), Archive all with products to the parent, to another category, or left in the archived branch (S3, S4)?
- Subcategories "get the same choices independently" (design decision) — how is that presented and tested for a three-level branch?
- A root category has no parent: "Move to parent" isn't offered (S5).
- A leaf with no products gets a plain confirmation (S6).
- The confirmation text: the story says type "Suncare"; the settled H-16 frame says "ARCHIVE SUNCARE" (MI-55). Which?
- Products left in an archived branch: orderable and searchable, labelled "Suncare (archived)", but absent from browsing on the tablet, website and customer site.

---

### T-11.3.1 — Archive a category branch through a deliberate decision

**Parent story**

> As a Head Office User, I want to be shown what archiving a branch would do and choose whether to move things first or archive everything, deciding where products go so that nothing disappears without my saying so.
>
> Acceptance criteria:
> - Archive on "Suncare" shows "Archiving Suncare will also archive 4 subcategories and take 180 products out of this branch" with Move first, Archive all, Cancel (S1)
> - Move first, moving 2 subcategories to "Body Care" and recategorising the 12 direct products, returns to the decision with "2 subcategories, 168 products" (S2)
> - Archive all with products → "Move to parent (Health > Skincare)" and subcategories archived requires typing to confirm; then Suncare and its 4 subcategories are archived and the 180 products sit in Health > Skincare (S3)
> - Products → "Stay in Suncare" shows "180 products stay in Suncare. They remain orderable and searchable but won't appear when browsing categories"; after the typed confirm they're searchable with breadcrumb "Suncare (archived)" (S4)
> - For root "Health", "Move to parent" isn't offered; a destination or Stay must be picked (S5)
> - A category with no children and no products gets a plain confirmation (S6)

**Slice** — Archiving a category with children opens a decision screen stating what it affects and that the loss shows only gradually, offers moving things first or archiving all with an explicit destination for the products, and completes only after a typed confirmation.
**Spec source** — Product Management US-009 S1–S6; design decision "Branch archive as a decision, with type-to-confirm on the wide path"; uxdocs 02 H-16 (settled H16.1–H16.4)
**Depends on** — T-11.1.1, T-11.2.1
**Pattern to follow** — T-11.1.1 (move), T-11.2.1 (recategorise)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — far-reaching and only gradually visible (Blast Radius High); nested choices need a human oracle (Edge-Case Discovery High); the screen is settled, so an agent builds it against agreed scenarios.

**Provisional commit message**

```
feat(catalogue): archive a category branch through an explicit decision

- Archiving a subtree hides products from browsing over the following
  weeks, so the screen says so and makes the destination a choice
- Moving first is the recommended path; type-to-confirm comes last, after
  the path is chosen
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A high-consequence settled screen built against a human oracle.

**Agent prompt**

```
Role: You are building the category archive decision (H-16) for the Field
Sales Management System.

Context:
- Slice: archiving a category with children opens a decision screen stating
  what it affects and that the loss shows only gradually, offers moving
  things first or archiving all with an explicit destination for the
  products, and completes only after a typed confirmation.
- Specs: plan_docs/stories/product-management.md US-009 S1–S6 and design
  decision "Branch archive as a decision, with type-to-confirm on the wide
  path"; plan_docs/uxdocs/02-head-office.md H-16 (settled: H16.1 the
  gradual-visibility warning is written out; H16.2 "Move first" recommended
  and listed first; H16.3 one typed confirmation, the destination nested
  inside "Archive everything now"; H16.4 type-to-confirm last).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — move (T-11.1.1),
  recategorise (T-11.2.1), archive pattern (T-1.5.1).
- Pattern to follow: T-11.1.1 and T-11.2.1 for the Move-first path.

Acceptance criteria:
1. Archive on "Suncare" (4 subcategories, 180 products beneath) shows
   "Archiving Suncare will also archive 4 subcategories and take 180 products
   out of this branch", the gradual-visibility warning, and paths "Move the
   products first, then archive" (recommended, first), "Archive everything
   now", and Cancel.
2. Move first, moving 2 subcategories to "Body Care" and recategorising the
   12 direct products, returns to the decision with "2 subcategories, 168
   products".
3. Archive everything now → products "Up to Health > Skincare" → typed
   confirmation archives Suncare and its 4 subcategories and puts the 180
   products in Health > Skincare.
4. Products "Leave them in the archived branch" repeats "180 products will
   remain orderable and searchable but disappear from category browsing"
   above the confirmation; afterwards they're searchable with breadcrumb
   "Suncare (archived)".
5. For a root category, "Up to parent" is not offered; another category or
   leave-in-branch must be chosen.
6. A category with no children and no products gets a plain confirmation.
7. The typed confirmation text follows the MI-55 decision.

Constraints:
- Use the project's existing conventions and test framework.
- One transaction per archive.
- No tests of framework internals or trivial members.
- This task opts in to an archive flag on categories.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after every agreed scenario passes against the archive
operation, stop and show the results with the screen. Resume only on
"Continue T-11.3.1".

Steps: 1. archive operation with destination choices; 2. H-16 screen;
3. Move-first loop; 4. typed confirmation; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-11.3.1-S. You
are forbidden from designing your own test cases.

Definition of done: archiving a category with children opens a decision
screen stating what it affects and that the loss shows only gradually, offers
moving things first or archiving all with an explicit destination for the
products, and completes only after a typed confirmation.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Screen matches H-16
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: how archived-category products appear in search and browsing
across surfaces (T-11.3.2).
```

**Checkpoint**

Produces before pausing — every agreed scenario passing against the archive operation, and the H-16 screen.
Human reviews — Is it impossible to archive a branch without having chosen, and seen, where its products go?
Resume trigger — `Continue T-11.3.1`

---

### T-11.3.2-S — Test scenarios for products in archived categories

**Owner** — Scenario Review
**Gates** — T-11.3.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for how products in archived categories appear
(task T-11.3.2). Read plan_docs/stories/product-management.md US-009 S4 and
US-010 S3. Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Start with characterisation scenarios pinning
product search (T-1.7.1) and the Order Pad (T-5.1.2). Cover S4 and US-010 S3,
then derivable edges: a product in an archived category that is also in a
rep's Range, the tablet pad's category sections, a subcategory of an archived
category. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-11.3.2 — Keep archived-category products searchable but out of browsing

**Parent story**

> As a Head Office User, I want to search products by code, name, brand or supplier and see each with its breadcrumb and status so that I find the right record among near-duplicates.
>
> Acceptance criteria:
> - Ticking "Include unavailable and archived-category products" shows products in archived categories, labelled (US-010 S3)
> - Products left in an archived branch remain orderable and searchable with breadcrumb "Suncare (archived)", but don't appear when browsing categories (US-009 S4)

**Slice** — Products left in an archived category stay orderable and findable by search with a breadcrumb marked "(archived)", but no longer appear when browsing categories on the website or the tablet, and head office can include them in product search.
**Spec source** — Product Management US-010 S3; US-009 S4
**Depends on** — T-11.3.1, T-1.7.1, T-5.1.2
**Pattern to follow** — T-1.7.1 (search), T-5.1.2 (pad)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies search and the tablet pad and adds a snapshot flag, so characterisation comes first.

**Provisional commit message**

```
feat(catalogue): keep archived-category products searchable only

- Leaving products in an archived branch is a legitimate choice, so they
  stay orderable and findable by search, labelled, but not browsable
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Cross-surface change behind a characterisation pass.

**Agent prompt**

```
Role: You are making products in archived categories searchable but not
browsable across the Field Sales Management System.

Context:
- Slice: products left in an archived category stay orderable and findable
  by search with a breadcrumb marked "(archived)", but no longer appear when
  browsing categories on the website or the tablet; head office can include
  them in product search.
- Specs: plan_docs/stories/product-management.md US-009 S4, US-010 S3.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — product search
  (T-1.7.1), Order Pad (T-5.1.2), snapshot (T-4.1.1), category archive
  (T-11.3.1).
- Pattern to follow: T-1.7.1 and T-5.1.2.

Acceptance criteria:
1. A product in archived "Suncare" is found by search with breadcrumb
   "Suncare (archived)" and can still be ordered.
2. It does not appear when browsing categories on H-15 or on the tablet's
   Order Pad sections.
3. On H-12, it is excluded by default and included, labelled, when "Include
   unavailable and archived-category products" is ticked.
4. Its Range membership and availability are unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot changes (archived flag on categories) go through T-4.1.1's
  versioning and its owner's review.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
search and the pad; stop and show them passing. Resume only on
"Continue T-11.3.2".

Steps: 1. characterisation tests; 2. search label and filter; 3. browse
exclusion on website and tablet; 4. snapshot flag; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-11.3.2-S; do not design
your own.

Definition of done: products left in an archived category stay orderable and
findable by search with a breadcrumb marked "(archived)", but no longer
appear when browsing categories on the website or the tablet, and head office
can include them in product search.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: customer catalogue browsing (E26 applies the same rule).
```

**Checkpoint**

Produces before pausing — characterisation tests of search and the pad, passing.
Human reviews — Do archived-category products stay orderable everywhere while leaving browsing?
Resume trigger — `Continue T-11.3.2`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | PM-006 (T-11.1.1), PM-007 (T-11.2.1), PM-009 (T-11.3.1), PM-010 part (T-11.3.2) |
| Every task satisfies the three slice criteria | Pass | 4 of 4 |
| Every task carries a tier with a rationale citing dimensions | Pass | 4 of 4 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-11.3.1 (MI-55) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-11.3.2 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | No Agent-Autonomous task in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-11.3.1 is Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | 4 of 4 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | None in this epic |
| Every scenario task precedes the task it gates | Pass | 4 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-08, 55 |
