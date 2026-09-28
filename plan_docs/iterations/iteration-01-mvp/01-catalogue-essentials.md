# E1 — Staff website access and catalogue essentials

**Iteration** — 1, MVP: orders from the field reach the warehouse
**Outcome** — Staff sign in once to their permitted areas before head office lists a product from the minimum record in a findable category tree, gives it dated base prices, a unit, classification and a restriction group, and finds it again by search.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 0a | Staff and Tablet Sign-in US-001 — Sign in to the staff website | Must | S1–S8 (T-1.0.1) |
| 0b | Staff and Tablet Sign-in US-002 — Enter only staff areas allowed by my role | Must | S1–S3 (T-1.0.1, T-1.0.2) |
| 1 | Product Management US-005 — Browse and edit the Category tree | Must | S1–S5 (the "Recategorise products?" offer in S3 arrives with E11) |
| 2 | Product Management US-001 — Create a product with the minimum record | Must | S1–S5 except the Range tick in S1 (E9, Range Lifecycle US-003) |
| 3 | Product Management US-002 — Set and change the Base Price | Must | S1–S5 |
| 4 | Product Management US-003 — Set Unit of Measure, step and minimum | Should | S1–S5 |
| 5 | Product Management US-008 — Maintain reference lists with archive-not-delete | Must | S1–S5 |
| 6 | Product Management US-004 — Maintain Profile, Attributes, Brands, supplier and Restriction Group | Should | S1–S5 |
| 7 | Product Management US-010 — Find products | Must | S1–S3 (archived-category products arrive with E11) |

**Exit criterion** — A Staff Member signs in once, reaches only their held areas, and loses removed access on the next website request. A Head Office User can then create a category tree to six levels, list a product from its minimum record, add future- and past-dated base prices, set a measure unit with step and minimum, classify the product (profile, brands, supplier, restriction group, attributes), retire reference data without breaking history, and find any product by code, name, brand or supplier with its breadcrumb. A future-dated price applies on the tablet on its day.

**Capability-class stamp** — Frontier + extended reasoning for T-1.1.1's hardest slivers; Frontier workhorse for Agent-Assisted tasks and scenario drafting; Fast mid-tier for Agent-Autonomous tasks. Concrete models: see the matrix in [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; no delegation-triage report was supplied, and no code exists. Every tier below is `(inferred)`.

**Spec sources** — [staff-and-tablet-sign-in.md](../../stories/staff-and-tablet-sign-in.md) (US-001, US-002), [07-access-and-sign-in.md](../../uxdocs/07-access-and-sign-in.md) (I-01, I-02, I-05), [product-management.md](../../stories/product-management.md), [02-head-office.md](../../uxdocs/02-head-office.md) (H-12, H-13, H-15, H-17), [00-conventions-and-shared-elements.md](../../uxdocs/00-conventions-and-shared-elements.md) (§7 Archive, never delete).
**Screen review** — I-01, I-02 and I-05 are layout proposals. Confirm their layouts under MI-08 before building them; the sign-in and role behaviour comes from the stories.

---

### T-1.0.1-S — Test scenarios for staff website sign-in and held areas

**Owner** — Human-Led
**Gates** — T-1.0.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Single-role Head Office User, Sales Manager and Field Salesperson: which area opens for each (US-001 S1–S3)?
- With two roles, does one sign-in open the last-used permitted area and allow switching without another sign-in (S4)? What happens on first use or when the last-used area is no longer held (S5–S6)?
- What do rejected sign-in and Sign out leave accessible (S7–S8)? Test a direct link as well as navigation.
- Which credential method and staff account recovery path were chosen under MI-03? Do not invent them in the test list.

---

### T-1.0.1 — Sign staff into their permitted website areas

**Parent story**

> As a Staff Member, I want to sign in and reach an area allowed by my role so that I can start my work without searching for the right entry point.
>
> Acceptance criteria:
> - A single-role staff member opens the rep, manager or head-office area held by that role (US-001 S1–S3).
> - A person with both manager and head-office roles signs in once, opens their last-used permitted area and can switch areas without signing in again (S4).
> - First use or a removed last-used area leads to a permitted choice or the only remaining area (S5–S6).
> - Rejected sign-in opens no staff area; Sign out ends the shared session (S7–S8).

**Slice** — A Staff Member signs in once and reaches a permitted staff landing, with multi-role routing, rejection and sign-out demonstrable before catalogue pages exist.
**Spec source** — Staff and Tablet Sign-in US-001 S1–S8, US-002 S1–S2; uxdocs 07 I-01, I-02
**Depends on** — none
**Pattern to follow** — novel — first staff identity slice
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — authentication and shared role routing have High Blast Radius and Novelty; MI-01, MI-03 and MI-51 leave the stack and credential integration unscoreable, treated High.

**Provisional commit message**

```
feat(auth): sign staff into permitted website areas

- Staff with several roles enter once and can reach each area they hold
- Rejected sign-in and sign-out never leave a staff area open
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The first identity and area boundary is copied by every later staff page.

**Work package**

Increments:
1. Record the stack, staff identity method and whether the areas share one site or several (MI-01, MI-03, MI-51); one sign-in across held roles is the required experience either way.
2. Establish staff identities and the Field Salesperson, Sales Manager and Head Office User roles; keep the administrator-role question visible under MI-03.
3. Make I-01 sign-in and one protected landing per area work end to end, with rejection and Sign out.
4. Route to the last-used permitted area; on first use with several roles show I-02; allow area switching in the same session. Deny a direct link to an unheld area without showing its content.
5. Execute the agreed Human-Led scenario list against the website boundary, including one- and two-role accounts.

Decision points:
- Which credential method and recovery route are chosen for staff (MI-03), without mixing in Customer User invitations?
- How are the areas hosted (MI-51) while keeping one session and direct-link return across them?
- Is administrator a separate role or a permission held by another staff role (MI-03)?

Delegable slivers:
- **Sign-in page shell** — Build the I-01 page around the chosen staff credential control, with accessible labels, a retry state and Sign out return. Do not choose the credential method or implement authorisation.
- **Role-routing scenario fixtures** — Given the agreed T-1.0.1-S list, create test fixtures for single-role and combined-role staff and assert their landing and switch destinations. Do not change role resolution or choose a new landing rule.

---

### T-1.0.2-S — Test scenarios for immediate website role removal

**Owner** — Human-Led
**Gates** — T-1.0.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Characterise access under T-1.0.1 before the change: permitted direct read, permitted save, and denied direct link.
- Remove Head Office User from a signed-in manager/head-office account: does the next catalogue read and save fail without restricted data (US-002 S3)?
- What does I-05 say for a rejected write versus a rejected page read, and which remaining area can be opened?
- If no staff area remains, what can the person see and do after the denial?

---

### T-1.0.2 — Enforce a removed website role on the next request

**Parent story**

> As a Field Salesperson, I want the website to make my permitted area clear so that I can work without entering a head-office function by mistake.
>
> Acceptance criteria:
> - A held rep area opens and an unheld head-office area does not reveal content or actions (US-002 S1–S2).
> - Removing Head Office User during an open session makes the next catalogue read or action fail, with I-05 explaining the change (S3).

**Slice** — A Staff Member whose role was removed loses that area's reads and writes on the next request while the permitted areas remain available.
**Spec source** — Staff and Tablet Sign-in US-002 S1–S3; uxdocs 07 I-05
**Depends on** — T-1.0.1
**Pattern to follow** — T-1.0.1 (staff role boundary)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — authorisation has High Blast Radius despite settled UX; characterisation and human-owned denial scenarios precede the change.

**Provisional commit message**

```
feat(auth): apply staff role removals to the next request

