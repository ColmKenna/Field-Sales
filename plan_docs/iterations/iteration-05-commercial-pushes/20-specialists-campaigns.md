# E20 — Specialists and campaigns

**Iteration** — 5, Commercial pushes
**Outcome** — A manager can attach specialists to the shops that match a rule, and launch a campaign of one-off visits across many shops in one flow that routes each visit to the right specialist or owner; reps record the outcome on the call; and the manager follows and adjusts the campaign as one piece of work.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Coverage Management US-006 — Attach a Specialist to Locations by scope | Should | S1, S2, S4 (reporting flag → E28), S5, CV006-A–G (S3 refined by CV006-E–G; single-condition scope superseded) |
| 2 | Visit Planning US-011 — Create a Visit Campaign | Must | S1, S1b, S2, S4, S5, VP011-A–J (S3 → E14) |
| 3 | Visit Planning US-013 — Record a Campaign Outcome on a Call | Must | S1–S5 |
| 4 | Visit Planning US-012 — Track and change a campaign | Should | S1–S4 (S3b with T-20.1.3), VP012-A–F |
| 5 | Visit Planning US-005 — See day load and over-limit warnings (part) | Should | S4, and S5's campaign duration (the rest → E13, E15) |

Also delivered here: Rep at a Location US-006 S6, S7 (the campaign outcome on the tablet's Call screen), Coverage Management US-002's specialists line (deferred from E3) and US-008's specialist scopes (deferred from E16).

**Exit criterion** — A manager can give a rep a specialist scope built from Customer, Location Profile, Brand and Area conditions, seen as a sentence with its match count, which Locations join and leave automatically; a Brand condition adds its products to the specialist's Order Pad. A manager can create a campaign through Filter, Review, Details and Confirm, with each visit routed to the matching specialist, the Primary Rep or a chosen rep (giving unassigned Locations a permanent Primary Rep). Reps record campaign outcomes on the call offline, and completing outcomes close the visit. The campaign detail shows progress, outcomes and each rep's share, and lets the manager cancel a visit or extend the remaining visits.

**Capability-class stamp** — Frontier + extended reasoning for scope evaluation and routing (T-20.1.1, T-20.2.2); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [coverage-management.md](../../stories/coverage-management.md) (US-002, US-006, US-008, glossary), [visit-planning.md](../../stories/visit-planning.md) (US-005, US-011, US-012, US-013), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-006 S6, S7), [05-manager.md](../../uxdocs/05-manager.md) (M-03, M-04, M-07, M-10), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (Call screen).

---

### T-20.1.1-S — Test scenarios for specialist scopes

**Owner** — Human-Led
**Gates** — T-20.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "Brian — Brand: SunCo": which Locations does a Brand-only scope select — every Location with a Primary Rep (S1) — and does an unassigned Location get the specialist?
- "Aoife — Customer: Hickey's Pharmacies" covers all 12 and any Location added later (S2).
- Profile "Customer campaign X" AND Area "Wicklow" selects only those (CV006-A); a Location gaining the profile joins with no action (CV006-C); one losing it, or moving County (T-16.4.1), leaves.
- Advanced AND/OR with grouping (CV006-B): an empty group, a scope matching nothing, a scope matching everything.
- Area as Region, County or Town — a Town moved to another County changes an Area: County scope.
- An archived Brand keeps its scopes but isn't offered for new ones (glossary).
- Are automatic joins and leaves written to Assignment History, or only the scope's creation and removal?
- A closed Location (T-17.2.1) still in scope?
- The sentence and match count before saving: count as of now, including archived or closed Locations?

---

### T-20.1.1 — Attach specialists to the shops a rule matches

**Parent story**

> As a Sales Manager, I want to attach a rep to Locations by Customer, Location Profile or Brand without making them the owner so that a brand push or key account gets a specialist alongside the Primary Rep.
>
> Acceptance criteria:
> - "Brian — Brand: SunCo" shows Brian as Specialist for SunCo on every Location with a Primary Rep (S1)
> - "Aoife — Customer: Hickey's Pharmacies" makes Aoife Specialist on all 12 Hickey's Locations and any added later (S2)
> - Profile "Customer campaign X" and Area "Wicklow" makes Brian Specialist on every Location holding that profile in Wicklow, and only those; before saving I see the scope as a sentence with its match count (CV006-A)
> - "Advanced" combines conditions with AND and OR, grouped (CV006-B)
> - A Location in Wicklow gaining that profile gets Brian as Specialist with no further action (CV006-C)
> - The Location shows "Specialists: Brian (Brand: SunCo)" (Coverage US-002 S1); the rep's page lists their specialist scopes (Coverage US-008 S1)

**Slice** — A manager gives a rep a specialist scope built from Customer, Location Profile, Brand and Area conditions, all required by default or grouped with AND/OR in Advanced, sees it as a sentence with the number of shops it matches before saving, and from then on matching shops show the specialist on their coverage and join or leave as they change.
**Spec source** — Coverage US-006 S1, S2, CV006-A–C, glossary (Specialist Assignment, Scope, Brand, Assignment History); US-002 S1 specialists line; US-008 S1 specialist line; uxdocs 05 M-10 (M10.1)
**Depends on** — T-3.1.1, T-3.2.1, T-12.2.1, T-1.5.1, T-2.1.1, T-16.3.1
**Pattern to follow** — T-3.1.1 (rule-based ownership resolution), T-3.1.2 (preview before saving)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — a live rule engine over Locations that feeds routing and the tablet's pad (Blast Radius High); Edge-Case Discovery High (joins and leaves as data changes).

