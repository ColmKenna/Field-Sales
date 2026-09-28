# E2 — Customers, locations and contacts

**Iteration** — 1, MVP: orders from the field reach the warehouse
**Outcome** — Head office can set up a customer's shops in their towns, each with a usable map position and a main contact that can never be left blank.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Customer Directory US-007 — Maintain geography and move a Town | Should | S1, S2, S4 (S3 "Move a Town" arrives with E16) |
| 2 | Customer Directory US-009 — Maintain Location Type and Contact Type lists | Could | S1–S3 |
| 3 | Customer Directory US-001 — Create a Customer with its Locations | Must | S1–S4 (the No Visit Schedule list in S1 arrives with E17; the Unassigned list with E3) |
| 4 | Customer Directory US-003 — Manage Contacts and their Location links | Must | S1–S5 |
| 5 | Customer Directory US-004 — Replace or retire a Main Contact | Must | S1–S6 |

**Exit criterion** — A Head Office User can maintain Region → County → Town, create a customer with its shops (town required, coordinates from Eircode or town with their precision), keep type lists, link contacts to several shops with exactly one active main contact per shop, and retire a main contact only by naming a replacement or flagging the gap, which the rep then sees on the tablet.

**Capability-class stamp** — Frontier workhorse for Agent-Assisted tasks, the Eircode tight-loop slivers and scenario drafting; Fast mid-tier for Agent-Autonomous tasks. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [customer-directory.md](../../stories/customer-directory.md), [02-head-office.md](../../uxdocs/02-head-office.md) (H-17, H-25, H-26, H-27, H-29), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-05, T5.2).

---

### T-2.1.1-S — Test scenarios for maintaining geography

**Owner** — Scenario Review
**Gates** — T-2.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for maintaining Region → County → Town (task
T-2.1.1). Read plan_docs/stories/customer-directory.md US-007 S1–S2 and the
glossary (Region / County / Town, "strict hierarchy"). Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Cover S1 and S2, then derivable edges: a County with no Region, two Towns
with the same name in different Counties, renaming a Town, loading the
initial geography. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-2.1.1 — Maintain Regions, Counties and Towns

**Parent story**

> As a Head Office User, I want to maintain Regions, Counties and Towns, and move a Town to the right County with the effect on territories shown so that a geography fix doesn't silently reassign shops.
>
> Acceptance criteria:
> - Creating Region "Leinster", County "Wicklow" under it and Town "Rathdrum" under Wicklow makes Rathdrum available on Locations (S1)
> - Creating a Town with no County is rejected with "Choose a county" (S2)

**Slice** — A Head Office User builds Region → County → Town, a town without a county is refused, and the towns become choosable for locations.
**Spec source** — Customer Directory US-007 S1, S2; uxdocs 02 H-29 (H29.1)
**Depends on** — T-1.1.1
**Pattern to follow** — T-1.1.1 (tree navigated one level at a time, H29.1 mirrors H-15)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — Blast Radius Medium: territory resolution (T-3.1.1) reads this tree; checkpoint on the model and the initial load (MI-16).

**Provisional commit message**

```
feat(customers): maintain regions, counties and towns

- A Location's territory is derived from its Town, so the hierarchy is
  strict: every Town has one County and every County one Region
- Initial geography is loaded once rather than typed shop by shop
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared model with a design pause.

**Agent prompt**

```
Role: You are building geography maintenance (Region → County → Town) for
the Field Sales Management System's head office website.

Context:
- Slice: a Head Office User builds Region → County → Town; a town without a
  county is refused; towns become choosable for locations.
- Specs: plan_docs/stories/customer-directory.md US-007 S1–S2 and glossary
  (Region / County / Town; "Territory — derived from Town");
  plan_docs/uxdocs/02-head-office.md H-29 (H29.1: one level at a time with
  the path as navigation).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}}.
- Pattern to follow: the category tree view from T-1.1.1.

Acceptance criteria:
1. Creating Region "Leinster", County "Wicklow" under it and Town
   "Rathdrum" under Wicklow succeeds and Rathdrum is offered when creating a
   Location.
2. Creating a Town with no County is rejected with "Choose a county".
3. A County belongs to exactly one Region; a Town to exactly one County.
4. Two Towns may share a name in different Counties and are shown with
   their County to tell them apart.