- Open sessions cannot keep using a staff area after its role is removed
- Denied reads and writes explain the change without exposing area data
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
The behaviour is specified, but the shared authorisation boundary needs close review.

**Work package**

Increments:
1. Characterise T-1.0.1's current permitted and denied reads, writes, sign-out and last-used-area routing; keep those tests passing.
2. Recheck the current staff role on each protected request, including a direct page read and a write from an already open form.
3. Show I-05 when access changes, with “not saved” only for a denied write; offer only remaining permitted destinations.
4. Execute the agreed T-1.0.2-S scenarios, including removal while a session stays open.

Decision points:
- Which requests are the shared authorisation boundary across the chosen site structure (MI-51)?
- How does the site clear or replace a no-longer-permitted last-used destination without another sign-in?

Delegable slivers:
- **Access-changed page** — Build I-05 from the agreed read/write states and supplied remaining-area list. Do not implement the role check or render restricted content behind the page.
- **Denial tests** — Given the agreed scenarios, test direct-link and already-open-form denial after a role removal. Test only the observable response; do not change authorisation logic.

---

### T-1.1.1-S — Test scenarios for creating and renaming categories

**Owner** — Human-Led
**Gates** — T-1.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Use the already agreed T-1.0.1-S and T-1.0.2-S role scenarios; which catalogue-specific access check must still be demonstrated here?
- What must happen to breadcrumbs shown on historic orders and calls after a rename — current path, or path at capture? The story says "no order or call is changed"; confirm that means the stored record, while display uses the current path.
- Is there a maximum depth at all, or only a design target of six?
- Which category form state survives a rejected or expired staff session without allowing an unauthorised save?

---

### T-1.1.1 — Create and rename categories to any depth on the staff website

**Parent story**

> As a Head Office User, I want a tree that shows how many products are in each category and beneath it, with products allowed on branches, so that I can see where the catalogue is and keep it findable.
>
> Acceptance criteria:
> - Renaming "Lotions" to "Lotions & Creams" updates every product's breadcrumb and changes no order or call (S4)
> - Adding a sixth level is accepted and shown with its breadcrumb (S5)

**Slice** — A signed-in Head Office User creates root and nested categories to six levels and renames one, and every breadcrumb beneath reflects the new name.
**Spec source** — Product Management US-005 S4, S5; uxdocs 02 H-15 (drafting calls H15.1–H15.3)
**Depends on** — T-1.0.2
**Pattern to follow** — T-1.0.1 (staff website shell), T-1.0.2 (role boundary); novel catalogue persistence
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — Novelty High: first catalogue slice, while stack and site structure remain undecided (MI-01, MI-51); Blast Radius High: persistence and category identity are shared by later tasks. Staff sign-in and role guards arrive through T-1.0.1 and T-1.0.2.

**Provisional commit message**

```
feat(catalogue): create and rename categories on the staff website

- First catalogue slice proves category persistence through the signed-in
  staff website before anything wider is built
- Breadcrumbs are derived from the tree, never stored per product, so a
  rename can never leave stale paths on orders or calls
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
The catalogue persistence and category identity choices made here are copied by later catalogue tasks, so the delegated slivers that touch them need close review.

**Work package**

Increments:
1. Use the stack and hosting decision from T-1.0.1; record the catalogue persistence and category identity choices as a short decision record in the repository.
2. Use T-1.0.1 and T-1.0.2's staff sign-in and role boundary for the catalogue area; record the rep → manager reporting line needed by later work (MI-03). Verify: a Head Office User reaches the catalogue area; a Field Salesperson cannot.
3. Category domain type: a node with a parent, unlimited depth, and a breadcrumb derived by walking to the root. Verify with unit tests at depth 6.
4. Persistence for categories; create root and child categories through the site. Verify a six-level tree survives a restart.
5. Rename: the breadcrumb of every descendant changes; nothing that references a category by identity is rewritten. Verify with two same-named leaves under different parents ("Lotions" under Suncare and under Body Care).
6. Test harness conventions for the rest of the plan: where unit, integration and UI tests live, and the `Should_Outcome_When_Condition` naming already used in the story documents.

Decision points:
- Is the tablet app in the same language as the server, so the price engine (T-6.4.1) can be one shared module? This choice constrains E6.
- Are breadcrumbs derived on read, or cached with invalidation on rename and move? Derived is simpler; caching must never serve a stale path.
- Does "administrator" (Visit Planning BR-NEW-004) exist as a separate role, or is it a Head Office User permission?
- What is the time zone and day boundary for dated prices, cut-offs and whole-day promotions (MI-53)? Europe/Dublin is assumed.

Delegable slivers:
- **Category form and list UI** — Build the create and rename forms and a single-category view with its breadcrumb as navigation, per `plan_docs/uxdocs/02-head-office.md` H-15 (H15.1: one category at a time, breadcrumb as navigation). Use the domain type and persistence already in the repository. Do not add counts, search, move or archive; those are T-1.1.2 and E11.
- **Breadcrumb unit tests** — Given the agreed scenario list from T-1.1.1-S, write the tests for breadcrumb derivation and rename in the project's test framework. Do not change domain code; report any failing scenario rather than fixing it.
- **Role-guard tests** — Write tests proving a Field Salesperson and a Sales Manager without head-office rights cannot reach the catalogue area, using the role model already in the repository. Do not change the role model.

---

### T-1.2.1-S — Test scenarios for creating a product from the minimum record

**Owner** — Scenario Review
**Gates** — T-1.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for creating a product from the minimum record
(task T-1.2.1). Read plan_docs/stories/product-management.md US-001 and the
glossary. Output one line per scenario as Should_Outcome_When_Condition,
followed by "→" and a one-line intent. Cover every story scenario (S1–S5),
then the edge cases you can derive: whitespace or case differences in codes,
a category that is archived, a price of zero, very long names. Mark any case
where the story does not say what correct is as "Needs a decision". Write no
test code and change no files. Stop after producing the list.
```

---

### T-1.2.1 — Create a product from the minimum record

**Parent story**

> As a Head Office User, I want to list a new product with only what I know at the time — code, name, Category, unit, price and Range — so that it reaches reps quickly and the rest is added later.
>
> Acceptance criteria:
> - Entering code, name, Category, Unit Each and Base Price from today saves the product (S1, without the Range tick)
> - A duplicate code is rejected with "Code SUN-0342 is already used by SPF30 Sun Lotion v2 200ml" (S2)
> - Saving without a Range saves it as unranged with "Unranged — orderable by all reps" (S3)
> - Saving without a Category is rejected with "Choose a category" (S4)
> - The saved product shows Profile, Attributes, Brands, supplier, Restriction Group and Replacements as empty sections (S5)

**Slice** — A Head Office User saves a new product from code, name, category, unit and a base price effective today, and it opens as a record with its other sections empty.
**Spec source** — Product Management US-001 S1–S5; uxdocs 02 H-13 (H13.1)
**Depends on** — T-1.1.1
**Pattern to follow** — T-1.1.1 (staff site form, persistence, role guard)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — Blast Radius Medium: every order line, stock check line and snapshot references Product; Oracle Ambiguity Low.

**Provisional commit message**