**Provisional commit message**

```
feat(coverage): attach specialists to the shops a rule matches

- The common need is a profile within an area, which a single condition
  can't express; conditions combine with AND, with an advanced mode for
  the rare rule that needs OR
- Scopes stay rules, so a shop that starts to match joins without anyone
  remembering to add it
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A live rule set that routing and the pad depend on.

**Work package**

Increments:
1. Scope model: a condition tree (Customer, Location Profile, Brand, Area as Region/County/Town) with AND by default and grouped AND/OR.
2. Evaluator: does a Location match a scope today? Brand conditions match Locations per the agreed S1 rule; shared with routing (T-20.2.2) and the pad (T-20.1.2).
3. Membership as data changes: decide between evaluating on read and materialising on change; either way, Location, profile, Customer and geography changes are reflected without manager action.
4. M-10 builder: default conditions, Advanced, the sentence and live match count before saving; remove a scope (T-20.1.3 handles its visits).
5. Coverage and history: "Specialists: Brian (Brand: SunCo)" on the Location page (T-3.2.1), scopes on the rep's page (T-16.3.1), Assignment History entries per the agreed rule.

Decision points:
- Materialise membership or evaluate on read (performance of routing and snapshot versus freshness).
- Which scope changes enter Assignment History.
- Whether unassigned or closed Locations take specialists.

Delegable slivers:
- **Scope sentence** — Render a scope's condition tree as one sentence in the M10.1 style ("Brian · Profile: Customer campaign X · in Wicklow"), including Advanced groups. Pure function; no evaluation.
- **Coverage lines** — Once the evaluator exists, show "Specialists: …" with each scope's sentence on the Location coverage page and the specialist scopes on the rep's page, replacing "Specialist: none". Characterisation tests of both pages first.

---

### T-20.1.2-S — Test scenarios for brand-scope products on the specialist's Order Pad

**Owner** — Scenario Review
**Gates** — T-20.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for adding brand-scope products to a
specialist's Order Pad (task T-20.1.2). Read
plan_docs/stories/coverage-management.md US-006 S1, S4, CV006-D and glossary
(Scope, Brand), and plan_docs/uxdocs/05-manager.md M-10. Output one line per
scenario as Should_Outcome_When_Condition, then "→" and a one-line intent.
Start with characterisation scenarios pinning T-9.8.1's pad (assigned Ranges
plus unranged; "Outside your ranges"). Cover S1, S4 and CV006-D, then
derivable edges: a Brand condition combined with Area (does the pad grow
everywhere or only at matching Locations?), an alternative brand, a restricted
product in the brand, a scope removed. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-20.1.2 — Add a specialist's brand products to their Order Pad

**Parent story**

> As a Sales Manager, I want to attach a rep to Locations by Customer, Location Profile or Brand without making them the owner so that a brand push or key account gets a specialist alongside the Primary Rep.
>
> Acceptance criteria:
> - Brian's Order Pad includes every product belonging to SunCo, primary or alternative brand (S1)
> - "SunCo/GlowCo SPF30" (Primary SunCo, Alternative GlowCo) is seen by both Brian (SunCo) and Ciara (GlowCo) (S4)
> - A scope with no Brand condition adds no products to the specialist's Order Pad (CV006-D)

**Slice** — After syncing, a specialist whose scope has a Brand condition finds every product of that brand, primary or alternative, on their Order Pad, while scopes without a Brand condition add nothing.
**Spec source** — Coverage US-006 S1, S4, CV006-D; uxdocs 05 M-10 ("Follows from the source")
**Depends on** — T-20.1.1, T-9.8.1, T-4.1.1, T-4.4.1
**Pattern to follow** — T-9.8.1 (pad from assigned Ranges)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the Order Pad's membership (characterisation first); rules are stated.

**Provisional commit message**

```
feat(tablet): add a specialist's brand products to their order pad

- Only a brand condition adds products; other conditions route visits,
  so a profile-and-area specialist isn't handed a catalogue they don't sell
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Pad membership behind a characterisation pass.

**Agent prompt**

```
Role: You are extending the tablet's Order Pad with specialist brand products
in the Field Sales Management System.

Context:
- Slice: after syncing, a specialist whose scope has a Brand condition finds
  every product of that brand, primary or alternative, on their Order Pad,
  while scopes without a Brand condition add nothing.
- Specs: plan_docs/stories/coverage-management.md US-006 S1, S4, CV006-D and
  glossary (Scope, Brand); plan_docs/uxdocs/05-manager.md M-10.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — scope evaluator
  (T-20.1.1), pad membership (T-9.8.1), snapshot (T-4.1.1), restricted
  products (T-4.4.1).
- Pattern to follow: T-9.8.1's pad membership.

Acceptance criteria:
1. Brian (Brand: SunCo) has every SunCo product on his pad, primary or
   alternative brand.
2. "SunCo/GlowCo SPF30" appears on both Brian's (SunCo) and Ciara's (GlowCo)
   pads.
3. A scope with no Brand condition adds no products.
4. Restricted products still need the rep's permission (T-4.4.1).
5. Removing the scope removes its products at the next sync, unless they're
   on the pad for another reason.
6. A Brand combined with other conditions behaves as agreed in T-20.1.2-S.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot changes go through T-4.1.1's versioning.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-9.8.1's pad membership; stop and show them passing. Resume only on
"Continue T-20.1.2".

Steps: 1. characterisation tests; 2. brand products in the snapshot;
3. pad membership; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-20.1.2-S; do not design
your own.

Definition of done: after syncing, a specialist whose scope has a Brand
condition finds every product of that brand, primary or alternative, on their
Order Pad, while scopes without a Brand condition add nothing.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the brand-overlap flag on performance reporting (E28).
```

