# E26 — Ordering online

**Iteration** — 8, Customer self-service
**Outcome** — A signed-in contact picks the shop they're ordering for, lands on the products they usually buy, finds anything else they're allowed to buy, places an order at their own prices with offers explained, can change it until the cut-off, follows what's been sent, repeats past orders, and — at a chain's head office — orders for every branch in one go.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Self-service US-003 — See and choose my Locations | Must | S1–S7 |
| 2 | Self-service US-004 — Find products | Must | S1–S10, AC-SS004-A–D |
| 3 | Self-service US-005 — Place an order for one Location | Must | S1–S6 (S1's "on receipt" read as "at the cut-off") |
| 4 | Self-service US-007 — Request a change after placing an order | Must | S1–S7 |
| 5 | Self-service US-008 — See order history and what has been sent | Should | S1–S6 |
| 6 | Self-service US-009 — Repeat a past order | Should | S1–S9 |
| 7 | Self-service US-006 — Order for several branches at once | Should | S1–S20 (S2 as refined by the grid's "All" rule) |

Also carried here, with no story: head office assigning Ranges to a customer for the curated catalogue (`{{NEEDS ACCEPTANCE CRITERIA}}`, MI-17).

**Exit criterion** — A Customer User chooses a shop within their Ordering Scope on every visit; a chain buyer's primary action is the chain order. They land on their usual products, with starting-point regulars for new users and "ordered by someone else" marks. They browse their curated catalogue or all products, and one search puts curated matches first with other permitted matches below; Restricted products never show, and availability is explained with replacements. An order for one shop shows only the customer's price, break prompts and the Offer Summary, and is Pending until the cut-off. Until then the customer can edit or cancel it, with a warning as the lock nears; afterwards it is read-only with the company's phone and email. History shows every order for their shops with despatch detail, grouping a chain submission. Repeat adds a whole or selected past order to the current one at today's prices. A head office buyer orders for every branch on a grid (or product by product on a phone), with ranges from a dropdown and a personal default.

**Capability-class stamp** — Frontier + extended reasoning for ordering scope (T-26.1.1); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [self-service.md](../../stories/self-service.md) (US-003–US-009, glossary, design decisions), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (US-NEW-007, AC-SS004-A–D, AC-SS006-A–O, BR-NEW-008, BR-NEW-009), [06-customer.md](../../uxdocs/06-customer.md) (C-02–C-07, C-09).

---

### T-26.1.1-S — Test scenarios for ordering scope and choosing a shop

**Owner** — Human-Led
**Gates** — T-26.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Scope = the Contact's linked Locations plus the branches of any Master Location among them. Linked to Rathdrum and Arklow: must choose (S1); at Hickey's Head Office with 12 branches: head office or any branch, multi-branch offered (S2); one Location: selected automatically (S3).
- Closed branches not offered; Temporarily Closed offered with "Closed until 14 Oct" (S4).
- Opening a Location outside scope by any route (URL, API, stale page) is prevented (S5), including orders, history and repeat.
- Chosen on every visit, never remembered (S6; C2.1); a head office buyer's primary action is "Order for several branches" (S7; C2.2).
- A branch added to the chain silently widens scope (design decision); a branch removed mid-session; the contact unlinked from a Location with an order in progress.
- A Location that is a Prospect: no Customer Users (assumption).

---

### T-26.1.1 — Limit each customer to their shops and ask which one on every visit

**Parent story**

> As a Customer User, I want to see the shops I can order for and pick one so that I order for the right place.
>
> Acceptance criteria:
> - Linked to Hickey's Rathdrum and Hickey's Arklow, starting an order asks which Location it is for (S1)
> - At Hickey's Head Office with 12 branches, I can order for the head office or any branch, and the multi-branch option is offered (S2)
> - Linked to one Location, it's selected automatically (S3)
> - A Closed branch isn't offered; a Temporarily Closed one shows "Closed until 14 Oct" (S4)
> - Opening a Location I'm not linked to is prevented (S5)
> - The choice is asked on every visit with nothing preselected (S6)
> - A head office buyer's primary action is "Order for several branches", with the head office and branches listed below, none preselected (S7)

**Slice** — Every customer request is limited to the Ordering Scope — the contact's Locations plus a master's branches — and after sign-in the customer picks the shop each time (or goes straight in with one shop, or starts a chain order as a head office buyer).
**Spec source** — Self-service US-003 S1–S7; glossary (Ordering Scope); design decision "Scope follows the Contact, and a master carries its branches"; uxdocs 06 C-02 (C2.1, C2.2)
**Depends on** — T-25.1.1, T-2.4.1, T-12.2.1, T-17.1.1, T-17.2.1
**Pattern to follow** — T-4.4.1 (enforcing a limit on the server, not only in the view)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — authorisation for outsiders across every customer endpoint (Blast Radius High).

**Provisional commit message**

```
feat(self-service): limit customers to their shops and ask which one

- A head office buyer would otherwise be linked to every branch by hand,
  so a master carries its branches
- The shop is asked every visit, because people with several shops are
  usually managers moving between them
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Authorisation that every customer screen relies on.

**Work package**

Increments:
1. Scope resolver: the Contact's linked Locations plus branches of any linked master; Closed excluded; server-side, applied to every customer request.
2. Enforcement on every customer endpoint (orders, history, repeat, catalogue prices) with tests that try out-of-scope access.
3. C-02: shop choice each visit; one shop goes straight to C-09; head office buyer's primary "Order for several branches".
4. Scope changes (branch added or closed, contact unlinked) take effect at the next request.

Decision points:
- The contact unlinked with an order in progress.
- Whether scope is shown on an account page (design decision says visible on the account screen).

Delegable slivers:
- **C-02 screen** — Given the resolved scope, render the location choice per C2.1 and C2.2 (no preselection; chain order primary for a head office buyer; "Closed until 14 Oct" flags). No scope logic.
- **Out-of-scope tests** — For each customer endpoint, add a test that a Location outside scope is refused. No production code.

---

### T-26.2.1-S — Test scenarios for assigning Ranges to a customer

**Owner** — Human-Led
**Gates** — T-26.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-17: where head office assigns Ranges to a Customer (H-25?), and whether it's the same mechanism as reps (T-9.8.1) and chains (T-23.1.1).
- Per Customer or per Location? A chain's branches: the chain's Agreed Ranges, the customer's own, or both?
- A customer with no Ranges: curated catalogue is unranged products only?
- Archived Ranges (MI-18).

---

### T-26.2.1 — Assign Ranges to a customer for their curated catalogue

**Parent story**

> As a Customer User, I want to browse what I normally buy and be able to look further when I need to so that ordering is quick but nothing is out of reach.
>
> Acceptance criteria:
> - With Customer Ranges "Core Stock" and "Summer 2027", category browsing shows those Ranges' products plus unranged products (S1)
> - {{NEEDS ACCEPTANCE CRITERIA}} for head office assigning Ranges to a Customer (MI-17)

**Slice** — Head office assigns catalogue Ranges to a customer, with who and when recorded, and those assignments define the customer's curated catalogue.
**Spec source** — Self-service US-004 S1, glossary (Curated catalogue); design decision "Assigned Ranges curate; they do not restrict"; uxdocs 02 H-25
**Depends on** — T-23.1.1, T-9.8.1
**Pattern to follow** — T-23.1.1 (assigning Ranges to a chain)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: L
  (inferred) — no story defines customer assignment (MI-17), so Oracle Ambiguity is High; the mechanism exists.

**Provisional commit message**

```
feat(self-service): assign ranges to a customer for their catalogue

- Ranges curate what a customer sees first; they never limit what the
  customer may buy
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A missing story settled first, then reuse.

**Agent prompt**

```
Role: You are adding customer Range assignment to the head office website of
the Field Sales Management System.

Context:
- Slice: head office assigns catalogue Ranges to a customer, with who and
  when recorded, and those assignments define the customer's curated
  catalogue.
- Specs: plan_docs/stories/self-service.md US-004 S1, glossary (Curated
  catalogue), design decision "Assigned Ranges curate; they do not
  restrict"; plan_docs/uxdocs/02-head-office.md H-25. The acceptance
  criteria for assignment are agreed in T-26.2.1-S (MI-17); do not start
  until they exist.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — chain assignment
  (T-23.1.1), rep assignment (T-9.8.1).
- Pattern to follow: T-23.1.1.

Acceptance criteria:
1. Head office can assign and remove Active Ranges for a customer, per the
   agreed criteria, recording who and when.
2. The curated catalogue for a customer is their assigned Ranges' products
   plus unranged products.
3. Assignment never changes what the customer is permitted to buy.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse the existing assignment mechanism where the scenarios agree.
- No tests of framework internals or trivial members.
- This task opts in to customer range assignment storage if a new one is
  agreed.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after assignment passes every agreed scenario, stop and show it.
Resume only on "Continue T-26.2.1".

Steps: 1. assignment; 2. record page; 3. curated catalogue query; 4. tests.

Test expectations: implement exactly the scenarios agreed in T-26.2.1-S. You
are forbidden from designing your own test cases.

Definition of done: head office assigns catalogue Ranges to a customer, with
who and when recorded, and those assignments define the customer's curated
catalogue.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the catalogue screens (T-26.2.2).
```

**Checkpoint**

Produces before pausing — assignment passing every agreed scenario.
Human reviews — Is this the agreed answer to MI-17, consistent with chains and reps?
Resume trigger — `Continue T-26.2.1`

---

### T-26.2.2-S — Test scenarios for catalogue and search

**Owner** — Scenario Review
**Gates** — T-26.2.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the customer catalogue and search (task
T-26.2.2). Read plan_docs/stories/self-service.md US-004 S1–S6 and the
glossary (Curated catalogue, Product search, Browse scope control,
Temporarily Unavailable, Live figures), AC-SS004-A–D in
plan_docs/uxdocs/04-user-stories-amendments.md, and
plan_docs/uxdocs/06-customer.md C-03 (C3.1–C3.3). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: a category that exists only
in "All products", a search with no curated matches, an Unavailable product
with no replacement, wording that must not say "as of sync". Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-26.2.2 — Browse the curated catalogue or all products, and search both at once

**Parent story**

> As a Customer User, I want to browse what I normally buy and be able to look further when I need to so that ordering is quick but nothing is out of reach.
>
> Acceptance criteria:
> - Browsing shows my Ranges' products plus unranged ones; "All products" widens browsing on the same page and "Your catalogue" returns; search puts curated matches first (S1)
> - Searching "lozenge" shows curated matches first and other permitted products underneath, with no second step (S2)
> - A Restricted product never appears in browsing or search (S3)
> - Temporarily Unavailable, Expected Back 25 October 2026: "Back in stock around 25 October", can't be added (S4)
> - Discontinuing with a replacement: orderable, "Being discontinued — replaced by SPF30 Sun Lotion v2 200ml"; Run-out: "Limited stock, no restock — about 120 left" (S5)
> - Unavailable with replacements: shown unavailable with its replacements offered (S6)

**Slice** — On the customer site, category browsing starts on the curated catalogue with a same-page switch to all permitted products, one search returns curated matches then other permitted ones, Restricted products never appear, and each product shows the customer's price and its availability in plain, current words, with replacements where relevant.
**Spec source** — Self-service US-004 S1–S6, AC-SS004-A–D; design decisions "Assigned Ranges curate; they do not restrict", "Nothing is hedged", "Availability is explained"; uxdocs 06 C-03 (C3.1–C3.3)
**Depends on** — T-26.1.1, T-26.2.1, T-1.1.2, T-1.7.1, T-4.4.1, T-8.1.1, T-10.1.1, T-10.2.1, T-10.3.1, T-6.4.1
**Pattern to follow** — T-1.7.1 (product search), T-8.3.1 (availability labels, reworded for a live channel)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — reuses reviewed search, availability and pricing; C-03 is settled.

**Provisional commit message**

```
feat(self-service): browse the curated catalogue and search everything permitted

- Ranges still guide browsing and result order, but one search finds any
  permitted product without a second step
- Figures are live here, so nothing is hedged "as of sync"
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A settled screen over reviewed rules.

**Agent prompt**

```
Role: You are building the customer catalogue and search (C-03) for the Field
Sales Management System's customer site.

Context:
- Slice: on the customer site, category browsing starts on the curated
  catalogue with a same-page switch to all permitted products, one search
  returns curated matches then other permitted ones, Restricted products
  never appear, and each product shows the customer's price and its
  availability in plain, current words, with replacements where relevant.
- Specs: plan_docs/stories/self-service.md US-004 S1–S6 and glossary;
  plan_docs/uxdocs/04-user-stories-amendments.md AC-SS004-A–D;
  plan_docs/uxdocs/06-customer.md C-03 (C3.1–C3.3).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — scope (T-26.1.1),
  curated catalogue (T-26.2.1), categories (T-1.1.2), search (T-1.7.1),
  restricted products (T-4.4.1), availability (T-8.1.1, T-10.2.1, T-10.3.1),
  replacements (T-10.1.1), price engine (T-6.4.1).
- Pattern to follow: T-1.7.1 and T-8.3.1.

Acceptance criteria:
1. Browsing starts on "Your catalogue" (assigned Ranges plus unranged);
   "All products" widens it in place, keeping the selected category where it
   exists; "Your catalogue" returns.
2. Search shows curated matches first and other permitted matches below,
   from one query.
3. Restricted products never appear in browsing or search.
4. Temporarily Unavailable: "Back in stock around 25 October", not
   addable.
5. Discontinuing: "Being discontinued — replaced by SPF30 Sun Lotion v2
   200ml", addable; Run-out: "Limited stock, no restock — about 120 left".
6. Unavailable: shown as unavailable with replacements offered.
7. Each product shows the customer's price only, with no tier name, list
   price or breakdown, and no "as of sync" wording.

Constraints:
- Use the project's existing conventions and test framework.
- Permission, availability and price come from the reviewed rules; none are
  re-implemented here.
- No "ordered by someone else" mark on these rows (C9.9).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after browse, switch and search work against seeded data, stop
and show them. Resume only on "Continue T-26.2.2".

Steps: 1. browse; 2. scope switch; 3. grouped search; 4. labels and
replacements; 5. prices; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-26.2.2-S; do not design
your own.

Definition of done: on the customer site, category browsing starts on the
curated catalogue with a same-page switch to all permitted products, one
search returns curated matches then other permitted ones, Restricted
products never appear, and each product shows the customer's price and its
availability in plain, current words, with replacements where relevant.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: adding to an order (T-26.4.1), usual products (T-26.3.1).
```

**Checkpoint**

Produces before pausing — browse, switch and search against seeded data.
Human reviews — Could a customer ever see a Restricted product or a rep-only price detail?
Resume trigger — `Continue T-26.2.2`

---

### T-26.4.1-S — Test scenarios for placing an order for one shop

**Owner** — Scenario Review
**Gates** — T-26.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for building and placing a customer order for
one shop (task T-26.4.1). Read plan_docs/stories/self-service.md US-005
S1–S5 (read "accepted automatically on receipt" as "at the next cut-off",
per the BR-NEW-009 amendment in its bounded context), the glossary (Live
figures), and plan_docs/uxdocs/06-customer.md C-04. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover the story scenarios, then derivable edges: a product becoming
Unavailable between adding and placing, a promotion ending mid-order, two
users of one shop with orders in progress at once, attribution with no
Capturing Rep. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-26.4.1 — Build and place an order for one shop at the customer's price

**Parent story**

> As a Customer User, I want to build and submit an order for one of my shops so that I get stock without waiting for a visit.
>
> Acceptance criteria:
> - Reviewing 6 lines and choosing "Place order" confirms and submits it, with no Capturing Rep; it's accepted automatically at the next cut-off (S1, as amended by BR-NEW-009)
> - Each line shows the price I pay, with no tier name, list price or breakdown (S2)
> - A break at 10 with my line at 8: "2 more for €2.00 each — save €4.00 on 10"; the Offer Summary shows applied offers and what's within reach (S3)
> - A product per kg, step 0.5, minimum 1.0: the quantity control steps accordingly and shows the unit (S4)
> - Placing with no lines: "Add at least one product" (S5)

**Slice** — A customer adds products from the catalogue to the current order for their chosen shop, reviews it on its own page with their prices, break prompts and the Offer Summary, and places it; the order enters the same Pending-until-cut-off processing as any other, with no Capturing Rep.
**Spec source** — Self-service US-005 S1–S5 and the BR-NEW-009 amendment; glossary (Live figures); uxdocs 06 C-04
**Depends on** — T-26.2.2, T-6.4.1, T-6.5.1, T-18.3.1, T-5.1.3, T-7.1.1, T-7.4.1
**Pattern to follow** — T-5.1.1 (order capture), T-18.3.1 (Offer Summary)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: C | Confidence: M
  (inferred) — a new capture channel into the reviewed order pipeline; prices and offers reuse the engine.

**Provisional commit message**

```
feat(self-service): build and place an order for one shop

- Customer orders enter the same automatic processing as reps' orders;
  nothing about pricing, review or fulfilment changes
- The customer sees their price, never how it was reached
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new channel into the order pipeline, with a review pause.

**Agent prompt**

```
Role: You are building single-shop order entry (C-04) for the Field Sales
Management System's customer site.

Context:
- Slice: a customer adds products from the catalogue to the current order for
  their chosen shop, reviews it on its own page with their prices, break
  prompts and the Offer Summary, and places it; the order enters the same
  Pending-until-cut-off processing as any other, with no Capturing Rep.
- Specs: plan_docs/stories/self-service.md US-005 S1–S5 and the BR-NEW-009
  amendment in its bounded context; plan_docs/uxdocs/06-customer.md C-04.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — catalogue (T-26.2.2),
  price engine (T-6.4.1), break prompts (T-6.5.1), Offer Summary (T-18.3.1),
  measure-based quantities (T-5.1.3), cut-off acceptance (T-7.1.1),
  annotations (T-7.4.1).
- Pattern to follow: T-5.1.1's order model.

Acceptance criteria:
1. Adding from the catalogue puts lines in the shop's one current unplaced
   order.
2. The order page shows each line at the customer's price with no tier
   name, list price or breakdown.
3. A break at 10 with the line at 8 reads "2 more for €2.00 each — save €4.00
   on 10"; the Offer Summary shows applied and within-reach offers.
4. A per-kg product steps by 0.5 from a minimum of 1.0 and shows its unit.
5. Placing with no lines shows "Add at least one product".
6. "Place order" submits it with no Capturing Rep; it is Pending until the
   cut-off and then accepted with the usual dispositions.
7. No rep discount or free-of-charge action exists for customers.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse the reviewed order model and pipeline; no customer-only branch in
  acceptance.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after a placed customer order is Pending and then accepted at a
simulated cut-off, stop and show it. Resume only on "Continue T-26.4.1".

Steps: 1. current order per shop; 2. add from catalogue; 3. C-04 review;
4. prompts and offers; 5. place; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-26.4.1-S; do not design
your own.

Definition of done: a customer adds products from the catalogue to the
current order for their chosen shop, reviews it on its own page with their
prices, break prompts and the Offer Summary, and places it; the order enters
the same Pending-until-cut-off processing as any other, with no Capturing
Rep.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: editing after placing (T-26.4.2), usual products (T-26.3.1),
chain orders (T-26.6.1).
```

**Checkpoint**

Produces before pausing — a placed customer order Pending, then accepted at a simulated cut-off.
Human reviews — Does a customer order look exactly like a rep's order to everything downstream?
Resume trigger — `Continue T-26.4.1`

---

### T-26.3.1-S — Test scenarios for the usual products page

**Owner** — Human-Led
**Gates** — T-26.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "Ordered at least twice in the last six months" on my own orders for this shop (S7; C9.1): two lines on one order count once or twice? Cancelled orders?
- Fewer than 3 orders: "Often ordered for Hickey's Rathdrum" from any order for the Location, removed entirely after my 3rd (S8; C9.4). Does a cancelled order count toward three?
- The mark: "Ordered 2 Oct by your rep · 6 on the way"; "Despatched 4 Oct · expected soon" until the company's expected-delivery period passes (S9; C9.3) — the period is a setting (MI-12). Partial despatch shows both.
- Wording names the source: "your rep", a colleague's name, or the chain's head office by name (C9.8).
- The mark appears only on this page (C9.9).
- Add opens an empty quantity popover; on Add it closes and the row reads "In order · 12"; a bar shows the line count and "View order" (US-005 S6; C9.5, C9.6).
- No open-orders summary; history by a link (S10; C9.7).

---

### T-26.3.1 — Land on the person's usual products for the shop

**Parent story**

> As a Customer User, I want to browse what I normally buy and be able to look further when I need to so that ordering is quick but nothing is out of reach.
>
> Acceptance criteria:
> - Hand Cream ordered on two of my own orders in six months is under "Your usual products"; Arnica Gel ordered once is not; products ordered only by others never join (S7)
> - With fewer than 3 of my orders, "Often ordered for Hickey's Rathdrum" lists products ordered at least twice on any order for the Location; after my 3rd it's gone (S8)
> - The rep's undespatched 6 Sudocrem shows "Ordered 2 Oct by your rep · 6 on the way", still addable; once despatched, "Despatched 4 Oct · expected soon" until the expected-delivery period passes; only on this page (S9)
> - No open-orders summary; history is reached by a link (S10)
> - Add opens an empty quantity popover; on Add I stay on the page, the row reads "In order · 12", and a bar shows the line count with "View order" (Self-service US-005 S6)

**Slice** — After choosing a shop, the customer lands on the products they themselves order regularly there (with the shop's regulars as a starting point for new users), sees when someone else has already ordered one, and adds items through an empty quantity popover without leaving the page.
**Spec source** — Self-service US-004 S7–S10; US-005 S6; design decision "Land on the person's usual products"; uxdocs 06 C-09 (C9.1–C9.9)
**Depends on** — T-26.4.1, T-7.3.1
**Pattern to follow** — T-5.5.1 (quantity popover), T-5.4.1 (suggested list from order history)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — frequency and mark rules have several counting edges (Edge-Case Discovery High); the page is settled.

**Provisional commit message**

```
feat(self-service): land on the person's usual products

- Customers buy the same products but rarely the same order, so the page
  is built around what they order often, not around past orders
- A mark says when someone else has already ordered it, so nobody orders
  twice by accident
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Counting rules against a human oracle.

**Agent prompt**

```
Role: You are building the usual products landing page (C-09) for the Field
Sales Management System's customer site.

Context:
- Slice: after choosing a shop, the customer lands on the products they
  themselves order regularly there (with the shop's regulars as a starting
  point for new users), sees when someone else has already ordered one, and
  adds items through an empty quantity popover without leaving the page.
- Specs: plan_docs/stories/self-service.md US-004 S7–S10, US-005 S6 and
  design decision "Land on the person's usual products";
  plan_docs/uxdocs/06-customer.md C-09 (C9.1–C9.9).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — current order
  (T-26.4.1), despatches (T-7.3.1), popover pattern (T-5.5.1), order-history
  rules (T-5.4.1), settings (MI-12).
- Pattern to follow: T-5.5.1's popover.

Acceptance criteria:
1. "Your usual products": products on at least two of my own orders for this
   shop in the last six months, per the agreed counting rule.
2. "Often ordered for Hickey's Rathdrum": shown until my 3rd order, from any
   order for the Location, then removed entirely.
3. Marks: "Ordered 2 Oct by your rep · 6 on the way" before despatch;
   "Despatched 4 Oct · expected soon" after, until the expected-delivery
   setting passes; both for a partial despatch; the source named as "your
   rep", a colleague's name, or the chain's head office.
4. Adding stays allowed with no confirmation; marks appear on this page only.
5. Add opens an empty quantity popover; on Add it closes, the row reads "In
   order · 12", and a bar shows the line count and "View order".
6. No open-orders summary; a link to history.

Constraints:
- Use the project's existing conventions and test framework.
- No quantity is prefilled or hinted.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  expected-delivery setting.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the lists and marks pass every agreed scenario, stop and
show the page. Resume only on "Continue T-26.3.1".

Steps: 1. usual list; 2. starting-point section; 3. marks; 4. popover and
bar; 5. tests.

Test expectations: implement exactly the scenarios agreed in T-26.3.1-S. You
are forbidden from designing your own test cases.

Definition of done: after choosing a shop, the customer lands on the
products they themselves order regularly there (with the shop's regulars as
a starting point for new users), sees when someone else has already ordered
one, and adds items through an empty quantity popover without leaving the
page.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: history (T-26.5.1).
```

**Checkpoint**

Produces before pausing — lists and marks passing every agreed scenario, and the page.
Human reviews — Does the usual list stay stable from visit to visit, and is every mark's source right?
Resume trigger — `Continue T-26.3.1`

---

### T-26.4.2-S — Test scenarios for changing an order until the cut-off

**Owner** — Human-Led
**Gates** — T-26.4.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Placed 10:12, cut-off 4pm: "Pending", "You can change this until 4pm today", Edit and Cancel order; Edit 12 → 6 and Save changes keeps it Pending without placing again (S4).
- Cancel order asks once ("Cancel order #12345? This can't be undone.") and it shows Cancelled (S5).
- Editing at 3:55pm: "This order locks at 4pm — 5 minutes left to save"; saving at 4:01 is refused, the accepted order stands, and the contact route is shown (S6). Only while editing, not while viewing.
- Friday 2pm with a 1pm Friday cut-off and none at weekends: "You can change this until 4pm Monday" (S7; BR-NEW-009 rule 4).
- After the cut-off: read-only with reference, phone and email and "quote the reference" (S1, S2); someone else's order: view only (S3).
- Held by head office before the cut-off (H2.9): can the customer still edit? (A held order can't be edited by the rep or customer.)
- Each branch order from a chain submission is edited or cancelled on its own (C4.3).
- Time zone and clock source for the cut-off (MI-53).

---

### T-26.4.2 — Let a customer change or cancel their own order until the cut-off

**Parent story**

> As a Customer User, I want to know how to request a correction after placing an order so that I can contact the company with the order details.
>
> Acceptance criteria:
> - After the cut-off, I can view the order's lines and status but can't edit or cancel (S1)
> - The reference, company phone and email are shown together, with a prompt to quote the reference; no self-service change is offered (S2)
> - An order placed by my rep or a colleague shows no edit or cancel controls (S3)
> - Placed 10:12 with a 4pm cut-off: "Pending", "You can change this until 4pm today", Edit and Cancel order; changing Hand Cream 12 → 6 and Save changes keeps it Pending (S4)
> - Cancel order and confirm: Cancelled, shown in history (S5)
> - Editing at 3:55pm: "This order locks at 4pm — 5 minutes left to save"; saving at 4:01pm is refused, the order stands as accepted, and I'm told to contact the company with the reference, phone and email (S6)
> - Cut-off 4pm Mon–Thu, 1pm Fri, none at weekends: an order at 2pm Friday reads "You can change this until 4pm Monday" (S7)

**Slice** — Until the cut-off, a customer's own placed order says when it locks and can be edited or cancelled; a save that arrives after the cut-off is refused with the contact route; afterwards, and for anyone else's order, it is read-only with the reference, phone and email.
**Spec source** — Self-service US-007 S1–S7; uxdocs 06 C-04 (C4.1–C4.4); BR-NEW-009 rule 4; uxdocs 02 H2.9 (held orders can't be edited)
**Depends on** — T-26.4.1, T-13.10.1, T-7.1.1, T-7.2.1
**Pattern to follow** — T-13.10.1 (editing a Pending order until the cut-off on R-04)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — races the cut-off (Edge-Case Discovery High); follows the reviewed R-04 pattern.

**Provisional commit message**

```
feat(self-service): let customers change their own order until the cut-off

- Orders wait until the cut-off anyway, so the customer who placed one
  can correct it until then; afterwards the company handles changes
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A reviewed pattern in a new channel, against a human oracle.

**Agent prompt**

```
Role: You are adding edit and cancel until the cut-off to placed customer
orders in the Field Sales Management System's customer site.

Context:
- Slice: until the cut-off, a customer's own placed order says when it locks
  and can be edited or cancelled; a save that arrives after the cut-off is
  refused with the contact route; afterwards, and for anyone else's order, it
  is read-only with the reference, phone and email.
- Specs: plan_docs/stories/self-service.md US-007 S1–S7;
  plan_docs/uxdocs/06-customer.md C-04 (C4.1–C4.4);
  plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-009.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — placed orders
  (T-26.4.1), R-04 pattern (T-13.10.1), cut-off (T-7.1.1), hold (T-7.2.1).
- Pattern to follow: T-13.10.1.

Acceptance criteria:
1. Before the cut-off, the customer's own order shows "Pending" and "You can
   change this until 4pm today" (next cut-off by weekday rules), with Edit and
   Cancel order.
2. Edit reopens the lines on the page; Save changes keeps it Pending without
   a second Place order.
3. Cancel asks once and the order shows Cancelled in history.
4. While editing within the final minutes: "This order locks at 4pm — 5
   minutes left to save"; a save after the cut-off is refused and the
   accepted order stands, with the contact route shown.
5. After the cut-off, and for others' orders, no edit or cancel; the
   reference, phone and email show together with "quote the reference".
6. A held order can't be edited.

Constraints:
- Use the project's existing conventions and test framework.
- The server decides whether the cut-off has passed; the page's clock is only
  for the warning.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  Cancelled state.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the cut-off race passes every agreed scenario, stop and
show it. Resume only on "Continue T-26.4.2".

Steps: 1. window and wording; 2. edit; 3. cancel; 4. lock warning and late
save; 5. read-only view; 6. tests.

Test expectations: implement exactly the scenarios agreed in T-26.4.2-S. You
are forbidden from designing your own test cases.

Definition of done: until the cut-off, a customer's own placed order says
when it locks and can be edited or cancelled; a save that arrives after the
cut-off is refused with the contact route; afterwards, and for anyone else's
order, it is read-only with the reference, phone and email.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: company-side changes to accepted orders (MI-10).
```

**Checkpoint**

Produces before pausing — the cut-off race passing every agreed scenario.
Human reviews — Can any customer change ever land after the cut-off?
Resume trigger — `Continue T-26.4.2`

---

### T-26.5.1-S — Test scenarios for order history

**Owner** — Scenario Review
**Gates** — T-26.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the customer's order history and order
detail (task T-26.5.1). Read plan_docs/stories/self-service.md US-008 S1–S4
and plan_docs/uxdocs/06-customer.md C-06, C-07 (read-only detail). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover the story scenarios, then derivable edges: an order with a line
removed as unavailable at the cut-off, a rep's order with a rep price (the
customer sees the price, not the working), a cancelled order, a Location that
left the customer's scope. Mark undecided cases as "Needs a decision". Write
no test code; change no files.
```

---

### T-26.5.1 — Show every order for the customer's shops with what has been sent

**Parent story**

> As a Customer User, I want to see every order for my shops with what has actually been despatched so that I know what is coming.
>
> Acceptance criteria:
> - History lists all orders for my Locations — mine, colleagues' and my rep's — with date, Location, status and value, none offering edit or cancel after placement (S1)
> - A line 24 of 36 despatched: "Accepted, partly sent" and "24 sent 12 Oct · 12 outstanding" (S2)
> - A rejected order shows its reason (S3)
> - Filtering by Location and date range lists only matching orders (S4)

**Slice** — The customer opens history for every order across their shops, whoever placed it, filters by shop and date, and opens a read-only detail showing each line's despatched and outstanding quantities, or a rejection's reason.
**Spec source** — Self-service US-008 S1–S4; uxdocs 06 C-06, C-07
**Depends on** — T-26.4.1, T-26.1.1, T-7.3.1, T-7.2.1
**Pattern to follow** — T-7.5.1 (status and despatch on a sent order)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — read views across scope; customer-facing data must omit rep provenance.

**Provisional commit message**

```
feat(self-service): show order history with what has been sent

- The customer needs to know what's coming, whoever ordered it, so every
  order for their shops is listed with its despatch detail
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Read views with a review pause.

**Agent prompt**

```
Role: You are building order history and order detail (C-06, C-07) for the
Field Sales Management System's customer site.

Context:
- Slice: the customer opens history for every order across their shops,
  whoever placed it, filters by shop and date, and opens a read-only detail
  showing each line's despatched and outstanding quantities, or a
  rejection's reason.
- Specs: plan_docs/stories/self-service.md US-008 S1–S4;
  plan_docs/uxdocs/06-customer.md C-06, C-07.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — orders (T-26.4.1),
  scope (T-26.1.1), despatches (T-7.3.1), rejections (T-7.2.1), sent-order
  pattern (T-7.5.1).
- Pattern to follow: T-7.5.1.

Acceptance criteria:
1. History lists all orders for the customer's Locations with date,
   Location, status and value.
2. Filters by Location and date range.
3. Detail shows lines with "24 sent 12 Oct · 12 outstanding" and statuses
   such as "Accepted, partly sent".
4. A rejected order shows its reason.
5. No Capturing Rep, Ordered By, tier or rep-price working appears.
6. Edit and Cancel appear only as T-26.4.2 allows.

Constraints:
- Use the project's existing conventions and test framework.
- Scope enforcement comes from T-26.1.1.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after history and detail work against seeded orders from reps,
colleagues and the customer, stop and show them. Resume only on
"Continue T-26.5.1".

Steps: 1. history query; 2. filters; 3. detail; 4. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-26.5.1-S; do not design
your own.

Definition of done: the customer opens history for every order across their
shops, whoever placed it, filters by shop and date, and opens a read-only
detail showing each line's despatched and outstanding quantities, or a
rejection's reason.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: chain submission grouping (T-26.6.1), repeat (T-26.5.2).
```

**Checkpoint**

Produces before pausing — history and detail against seeded orders from every source.
Human reviews — Is anything rep-only visible to the customer?
Resume trigger — `Continue T-26.5.1`

---

### T-26.5.2-S — Test scenarios for repeating a past order

**Owner** — Scenario Review
**Gates** — T-26.5.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for repeating a past order (task T-26.5.2). Read
plan_docs/stories/self-service.md US-009 S1–S9 and design decision "Repeat
prefills, never submits", and plan_docs/uxdocs/06-customer.md C-07
(C7.1–C7.4). Output one line per scenario as Should_Outcome_When_Condition,
then "→" and a one-line intent. Cover the story scenarios, then derivable
edges: repeating into a shop whose current order is Pending (C7.3 says only
unplaced orders), a Restricted product on the past order, a free-of-charge
line on a rep's order. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-26.5.2 — Repeat a past order, whole or in part, into the current order

**Parent story**

> As a Customer User, I want to start a new order from a previous one, all of it or just some lines, so that a regular reorder takes seconds.
>
> Acceptance criteria:
> - Repeat prefills a new order with the lines and quantities, not submitted; a branch order from a chain submission repeats for that branch only (S1)
> - Ticking 4 of 9 lines and Repeat selected prefills only those (S2)
> - An Unavailable product is listed with its replacement, not added; a Temporarily Unavailable one shows "Back in stock around 25 October" (S3)
> - An ended promotional price isn't carried; today's prices apply (S4)
> - Changing quantities and placing creates a new order; the original is unchanged (S5)
> - A chain group header has no Repeat; an individual branch order repeats into that branch (S6)
> - A rep's order can be repeated, at today's prices without the rep discount (S7)
> - Repeating into a shop with an unplaced order adds to it; no second order (S8)
> - A product already in the order: one warning lists "Hand Cream 6 → 12, Sudocrem 4 → 8"; Continue takes the repeated quantities on one line each; Cancel leaves the order unchanged (S9)

**Slice** — From any visible order, the customer repeats all or selected lines into the shop's current unplaced order (starting one if none), at today's prices and availability, with unavailable products shown with replacements and any quantity changes to existing lines confirmed once.
**Spec source** — Self-service US-009 S1–S9; design decision "Repeat prefills, never submits"; uxdocs 06 C-07 (C7.1–C7.4)
**Depends on** — T-26.5.1, T-26.4.1, T-10.1.1, T-8.1.1
**Pattern to follow** — T-5.1.5 (carrying lines into an order)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — rules are fully stated; merges into the current order.

**Provisional commit message**

```
feat(self-service): repeat a past order into the current one

- Repeat proposes and the customer decides: nothing is submitted, and old
  prices and changed products are never presented as current
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A stated merge flow with a review pause.

**Agent prompt**

```
Role: You are adding Repeat to the customer order detail (C-07) in the Field
Sales Management System's customer site.

Context:
- Slice: from any visible order, the customer repeats all or selected lines
  into the shop's current unplaced order (starting one if none), at today's
  prices and availability, with unavailable products shown with replacements
  and any quantity changes to existing lines confirmed once.
- Specs: plan_docs/stories/self-service.md US-009 S1–S9 and design decision
  "Repeat prefills, never submits"; plan_docs/uxdocs/06-customer.md C-07
  (C7.1–C7.4).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — history and detail
  (T-26.5.1), current order (T-26.4.1), replacements (T-10.1.1),
  availability (T-8.1.1).
- Pattern to follow: T-5.1.5's carrying lines into an order.

Acceptance criteria:
1. Repeat all and Repeat selected on any visible order; none on a chain
   group header.
2. Lines go into the shop's current unplaced order, or a new one if none;
   never into a Pending order.
3. Today's prices apply; rep discounts and ended promotions are not carried.
4. Unavailable products aren't added and are listed with replacements;
   Temporarily Unavailable ones show "Back in stock around 25 October".
5. Existing lines: one warning lists each change (e.g. "Hand Cream 6 → 12");
   Continue applies the repeated quantities on one line each; Cancel changes
   nothing.
6. Nothing is submitted; the original order is unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- Restricted and free-of-charge lines are never copied.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after repeat into an existing order works with the warning, stop
and show it. Resume only on "Continue T-26.5.2".

Steps: 1. repeat actions; 2. target order; 3. fresh prices and availability;
4. merge warning; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-26.5.2-S; do not design
your own.

Definition of done: from any visible order, the customer repeats all or
selected lines into the shop's current unplaced order (starting one if none),
at today's prices and availability, with unavailable products shown with
replacements and any quantity changes to existing lines confirmed once.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: repeating a whole chain submission (deferred, C7.1).
```

**Checkpoint**

Produces before pausing — repeat into an existing order, with the warning.
Human reviews — Is anything from the old order ever presented as current?
Resume trigger — `Continue T-26.5.2`

---

### T-26.6.1-S — Test scenarios for the customer chain grid

**Owner** — Scenario Review
**Gates** — T-26.6.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a head office buyer's multi-branch grid
(task T-26.6.1). Read plan_docs/stories/self-service.md US-006 S1–S13,
S15–S20 and its amendment, US-008 S5, S6, AC-SS006-A–O and BR-NEW-008 in
plan_docs/uxdocs/04-user-stories-amendments.md, and
plan_docs/uxdocs/06-customer.md C-05 (C5.1–C5.16), C-06 (C6.1, C6.2). Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover the story scenarios, then derivable edges: two buyers
with different defaults, a default range that is later removed from the
chain, the "All" cell after hand edits (as on the rep grid, M11.3). Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-26.6.1 — Let a head office buyer order for every branch on a grid

**Parent story**

> As a Customer User at a head office, I want to enter quantities for all our shops in one grid so that a chain order is one job.
>
> Acceptance criteria:
> - "Order for several branches" shows products down and branches across, with row and column totals (S1), and no Capturing Rep, Ordered By, tier names or breakdowns (S3)
> - Review shows lines and units per branch; placing creates one order per branch, each read-only afterwards with the contact route beside its reference (S4); a branch with nothing gets no order (S5)
> - The grid's products come from the chain's Agreed Ranges, unprefilled, without preventing other permitted products (S6); a dropdown picks a range, with the buyer's personal default or the first option (S8–S12); entered rows stay at the top, once each (S7, S13, S15, S16)
> - All eligible branches start selected (Closed excluded, Temporarily Closed flagged); a branch with quantities can't be removed and the block opens its quantities; clearing the last quantity doesn't remove it (S17–S20)
> - History shows the submission as one expandable entry, each branch order with its own reference, status and value; filtered to one branch, it's an ordinary row (Self-service US-008 S5, S6)

**Slice** — A head office buyer builds one chain order on a grid of the chain's range products against its branches, switches ranges from a dropdown that remembers their own default, reviews each branch's share and places it as one order per branch, which history then groups as one submission.
**Spec source** — Self-service US-006 S1–S13, S15–S20 and amendment; US-008 S5, S6; AC-SS006-A–O; BR-NEW-008; uxdocs 06 C-05 (C5.1–C5.16), C-06 (C6.1, C6.2)
**Depends on** — T-26.1.1, T-26.4.1, T-26.5.1, T-24.3.1, T-24.2.1, T-23.1.1
**Pattern to follow** — T-24.3.1 (rep grid), T-24.2.1 (split)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: C | Confidence: M
  (inferred) — reuses the reviewed grid and split in a new channel; C-05 is settled for the first round.

**Provisional commit message**

```
feat(self-service): let a head office buyer order for every branch at once

- The chain case is where self-service saves the most work; the buyer
  starts from the chain's agreed ranges and never loses rows when
  switching between them
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A reused grid in a new channel, with a review pause.

**Agent prompt**

```
Role: You are building the customer multi-branch grid (C-05) for the Field
Sales Management System's customer site.

Context:
- Slice: a head office buyer builds one chain order on a grid of the chain's
  range products against its branches, switches ranges from a dropdown that
  remembers their own default, reviews each branch's share and places it as
  one order per branch, which history then groups as one submission.
- Specs: plan_docs/stories/self-service.md US-006 S1–S13, S15–S20 and its
  amendment, US-008 S5, S6; plan_docs/uxdocs/04-user-stories-amendments.md
  AC-SS006-A–O and BR-NEW-008; plan_docs/uxdocs/06-customer.md C-05
  (C5.1–C5.16), C-06 (C6.1, C6.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — scope (T-26.1.1),
  customer orders (T-26.4.1), history (T-26.5.1), rep grid (T-24.3.1), split
  (T-24.2.1), chain ranges (T-23.1.1).
- Pattern to follow: T-24.3.1 and T-24.2.1.

Acceptance criteria:
1. Products down, selected branches across, row and column totals; no rep
   provenance, tier names or breakdowns.
2. A range dropdown lists the chain's Agreed Ranges; it opens on the buyer's
   personal default, or the first option without saving a default; setting
   a default affects only that buyer.
3. Entered rows stay at the top with their quantities, once each, above the
   selected range's remaining products.
4. All eligible branches start selected; Closed excluded; Temporarily Closed
   flagged; a branch with quantities can't be removed and the block opens its
   quantities with the reason; clearing the last quantity leaves it selected.
5. Review shows lines and units per branch; placing creates one order per
   branch with quantities, each Pending until the cut-off; a branch with none
   gets no order.
6. History shows the submission as one expandable entry; filtered to one
   branch, its order is an ordinary row.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-24.3.1's grid and T-24.2.1's split; no second implementation.
- Permitted products outside the ranges stay reachable by search.
- No tests of framework internals or trivial members.
- This task opts in to the personal default preference.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after a chain order is placed from the grid and shows grouped in
history, stop and show it. Resume only on "Continue T-26.6.1".

Steps: 1. grid in the customer site; 2. range dropdown and default;
3. entered rows; 4. branch selection rules; 5. review and place; 6. history
grouping; 7. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-26.6.1-S; do not design
your own.

Definition of done: a head office buyer builds one chain order on a grid of
the chain's range products against its branches, switches ranges from a
dropdown that remembers their own default, reviews each branch's share and
places it as one order per branch, which history then groups as one
submission.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: phone entry (T-26.6.2), dropdown sort order (deferred).
```

**Checkpoint**

Produces before pausing — a chain order placed from the grid and grouped in history.
Human reviews — Is the customer grid's outcome identical to the rep grid's?
Resume trigger — `Continue T-26.6.1`

---

### T-26.6.2-S — Test scenarios for chain ordering on a phone

**Owner** — Scenario Review
**Gates** — T-26.6.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for product-at-a-time chain ordering on a phone
(task T-26.6.2). Read plan_docs/stories/self-service.md US-006 S14 and
plan_docs/uxdocs/06-customer.md C-05 (C5.9 and the phone frame). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover S14, then derivable edges: switching from phone to laptop mid
order, the branch removal rules on a phone. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-26.6.2 — Enter a chain order product by product on a phone

**Parent story**

> As a Customer User at a head office, I want to enter quantities for all our shops in one grid so that a chain order is one job.
>
> Acceptance criteria:
> - On a phone, choosing a product from the selected Agreed Range lets me enter one quantity for the selected branches and adjust individual ones, with a running summary, reaching the same branch-order review as the grid (S14)

**Slice** — On a phone, the buyer picks products one at a time from the selected range, enters one quantity for all branches and adjusts exceptions, sees a running summary, and reaches the same review and placement as the grid.
**Spec source** — Self-service US-006 S14; uxdocs 06 C-05 (C5.9, phone frame — a drafting call)
**Depends on** — T-26.6.1, T-24.1.1
**Pattern to follow** — T-24.1.1 (product-at-a-time entry)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — reuses the tablet pattern; the phone composition is a drafting call.

**Provisional commit message**

```
feat(self-service): enter a chain order product by product on a phone

- A grid doesn't fit a phone; the same one-product-at-a-time entry reps
  use on the tablet reaches the same review
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A reused pattern on a new form factor.

**Agent prompt**

```
Role: You are adding phone entry for chain orders to the Field Sales
Management System's customer site.

Context:
- Slice: on a phone, the buyer picks products one at a time from the
  selected range, enters one quantity for all branches and adjusts
  exceptions, sees a running summary, and reaches the same review and
  placement as the grid.
- Specs: plan_docs/stories/self-service.md US-006 S14;
  plan_docs/uxdocs/06-customer.md C-05 (C5.9 and the phone frame).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — customer grid and
  review (T-26.6.1), tablet product-at-a-time pattern (T-24.1.1).
- Pattern to follow: T-24.1.1.

Acceptance criteria:
1. On a phone-sized screen, chain ordering is product-at-a-time from the
   selected range.
2. One quantity applies to the selected branches; individual branches can be
   adjusted; hand-adjusted branches keep their values when the all-branches
   quantity changes.
3. A running summary shows products and branch quantities entered.
4. Review and placement are the grid's.
5. Branch selection rules are the grid's.

Constraints:
- Use the project's existing conventions and test framework.
- Same order model as the grid, so an order started on one form factor can
  be continued on the other.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after one product's entry works on a phone, stop and show it.
Resume only on "Continue T-26.6.2".

Steps: 1. phone layout; 2. product entry; 3. summary; 4. review; 5. tests
from agreed scenarios.

Test expectations: implement the scenarios agreed in T-26.6.2-S; do not design
your own.

Definition of done: on a phone, the buyer picks products one at a time from
the selected range, enters one quantity for all branches and adjusts
exceptions, sees a running summary, and reaches the same review and
placement as the grid.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: a native app (MI-35).
```

**Checkpoint**

Produces before pausing — one product's entry working on a phone.
Human reviews — Is the phone composition usable one-handed, and does it reach the same review?
Resume trigger — `Continue T-26.6.2`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | SS-003 (T-26.1.1), SS-004 (T-26.2.2, T-26.3.1), SS-005 (T-26.4.1, T-26.3.1 for S6), SS-007 (T-26.4.2), SS-008 (T-26.5.1; S5, S6 in T-26.6.1), SS-009 (T-26.5.2), SS-006 (T-26.6.1, T-26.6.2); customer range assignment with no story (T-26.2.1) |
| Every task satisfies the three slice criteria | Pass | 10 of 10 |
| Every task carries a tier with a rationale citing dimensions | Pass | 10 of 10 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-17 (T-26.2.1), MI-12 (expected-delivery setting), MI-53 (cut-off clock), MI-35 (app = website), MI-10 (company-side changes) |
| Tasks modifying existing behaviour order characterisation first | Pass | None modify existing screens; new channel reusing reviewed rules |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-26.1.1, T-26.2.1, T-26.3.1, T-26.4.2 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | All 9 Agent-Assisted tasks |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-26.1.1 |
| Every scenario task precedes the task it gates | Pass | 10 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, `{{NEEDS ACCEPTANCE CRITERIA}}` (T-26.2.1), MI-10, 12, 17, 35, 53 |