```
feat(catalogue): list a product from its minimum record

- Head office lists products the moment a supplier notice arrives and fills
  the rest later, so only code, name, category, unit and price are required
- Model keeps an optional parent reference and name/value attributes so
  Phase 2 variant groups can be added without reworking order lines
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared model with a design checkpoint, built by an agent under review.

**Agent prompt**

```
Role: You are implementing "create a product from the minimum record" for
the Field Sales Management System's head office website.

Context:
- Slice: a Head Office User saves a new product from code, name, category,
  unit and a base price effective today; it opens as a record with its other
  sections empty.
- Specs in this repository: plan_docs/stories/product-management.md US-001
  (and glossary §1, design decision "Profile plus Attributes; variants
  deferred but not blocked"); plan_docs/uxdocs/02-head-office.md H-13
  (drafting call H13.1: create is one short form; on save open the record).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — follow the structure
  created by the category task (T-1.1.1).
- Pattern to follow: the category create form, persistence and role guard
  from T-1.1.1.

Acceptance criteria:
1. Code "SUN-0342", name "SPF30 Sun Lotion v2 200ml", Category
   "Health > Skincare > Suncare > Lotions", Unit Each, Base Price €12.50
   from today saves the product.
2. A duplicate code is rejected with "Code SUN-0342 is already used by
   SPF30 Sun Lotion v2 200ml".
3. Saving without a Range saves it unranged with the note "Unranged —
   orderable by all reps". (No Range picker exists yet.)
4. Saving without a Category is rejected with "Choose a category".
5. The saved record shows Profile, Attributes, Brands, supplier, Restriction
   Group and Replacements as empty sections.
6. The base price is stored as the first entry of a price history with an
   Effective From date (today), not as a single overwritable field.

Constraints:
- Use the project's existing conventions and test framework; if none exist
  for a concern, stop and ask.
- Locality of logic: a reader must understand product creation without
  tracing a chain of delegations across files.
- No tests of framework internals or trivial members; meaningful coverage
  over coverage metrics.
- This task opts in to creating the product and price-history tables.
  Any other irreversible or schema-altering operation is out of scope.
- Product codes are entered by head office, never generated (assumption,
  MI-13). Quantities and prices must use exact decimal types, never floats.
- The model keeps an optional parent-product reference and attributes as
  name/value pairs (Phase 2 allowance); do not build variants.
- UI wording comes verbatim from the acceptance criteria.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the Product and price-history model and its persistence
design (fields, keys, uniqueness rule, how "price on a date" will be read),
stop and show it to me. Resume only on "Continue T-1.2.1".

Steps (domain → persistence → handler → UI → tests):
1. Product domain type with code, name, category reference, unit (Each for
   now; T-1.4.1 adds measures), optional parent reference, attributes list,
   and a price-history collection seeded with one entry.
2. Persistence with a unique constraint on code.
3. Create handler enforcing criteria 2 and 4 and the unranged note.
4. The one-form create page (H13.1) that opens the saved record with empty
   sections (criterion 5).
5. Tests from the agreed scenario list.

Test expectations: implement exactly the scenarios agreed in T-1.2.1-S. Do
not design your own test cases; if a scenario seems missing, list it in your
report instead of adding it.

Definition of done: a Head Office User saves a new product from code, name,
category, unit and a base price effective today, and it opens as a record
with its other sections empty.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 each demonstrated by a passing test or a screenshot
- [ ] Duplicate-code check holds under concurrent saves (unique constraint)
- [ ] No float types for money or quantity
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: dated price changes (T-1.3.1), units of measure (T-1.4.1),
classification and attributes editing (T-1.6.1, T-1.6.2), the Range picker
(E9, Range Lifecycle US-003), product search (T-1.7.1).
```

**Checkpoint**

Produces before pausing — the Product and price-history schema, the uniqueness rule on code, and the signature of the "price in effect on date D" read.
Human reviews — Does this model let every later area reference a product and its price at capture without schema change, including Phase 2 variants?
Resume trigger — `Continue T-1.2.1`

---

### T-1.1.2 — Show category counts, branch products and breadcrumb search

**Parent story**

> As a Head Office User, I want a tree that shows how many products are in each category and beneath it, with products allowed on branches, so that I can see where the catalogue is and keep it findable.
>
> Acceptance criteria:
> - Opening "Suncare" shows "180 products beneath · 12 here", its 4 subcategories, then the 12 direct products under "In Suncare" (S1)
> - Searching "Lotions" shows both matches with full paths (S2)
> - Adding subcategory "After Sun" to Suncare keeps its 12 products in Suncare (S3; the inline "Recategorise products?" offer arrives with E11)

**Slice** — Opening a category shows how many products sit beneath and directly in it, its subcategories first and its own products after, and category search tells same-named categories apart by breadcrumb.
**Spec source** — Product Management US-005 S1–S3; uxdocs 02 H-15
**Depends on** — T-1.2.1
**Pattern to follow** — T-1.1.1 (category view)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: M | Confidence: M
  (inferred) — fully specified by the story, Blast Radius Low, test precedent from T-1.1.1; Taste Medium because H-15 is a draft — if H-15 is not confirmed before this task starts (MI-08), treat it as Agent-Assisted.

**Provisional commit message**

```
feat(catalogue): show category counts and breadcrumb search

- Head office needs to see where the catalogue's weight is before it
  restructures a branch that has grown to six levels
- Breadcrumbs in results disambiguate leaves that share a name
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Pattern-matched read views over the established category model.

**Agent prompt**

```
Role: You are adding category counts, branch products and category search to
the head office catalogue of the Field Sales Management System.

Context:
- Slice: opening a category shows "N products beneath · M here", its
  subcategories first and its own products after; category search shows
  full breadcrumbs.
- Specs: plan_docs/stories/product-management.md US-005 S1–S3 and design
  decision "Unlimited depth, designed for six, breadcrumbs everywhere";
  plan_docs/uxdocs/02-head-office.md H-15 (H15.1).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — extend the category
  view created in T-1.1.1.
- Pattern to follow: T-1.1.1's category view and tests.

Acceptance criteria:
1. Opening "Suncare" (4 subcategories, 12 direct products, 180 beneath)
   shows "180 products beneath · 12 here", then the 4 subcategories, then
   the 12 products under the heading "In Suncare".
2. Searching "Lotions" where it exists under Suncare and Body Care returns
   both, each with its full breadcrumb.
3. Adding subcategory "After Sun" to Suncare creates it and leaves the 12
   products in Suncare.
4. Counts include products at every depth beneath, counted once.

Constraints:
- Use the project's existing conventions and test framework.
- Locality of logic: count derivation lives in one place, readable alone.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.
- Do not add the "Recategorise products?" offer; that belongs to E11.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. count query for "here" and "beneath"; 2. category view ordering
(subcategories, then "In <name>"); 3. category search with breadcrumbs;
4. tests.

Test expectations: select the scenarios yourself from the criteria above and
write the tests. Include a six-level tree and two same-named leaves.

Definition of done: opening a category shows how many products sit beneath
and directly in it, subcategories first and its own products after, and
category search tells same-named categories apart by breadcrumb.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Counts correct at depth 6
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: moving categories (E11, T-11.1.1), recategorising (E11),
archiving (E11), product search (T-1.7.1).
```

---

### T-1.4.1-S — Test scenarios for units of measure

**Owner** — Scenario Review
**Gates** — T-1.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for units of measure, step and minimum (task
T-1.4.1). Read plan_docs/stories/product-management.md US-003 and US-002 S5,
and plan_docs/stories/rep-at-a-location-tablet.md US-008 S6 for the
validation wording. Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover every
story scenario, then derivable edges: step 0.25, minimum equal to step,
quantities one step off, very large quantities, litre and metre units.
Mark anything the story leaves undecided as "Needs a decision". Write no
test code and change no files.
```

---

### T-1.4.1 — Set a unit of measure with step and minimum

**Parent story**

> As a Head Office User, I want to mark a product as sold by a measure with a step and minimum so that reps order sensible quantities and the tablet shows the right unit.
>
> Acceptance criteria:
> - Unit kg, Step 0.5, Minimum 1.0 allows 1.0, 1.5, 2.0 kg (S1)
> - Minimum left blank defaults to the step (S2)
> - Minimum 0.7 with Step 0.5 is rejected with "Minimum must be a multiple of the step" (S3)
> - Unit Each hides step and minimum; quantities are whole numbers of 1 or more (S4)
> - A measure-based price is shown and entered as "€4.80 per kg" (US-002 S5)

**Slice** — A Head Office User sets a product to sell per kg with a step and minimum, invalid minimums are rejected, and its price reads "per kg".
**Spec source** — Product Management US-003 S1–S4; US-002 S5
**Depends on** — T-1.2.1
**Pattern to follow** — T-1.2.1 (product record section)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — Blast Radius Medium: the quantity value type is shared by every order line and stock count; checkpoint on that type.

**Provisional commit message**

```
feat(catalogue): sell products by measure with a step and minimum

- Weight and volume selling needs usable increments; a step stops orders
  such as 2.4718 kg reaching the warehouse
- Pack sizes stay separate products, so the unit describes how one product
  is sold, not how it is packed
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Introduces the quantity value type every later order and count task uses.

**Agent prompt**

```
Role: You are adding unit of measure, quantity step and minimum to products
in the Field Sales Management System.

Context:
- Slice: a Head Office User sets a product to sell per kg with a step and
  minimum; invalid minimums are rejected; its price reads "per kg".
- Specs: plan_docs/stories/product-management.md US-003, US-002 S5 and the
  design decision "Unit of Measure with a step; pack sizes stay separate".
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — extend the Product
  model and record page from T-1.2.1.
- Pattern to follow: T-1.2.1's product record sections.

Acceptance criteria:
1. Unit kg, Quantity Step 0.5, Minimum 1.0 saves; valid quantities are 1.0,
   1.5, 2.0 kg and so on.
2. Unit kg, Step 0.5, Minimum blank saves with Minimum 0.5.
3. Step 0.5, Minimum 0.7 is rejected with "Minimum must be a multiple of the
   step".
4. Unit Each hides step and minimum; valid quantities are whole numbers of 1
   or more.
5. For a kg product the base price is entered and shown as "€4.80 per kg".
6. A reusable quantity validation reports "Enter a quantity of 1 or more" for
   Each and "Enter at least 1.0 kg in steps of 0.5 kg" for this measure
   product (wording from Rep at a Location US-008 S6), for later use on the
   tablet.

Constraints:
- Use the project's existing conventions and test framework.
- Quantities are exact decimals; never floats. Step arithmetic must not
  suffer rounding error (0.1 + 0.2 style).
- Locality of logic: the quantity rule lives in one value type.
- No tests of framework internals or trivial members.
- This task opts in to adding unit, step and minimum columns to products.
- Changing the unit of a product that already has orders is T-1.4.2; for
  now, allow the change only on products with no orders.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the quantity value type and its validation API are
written with their unit tests, stop and show them. Resume only on
"Continue T-1.4.1".

Steps: 1. quantity value type (unit, step, minimum, validate); 2. persist
unit/step/minimum; 3. product record section; 4. price display per unit;
5. tests from the agreed scenarios.

Test expectations: implement the scenarios agreed in T-1.4.1-S; do not
design your own.

Definition of done: a Head Office User sets a product to sell per kg with a
step and minimum, invalid minimums are rejected, and its price reads
"per kg".

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] No float arithmetic in the quantity type
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: unit change on products with orders (T-1.4.2), tablet
quantity controls (T-5.1.3), quantity breaks (T-6.3.1).
```

**Checkpoint**

Produces before pausing — the quantity value type, its validation API and passing unit tests for Each and for kg with step 0.5.
Human reviews — Is this the one quantity type the tablet, the order record and stock counts will all use, with no rounding path?
Resume trigger — `Continue T-1.4.1`

---

### T-1.3.1-S — Test scenarios for dated base prices

**Owner** — Scenario Review
**Gates** — T-1.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for dated base prices (task T-1.3.1). Read
plan_docs/stories/product-management.md US-002. Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S1,
S3 and S4, then derivable edges: two future prices, a past-dated price
between two existing ones, a price effective today, the last day before a
change. Mark anything the story leaves undecided (editing, deleting a future
price, time zone) as "Needs a decision". Write no test code; change no files.
```

---

### T-1.3.1 — Add dated base prices and keep their history

**Parent story**

> As a Head Office User, I want to add a new price with the date it applies from, keeping every previous price, so that old orders keep their price and a rise takes effect on the right day.
>
> Acceptance criteria:
> - Adding €13.20 effective 1 November 2026 over €12.50 shows "€12.50 · rising to €13.20 on 1 Nov" and the history lists both (S1, record part)
> - Adding €12.00 effective 1 September 2026 inserts it into history for 1 Sep – 31 Oct, with "Existing orders keep the price captured" (S3)
> - A second price with the same Effective From is rejected with "A price already starts on 1 Nov 2026 — edit it instead" (S4)

**Slice** — A Head Office User adds a future- or past-dated base price, the record shows the current price and any coming change, and the full history is kept.
**Spec source** — Product Management US-002 S1 (record), S3, S4; design decision "Dated Base Price, routine edits, future prices reach the tablet"
**Depends on** — T-1.2.1
**Pattern to follow** — T-1.2.1 (price-history model created there)
**Ownership** — Impl: Human Tight-Loop | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — Blast Radius High: money — the "price in effect on a date" rule feeds the snapshot and therefore every captured line; Oracle Ambiguity Low.

**Provisional commit message**

```
feat(catalogue): add dated base prices with full history

- "What did this cost when that order was placed" must always be
  answerable, so prices are never overwritten
- A past-dated price is allowed but never reprices captured orders; lines
  keep the price in their snapshot
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Delegated slivers are small, but each touches money.

**Work package**

Increments:
1. The "price in effect on date D" function over the price history, with ranges derived from consecutive Effective From dates. Verify with the S1 and S3 examples.
2. Add-price handler: future and past dates, same-date rejection with the S4 message.
3. Record display: "€12.50 · rising to €13.20 on 1 Nov" and a history list.
4. The past-dated note "Existing orders keep the price captured".

Decision points:
- Is a price row ever editable (S4 says "edit it instead") — and if so, can an edit change a date already in the past? Editing past history rewrites what an audit would read.
- Which day boundary applies — head office time zone (MI-53)?
- Can a future-dated price be deleted before it takes effect?

Delegable slivers:
- **History list UI** — Render the product's price history newest first with Effective From dates and the "rising to … on …" line on the H-13 record, using the existing "price on date" function. Do not change how prices are stored or resolved.
- **Validation messages** — Add the same-date rejection "A price already starts on 1 Nov 2026 — edit it instead" in the add-price handler, reading existing history only. Do not alter existing rows.

---

### T-1.5.1-S — Test scenarios for archive-not-delete on Brands

**Owner** — Scenario Review
**Gates** — T-1.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the Brands archive-not-delete list (task
T-1.5.1). Read plan_docs/stories/product-management.md US-008 S1–S4 and
plan_docs/uxdocs/00-conventions-and-shared-elements.md §7. Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover every story scenario, then derivable edges: a reference added
after the page loads, a brand used only as an alternative brand, archiving
then editing a product that holds the archived brand, duplicate names. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-1.5.1 — Maintain Brands with archive-not-delete

**Parent story**

> As a Head Office User, I want to add, edit and retire Profiles, Attribute names, Brands, suppliers and Restriction Groups, with retirement archiving anything in use so that history never breaks.
>
> Acceptance criteria:
> - Brand "TestCo" with no products offers Delete, and confirming removes it (S1)
> - Brand "SunCo" with 24 products shows no Delete; Archive shows "Used by 24 products and 1 specialist assignment", and the archived brand stays labelled on its products and is not offered for new ones (S2)
> - Archived brands are hidden behind "Show archived (3)" (S3)
> - Un-archiving "SunCo" offers it again (S4)

**Slice** — A Head Office User adds, edits, deletes an unused Brand or archives one in use, with its usage shown and archived brands hidden behind a toggle.
**Spec source** — Product Management US-008 S1–S4; uxdocs 00 §7; uxdocs 02 H-17 (H17.1, H17.2)
**Depends on** — T-1.1.1
**Pattern to follow** — novel — see design notes (first archive-not-delete list; about ten later lists copy it)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — Blast Radius Medium: a missed reference lets Delete break history months later; checkpoint on the cross-context reference-count interface.

**Provisional commit message**

```
feat(catalogue): archive reference data instead of deleting it in use

- Deleting something history references breaks old orders and reports
  months later, so Delete is not even shown while anything uses the item
- Usage counts come from each owning area, so the rule holds as new areas
  start referencing brands
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Sets a pattern that later lists repeat.

**Agent prompt**

```
Role: You are building the first archive-not-delete reference list (Brands)
for the Field Sales Management System's head office website.

Context:
- Slice: a Head Office User adds, edits, deletes an unused Brand or archives
  one in use, with its usage shown and archived brands behind a toggle.
- Specs: plan_docs/stories/product-management.md US-008 S1–S4 and design
  decision "Archive-not-delete, with Delete hidden when referenced";
  plan_docs/uxdocs/00-conventions-and-shared-elements.md §7;
  plan_docs/uxdocs/02-head-office.md H-17 (H17.1 one screen with a list
  switcher; H17.2 only Delete or Archive is offered, never both).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}}.