5. An initial geography can be loaded from a file in one step (source to be
   confirmed, MI-16).

Constraints:
- Use the project's existing conventions and test framework.
- Moving a Town between Counties is out of scope (E16); do not expose it.
- Archiving a Town is T-2.1.2.
- No tests of framework internals or trivial members.
- This task opts in to creating the region, county and town tables and a
  one-off load routine.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the geography model and the initial-load format are
designed, stop and show them. Resume only on "Continue T-2.1.1".

Steps: 1. hierarchy model; 2. persistence; 3. load routine; 4. H-29 pages;
5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-2.1.1-S; do not design
your own.

Definition of done: a Head Office User builds Region → County → Town, a town
without a county is refused, and the towns become choosable for locations.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: archiving (T-2.1.2), moving a Town (E16), territory
assignment (T-3.1.1).
```

**Checkpoint**

Produces before pausing — the geography model and the initial-load file format.
Human reviews — Will territory resolution and later Town moves work on this model without change, and where does the initial data come from?
Resume trigger — `Continue T-2.1.1`

---

### T-2.1.2 — Archive a Town that is in use

**Parent story**

> As a Head Office User, I want to maintain Regions, Counties and Towns, and move a Town to the right County with the effect on territories shown so that a geography fix doesn't silently reassign shops.
>
> Acceptance criteria:
> - Laragh with 4 Locations shows Archive with the count and no Delete; archived Towns are not offered on new Locations (S4)

**Slice** — A Town, County or Region in use can only be archived, with its usage shown, and archived towns are no longer offered for new locations.
**Spec source** — Customer Directory US-007 S4; design decision "Geography edits show their reach"
**Depends on** — T-2.1.1, T-1.5.1
**Pattern to follow** — T-1.5.1 (archive-not-delete component)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — repetition of a reviewed pattern; Blast Radius Low.

**Provisional commit message**

```
feat(customers): archive towns, counties and regions in use

- Locations keep their Town for history and territory, so geography in use
  can only be taken out of new selection, never deleted
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Pattern repetition.

**Agent prompt**

```
Role: You are applying archive-not-delete to geography in the Field Sales
Management System.

Context:
- Slice: a Town, County or Region in use can only be archived, with its
  usage shown; archived towns aren't offered for new locations.
- Specs: plan_docs/stories/customer-directory.md US-007 S4.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — geography from
  T-2.1.1, list component and reference-count interface from T-1.5.1.
- Pattern to follow: T-1.5.1.

Acceptance criteria:
1. Laragh with 4 Locations shows Archive with "Used by 4 locations" and no
   Delete; an unused Town shows Delete only.
2. An archived Town is not offered on new Locations; existing Locations
   keep it, labelled "(archived)".
3. The same rule applies to Counties (used by Towns) and Regions (used by
   Counties).

Constraints:
- Use the project's existing conventions and test framework; reuse
  T-1.5.1's component and interface.
- No tests of framework internals or trivial members.
- This task opts in to adding archive flags to geography.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. archive flags; 2. reference counts for geography; 3. pickers skip
archived; 4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: a Town, County or Region in use can only be archived,
with its usage shown, and archived towns are no longer offered for new
locations.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: moving a Town (E16), territory effects (T-3.1.1).
```

---

### T-2.3.1-S — Test scenarios for creating a customer with its locations

**Owner** — Scenario Review
**Gates** — T-2.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for creating a Customer with its Locations
(task T-2.3.1). Read plan_docs/stories/customer-directory.md US-001 S1, S2,
S4 and the glossary (Customer, Location). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover the
story scenarios, then derivable edges: an archived Town, a customer with no
locations, renaming a Location to a duplicate name. Coordinates are a later
task (T-2.3.2); leave them out. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-2.3.1 — Create a customer with its locations

**Parent story**

> As a Head Office User, I want to set up a Customer and add its Locations, each with its Town, so that reps and planning know where the customer's shops are.
>
> Acceptance criteria:
> - Creating Customer "Hickey's Pharmacies" and Location "Hickey's Rathdrum", Town Rathdrum, Eircode A67 X123 saves both (S1, without coordinates or Type)
> - A Location without a Town is rejected with "Choose a town" (S2)
> - A second "Hickey's Rathdrum" in the same Customer shows "This customer already has a location with that name" and can continue or rename (S4)