**Checkpoint**

Produces before pausing — characterisation tests of the pad's membership, passing.
Human reviews — Do the tests pin today's pad exactly, including "Outside your ranges"?
Resume trigger — `Continue T-20.1.2`

---

### T-20.2.1-S — Test scenarios for the campaign wizard

**Owner** — Scenario Review
**Gates** — T-20.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for creating a Visit Campaign through the
four-step wizard (task T-20.2.1). Read plan_docs/stories/visit-planning.md
US-011 S1, S4, S5 and VP011-A–D, and plan_docs/uxdocs/05-manager.md M-03
(M3.1, M3.2). Output one line per scenario as Should_Outcome_When_Condition,
then "→" and a one-line intent. Cover the story scenarios, then derivable
edges: going back after unticking rows, changing the filter after review
(what happens to exclusions?), a window in the past, a duplicate campaign
name. Routing and unassigned Locations are T-20.2.2's; leave them out. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-20.2.1 — Create a campaign through Filter, Review, Details and Confirm

**Parent story**

> As a Sales Manager, I want to create One-off Visit Dues for a whole Customer or Location Profile at once, reviewing the list first, so that a range launch reaches every relevant shop without 40 separate entries.
>
> Acceptance criteria:
> - Filtering "Large pharmacy" in Wicklow and Wexford lists 40 Locations, all ticked; unticking 2, setting window 5–30 October 2026, Due Reason "Autumn range launch", Duration 60 min, name and Outcome List gives "Create 38 visits across 5 reps?" and 38 Visit Dues in the campaign (S1)
> - No completing outcome: "At least one outcome must complete the visit" (S4)
> - A filter matching 0: "No Locations match" and I can't create (S5, VP011-C)
> - Four labelled steps "Filter", "Review", "Details", "Confirm" (VP011-A); moving between completed steps preserves everything (VP011-B)
> - Confirm states the visit count, rep count and number routed to specialists before creation (VP011-D)

**Slice** — A manager filters Locations by Customer, Profile, Region and optionally Brand, reviews and unticks matches, sets the campaign's name, window, reason, duration and outcome list, and confirms a stated number of visits across reps — creating a campaign of One-off Visit Dues, with every step keeping its entries when revisited.
**Spec source** — Visit Planning US-011 S1, S4, S5, VP011-A–D; uxdocs 05 M-03 (M3.1, M3.2)
**Depends on** — T-14.4.1, T-14.3.1, T-12.2.1, T-3.1.1
**Pattern to follow** — T-14.4.1 (one-off Visit Dues), T-3.1.2 (preview before saving)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: C | Confidence: M
  (inferred) — multi-step state and bulk creation across reps; the rules are stated and the screen settled.

**Provisional commit message**

```
feat(visits): create a campaign of one-off visits in four steps

- The candidate set, the details and the routing depend on each other, so
  each gets its own step, and going back never loses what was entered
- Confirm states what will be created before anything is
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A multi-step creation flow with a review pause.

**Agent prompt**

```
Role: You are building Visit Campaign creation (M-03) for the Field Sales
Management System's manager website.

Context:
- Slice: a manager filters Locations by Customer, Profile, Region and
  optionally Brand, reviews and unticks matches, sets the campaign's name,
  window, reason, duration and outcome list, and confirms a stated number of
  visits across reps — creating a campaign of One-off Visit Dues, with every
  step keeping its entries when revisited.