- Pattern to follow: T-1.1.1's staff site pages. This task defines the list
  pattern later lists reuse (Product Profiles, Attribute names, suppliers,
  Restriction Groups, Location Types, Contact Types, Towns, Location Profiles,
  Visit Due Reason Types).

Acceptance criteria:
1. A Brand with no references shows Delete (not Archive); confirming
   removes it.
2. A Brand used by 24 products and 1 specialist assignment shows Archive
   (not Delete) with "Used by 24 products and 1 specialist assignment".
3. An archived Brand stays on its products labelled "(archived)" and is not
   offered for new selections.
4. The list hides archived items behind "Show archived (N)".
5. Un-archiving makes the Brand selectable again.
6. Usage counts are gathered through one interface that any area can
   implement ("what references item X?"); products implement it now.

Constraints:
- Use the project's existing conventions and test framework.
- Delete must be impossible while any reference exists, including when two
  users act at once (check references inside the delete transaction).
- Locality of logic: the Delete/Archive decision lives in one place.
- No tests of framework internals or trivial members.
- This task opts in to creating the brand table and archive flag.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the reference-count interface and the generic list
component are designed (signatures and one worked example), stop and show
them. Resume only on "Continue T-1.5.1".

Steps: 1. reference-count interface and product implementation; 2. Brand
entity with archive flag; 3. list page with switcher (Brands only for now);
4. delete guard in a transaction; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-1.5.1-S; do not design
your own.