**Slice** — A Head Office User creates a customer and adds its shops, each in a town, and a duplicate shop name is warned about but allowed.
**Spec source** — Customer Directory US-001 S1, S2, S4; uxdocs 02 H-25 (H25.1), H-26
**Depends on** — T-2.1.1
**Pattern to follow** — T-1.2.1 (create form and record page)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — Blast Radius Medium: Location is the unit of performance and is read by every area; checkpoint on a model with room for types, profiles, master, contacts and closure.

**Provisional commit message**

```
feat(customers): create customers and their locations

- Every other area reads Locations, so the model leaves room for profiles,
  a master Location, contacts and closures from the start
- Duplicate shop names are warned, not blocked, because chains reuse names
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared model with a design pause.

**Agent prompt**

```
Role: You are building customer and location creation for the Field Sales
Management System's head office website.

Context:
- Slice: a Head Office User creates a customer and adds its shops, each in a
  town; a duplicate shop name is warned about but allowed.
- Specs: plan_docs/stories/customer-directory.md US-001 S1, S2, S4 and the
  glossary (Customer, Location, Master Location, Temporarily Closed, Closed,
  Location Profile — "a Location may hold many profiles");
  plan_docs/uxdocs/02-head-office.md H-25 (H25.1 locations listed with
  Town, master, type and closure, each opening H-26) and H-26.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}}.
- Pattern to follow: T-1.2.1's create form and record page.

Acceptance criteria:
1. Creating Customer "Hickey's Pharmacies" and adding Location "Hickey's
   Rathdrum", Town Rathdrum, Eircode A67 X123 saves both.
2. A Location without a Town is rejected with "Choose a town".
3. A second Location named "Hickey's Rathdrum" in the same Customer shows
   "This customer already has a location with that name" and can be saved or
   renamed.
4. Each Location belongs to exactly one Customer.
5. The Customer record lists its Locations with Town (type, master and
   closure columns stay empty until their tasks land).

Constraints:
- Use the project's existing conventions and test framework.
- Design the Location model so these later additions need no reshaping:
  Location Type (T-2.2.1), coordinates with precision and history
  (T-2.3.2), contacts and one Main Contact (T-2.4.1), many Location
  Profiles and overrides (E12), a Master Location of the same Customer
  (E12), temporary and permanent closure (E17). Do not implement them.
- No tests of framework internals or trivial members.
- This task opts in to creating the customer and location tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the Customer and Location model is designed, stop and show
how each later addition fits. Resume only on "Continue T-2.3.1".

Steps: 1. Customer and Location types; 2. persistence; 3. create handler
with Town rule and duplicate warning; 4. H-25 record and H-26 page shell;
5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-2.3.1-S; do not design
your own.

Definition of done: a Head Office User creates a customer and adds its
shops, each in a town, and a duplicate shop name is warned about but allowed.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Each later addition listed in Constraints fits without reshaping
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: coordinates (T-2.3.2), types (T-2.2.1), contacts (T-2.4.1),
rep assignment (T-3.1.1), profiles and master (E12), closures (E17), tiers
on the customer (T-6.2.1), bulk import (out of scope for the system; see
MI-06).
```

**Checkpoint**

Produces before pausing — the Customer and Location model, with a note on where types, coordinates, contacts, profiles, master and closures attach.
Human reviews — Can every later area hang its data off this Location without a schema reshape?
Resume trigger — `Continue T-2.3.1`

---

### T-2.2.1 — Maintain Location Type and Contact Type lists

**Parent story**

> As a Head Office User, I want to maintain the Type lists with descriptions, archiving rather than deleting anything in use so that classification stays consistent and history intact.
>
> Acceptance criteria:
> - Adding Location Type "Head office — no stock held" offers it on Locations (S1)
> - Contact Type "Buyer" used by 14 Contacts shows Archive, keeps it on them labelled Archived, and no Delete (S2)
> - An unused Type offers Delete with a plain confirmation (S3)

**Slice** — Location Types and Contact Types are archive-not-delete lists with descriptions, and a Location's type can be set from its list.
**Spec source** — Customer Directory US-009 S1–S3; US-001 S1 (Type Pharmacy on a Location)
**Depends on** — T-1.5.1, T-2.3.1
**Pattern to follow** — T-1.5.1
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: M
  (inferred) — repetition of a reviewed pattern; Blast Radius Low.

**Provisional commit message**

```
feat(customers): maintain location and contact type lists