- Specs: plan_docs/stories/visit-planning.md US-011 (S1, S4, S5, VP011-A–D)
  and glossary (Visit Campaign, Outcome List); plan_docs/uxdocs/05-manager.md
  M-03 (M3.1, M3.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — one-off Visit Dues
  (T-14.4.1), reason types (T-14.3.1), profiles (T-12.2.1), effective owner
  (T-3.1.1).
- Pattern to follow: T-14.4.1's one-off creation.

Acceptance criteria:
1. Four labelled steps: Filter, Review, Details, Confirm; each states its
   result before advancing (match count; selected/excluded counts; details
   valid; totals).
2. Filtering "Large pharmacy" in Wicklow and Wexford lists 40, all ticked;
   unticking 2 leaves 38.
3. Details takes name, Due Window, Reason Type and text, Duration and an
   Outcome List where each outcome completes or doesn't; with no completing
   outcome: "At least one outcome must complete the visit".
4. A zero-match filter shows "No Locations match" and can't advance.
5. Moving back and forward preserves filters, selections and details.
6. Confirm reads e.g. "Create 38 visits across 5 reps?"; confirming creates
   38 Visit Dues in the campaign, each carrying its reason, duration and
   outcomes, delivered to reps at their next sync.
7. Routing is to each Location's Primary Rep until T-20.2.2 adds specialists
   and unassigned choices.

Constraints:
- Use the project's existing conventions and test framework.
- Creation is one transaction: all visits or none.
- No tests of framework internals or trivial members.
- This task opts in to the campaign tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the four steps work end to end and create visits that reach
a tablet, stop and show it. Resume only on "Continue T-20.2.1".

Steps: 1. campaign storage; 2. Filter; 3. Review; 4. Details; 5. Confirm and
create; 6. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-20.2.1-S; do not design
your own.

Definition of done: a manager filters Locations by Customer, Profile, Region
and optionally Brand, reviews and unticks matches, sets the campaign's name,
window, reason, duration and outcome list, and confirms a stated number of
visits across reps — creating a campaign of One-off Visit Dues, with every
step keeping its entries when revisited.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: specialist routing and unassigned Locations (T-20.2.2), outcomes
on the call (T-20.3.1), campaign detail (T-20.4.1).
```

**Checkpoint**

Produces before pausing — the four steps end to end, creating visits that reach a tablet.
Human reviews — Does each step say what it will produce, and does Back never lose work?
Resume trigger — `Continue T-20.2.1`

---

### T-20.2.2-S — Test scenarios for campaign routing and permanent owners

**Owner** — Human-Led
**Gates** — T-20.2.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- Brand-linked campaign, Brian SunCo Specialist on 10 of 38: "Create 38 visits across 6 reps (10 to specialists)" (S1b).
- Ciara (Brand: SunCo) and Brian (Profile · Wicklow) both at a Location: Ciara preselected, "also matches Brian" (CV006-E); two matching the link, or no link and two present: "2 specialists match — choose", can't advance (CV006-F); none: Primary Rep (CV006-G).
- Is a campaign linked to one Brand only? What does "matches the campaign's link" mean for a Brand condition combined with Area?
- 3 unassigned matches: listed separately, unticked by default (S2); ticking one requires a rep, and Review states that rep becomes the permanent Primary Rep (VP011-E); bulk default with row overrides (VP011-I, J).
- A Location with a Primary Rep, another rep chosen: only this visit moves (VP011-F).
- Confirm lists permanent assignments separately (VP011-G) and saves them atomically with the campaign and their Assignment History (VP011-H). What if an ownership change lands between Review and Confirm?
- Does the permanent assignment trigger T-14.5.1's handover logic (there are no open visits for an unassigned Location)?

---

### T-20.2.2 — Route each campaign visit to a specialist, the owner or a chosen rep

**Parent story**

> As a Sales Manager, I want to create One-off Visit Dues for a whole Customer or Location Profile at once, reviewing the list first, so that a range launch reaches every relevant shop without 40 separate entries.
>
> Acceptance criteria:
> - Linked to Brand SunCo with Brian SunCo Specialist on 10 of 38: 10 go to Brian, 28 to Primary Reps, "Create 38 visits across 6 reps (10 to specialists)" (S1b; Coverage US-006 S3)
> - 3 matched Locations with no responsible rep are listed as "3 have no assigned rep", unticked by default (S2)
> - One matching specialist is preselected with others noted (CV006-E); several: "2 specialists match — choose", no advance until chosen (CV006-F); none: the Primary Rep (CV006-G)
> - Choosing a rep for an unassigned Location states it will become the permanent Primary Rep (VP011-E); for an owned Location, only the campaign visit moves (VP011-F)
> - Confirm lists permanent assignments separately (VP011-G); they save atomically with the campaign and Assignment History (VP011-H)
> - A bulk Primary Rep default applies to all selected unassigned rows, overridable per row, with counts using each row's effective rep (VP011-I, J)

**Slice** — In campaign Review, each visit starts with the specialist matching the campaign's link, or the manager chooses among several, or it falls to the Primary Rep; unassigned Locations can be given a rep (in bulk or per row) who becomes their permanent Primary Rep on confirmation; and Confirm counts specialist routing and permanent assignments separately before saving everything together.
**Spec source** — Visit Planning US-011 S1b, S2, VP011-E–J; Coverage US-006 S3, CV006-E–G; uxdocs 05 M-03 (M3.3, M3.4), M-10 (M10.2)
**Depends on** — T-20.2.1, T-20.1.1, T-3.1.1
**Pattern to follow** — T-3.1.1 (direct assignment with Assignment History)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: M
  (inferred) — routing across scopes plus a permanent ownership change inside a bulk transaction (Blast Radius High); Edge-Case Discovery High.

**Provisional commit message**

```
feat(visits): route campaign visits to specialists, owners or a chosen rep

- The specialist matching the campaign's link is preselected; when the
  link doesn't decide, the manager does, rather than a hidden precedence
- Giving an unassigned shop a campaign rep also makes them its owner, so
  the shop stops being silently uncovered; an existing owner is never
  replaced by a campaign
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Routing plus ownership in one transaction.

**Work package**

Increments:
1. Campaign link (Brand, optional) in Filter; candidate specialists per Location from T-20.1.1's evaluator.
2. Review routing column: none → Primary Rep; one → preselected with "also matches …"; several → "2 specialists match — choose" and a hard stop; bulk selection to resolve many rows.
3. Unassigned rows: listed separately, unticked by default; choosing a rep states the permanent consequence; bulk default with row overrides.
4. Confirm: "38 campaign visits across 6 reps · 10 to specialists · 3 Locations will also receive a Primary Rep", listing the three.
5. Atomic save: campaign, visits and direct Primary Rep assignments with their Assignment History; re-check ownership at save and refuse on change.

Decision points:
- The meaning of "matches the link" for combined conditions.
- What happens if a Location's owner changes between Review and Confirm.
- Whether the routing chosen is stored per visit for T-20.1.3 (it must be, to find visits routed through a scope).

Delegable slivers:
- **Confirm summary** — Given the effective rows (rep, routed-to-specialist flag, permanent assignment flag), render Confirm's counts and the list of permanent assignments per M3.3–M3.4. No routing logic.
- **Bulk default control** — Implement the bulk Primary Rep default for unassigned rows with per-row override, keeping effective values per row. No saving.

---

### T-20.1.3-S — Test scenarios for removing a specialist with open campaign visits

**Owner** — Scenario Review
**Gates** — T-20.1.3
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for removing a specialist assignment that has
open campaign visits (task T-20.1.3). Read
plan_docs/stories/coverage-management.md US-006 S5,
plan_docs/stories/visit-planning.md US-012 S3b, and T-14.5.1's handover in
plan_docs/iterations/iteration-03-visit-rhythm/14-manager-view-one-offs-handover.md.
Output one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover S5 and S3b, then derivable edges: a Location leaving
the scope because its data changed, a visit already scheduled on the
specialist's tablet, completed visits. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-20.1.3 — Put a removed specialist's open campaign visits into Handover Pending

**Parent story**

> As a Sales Manager, I want to attach a rep to Locations by Customer, Location Profile or Brand without making them the owner so that a brand push or key account gets a specialist alongside the Primary Rep.
>
> Acceptance criteria:
> - Removing Brian's SunCo assignment makes open campaign visits routed to him via that scope Handover Pending (S5)
> - With 6 campaign visits open, those 6 become Handover Pending for the manager to Move to each Primary Rep or Leave with Brian (Visit Planning US-012 S3b)

**Slice** — When a manager removes a specialist's scope, the open campaign visits routed through it become Handover Pending, and the manager moves each to the Location's Primary Rep or leaves it with the specialist, as in any handover.
**Spec source** — Coverage US-006 S5; Visit Planning US-012 S3b
**Depends on** — T-20.1.1, T-20.2.2, T-14.5.1
**Pattern to follow** — T-14.5.1 (Move/Leave handover)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — reuses the reviewed handover; the trigger is new.

**Provisional commit message**

```
feat(visits): hand over a removed specialist's open campaign visits

- Removing a scope shouldn't silently strand or reassign work already
  planned; the manager decides, as with any change of owner
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A new trigger on a reviewed flow.

**Agent prompt**

```
Role: You are handing over open campaign visits when a specialist scope is
removed, in the Field Sales Management System.

Context:
- Slice: when a manager removes a specialist's scope, the open campaign
  visits routed through it become Handover Pending, and the manager moves
  each to the Location's Primary Rep or leaves it with the specialist, as in
  any handover.
- Specs: plan_docs/stories/coverage-management.md US-006 S5;
  plan_docs/stories/visit-planning.md US-012 S3b.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — scopes (T-20.1.1),
  campaign routing records (T-20.2.2), handover (T-14.5.1).
- Pattern to follow: T-14.5.1's Move/Leave handover.

Acceptance criteria:
1. Removing Brian's SunCo scope makes each open campaign visit routed to him
   through it Handover Pending.
2. The manager can Move each to the Location's Primary Rep or Leave it with
   Brian.
3. Completed and cancelled visits are unchanged.
4. Visits routed to Brian through another scope that still matches are
   unaffected.
5. A Location leaving the scope because its data changed behaves as agreed
   in T-20.1.3-S.

Constraints:
- Use the project's existing conventions and test framework.
- Reuse T-14.5.1's Handover Pending; no second handover mechanism.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after removal produces Handover Pending visits end to end, stop
and show it. Resume only on "Continue T-20.1.3".

Steps: 1. find visits routed through the scope; 2. mark Handover Pending;
3. Move/Leave; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-20.1.3-S; do not design
your own.

Definition of done: when a manager removes a specialist's scope, the open
campaign visits routed through it become Handover Pending, and the manager
moves each to the Location's Primary Rep or leaves it with the specialist, as
in any handover.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: specialist scopes in Impact Preview (they follow scope, not
ownership — Coverage scope note).
```

**Checkpoint**

Produces before pausing — a scope removal producing Handover Pending visits, end to end.
Human reviews — Are exactly the right visits pending, and does Move/Leave behave as in T-14.5.1?
Resume trigger — `Continue T-20.1.3`

---

### T-20.3.1-S — Test scenarios for campaign outcomes on the call

**Owner** — Human-Led
**Gates** — T-20.3.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- "Pitched — declined" completes the campaign visit and the recurring visit too (VP-013 S1). Does any Call complete the recurring visit anyway (T-13.1.1), so S1 only restates it?
- "Follow up" records the outcome and keeps the campaign visit open (S2).
- No open campaign: no Campaigns section (S3); two open campaigns: two outcome choices.
- Save with no outcome: "Autumn range launch: no outcome" in the save review; the visit stays open (S4); added later only on a Follow-up Call (S5).
- The campaign visit routed to Brian but the call made by the Primary Rep: does the section show, and does the outcome complete Brian's visit?
- Offline from the snapshot; a campaign cancelled or extended since the snapshot: judged as captured (BR-NEW-006)?
- A phone call (T-5.3.2): does it show the Campaigns section?

---

### T-20.3.1 — Record a campaign outcome on the call

**Parent story**

> As a Field Salesperson, I want the Call screen to show any open campaign at this Location and let me record its outcome so that the campaign visit counts only when I actually covered it.
>
> Acceptance criteria:
> - With "Autumn range launch" open at Murphy's Pharmacy, a Call with "Pitched — declined" completes the campaign visit and the recurring visit (S1)
> - "Follow up" is recorded and the campaign visit stays open (S2)
> - No open campaign: no Campaigns section (S3)
> - No outcome chosen: the save review shows "Autumn range launch: no outcome", I can go back or save, and the visit stays open (S4)
> - An outcome for a call saved without one is recorded on a Follow-up Call (S5)
> - The Campaigns section shows the campaign name, its Due Reason and its outcomes (Rep at a Location US-006 S6, S7)

**Slice** — On the tablet's Call screen, offline, any open campaign at the Location shows with its reason and outcomes; a completing outcome closes the campaign visit, a non-completing one records it and leaves the visit open, a forgotten one is named in the save review, and a Follow-up Call can add it later.
**Spec source** — Visit Planning US-013 S1–S5 and edge cases; Rep at a Location US-006 S6, S7
**Depends on** — T-20.2.1, T-5.3.1, T-5.7.1, T-13.1.1, T-14.4.2, T-4.1.1
**Pattern to follow** — T-14.4.2 (completing a one-off by call), T-5.3.1 (save review)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — completion interacts with recurring and one-off rules and with offline capture (Edge-Case Discovery High).

**Provisional commit message**

```
feat(tablet): record a campaign outcome on the call

- A campaign visit counts only when the rep says what happened, so a
  call that never mentioned the launch doesn't close it
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Completion rules against a human oracle.

**Agent prompt**

```
Role: You are adding campaign outcomes to the tablet's Call screen in the
Field Sales Management System.

Context:
- Slice: on the tablet's Call screen, offline, any open campaign at the
  Location shows with its reason and outcomes; a completing outcome closes
  the campaign visit, a non-completing one records it and leaves the visit
  open, a forgotten one is named in the save review, and a Follow-up Call can
  add it later.
- Specs: plan_docs/stories/visit-planning.md US-013 S1–S5 and edge cases;
  plan_docs/stories/rep-at-a-location-tablet.md US-006 S6, S7.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — campaigns and their
  visits (T-20.2.1), Call and save review (T-5.3.1), Follow-up Call (T-5.7.1),
  recurring completion (T-13.1.1), one-off completion (T-14.4.2), snapshot
  (T-4.1.1).
- Pattern to follow: T-14.4.2's completion by call.

Acceptance criteria:
1. With "Autumn range launch" open at Murphy's Pharmacy, the Call shows a
   Campaigns section with its name, Due Reason and outcomes Pitched —
   ordered, Pitched — declined, Follow up.
2. "Pitched — declined" completes the campaign visit and the recurring
   visit.
3. "Follow up" records the outcome and leaves the campaign visit open.
4. No open campaign: no Campaigns section. Two open campaigns: two outcome
   choices.
5. Saving with none chosen shows "Autumn range launch: no outcome" in the save
   review, with go back or save; the visit stays open.
6. A Follow-up Call can record the outcome later.
7. All of it works offline from the snapshot.

Constraints:
- Use the project's existing conventions and test framework.
- Outcomes upload with the call through T-4.1.2's upload.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond storing
  outcomes on calls.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after completion passes every agreed scenario, including the
recurring and one-off interactions, stop and show it. Resume only on
"Continue T-20.3.1".

Steps: 1. campaigns in the snapshot; 2. Campaigns section; 3. completion;
4. save review; 5. Follow-up Call; 6. tests.

Test expectations: implement exactly the scenarios agreed in T-20.3.1-S. You
are forbidden from designing your own test cases.

Definition of done: on the tablet's Call screen, offline, any open campaign
at the Location shows with its reason and outcomes; a completing outcome
closes the campaign visit, a non-completing one records it and leaves the
visit open, a forgotten one is named in the save review, and a Follow-up Call
can add it later.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–7 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: campaign detail and counts (T-20.4.1).
```

**Checkpoint**

Produces before pausing — completion passing every agreed scenario, including recurring and one-off interactions.
Human reviews — Does a campaign visit close only when an outcome that completes it is recorded, whoever made the call?
Resume trigger — `Continue T-20.3.1`

---

### T-20.4.1-S — Test scenarios for campaign detail

**Owner** — Scenario Review
**Gates** — T-20.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the campaign detail page (task T-20.4.1).
Read plan_docs/stories/visit-planning.md US-012 S1, S3, S4 and VP012-A–D, and
plan_docs/uxdocs/05-manager.md M-04 (M4.1–M4.3). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Cover the
story scenarios, then derivable edges: a visit with an outcome recorded twice
(Follow up then Ordered), a cancelled visit already scheduled on a tablet
(schedule conflict per T-15.5.1), a rep with no visits left. Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-20.4.1 — Follow a campaign's progress and outcomes, and cancel a visit

**Parent story**

> As a Sales Manager, I want to see a campaign's progress and outcome breakdown and change its deadline for all remaining visits so that I manage a launch as one thing.
>
> Acceptance criteria:
> - 38 visits, 22 completing and 3 Follow up: "22 of 38 done" and "15 ordered · 7 declined · 3 follow up · 13 not yet visited" (S1)
> - Cancelling the visit for a closed Location removes it from the rep's planner and counts "1 cancelled" (S3)
> - By rep shows each rep's done of due for the campaign (S4, VP012-B), with overall progress and outcomes still visible
> - The page opens on Overall (VP012-A)
> - Each outcome count filters the visit list, shown by text and state, not colour alone (VP012-C); selecting it again or "All visits" clears it, and headline counts stay campaign totals (VP012-D)

**Slice** — A manager opens a campaign on Overall to see progress, the outcome breakdown and the visit list, filters the list by any outcome count, switches to By rep for each rep's done of due, and cancels a visit so it leaves the rep's planner and counts as cancelled.
**Spec source** — Visit Planning US-012 S1, S3, S4, VP012-A–D; uxdocs 05 M-04 (M4.1–M4.3)
**Depends on** — T-20.2.1, T-20.3.1, T-15.5.1
**Pattern to follow** — T-14.1.1 (manager overview by rep)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — counts over stated rules; the cancel changes a rep's planner and may raise a conflict.

**Provisional commit message**

```
feat(visits): follow a campaign as one piece of work

- The campaign is the primary object, so it opens on overall progress;
  the per-rep view is a drill-down, not the default
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A detail page with a planner-changing action.

**Agent prompt**

```
Role: You are building the campaign detail page (M-04) for the Field Sales
Management System's manager website.

Context:
- Slice: a manager opens a campaign on Overall to see progress, the outcome
  breakdown and the visit list, filters the list by any outcome count,
  switches to By rep for each rep's done of due, and cancels a visit so it
  leaves the rep's planner and counts as cancelled.
- Specs: plan_docs/stories/visit-planning.md US-012 S1, S3, S4, VP012-A–D;
  plan_docs/uxdocs/05-manager.md M-04 (M4.1–M4.3).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — campaigns (T-20.2.1),
  outcomes (T-20.3.1), schedule conflicts (T-15.5.1), overview pattern
  (T-14.1.1).
- Pattern to follow: T-14.1.1.

Acceptance criteria:
1. The page opens on Overall with "22 of 38 done", the window, the outcome
   breakdown "15 ordered · 7 declined · 3 follow up · 13 not yet visited",
   and the visit list.
2. By rep shows each rep's done of due; progress and outcomes stay above it.
3. Selecting Ordered, Declined, Follow up, Not yet visited or Cancelled
   filters the list; the selection is shown by text and state, not colour
   alone; selecting it again or "All visits" clears it; headline counts don't
   change while filtered.
4. Cancelling a visit removes it from the rep's planner at their next sync
   and counts "1 cancelled".
5. A cancel that overtakes a rep's offline move raises a schedule conflict
   (T-15.5.1).
6. The page is reached from a list of campaigns (inferred; no story defines
   the list — keep it to name, window and progress).

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope beyond the
  cancelled state.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after counts and filters match the agreed scenarios on seeded
data, stop and show them. Resume only on "Continue T-20.4.1".

Steps: 1. counts; 2. Overall; 3. filters; 4. By rep; 5. cancel; 6. campaign
list; 7. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-20.4.1-S; do not design
your own.

Definition of done: a manager opens a campaign on Overall to see progress,
the outcome breakdown and the visit list, filters the list by any outcome
count, switches to By rep for each rep's done of due, and cancels a visit so
it leaves the rep's planner and counts as cancelled.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: extending remaining visits (T-20.4.2), specialist removal
(T-20.1.3).
```

**Checkpoint**

Produces before pausing — counts and filters matching the agreed scenarios on seeded data.
Human reviews — Do the counts always add up to the campaign total, including cancelled?
Resume trigger — `Continue T-20.4.1`

---

### T-20.4.2-S — Test scenarios for extending remaining visits

**Owner** — Scenario Review
**Gates** — T-20.4.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for extending a campaign's remaining visits
(task T-20.4.2). Read plan_docs/stories/visit-planning.md US-012 S2,
VP012-E, VP012-F, and plan_docs/uxdocs/05-manager.md M-04 (M4.4). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover the story scenarios, then derivable edges: shortening rather
than extending, a new window over a rep's absence, an open visit already
scheduled on a tablet inside the old window. Mark undecided cases as "Needs a
decision". Write no test code; change no files.
```

---

### T-20.4.2 — Extend a campaign's remaining visits through a review

**Parent story**

> As a Sales Manager, I want to see a campaign's progress and outcome breakdown and change its deadline for all remaining visits so that I manage a launch as one thing.
>
> Acceptance criteria:
> - Extending the window to 13 November 2026 gives all 16 open visits the new window, seen by reps at next sync; completed visits are unchanged (S2)
> - "Extend remaining..." opens a review with every open visit selected, excludable, and the proposed window (VP012-E)
> - Applying moves only the selected open visits; excluded ones keep their dates; completed and cancelled are unchanged (VP012-F)

**Slice** — From a campaign, the manager opens "Extend remaining...", sees every open visit selected with the proposed new window, excludes any, and applies it so only the selected open visits move, reaching reps at their next sync.
**Spec source** — Visit Planning US-012 S2, VP012-E, VP012-F; uxdocs 05 M-04 (M4.4)
**Depends on** — T-20.4.1
**Pattern to follow** — T-13.5.2 (review and apply to many visits)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — bulk change to reps' visits, following the reviewed bulk pattern.

**Provisional commit message**

```
feat(visits): extend a campaign's remaining visits through a review

- Everything open is selected by default so the common case is one
  action, but a visit that shouldn't move can be left out first
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A bulk change with a review pause.

**Agent prompt**

```
Role: You are adding "Extend remaining..." to the campaign detail page (M-04)
of the Field Sales Management System.

Context:
- Slice: from a campaign, the manager opens "Extend remaining...", sees every
  open visit selected with the proposed new window, excludes any, and
  applies it so only the selected open visits move, reaching reps at their
  next sync.
- Specs: plan_docs/stories/visit-planning.md US-012 S2, VP012-E, VP012-F;
  plan_docs/uxdocs/05-manager.md M-04 (M4.4).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — campaign detail
  (T-20.4.1), bulk review pattern (T-13.5.2), schedule conflicts (T-15.5.1).
- Pattern to follow: T-13.5.2.

Acceptance criteria:
1. "Extend remaining..." opens a review listing every open visit, all
   selected, with the proposed new window.
2. Individual visits can be excluded.
3. Applying a window ending 13 November 2026 moves only the selected open
   visits; excluded ones keep their dates; completed and cancelled visits are
   unchanged.
4. Reps see the new window at their next sync.
5. A change overtaking a rep's offline move raises a schedule conflict
   (T-15.5.1).

Constraints:
- Use the project's existing conventions and test framework.
- The change is one transaction.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after an extension applies end to end and reaches a tablet, stop
and show it. Resume only on "Continue T-20.4.2".

Steps: 1. review; 2. exclusions; 3. apply; 4. sync; 5. tests from agreed
scenarios.

Test expectations: implement the scenarios agreed in T-20.4.2-S; do not design
your own.

Definition of done: from a campaign, the manager opens "Extend remaining...",
sees every open visit selected with the proposed new window, excludes any,
and applies it so only the selected open visits move, reaching reps at their
next sync.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: changing a campaign's other details after creation.
```

**Checkpoint**

Produces before pausing — an extension applied end to end and seen on a tablet.
Human reviews — Did only the selected open visits move?
Resume trigger — `Continue T-20.4.2`

---

### T-20.5.1-S — Test scenarios for a combined recurring and campaign visit's duration

**Owner** — Scenario Review
**Gates** — T-20.5.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a campaign visit's duration in the day load
(task T-20.5.1). Read plan_docs/stories/visit-planning.md US-005 S4, S5 and
edge cases. Output one line per scenario as Should_Outcome_When_Condition,
then "→" and a one-line intent. Start with characterisation scenarios
pinning T-13.4.1's day load. Cover S4 and S5, then derivable edges: two
campaigns at one Location, a campaign duration shorter than the default, the
campaign visit completed while the recurring one stays open. Mark undecided
cases as "Needs a decision". Write no test code; change no files.
```

---

### T-20.5.1 — Schedule a recurring and a campaign visit as one, at the longer duration

**Parent story**

> As a Field Salesperson, I want each day to show scheduled time against my Working Day so that I don't build a day I can't do.
>
> Acceptance criteria:
> - A campaign visit with Visit Duration 60 min at a Location whose default is 45 contributes 60 min plus Travel Allowance (S4)
> - Murphy's Pharmacy with a recurring visit (45 min) and an open campaign visit (60 min) scheduled on Tuesday 29 September 2026 is one Scheduled Visit pre-filled at 60 min; changing it to 75 min makes Tuesday's load use 75 min plus Travel Allowance (S5)

**Slice** — When a Location has both a recurring and a campaign visit due, scheduling it makes one visit whose duration starts at the longer of the two, which the rep can change, and the day load counts that duration once.
**Spec source** — Visit Planning US-005 S4, S5 and edge cases
**Depends on** — T-13.4.1, T-13.3.1, T-20.2.1
**Pattern to follow** — T-13.4.1 (day load)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies the day load (characterisation first); rules stated.

**Provisional commit message**

```
feat(visits): schedule recurring and campaign visits as one

- A rep calling once shouldn't be planned twice; the longer duration is
  the honest starting guess, and the rep can change it
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A small change behind a characterisation pass.

**Agent prompt**

```
Role: You are combining recurring and campaign visits in the planner's day
load in the Field Sales Management System.

Context:
- Slice: when a Location has both a recurring and a campaign visit due,
  scheduling it makes one visit whose duration starts at the longer of the
  two, which the rep can change, and the day load counts that duration once.
- Specs: plan_docs/stories/visit-planning.md US-005 S4, S5 and edge cases.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — day load (T-13.4.1),
  scheduling (T-13.3.1), campaign visits (T-20.2.1).
- Pattern to follow: T-13.4.1.

Acceptance criteria:
1. A 60 min campaign visit at a 45 min Location contributes 60 min plus
   Travel Allowance.
2. Scheduling Murphy's Pharmacy (recurring 45, campaign 60) on Tuesday 29
   September 2026 makes one Scheduled Visit pre-filled at 60 min.
3. Changing it to 75 min makes Tuesday's load use 75 min plus Travel
   Allowance, counted once.

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-13.4.1's day load; stop and show them passing. Resume only on
"Continue T-20.5.1".

Steps: 1. characterisation tests; 2. combined scheduling; 3. duration
pre-fill; 4. load; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-20.5.1-S; do not design
your own.

Definition of done: when a Location has both a recurring and a campaign visit
due, scheduling it makes one visit whose duration starts at the longer of the
two, which the rep can change, and the day load counts that duration once.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: tablet agenda changes beyond showing the one visit.
```

**Checkpoint**

Produces before pausing — characterisation tests of the day load, passing.
Human reviews — Do the tests pin today's load, including over-limit text?
Resume trigger — `Continue T-20.5.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | CV-006 (T-20.1.1, T-20.1.2, T-20.1.3, T-20.2.2), VP-011 (T-20.2.1, T-20.2.2), VP-013 (T-20.3.1), VP-012 (T-20.4.1, T-20.4.2, T-20.1.3), VP-005 part (T-20.5.1); CV-002 and CV-008 specialist lines (T-20.1.1) |
| Every task satisfies the three slice criteria | Pass | 9 of 9 |
| Every task carries a tier with a rationale citing dimensions | Pass | 9 of 9 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | Campaign list (T-20.4.1, inferred); link matching (T-20.2.2 decision) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-20.1.2, T-20.5.1; coverage lines sliver in T-20.1.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-20.1.1, T-20.2.2, T-20.3.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-20.1.2, T-20.1.3, T-20.2.1, T-20.3.1, T-20.4.1, T-20.4.2, T-20.5.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-20.1.1, T-20.2.2 |
| Every scenario task precedes the task it gates | Pass | 9 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, campaign list (inferred) |