Definition of done: a Head Office User adds, edits, deletes an unused Brand
or archives one in use, with its usage shown and archived brands hidden
behind a toggle.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Delete refused when a reference appears between page load and confirm
- [ ] The list component is reusable without copying (show how T-1.5.2 will
      plug in)
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Product Profiles, Attribute names, suppliers (T-1.5.2),
Restriction Groups (T-1.5.3), assigning brands to products (T-1.6.1),
specialist assignments (E20).
```

**Checkpoint**

Produces before pausing — the reference-count interface signature, the generic list component's contract, and one worked example (Brand used by products).
Human reviews — Will every future area be able to report its references to a list item through this interface, so Delete can never break history?
Resume trigger — `Continue T-1.5.1`

---

### T-1.5.2 — Apply archive-not-delete to Product Profiles, Attribute names and suppliers

**Parent story**

> As a Head Office User, I want to add, edit and retire Profiles, Attribute names, Brands, suppliers and Restriction Groups, with retirement archiving anything in use so that history never breaks.
>
> Acceptance criteria:
> - Each list offers Delete only when unused and Archive when used, with usage shown (S1, S2)
> - Archived items are hidden behind "Show archived (N)" and can be un-archived (S3, S4)

**Slice** — Product Profiles, Attribute names and suppliers each behave exactly as Brands do: Delete when unused, Archive with usage when used, archived hidden behind a toggle.
**Spec source** — Product Management US-008 S1–S4
**Depends on** — T-1.5.1
**Pattern to follow** — T-1.5.1 (list component and reference-count interface)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — pure pattern repetition over a reviewed component; Blast Radius Low.

**Provisional commit message**

```
feat(catalogue): archive-not-delete for profiles, attributes and suppliers

- Brings the remaining product reference lists under the same history-safe
  rule as brands, reusing one list component
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Straight repetition of a reviewed pattern.

**Agent prompt**

```
Role: You are extending the archive-not-delete reference list to Product
Profiles, Attribute names and suppliers in the Field Sales Management System.

Context:
- Slice: each of the three lists behaves exactly as Brands do.
- Specs: plan_docs/stories/product-management.md US-008 and glossary
  (Product Profile, Product Attribute, Supplier).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — reuse the list
  component and reference-count interface created in T-1.5.1.
- Pattern to follow: the Brands list from T-1.5.1, without copying code.

Acceptance criteria:
1. For each of Product Profiles, Attribute names and suppliers: an unused
   item offers Delete only; a used item offers Archive only, with its usage.
2. Archived items stay on existing products labelled "(archived)" and are
   not offered for new selections.
3. Each list hides archived items behind "Show archived (N)"; un-archive
   restores selection.
4. The H-17 list switcher offers all four lists.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-1.5.1's component and interface; do not fork them.
- No tests of framework internals or trivial members.
- This task opts in to creating the three reference tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. three entities with archive flags; 2. reference-count
implementations for products; 3. register lists in the switcher; 4. tests.

Test expectations: select scenarios yourself from the criteria, following
T-1.5.1's tests.

Definition of done: Product Profiles, Attribute names and suppliers each
behave exactly as Brands do.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated for all three lists
- [ ] No duplicated list logic
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Restriction Groups (T-1.5.3), setting these on a product
(T-1.6.1, T-1.6.2), Location and Contact Types (T-2.2.1).
```