- Classifications stay consistent and history-safe using the same
  archive-not-delete rule as product reference data
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
Pattern repetition.

**Agent prompt**

```
Role: You are adding the Location Type and Contact Type lists to the Field
Sales Management System.

Context:
- Slice: both lists are archive-not-delete with descriptions, and a
  Location's type is set from its list.
- Specs: plan_docs/stories/customer-directory.md US-009 and glossary
  (Location Type, Contact Type); US-001 S1 ("Type Pharmacy").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — list component from
  T-1.5.1, Location from T-2.3.1.
- Pattern to follow: T-1.5.1.

Acceptance criteria:
1. Adding Location Type "Head office — no stock held" with a description
   offers it on Locations.
2. Contact Type "Buyer" used by 14 Contacts shows Archive (not Delete) and
   stays on the 14 labelled "(archived)".
3. An unused Type shows Delete with a plain confirmation.
4. A Location's Type is optional and set from the active Location Types.
5. Both lists appear in the H-17 list switcher.

Constraints:
- Use the project's existing conventions and test framework; reuse
  T-1.5.1's component and interface.
- Location Type is separate from Location Profile (different concepts).
- No tests of framework internals or trivial members.
- This task opts in to the two type tables and the Location type column.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. two type entities; 2. Location type field; 3. switcher entries;
4. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: Location Types and Contact Types are archive-not-delete
lists with descriptions, and a Location's type can be set from its list.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: Contact Type on contacts (T-2.4.1 uses it), Location
Profiles (E12).
```

---

### T-2.4.1-S — Test scenarios for contacts and the main contact

**Owner** — Scenario Review
**Gates** — T-2.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for contacts linked to several Locations with
one Main Contact each (task T-2.4.1). Read
plan_docs/stories/customer-directory.md US-003 and the glossary (Contact,
Main Contact). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Cover S1–S5,
then derivable edges: linking the same contact twice to one Location, a
Location whose only contact is Inactive, two users setting a Main at once.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-2.4.1 — Link contacts to locations with one main contact each

**Parent story**

> As a Head Office User, I want to create Contacts, link them to one or more Locations, and set one as Main Contact per Location so that reps know who to ask for at each shop.
>
> Acceptance criteria:
> - Contact "Mary Walsh", Type Pharmacist, linked to Rathdrum, Arklow and Wicklow Town appears at all three (S1)
> - Setting Mary as Main at Rathdrum and Arklow shows "Main contact: Mary Walsh"; Wicklow Town still needs one (S2)
> - The first Contact linked to a Location with none becomes Main automatically, with a note (S3)
> - Setting a second Main at Rathdrum asks "Replace Mary Walsh as main contact?"; Mary remains a Contact (S4)
> - A Location with 3 Inactive Contacts shows Active ones with "Show inactive (3)" (S5)

**Slice** — A Head Office User links a contact to several shops, each shop has exactly one active main contact, and replacing the main contact asks first.
**Spec source** — Customer Directory US-003 S1–S5; uxdocs 02 H-27
**Depends on** — T-2.3.1, T-2.2.1
**Pattern to follow** — T-2.3.1 (record pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the "exactly one active Main per Location" invariant is data integrity; checkpoint on the link model.

**Provisional commit message**

```
feat(customers): link contacts to locations with one main contact each

- A rep must always know who to ask for, so every Location with contacts
  holds exactly one active Main Contact
- Contacts are never deleted because historic calls reference them
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
An invariant-bearing model with a design pause.

**Agent prompt**

```
Role: You are building contacts and their Location links for the Field Sales
Management System's head office website.

Context:
- Slice: a Head Office User links a contact to several shops; each shop has
  exactly one active main contact; replacing the main contact asks first.
- Specs: plan_docs/stories/customer-directory.md US-003 and design decision
  "Main Contact is the one thing that cannot be left blank";
  plan_docs/uxdocs/02-head-office.md H-27.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}}.
- Pattern to follow: T-2.3.1's record pages.

Acceptance criteria:
1. Contact "Mary Walsh", Type Pharmacist, linked to Rathdrum, Arklow and
   Wicklow Town appears as a Contact at all three.