---

### T-1.6.1-S — Test scenarios for product classification

**Owner** — Scenario Review
**Gates** — T-1.6.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for product classification (task T-1.6.1). Read
plan_docs/stories/product-management.md US-004 and glossary (Brand, Restriction
Group). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Cover S1, S3, S4 and S5, then derivable edges:
removing the primary brand while alternatives remain, the same brand as
primary and alternative, changing a product's restriction group, an archived
supplier. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-1.6.1 — Classify a product with profile, brands, supplier and restriction group

**Parent story**

> As a Head Office User, I want to classify a product and record its facts on one screen so that reps can answer customer questions and other areas can group products correctly.
>
> Acceptance criteria:
> - Setting Profile "Chilled", Primary Brand "SunCo", Alternative Brand "GlowCo", supplier "Irish Health Supplies", Restriction Group none saves and shows all (S1)
> - Adding an Alternative Brand with no Primary Brand shows "Choose a primary brand first" (S3)
> - Setting Restriction Group "Pharmacy-only medicines" saves it; hiding from reps is T-4.4.1 (S4)
> - An archived Profile "Frozen" is not offered, but a product already set to it shows "Frozen (archived)" (S5)

**Slice** — A Head Office User sets a product's profile, primary and alternative brands, supplier and restriction group in place on the record, with archived items labelled and not offered.
**Spec source** — Product Management US-004 S1, S3–S5; uxdocs 02 H-13 (H13.2)
**Depends on** — T-1.5.2, T-1.5.3
**Pattern to follow** — T-1.2.1 (record sections), T-1.5.1 (archived labelling)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — Taste Medium: H-13's section layout is a draft (H13.2); checkpoint on the layout; Blast Radius Low.

**Provisional commit message**

```
feat(catalogue): classify products by profile, brand, supplier and group

- Brands and supplier drive range building and specialist scopes later;
  the restriction group decides which reps may see the product at all
- Primary brand comes first so brand totals have one owner per product
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Screen composition on a drafted layout needs a review pause.

**Agent prompt**

```
Role: You are adding classification to the product record of the Field Sales
Management System's head office website.

Context:
- Slice: a Head Office User sets profile, primary and alternative brands,
  supplier and restriction group in place on the product record.
- Specs: plan_docs/stories/product-management.md US-004 S1, S3–S5;
  plan_docs/uxdocs/02-head-office.md H-13 (H13.2: one page of sections that
  edit in place, most-used first).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — extend the product
  record from T-1.2.1; reference lists from T-1.5.1–T-1.5.3.
- Pattern to follow: T-1.2.1's record sections; T-1.5.1's archived labels.

Acceptance criteria:
1. Profile "Chilled", Primary Brand "SunCo", Alternative Brand "GlowCo",
   supplier "Irish Health Supplies", Restriction Group none saves and shows.
2. Adding an Alternative Brand with no Primary Brand shows "Choose a primary
   brand first".
3. A product may hold at most one Restriction Group; setting "Pharmacy-only
   medicines" saves it.
4. Archived items (e.g. Profile "Frozen") are not offered, and a product
   already set to one shows "Frozen (archived)".
5. Brand, profile, supplier and group usage now appears in each list's
   "Used by …" count through T-1.5.1's interface.

Constraints:
- Use the project's existing conventions and test framework.
- A product has one Product Profile, one optional Primary Brand, any number
  of Alternative Brands, one optional supplier, at most one Restriction
  Group.
- Do not implement hiding restricted products from reps (T-4.4.1).
- No tests of framework internals or trivial members.
- This task opts in to adding classification columns and a
  product–alternative-brand link table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the record's section layout renders with sample data,
stop and show it against H-13. Resume only on "Continue T-1.6.1".

Steps: 1. classification fields on Product; 2. persistence; 3. usage
reporting to the lists; 4. edit-in-place sections; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-1.6.1-S; do not design
your own.

Definition of done: a Head Office User sets a product's profile, primary and
alternative brands, supplier and restriction group in place on the record,
with archived items labelled and not offered.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Layout matches the confirmed H-13
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: attributes (T-1.6.2), restricted-product hiding (T-4.4.1),
availability (E8), replacements (E10), quantity breaks (T-6.3.1).
```

**Checkpoint**

Produces before pausing — the product record rendered with all classification sections filled from sample data.
Human reviews — Does the section order and edit-in-place behaviour match the confirmed H-13, with the most-used sections first?
Resume trigger — `Continue T-1.6.1`

---

### T-1.7.1 — Find products by code, name, brand or supplier

**Parent story**

> As a Head Office User, I want to search products by code, name, brand or supplier and see each with its breadcrumb and status so that I find the right record among near-duplicates.
>
> Acceptance criteria:
> - Typing "SPF30" lists every match with code, breadcrumb, Primary Brand, availability state and current price (S1)
> - Filtering Category "Suncare" lists all 180 products beneath it, each with its own breadcrumb (S2)
> - Ticking "Include unavailable and archived-category products" adds Unavailable products, labelled (S3; archived-category products arrive with E11)

**Slice** — A Head Office User searches products by code, name, brand or supplier, or filters by a category and everything beneath it, and each result shows code, breadcrumb, primary brand, state and today's price.
**Spec source** — Product Management US-010 S1–S3; uxdocs 02 H-12 (H12.1–H12.3)
**Depends on** — T-1.6.1
**Pattern to follow** — T-1.1.2 (breadcrumb search)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: M | Confidence: M
  (inferred) — specified end to end, Blast Radius Low; Taste Medium (H-12 draft) — if H-12 is unconfirmed when this starts (MI-08), treat it as Agent-Assisted.

**Provisional commit message**

```
feat(catalogue): search products with breadcrumb, state and price

- Near-duplicate names are told apart by category path and state, not by
  opening each record
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
A list view over established models.

**Agent prompt**

```
Role: You are building product search (H-12) for the Field Sales Management
System's head office website.

Context:
- Slice: search products by code, name, brand or supplier, or filter by a
  category and everything beneath it; each row shows code, breadcrumb,
  primary brand, availability state and today's base price.
- Specs: plan_docs/stories/product-management.md US-010;
  plan_docs/uxdocs/02-head-office.md H-12 (H12.1 two lines per product;
  H12.2 price shown is today's Base Price; H12.3 "+ Product" opens the
  minimum create form).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}}.
- Pattern to follow: T-1.1.2's breadcrumb search.

Acceptance criteria:
1. Typing "SPF30" lists every matching product with code, breadcrumb,
   Primary Brand, availability state and current price.
2. Search matches code, name, any brand (primary or alternative) and
   supplier.
3. Filtering Category "Suncare" lists all products beneath it at any depth,
   each with its own breadcrumb.
4. By default Unavailable products are excluded; ticking "Include
   unavailable and archived-category products" includes them, labelled.
   (Availability states exist from E8; until then every product is Active.)
5. "+ Product" opens the minimum create form (T-1.2.1).

Constraints:
- Use the project's existing conventions and test framework.
- Today's price is the base price in effect today (T-1.3.1's function).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope (indexes are
  allowed if the project's conventions treat them as routine).

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. search query; 2. category-beneath filter; 3. results list per
H-12; 4. tests.

Test expectations: select scenarios yourself from the criteria, including a
name shared by products in two categories.

Definition of done: a Head Office User searches products by code, name,
brand or supplier, or filters by a category and everything beneath it, and
each result shows code, breadcrumb, primary brand, state and today's price.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Price shown is today's, not a future-dated price
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: archived categories (E11), availability changes (E8), product
editing (T-1.6.1).
```

---

### T-1.5.3-S — Test scenarios for Restriction Group archiving

**Owner** — Human-Led
**Gates** — T-1.5.3
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- For a rep with permission, a rep without, and a new rep, what does each see for a product in an archived group?
- What happens to an In Progress order line for such a product (valid when captured)?
- After un-archiving, does each rep's visibility return exactly as before?

---

### T-1.5.3 — Maintain Restriction Groups, archiving without effect

**Parent story**

> As a Head Office User, I want to add, edit and retire Profiles, Attribute names, Brands, suppliers and Restriction Groups, with retirement archiving anything in use so that history never breaks.
>
> Acceptance criteria:
> - Restriction Groups follow Delete-when-unused and Archive-when-used (S1–S4)
> - Archiving "High-value equipment", which has 3 reps with permission, shows "3 permissions will be kept but have no effect while archived" (S5)

**Slice** — A Head Office User maintains Restriction Groups as an archive-not-delete list, and archiving a group with permissions says how many are kept without effect.
**Spec source** — Product Management US-008 S5; Coverage Management glossary (Restriction Group)
**Depends on** — T-1.5.1 (the permission count in S5 becomes non-zero once T-3.4.1 reports permissions through the reference-count interface)
**Pattern to follow** — T-1.5.1
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: S | Confidence: L
  (inferred) — authorization: what "no effect while archived" means for who can see the group's products is unstated (MI-45), so Oracle Ambiguity is High.

**Provisional commit message**

```
feat(catalogue): archive restriction groups without losing permissions

- Permissions are kept so un-archiving restores who was qualified, rather
  than retraining reps from a blank record
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Small, but it decides who may see restricted products.

**Work package**

Increments:
1. Restriction Group as a T-1.5.1 list with usage counted from products; permissions register as a usage source when T-3.4.1 lands.
2. Archive confirmation with the permission count, using the S5 wording.
3. The visibility rule for products in an archived group, once decided, applied in one place the snapshot builder (T-4.4.1) reads.

Decision points:
- When a group is archived, are its products visible to every rep, hidden from every rep, or treated as unrestricted? (MI-45) Pharmacy-only medicines must not become visible by accident.
- Can a product be assigned to an archived group? The pattern says no; confirm.
- Does un-archiving need a confirmation that permissions become effective again?

Delegable slivers:
- **List registration** — Register Restriction Groups in the T-1.5.1 list switcher with product and permission usage counts, reusing the existing component. Do not implement any visibility logic.
- **Archive message** — Add the S5 message "N permissions will be kept but have no effect while archived" to the archive confirmation, counting permissions through the existing interface. Change nothing else.

---

### T-1.6.2 — Record product attributes and carry them to the tablet

**Parent story**

> As a Head Office User, I want to classify a product and record its facts on one screen so that reps can answer customer questions and other areas can group products correctly.
>
> Acceptance criteria:
> - Adding Weight 200 g, Shelf life 24 months and Barcode 5391234567890 shows name/value rows and includes them in the Morning Snapshot for reps (S2)

**Slice** — A Head Office User adds name/value attributes to a product, and after the next sync a rep sees them on that product on the tablet.
**Spec source** — Product Management US-004 S2; Product Management Requires Clarification 5 ("Attributes visible on the product")
**Depends on** — T-1.6.1, T-4.1.1
**Pattern to follow** — T-1.6.1 (record sections); T-4.1.1 (snapshot contents)
**Ownership** — Impl: Agent-Assisted | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — changes the snapshot contract, a shared surface owned by the sync work; the tests themselves are mechanical.

**Provisional commit message**

```
feat(catalogue): record product attributes and show them on the tablet

- Reps answer shelf-life and size questions at the counter from the
  offline snapshot instead of phoning the office
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Small, but it touches the snapshot contract.

**Agent prompt**

```
Role: You are adding product attributes, and their display on the rep's
tablet, to the Field Sales Management System.

Context:
- Slice: a Head Office User adds name/value attributes to a product; after
  the next sync a rep sees them on that product on the tablet.
- Specs: plan_docs/stories/product-management.md US-004 S2 and glossary
  (Product Attribute: names maintained by head office, values per product);
  plan_docs/uxdocs/01-tablet-day.md T-07 (product rows).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — attribute names list
  from T-1.5.2; snapshot builder from T-4.1.1.
- Pattern to follow: T-1.6.1's record sections; T-4.1.1's snapshot fields.

Acceptance criteria:
1. On a product, adding Weight 200 g, Shelf life 24 months, Barcode
   5391234567890 shows them as name/value rows.
2. Only non-archived attribute names are offered; existing values under an
   archived name stay, labelled.
3. After the rep's next sync the attributes are in the snapshot and visible
   on the tablet when the rep opens the product's detail.
4. Restricted products' attributes are never in a snapshot that excludes the
   product (T-4.4.1's rule).

Constraints:
- Use the project's existing conventions and test framework.
- The snapshot contract is shared: add fields without changing existing ones
  and bump its version as T-4.1.1 defines.
- No tests of framework internals or trivial members.
- This task opts in to an attribute-value table.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the snapshot contract change is written (field names,
version), stop and show it. Resume only on "Continue T-1.6.2".

Steps: 1. attribute values on Product; 2. record section; 3. snapshot field;
4. tablet product detail display; 5. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a Head Office User adds name/value attributes to a
product, and after the next sync a rep sees them on that product on the
tablet.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Snapshot version bumped; older fields untouched
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: attribute-based search or filters, Phase 2 variants, other
snapshot changes.
```

**Checkpoint**

Produces before pausing — the snapshot contract diff (new fields and version).
Human reviews — Is this change compatible with tablets still holding the previous snapshot version, and within the snapshot size budget (MI-02)?
Resume trigger — `Continue T-1.6.2`

---

### T-1.3.2-S — Test scenarios for future-dated prices on the tablet

**Owner** — Human-Led
**Gates** — T-1.3.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Which time zone defines "the day" — Europe/Dublin (MI-53)? What if the tablet's clock is wrong?
- A line added at 23:58 on 31 October and edited at 00:02 on 1 November: which price, and does editing the quantity re-resolve it?
- An In Progress order started before the change and finished after it: which price do new lines get, and do old lines change?

---

### T-1.3.2 — Apply a future-dated price on the tablet on its day

**Parent story**

> As a Head Office User, I want to add a new price with the date it applies from, keeping every previous price, so that old orders keep their price and a rise takes effect on the right day.
>
> Acceptance criteria:
> - A rep who syncs on 31 October sees €12.50 on 31 October and €13.20 on 1 November (S1, tablet part)
> - An order line captured on 28 October at €12.50 still shows €12.50 after 1 November (S2)

**Slice** — A rep who synced before a price rise quotes the new price on the day it takes effect without syncing again, and lines already captured keep their price.
**Spec source** — Product Management US-002 S1 (tablet), S2; Rep at a Location glossary (Resolved Price)
**Depends on** — T-1.3.1, T-6.4.1, T-4.1.1
**Pattern to follow** — T-6.4.1 (price engine input: base price on a date)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — Edge-Case Discovery High: the day boundary, the tablet clock and time zone (MI-53) need a human oracle; the build itself is modest.

**Provisional commit message**