2. Setting Mary as Main at Rathdrum and Arklow shows "Main contact: Mary
   Walsh" there; Wicklow Town still has no Main.
3. Linking the first Contact to a Location with none makes them Main
   automatically, with a note saying so.
4. Setting a second Main at Rathdrum asks "Replace Mary Walsh as main
   contact?"; after confirming, Mary remains a linked Contact.
5. A Location with 3 Inactive Contacts lists only Active ones with "Show
   inactive (3)".
6. No path ever leaves a Location with two Mains.

Constraints:
- Use the project's existing conventions and test framework.
- Contacts are never deleted; states are Active and Inactive.
- Enforce the one-Main rule in the model and in storage, not only in the UI.
- Removing or unlinking a Main is T-2.5.1; block it here with a clear
  message until then.
- No tests of framework internals or trivial members.
- This task opts in to the contact and contact–location link tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the contact–location link model with the one-Main
invariant is designed and its storage constraint written, stop and show it.
Resume only on "Continue T-2.4.1".

Steps: 1. Contact and link model; 2. storage constraint for one Main;
3. handlers for link, set Main, replace Main; 4. H-27 and contact list on
H-26; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-2.4.1-S; do not design
your own.

Definition of done: a Head Office User links a contact to several shops,
each shop has exactly one active main contact, and replacing the main
contact asks first.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Concurrent "set Main" cannot produce two Mains
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: unlinking or retiring a Main (T-2.5.1), tablet display
(T-2.5.2), customer online accounts (E25).
```

**Checkpoint**

Produces before pausing — the contact–location link model and the storage constraint enforcing one active Main per Location.
Human reviews — Is the one-Main rule enforced where no future screen can bypass it?
Resume trigger — `Continue T-2.4.1`

---

### T-2.3.2-S — Test scenarios for location coordinates

**Owner** — Scenario Review
**Gates** — T-2.3.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for defaulting a Location's coordinates from its
Eircode or Town (task T-2.3.2). Read plan_docs/stories/customer-directory.md
US-001 S1, S3 and design decision "Coordinates: rough by default, exact
through use". Output one line per scenario as Should_Outcome_When_Condition,
then "→" and a one-line intent. Cover S1 and S3, then derivable edges: the
lookup service is unavailable, an Eircode it cannot resolve, changing the
Eircode later, a Town with no coordinates. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-2.3.2 — Default location coordinates from Eircode or Town

**Parent story**

> As a Head Office User, I want to set up a Customer and add its Locations, each with its Town, so that reps and planning know where the customer's shops are.
>
> Acceptance criteria:
> - A Location with Eircode A67 X123 gets coordinates from the Eircode with Precision "Eircode" (S1)
> - A Location in Laragh with no Eircode gets coordinates from Laragh with Precision "Town" (S3)

**Slice** — Every new location gets a map position from its Eircode, or failing that its town, labelled with its precision, and earlier positions are kept.
**Spec source** — Customer Directory US-001 S1, S3; glossary (Coordinates); Requires Clarification 4
**Depends on** — T-2.3.1
**Pattern to follow** — novel — see design notes (first external service)
**Ownership** — Impl: Human Tight-Loop ↓ | Test: Scenario Review | Complexity: M | Confidence: L
  (inferred) — would be Agent-Assisted; downgraded one step because the Eircode lookup service, its terms and its rural coverage are unconfirmed (MI-07).

**Provisional commit message**

```
feat(customers): default location coordinates from Eircode or Town

- Every shop gets a usable pin on day one without a data project; the
  precision label stops anyone trusting a town-centre pin for navigation
- Previous coordinates are kept so a bad field capture can be reverted
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
External integration with unknown behaviour; slivers stay narrow.

**Work package**

Increments:
1. Choose the Eircode lookup service and confirm coverage and licence (MI-07); record the decision.
2. Coordinates value with Precision (Town, Eircode, Confirmed on site) and a history of previous values on the Location.
3. Town coordinates for every Town (from the geography load, T-2.1.1).
4. On create or Eircode change: look up; on success Precision "Eircode", otherwise fall back to the Town with Precision "Town".
5. Show coordinates and precision on H-26.

Decision points:
- What happens when the lookup is down during creation — save with Town precision and retry later, or block?
- Is an Eircode change allowed to overwrite a "Confirmed on site" position? (The field-captured position is more accurate.)
- Is the lookup called from the server only? The stories say the tablet never geocodes.

Delegable slivers:
- **Lookup adapter** — Implement an adapter for the chosen Eircode service behind an interface `lookup(eircode) → coordinates | not found | unavailable`, with a test double. Do not call it from any handler.
- **Precision display** — Show coordinates with their Precision label on the H-26 Location record from the stored value. Do not change how coordinates are set.

---

### T-2.5.1-S — Test scenarios for replacing or retiring a main contact

**Owner** — Scenario Review
**Gates** — T-2.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for replacing or retiring a Main Contact (task
T-2.5.1). Read plan_docs/stories/customer-directory.md US-004. Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Start with characterisation scenarios that pin T-2.4.1's current
Main-contact behaviour, then cover S1–S4 and S6, then derivable edges: the
replacement is Inactive, the contact is Main at five Locations, unlinking a
non-Main contact. Mark undecided cases as "Needs a decision". Write no test
code; change no files.
```

---

### T-2.5.1 — Replace or retire a main contact without leaving a gap

**Parent story**

> As a Head Office User, I want removing a Main Contact to require a replacement, or to mark them Inactive with the gap flagged, so that no shop is left with nobody to ask for.
>
> Acceptance criteria:
> - Unlinking Mary, Main at Rathdrum, asks for a replacement from Rathdrum's contacts or a new one; choosing Sean Byrne makes him Main and unlinks Mary (S1)
> - "No replacement yet" marks Mary Inactive at all her Locations, keeps her linked, and flags Rathdrum "Main contact inactive — replacement needed" (S2)
> - Mary Main at Rathdrum and Arklow, marked Inactive, flags both (S3)
> - Setting Sean Byrne as Main at Rathdrum clears its flag; Mary stays Inactive (S4)
> - Marking Mary Active again makes her a normal Contact who does not regain Main (S6)

**Slice** — Removing a main contact forces a choice between a named replacement and marking them inactive, which flags every shop they were main at until someone is named.
**Spec source** — Customer Directory US-004 S1–S4, S6; uxdocs 02 H-27 (H27.1)
**Depends on** — T-2.4.1
**Pattern to follow** — T-2.4.1
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies T-2.4.1's Main-contact behaviour, so characterisation comes first; the story enumerates the edge cases.

**Provisional commit message**

```
feat(customers): require a replacement or a flag when a main contact goes

- "Nobody" is worse than "Mary (inactive)", so a Location never loses its
  Main without the gap being flagged for head office
- Reactivation does not restore Main, because the shop may have moved on
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies established behaviour behind a characterisation pass.

**Agent prompt**

```
Role: You are adding main-contact replacement and retirement to the Field
Sales Management System.

Context:
- Slice: removing a main contact forces a choice between a named replacement
  and marking them inactive, which flags every shop they were main at until
  someone is named.
- Specs: plan_docs/stories/customer-directory.md US-004;
  plan_docs/uxdocs/02-head-office.md H-27 (H27.1: the replacement question
  appears inline when unlinking, with three choices: an existing contact,
  "Add a new contact", "No replacement yet — mark Mary inactive").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — contacts from
  T-2.4.1, which currently blocks unlinking a Main.
- Pattern to follow: T-2.4.1.

Acceptance criteria:
1. Unlinking Mary Walsh, Main at Rathdrum, asks who replaces her; choosing
   Sean Byrne makes him Main and unlinks Mary.
2. "No replacement yet" marks Mary Inactive at all her Locations, keeps her
   linked, and flags Rathdrum "Main contact inactive — replacement needed".
3. If Mary is Main at Rathdrum and Arklow, marking her Inactive flags both.
4. Setting Sean Byrne as Main at Rathdrum clears Rathdrum's flag; Mary stays
   Inactive.
5. Marking Mary Active again makes her a normal Contact; she does not regain
   Main anywhere.
6. The flag is queryable for a later gap list (E17), per Location.

Constraints:
- Use the project's existing conventions and test framework.
- Contacts are never deleted.
- No tests of framework internals or trivial members.
- This task opts in to a "replacement needed" flag on Locations (or an
  equivalent derived query — choose and justify at the checkpoint).

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-2.4.1's Main-contact behaviour; stop and show them passing, with your
choice of stored flag versus derived query. Resume only on "Continue T-2.5.1".

Steps: 1. characterisation tests; 2. replacement and retirement rules in
the model; 3. flag or query; 4. inline replacement question on H-27;
5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-2.5.1-S; do not design
your own.

Definition of done: removing a main contact forces a choice between a named
replacement and marking them inactive, which flags every shop they were main
at until someone is named.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: tablet display (T-2.5.2), gap lists (E17), suspending a
customer's online login (E25).
```