```
feat(pricing): switch to future-dated base prices on their day offline

- A rep quoting yesterday's price on the day of a rise is an avoidable
  conversation, so future prices travel in the snapshot
- Captured lines keep their price; only new resolutions use the new date
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Crosses the snapshot and the price engine.

**Agent prompt**

```
Role: You are making future-dated base prices take effect on the tablet on
their day, offline, in the Field Sales Management System.

Context:
- Slice: a rep who synced before a price rise quotes the new price on the
  day it takes effect without syncing again; captured lines keep their price.
- Specs: plan_docs/stories/product-management.md US-002 S1, S2 and design
  decision "Dated Base Price, routine edits, future prices reach the tablet";
  plan_docs/stories/rep-at-a-location-tablet.md glossary (Resolved Price,
  Valid when captured).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — snapshot builder
  (T-4.1.1), price engine (T-6.4.1), price history (T-1.3.1).
- Pattern to follow: T-6.4.1's "base price on a date" input.

Acceptance criteria:
1. The snapshot carries each product's current base price and every
   future-dated price with its Effective From date.
2. A tablet synced on 31 October resolves €12.50 on 31 October and €13.20
   on 1 November without another sync.
3. An order line captured on 28 October at €12.50 still shows €12.50 after
   1 November, on the tablet and on the server.
4. The day used is the agreed business day and time zone (see the agreed
   scenario list); the tablet's clock drift is handled as that list says.

Constraints:
- Use the project's existing conventions and test framework.
- The snapshot contract is shared: add fields, bump the version as T-4.1.1
  defines, change nothing else.
- Captured prices on lines are never recomputed.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the snapshot field and the engine's date input are wired,
stop and show a run of the agreed day-boundary scenarios. Resume only on
"Continue T-1.3.2".

Steps: 1. snapshot field for future prices; 2. engine reads base price for
"today" from the snapshot; 3. confirm captured lines are untouched; 4. tests.

Test expectations: implement exactly the scenarios agreed in T-1.3.2-S. You
are forbidden from designing your own test cases.

Definition of done: a rep who synced before a price rise quotes the new
price on the day it takes effect without syncing again, and lines already
captured keep their price.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–4 demonstrated
- [ ] Every agreed scenario passes
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: tier and break prices in the snapshot (T-6.4.2), price
display (T-6.4.3), head office price editing (T-1.3.1).
```

**Checkpoint**

Produces before pausing — the snapshot field for future prices and a passing run of the agreed day-boundary scenarios.
Human reviews — Does the tablet switch price at exactly the agreed moment, including a tablet left open over midnight?
Resume trigger — `Continue T-1.3.2`

---

### T-1.4.2-S — Test scenarios for changing a product's unit

**Owner** — Scenario Review
**Gates** — T-1.4.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for changing the unit of a product that has
orders (task T-1.4.2). Read plan_docs/stories/product-management.md US-003 S5.
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover S5 and derivable edges: In Progress orders on tablets
(which the server can't count — note it), stock counts in the old unit,
quantity breaks set in the old unit, changing back. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-1.4.2 — Warn when changing the unit of a product that has orders

**Parent story**

> As a Head Office User, I want to mark a product as sold by a measure with a step and minimum so that reps order sensible quantities and the tablet shows the right unit.
>
> Acceptance criteria:
> - Changing Unit to kg on a product with orders as Each shows "12 orders reference this product as Each — they are unchanged" and applies to new orders only (S5)

**Slice** — Changing the unit of a product that already has orders states how many orders keep the old unit, and only new orders use the new one.
**Spec source** — Product Management US-003 S5; uxdocs 02 H-13 (H13.4)
**Depends on** — T-1.4.1, T-7.1.1
**Pattern to follow** — T-1.4.1; T-1.5.1 (reference counts)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies T-1.4.1's unit behaviour, so characterisation comes first; Blast Radius Medium (order lines keep their unit).

**Provisional commit message**

```
feat(catalogue): keep existing orders' unit when a product's unit changes

- An order captured as 12 Each must never be read as 12 kg later, so each
  line keeps the unit it was captured in
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies established behaviour behind a characterisation pass.

**Agent prompt**

```
Role: You are allowing a product's unit of measure to change after it has
orders, in the Field Sales Management System.

Context:
- Slice: changing the unit of a product that has orders states how many
  orders keep the old unit; only new orders use the new one.
- Specs: plan_docs/stories/product-management.md US-003 S5;
  plan_docs/uxdocs/02-head-office.md H-13 (H13.4: the warning is shown
  before saving).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — unit settings from
  T-1.4.1 (which currently blocks changes on products with orders), order
  lines from T-5.1.1.
- Pattern to follow: T-1.5.1's reference counting.

Acceptance criteria:
1. On a product with 12 orders captured as Each, choosing Unit kg shows "12
   orders reference this product as Each — they are unchanged" before save.
2. After saving, those 12 orders still read in Each; new lines use kg with
   the product's step and minimum.
3. The block added in T-1.4.1 on products with orders is removed.

Constraints:
- Use the project's existing conventions and test framework.
- Order lines must carry their own unit (confirm T-5.1.1 stores it; if not,
  stop and report — do not migrate data in this task).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-1.4.1's current unit behaviour and how order lines store units; stop and
show them passing. Resume only on "Continue T-1.4.2".

Steps: 1. characterisation tests; 2. order count by unit; 3. pre-save
warning; 4. lift the block; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-1.4.2-S; do not design
your own.

Definition of done: changing the unit of a product that already has orders
states how many orders keep the old unit, and only new orders use the new
one.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after the change
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: quantity breaks on the changed unit (T-6.3.1), stock counts
recorded in the old unit (report them, do not convert).
```

**Checkpoint**

Produces before pausing — characterisation tests of T-1.4.1's unit rules and of the unit stored on order lines, passing.
Human reviews — Do order lines really carry their own unit, so no captured order can be reinterpreted?
Resume trigger — `Continue T-1.4.2`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | 9 stories; new Sign-in US-001 (T-1.0.1) and US-002 (T-1.0.1, T-1.0.2) precede the 7 catalogue stories listed above |
| Every task satisfies the three slice criteria | Pass | 15 of 15 have a shippable, testable staff or catalogue outcome |
| Every task carries a tier with a rationale citing dimensions | Pass | 15 of 15 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-1.0.1 (MI-01, MI-03, MI-51), T-1.1.1 (MI-01, MI-51), T-1.5.3 (MI-45), T-1.3.2 (MI-53) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-1.0.2 starts by characterising T-1.0.1; T-1.4.2's checkpoint is the characterisation pass |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | Autonomous: T-1.1.2, T-1.5.2, T-1.7.1 — all Medium or Low, with a stated fallback if their drafts stay unconfirmed |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-1.0.1, T-1.0.2, T-1.1.1, T-1.5.3 and T-1.3.2 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-1.2.1, T-1.4.1, T-1.5.1, T-1.6.1, T-1.6.2, T-1.3.2, T-1.4.2 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-1.0.1, T-1.0.2, T-1.1.1, T-1.3.1, T-1.5.3 |
| Every scenario task precedes the task it gates | Pass | 12 `-S` sections, including the two sign-in gates |
| Every agent prompt is self-contained | Pass | Each names its spec files, criteria, constraints, gates and out-of-scope tasks |
| No concrete model name in any task section | Pass | Capability classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-01, 03, 07, 08, 13, 45, 51, 53 in the index |