**Checkpoint**

Produces before pausing — characterisation tests of T-2.4.1, passing, and the choice between a stored flag and a derived query.
Human reviews — Does retiring a contact flag every affected Location, with no path that clears the flag without a new Main?
Resume trigger — `Continue T-2.5.1`

---

### T-2.5.2 — Show an inactive main contact on the tablet

**Parent story**

> As a Head Office User, I want removing a Main Contact to require a replacement, or to mark them Inactive with the gap flagged, so that no shop is left with nobody to ask for.
>
> Acceptance criteria:
> - A rep opening Rathdrum after syncing sees "Main contact: Mary Walsh (inactive) — replacement needed" (S5)

**Slice** — After syncing, a rep opening a shop whose main contact is inactive sees "Main contact: Mary Walsh (inactive) — replacement needed" in the shop's identity block.
**Spec source** — Customer Directory US-004 S5; uxdocs 01 T-05 (T5.2)
**Depends on** — T-2.5.1, T-4.2.2
**Pattern to follow** — T-4.2.2 (tablet Location screen)
**Ownership** — Impl: Agent-Autonomous | Test: Agent-Autonomous | Complexity: S | Confidence: H
  (inferred) — one field on a settled screen; Blast Radius Low.

**Provisional commit message**

```
feat(tablet): show an inactive main contact on the Location screen

- Reps are greeted by strangers when the office still names the old owner;
  showing the gap tells them to ask and report back
```

**Capability class** — Fast mid-tier · effort: Anthropic off / OpenAI medium / Google low
One field on a settled screen.

**Agent prompt**

```
Role: You are showing a Location's main contact status on the rep's tablet in
the Field Sales Management System.

Context:
- Slice: after syncing, a rep opening a shop whose main contact is inactive
  sees "Main contact: Mary Walsh (inactive) — replacement needed" in the
  shop's identity block.
- Specs: plan_docs/stories/customer-directory.md US-004 S5;
  plan_docs/uxdocs/01-tablet-day.md T-05 (DECISION T5.2: the inactive main
  contact appears in the identity block, not a warnings area).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — snapshot (T-4.1.1),
  Location screen (T-4.2.2), flag (T-2.5.1).
- Pattern to follow: T-4.2.2's identity block.

Acceptance criteria:
1. With Mary Walsh Inactive and Rathdrum flagged, after sync the rep sees
   "Main contact: Mary Walsh (inactive) — replacement needed".
2. With an active Main, the rep sees "Main contact: <name>".
3. The status comes from the snapshot and is correct offline.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot changes follow T-4.1.1's versioning; add a field, change nothing
  else.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".

Steps: 1. snapshot field for main contact status; 2. identity block text;
3. tests.

Test expectations: select scenarios yourself from the criteria.

Definition of done: after syncing, a rep opening a shop whose main contact
is inactive sees "Main contact: Mary Walsh (inactive) — replacement needed"
in the shop's identity block.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: editing contacts on the tablet, online ordering set-up from
the contact (E25).
```

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | CD-007 (T-2.1.1, T-2.1.2; S3 → E16), CD-009 (T-2.2.1), CD-001 (T-2.3.1, T-2.3.2), CD-003 (T-2.4.1), CD-004 (T-2.5.1, T-2.5.2) |
| Every task satisfies the three slice criteria | Pass | 8 of 8 name an observable outcome |
| Every task carries a tier with a rationale citing dimensions | Pass | 8 of 8 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-2.3.2 (MI-07), T-2.1.1 (MI-16) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-2.5.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | T-2.1.2, T-2.2.1, T-2.5.2 are Low or Medium |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | No such task in this epic |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-2.1.1, T-2.3.1, T-2.4.1, T-2.5.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-2.3.2 (downgraded, stated) |
| Every scenario task precedes the task it gates | Pass | 5 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-06, 07, 08, 16 |
