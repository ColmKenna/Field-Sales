# E12 — Servicing profiles

**Iteration** — 3, Visit rhythm
**Outcome** — Head office sets how shops are serviced through a handful of Location Profiles; each shop resolves every default — visit frequency, duration, low-stock thresholds — to the most demanding value with its source shown, and can override it; reps get a low-stock hint that never decides for them.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Customer Directory US-006 — Maintain Location Profiles and their defaults | Must | S1–S4 and the 23 Sep amendment |
| 2 | Customer Directory US-002 — Set a Location's Type, Profile and Master Location | Must | S1–S9 |
| 3 | Visit Planning US-001 — Override Visit Frequency and Duration on a Location | Must (was Blocked) | S1–S5 |
| 4 | Rep at a Location US-009 — See a Low Stock Hint | Could (was Blocked) | S2, S3, S4 (S1's wording superseded by S4) |

**Exit criterion** — A Head Office User can create profiles with defaults or as grouping only, see how many shops a default change affects, give a shop several profiles and a master Location, and a manager can override a shop's frequency, cycle start and duration; every resolved value shows its source; on the tablet a count at or below the resolved threshold shows "Below low-stock level (N)" and never ticks Low.

**Capability-class stamp** — Frontier workhorse for Agent-Assisted tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [customer-directory.md](../../stories/customer-directory.md), [visit-planning.md](../../stories/visit-planning.md) (US-001, glossary), [rep-at-a-location-tablet.md](../../stories/rep-at-a-location-tablet.md) (US-009), [04-user-stories-amendments.md](../../uxdocs/04-user-stories-amendments.md) (BR-NEW-005), [02-head-office.md](../../uxdocs/02-head-office.md) (H-26, H-28), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-06, T6.3).

---

### T-12.1.1-S — Test scenarios for Location Profiles

**Owner** — Scenario Review
**Gates** — T-12.1.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for maintaining Location Profiles with defaults
(task T-12.1.1). Read plan_docs/stories/customer-directory.md US-006 S1, S2,
S4 and glossary (Location Profile), and
plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-005 rules 1–2. Output
one line per scenario as Should_Outcome_When_Condition, then "→" and a
one-line intent. Cover S1, S2 (head office side) and S4, then derivable
edges: a grouping-only profile, a threshold for an archived product, a
duplicate profile name. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-12.1.1 — Define Location Profiles with visit and stock defaults

**Parent story**

> As a Head Office User, I want to define Location Profiles with a visit frequency, visit duration and low-stock thresholds per product so that hundreds of shops are serviced consistently from a handful of settings.
>
> Acceptance criteria:
> - Creating "Large pharmacy" with Visit Frequency 4 weeks and Visit Duration 45 min offers it on Locations (S1)
> - Thresholds 6 for "Cold & Flu Relief 16s" and 3 for "Vitamin D 1000IU 90s" are saved on "Large pharmacy" (S2, head office part)
> - "Large pharmacy" used by 40 Locations shows no Delete; Archive shows "Used by 40 locations — they keep their current defaults until reassigned" (S4)

**Slice** — A Head Office User creates Location Profiles carrying visit frequency, visit duration and per-product low-stock thresholds — or none, as a pure grouping — and archives one in use without breaking the shops that hold it.
**Spec source** — Customer Directory US-006 S1, S2, S4; BR-NEW-005 rules 1–2; uxdocs 02 H-28 (H28.2)
**Depends on** — T-1.5.1, T-2.3.1, T-1.2.1
**Pattern to follow** — T-1.5.1 (archive-not-delete list)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the profile model drives visit generation and stock hints for every shop; H-28 is a draft; checkpoint on the model.

**Provisional commit message**

```
feat(customers): define location profiles with visit and stock defaults

- A handful of profiles service hundreds of shops consistently; a profile
  with no defaults is a pure grouping for scopes, campaigns and reports
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared model with a design pause.

**Agent prompt**

```
Role: You are building Location Profiles (H-28) for the Field Sales
Management System's head office website.

Context:
- Slice: a Head Office User creates Location Profiles carrying visit
  frequency, visit duration and per-product low-stock thresholds — or none,
  as a pure grouping — and archives one in use without breaking the shops
  that hold it.
- Specs: plan_docs/stories/customer-directory.md US-006 S1, S2, S4 and
  glossary (Location Profile); plan_docs/uxdocs/04-user-stories-amendments.md
  BR-NEW-005; plan_docs/uxdocs/02-head-office.md H-28 (H28.2 a grouping-only
  profile shows "No defaults — used for grouping").
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — archive pattern
  (T-1.5.1), Locations (T-2.3.1), products (T-1.2.1).
- Pattern to follow: T-1.5.1's list and archive.

Acceptance criteria:
1. Creating "Large pharmacy" with Visit Frequency 4 weeks and Visit Duration
   45 min makes it available on Locations.
2. Thresholds 6 for "Cold & Flu Relief 16s" and 3 for "Vitamin D 1000IU 90s"
   save on "Large pharmacy"; products without a threshold have none.
3. A profile with no defaults shows "No defaults — used for grouping".
4. "Large pharmacy" used by 40 Locations shows Archive with "Used by 40
   locations — they keep their current defaults until reassigned" and no
   Delete; an archived profile isn't offered for new use.
5. Any default field may be left empty.

Constraints:
- Use the project's existing conventions and test framework.
- Store defaults only; resolution per Location is T-12.2.2.
- No tests of framework internals or trivial members.
- This task opts in to the profile and profile-threshold tables.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the profile model (defaults and thresholds, all optional)
is designed, stop and show it. Resume only on "Continue T-12.1.1".

Steps: 1. profile model; 2. H-28 list and detail; 3. thresholds per product;
4. archive; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-12.1.1-S; do not design
your own.

Definition of done: a Head Office User creates Location Profiles carrying
visit frequency, visit duration and per-product low-stock thresholds — or
none, as a pure grouping — and archives one in use without breaking the shops
that hold it.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–5 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: assigning profiles to Locations (T-12.2.1), resolution
(T-12.2.2), impact of changing a default (T-12.1.2), tablet hint (T-12.4.1).
```

**Checkpoint**

Produces before pausing — the profile model with optional frequency, duration and per-product thresholds.
Human reviews — Does the model support many profiles per shop and grouping-only profiles without special cases?
Resume trigger — `Continue T-12.1.1`

---

### T-12.2.1-S — Test scenarios for classifying a Location

**Owner** — Scenario Review
**Gates** — T-12.2.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for setting a Location's profiles and Master
Location (task T-12.2.1). Read plan_docs/stories/customer-directory.md US-002
S2–S5, S8 and glossary (Master Location), and design decision "Master
Location is a relationship recorded here, used elsewhere". Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover the story scenarios, then derivable edges: a three-level chain
(branch → regional → head office), removing a master, a master that is
Closed. Mark undecided cases as "Needs a decision". Write no test code;
change no files.
```

---

### T-12.2.1 — Give a Location several profiles and a master Location

**Parent story**

> As a Head Office User, I want to classify a Location by what it is and how it is serviced, and record which Location it reports to, so that visits are scheduled at the right rhythm and chains are understood.
>
> Acceptance criteria:
> - Type "Head office" with no Profile is saved and the Location stays on the No Visit Schedule list with "No profile — no visits will be scheduled" (S2)
> - Setting Master Location "Hickey's Head Office" on "Hickey's Rathdrum" shows "Reports to Hickey's Head Office", and Head Office lists Rathdrum under Branches (S3)
> - A Location of another Customer isn't offered as master (S4)
> - Making Rathdrum Head Office's master while Rathdrum reports to Head Office is rejected with "This would make the locations report to each other" (S5)
> - Adding grouping-only Profile "Customer campaign X" to Byrne's leaves its defaults unchanged and makes the profile available to scopes, filters and reporting (S8)

**Slice** — A Head Office User gives a Location any number of profiles and, optionally, a master Location of the same customer — never in a loop — and the master lists its branches.
**Spec source** — Customer Directory US-002 S2–S5, S8; uxdocs 02 H-26 (H26.1)
**Depends on** — T-12.1.1, T-2.3.1
**Pattern to follow** — T-2.3.1 (Location record)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — the master relationship underpins chains and chain targets later; H-26 is a draft.

**Provisional commit message**

```
feat(customers): give locations several profiles and a master location

- A shop can be serviced as more than one kind of place, so profiles are
  many-to-many; the master relationship is recorded here and used by chain
  ordering later
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Relationship model with a review pause.

**Agent prompt**

```
Role: You are adding Location Profiles and the Master Location to the
Location record (H-26) of the Field Sales Management System.

Context:
- Slice: a Head Office User gives a Location any number of profiles and,
  optionally, a master Location of the same customer — never in a loop — and
  the master lists its branches.
- Specs: plan_docs/stories/customer-directory.md US-002 S2–S5, S8 and
  glossary (Master Location, Location Profile); plan_docs/uxdocs/
  04-user-stories-amendments.md BR-NEW-005 rule 1; plan_docs/uxdocs/
  02-head-office.md H-26.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Location record
  (T-2.3.1), profiles (T-12.1.1).
- Pattern to follow: T-2.3.1's record sections.

Acceptance criteria:
1. A Location can hold any number of Location Profiles, added and removed on
   its record.
2. Type "Head office" with no profile carrying a Visit Frequency shows "No
   profile — no visits will be scheduled" (the No Visit Schedule gap list is
   E17).
3. Setting Master Location "Hickey's Head Office" on "Hickey's Rathdrum"
   shows "Reports to Hickey's Head Office"; Head Office lists Rathdrum under
   Branches.
4. Only Locations of the same Customer are offered as master.
5. A loop is rejected with "This would make the locations report to each
   other", at any depth.
6. Adding grouping-only "Customer campaign X" leaves defaults unchanged.

Constraints:
- Use the project's existing conventions and test framework.
- Resolution of defaults is T-12.2.2; show profiles only here.
- No tests of framework internals or trivial members.
- This task opts in to the location–profile link table and the master
  reference.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the link model and the loop check are written, stop and
show them. Resume only on "Continue T-12.2.1".

Steps: 1. location–profile links; 2. master reference with same-customer and
loop checks; 3. H-26 sections; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-12.2.1-S; do not design
your own.

Definition of done: a Head Office User gives a Location any number of
profiles and, optionally, a master Location of the same customer — never in a
loop — and the master lists its branches.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: default resolution (T-12.2.2), chain ordering (E23, E24), gap
lists (E17).
```

**Checkpoint**

Produces before pausing — the location–profile link model and the master loop check.
Human reviews — Can any sequence of edits create a reporting loop or a cross-customer master?
Resume trigger — `Continue T-12.2.1`

---

### T-12.2.2-S — Test scenarios for resolving a Location's defaults

**Owner** — Human-Led
**Gates** — T-12.2.2
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- The Byrne's example (BR-NEW-005): Large pharmacy (every 2 weeks, 45 min, SPF30 warn at 6) plus Rural shop (every 4 weeks, 20 min, SPF30 warn at 12) resolves to every 2 weeks, 45 minutes, warn at 12. Write it and its variations.
- Ties: two profiles supply the same winning value — "all of them are named". Exact wording?
- A per-Location override beats every profile (S9). Is an override per field?
- An archived profile still held by a Location: do its defaults still count?
- Changing a profile default (S6): "future Cycle Periods follow; the current open Visit Due is unchanged" — confirm for frequency and duration.

---

### T-12.2.2 — Resolve each Location's defaults to the most demanding value, with its source

**Parent story**

> As a Head Office User, I want to classify a Location by what it is and how it is serviced, and record which Location it reports to, so that visits are scheduled at the right rhythm and chains are understood.
>
> Acceptance criteria:
> - Setting Profile "Large pharmacy" (every 4 weeks, 45 min) shows "Every 4 weeks (profile default)" and takes the Location off the No Visit Schedule list; Visit Planning generates its dues from the Cycle Start asked for (S1)
> - Changing Profile to "Small rural shop" (every 8 weeks) makes future Cycle Periods follow 8 weeks; the current open Visit Due is unchanged (S6)
> - Byrne's with "Large pharmacy" and "Rural shop" resolves to every 2 weeks, 45 minutes, warn at 12, each showing its source such as "Every 2 weeks (from Large pharmacy)" (S7)
> - A per-Location override of every 3 weeks wins whatever the profiles supply (S9)

**Slice** — Every Location's visit frequency, visit duration and each product's low-stock threshold resolve separately to the most demanding value across its profiles — or its own override — and each shows where it came from.
**Spec source** — Customer Directory US-002 S1, S6, S7, S9; BR-NEW-005 rules 3–6; uxdocs 02 H-26 (H26.1)
**Depends on** — T-12.2.1
**Pattern to follow** — T-6.4.1 (a pure resolver with its source)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — the resolver drives visit generation and stock hints for every shop (Blast Radius High); ties and overrides need a human oracle; the build is a pure function an agent can write against agreed scenarios.

**Provisional commit message**

```
feat(customers): resolve location defaults to the most demanding value

- A shop with several profiles gets the most frequent visits, the longest
  duration and the earliest stock hint, each shown with its source so a
  mixed result is never a mystery
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A shared rule built against a human oracle.

**Agent prompt**

```
Role: You are implementing the Location defaults resolver for the Field Sales
Management System.

Context:
- Slice: every Location's visit frequency, visit duration and each product's
  low-stock threshold resolve separately to the most demanding value across
  its profiles — or its own override — and each shows where it came from.
- Specs: plan_docs/stories/customer-directory.md US-002 S1, S6, S7, S9;
  plan_docs/uxdocs/04-user-stories-amendments.md BR-NEW-005 rules 3–6 (most
  frequent visits, longest duration, highest threshold; override beats every
  profile; source shown; ties name all);
  plan_docs/uxdocs/02-head-office.md H-26 (H26.1 source in brackets).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — profiles (T-12.1.1),
  location links (T-12.2.1).
- Pattern to follow: a pure resolver returning value and source, like
  T-6.4.1.

Acceptance criteria:
1. With only "Large pharmacy" (every 4 weeks, 45 min), the Location shows
   "Every 4 weeks (profile default)".
2. Byrne's with "Large pharmacy" (every 2 weeks, 45 min, SPF30 warn at 6) and
   "Rural shop" (every 4 weeks, 20 min, SPF30 warn at 12) resolves to every 2
   weeks, 45 minutes, warn at 12, shown as "Every 2 weeks (from Large
   pharmacy)", "45 min (from Large pharmacy)", "Warn at 12 (from Rural shop)".
3. A per-Location override of every 3 weeks wins, shown as an override.
4. Ties name every profile that supplies the value.
5. A Location with no profile supplying a Visit Frequency resolves to none
   ("No profile — no visits will be scheduled").
6. Changing a profile changes future resolution only; stored open visits are
   untouched (visit generation reads the resolver, T-13.1.1).

Constraints:
- Use the project's existing conventions and test framework.
- One pure function, used by visit generation, the tablet snapshot and every
  display.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the resolver passes every agreed scenario, stop and show
it. Resume only on "Continue T-12.2.2".

Steps: 1. resolver function; 2. H-26 display with sources; 3. tests.

Test expectations: implement exactly the scenarios agreed in T-12.2.2-S. You
are forbidden from designing your own test cases.

Definition of done: every Location's visit frequency, visit duration and each
product's low-stock threshold resolve separately to the most demanding value
across its profiles — or its own override — and each shows where it came
from.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: setting overrides (T-12.3.1), visit generation (T-13.1.1), the
tablet hint (T-12.4.1).
```

**Checkpoint**

Produces before pausing — the resolver passing every agreed scenario.
Human reviews — Does the resolver produce the Byrne's result and name ties exactly as agreed?
Resume trigger — `Continue T-12.2.2`

---

### T-12.1.2-S — Test scenarios for the impact of changing a profile default

**Owner** — Scenario Review
**Gates** — T-12.1.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing the impact of changing a profile
default (task T-12.1.2). Read plan_docs/stories/customer-directory.md US-006
S3 and its 23 Sep amendment ("a changed default affects only Locations where
this profile supplies the winning value"), and plan_docs/uxdocs/
02-head-office.md H-28 (H28.1). Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning T-12.1.1's profile editing. Cover S3, then
derivable edges: a Location with an override, a Location where another
profile wins, a threshold change. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-12.1.2 — Show how many shops a profile default change affects

**Parent story**

> As a Head Office User, I want to define Location Profiles with a visit frequency, visit duration and low-stock thresholds per product so that hundreds of shops are serviced consistently from a handful of settings.
>
> Acceptance criteria:
> - With 40 Locations using "Large pharmacy", changing Visit Frequency to 3 weeks shows "40 locations without an override will change to every 3 weeks"; on confirm their future Cycle Periods follow 3 weeks (S3)
> - The count includes only Locations where this profile supplies the winning value (23 Sep amendment)

**Slice** — Editing a profile's default states, beside the field and before saving, how many shops will actually change — only those where this profile wins and no override applies.
**Spec source** — Customer Directory US-006 S3 and the 23 Sep amendment; uxdocs 02 H-28 (H28.1)
**Depends on** — T-12.2.2
**Pattern to follow** — T-3.1.2 (preview equals outcome)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies profile editing (T-12.1.1), so characterisation comes first; the count uses the reviewed resolver.

**Provisional commit message**

```
feat(customers): show how many shops a profile change affects

- One profile edit reschedules many shops, so the count of those that will
  actually change appears before saving
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Preview over a reviewed rule.

**Agent prompt**

```
Role: You are adding an impact count to profile default changes (H-28) in the
Field Sales Management System.

Context:
- Slice: editing a profile's default states, beside the field and before
  saving, how many shops will actually change — only those where this
  profile wins and no override applies.
- Specs: plan_docs/stories/customer-directory.md US-006 S3 and 23 Sep
  amendment; plan_docs/uxdocs/02-head-office.md H-28 (H28.1 the impact
  sentence appears beside the changed field as soon as it differs).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — profiles (T-12.1.1),
  resolver (T-12.2.2).
- Pattern to follow: T-3.1.2's preview.

Acceptance criteria:
1. Changing "Large pharmacy" Visit Frequency to 3 weeks shows "40 locations
   without an override will change to every 3 weeks" beside the field before
   saving.
2. The count includes only Locations where the resolver's winning value
   would change.
3. On confirm, those Locations' future Cycle Periods follow the new value
   (read by T-13.1.1).
4. The same applies to duration and thresholds.

Constraints:
- Use the project's existing conventions and test framework.
- Compute the count by resolving before and after with T-12.2.2; no second
  rule.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
T-12.1.1's profile editing; stop and show them passing. Resume only on
"Continue T-12.1.2".

Steps: 1. characterisation tests; 2. before/after count; 3. inline sentence;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-12.1.2-S; do not design
your own.

Definition of done: editing a profile's default states, beside the field and
before saving, how many shops will actually change — only those where this
profile wins and no override applies.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: regenerating visits (T-13.1.1 reads the resolver).
```

**Checkpoint**

Produces before pausing — characterisation tests of profile editing, passing.
Human reviews — Does the count match the shops whose resolved value actually changes?
Resume trigger — `Continue T-12.1.2`

---

### T-12.3.1-S — Test scenarios for per-Location overrides

**Owner** — Scenario Review
**Gates** — T-12.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for overriding a Location's visit frequency,
cycle start and duration (task T-12.3.1). Read
plan_docs/stories/visit-planning.md US-001 S1–S5, its edge cases and glossary
(Visit Frequency, Cycle Start, Cycle Period, Visit Duration). Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover S1–S5, then derivable edges: an override on a Location with no
profile, a cycle start in the past, clearing only the duration override.
Mark undecided cases as "Needs a decision". Write no test code; change no
files.
```

---

### T-12.3.1 — Override a Location's visit frequency, cycle start and duration

**Parent story**

> As a Sales Manager, I want to set a Location's own Visit Frequency, Cycle Start and Visit Duration where its profile default doesn't fit so that its visits are generated at the right rhythm without maintaining every Location by hand.
>
> Acceptance criteria:
> - Murphy's Pharmacy planning settings show "Every 4 weeks (profile default)" and "45 min (profile default)" (S1)
> - Setting 2 weeks shows "Every 2 weeks (profile default: 4 weeks)" and regenerates future Cycle Periods from the next Cycle Start (S2)
> - "Use profile default" reverts to 4 weeks (S3)
> - 0 weeks is rejected with "Enter 1 week or more" (S4)
> - An override with no Cycle Start is rejected with "Set a cycle start date" (S5)

**Slice** — A manager opens a shop's planning settings, sees its resolved frequency and duration with their source, overrides either — setting a cycle start with any frequency — or reverts to the profile default.
**Spec source** — Visit Planning US-001 S1–S5, edge cases; glossary (Cycle Start)
**Depends on** — T-12.2.2
**Pattern to follow** — T-12.2.2 (resolver with source)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — overrides feed the resolver and visit generation; checkpoint on the override and cycle-start fields.

**Provisional commit message**

```
feat(visit-planning): override a location's visit rhythm

- Most shops follow their profile; the few that don't get their own
  frequency and duration, shown against the default they replace
- A cycle start is always required so no shop drifts off the calendar
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Inputs to visit generation with a review pause.

**Agent prompt**

```
Role: You are adding per-Location visit rhythm overrides to the Field Sales
Management System.

Context:
- Slice: a manager opens a shop's planning settings, sees its resolved
  frequency and duration with their source, overrides either — setting a
  cycle start with any frequency — or reverts to the profile default.
- Specs: plan_docs/stories/visit-planning.md US-001 S1–S5, edge cases,
  glossary (Visit Frequency, Cycle Start, Cycle Period, Visit Duration).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — resolver (T-12.2.2),
  Location page (T-3.2.1 manager page; H-26 head office record).
- Pattern to follow: T-12.2.2's value-with-source display.

Acceptance criteria:
1. Murphy's Pharmacy planning settings show "Every 4 weeks (profile default)"
   and "45 min (profile default)".
2. Setting Visit Frequency to 2 weeks shows "Every 2 weeks (profile default:
   4 weeks)".
3. "Use profile default" reverts it.
4. 0 weeks is rejected with "Enter 1 week or more".
5. A frequency override without a Cycle Start is rejected with "Set a cycle
   start date"; any Location given a frequency has a Cycle Start.
6. A frequency change never alters the current period's open Visit Due;
   future periods follow from the next Cycle Start (visit generation reads
   this, T-13.1.1).

Constraints:
- Use the project's existing conventions and test framework.
- Overrides feed T-12.2.2's resolver; no second rule.
- No tests of framework internals or trivial members.
- This task opts in to override and cycle-start fields on Locations.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after the override and cycle-start fields are wired into the
resolver, stop and show them. Resume only on "Continue T-12.3.1".

Steps: 1. override and cycle-start fields; 2. resolver integration;
3. planning settings UI; 4. validation; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-12.3.1-S; do not design
your own.

Definition of done: a manager opens a shop's planning settings, sees its
resolved frequency and duration with their source, overrides either — setting
a cycle start with any frequency — or reverts to the profile default.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: generating visits (T-13.1.1), per-Location threshold overrides
(assumed out of scope, MI-22).
```

**Checkpoint**

Produces before pausing — override and cycle-start fields wired into the resolver.
Human reviews — Can a Location ever have a frequency without a cycle start?
Resume trigger — `Continue T-12.3.1`

---

### T-12.4.1-S — Test scenarios for the low-stock hint

**Owner** — Scenario Review
**Gates** — T-12.4.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for the low-stock hint on the tablet (task
T-12.4.1). Read plan_docs/stories/rep-at-a-location-tablet.md US-009 S2–S4
(S1's wording is superseded by S4) and plan_docs/uxdocs/01-tablet-day.md T-06
(T6.3). Output one line per scenario as Should_Outcome_When_Condition, then
"→" and a one-line intent. Start with characterisation scenarios pinning the
stock check (T-5.4.1). Cover S2–S4, then derivable edges: a count exactly at
the threshold, a threshold from two profiles, a Phone call's "Stock
mentioned" with a count. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-12.4.1 — Show a low-stock hint that never ticks Low

**Parent story**

> As a Field Salesperson, I want a hint when a count looks low so that I notice items I might overlook without the tablet deciding for me.
>
> Acceptance criteria:
> - With a resolved threshold of 6 for "Cold & Flu Relief 16s" at Murphy's, a count of 3 shows "Below low-stock level (6)" under the Low tick; Low stays unticked (S4)
> - Saving without marking Low saves the line as not Low; no order line is added (S2)
> - "Vitamin D 1000IU 90s" with no threshold shows no hint (S3)

**Slice** — On a stock check, a count at or below the shop's resolved threshold for that product shows "Below low-stock level (6)" under the Low tick, and Low stays the rep's choice.
**Spec source** — Rep at a Location US-009 S2–S4; Customer Directory US-006 S2 (tablet part); uxdocs 01 T-06 (T6.3)
**Depends on** — T-12.2.2, T-5.4.1
**Pattern to follow** — T-5.4.1 (stock check rows)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies the stock check and adds resolved thresholds to the snapshot, so characterisation comes first.

**Provisional commit message**

```
feat(calls): hint when a count is below the shop's low-stock level

- The hint informs and never decides: a pre-ticked Low would become a
  decision nobody made
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Modifies a core tablet screen behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the low-stock hint to the stock check in the Field Sales
Management System's tablet app.

Context:
- Slice: on a stock check, a count at or below the shop's resolved threshold
  for that product shows "Below low-stock level (6)" under the Low tick, and
  Low stays the rep's choice.
- Specs: plan_docs/stories/rep-at-a-location-tablet.md US-009 S2–S4;
  plan_docs/stories/customer-directory.md US-006 S2;
  plan_docs/uxdocs/01-tablet-day.md T-06 (DECISION T6.3: the hint sits under
  the Low tick, not next to the count).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — resolver (T-12.2.2),
  snapshot (T-4.1.1), stock check (T-5.4.1).
- Pattern to follow: T-5.4.1's rows.

Acceptance criteria:
1. With a resolved threshold of 6 for "Cold & Flu Relief 16s" at Murphy's, a
   count of 3 shows "Below low-stock level (6)" under the Low tick; Low stays
   unticked.
2. A count equal to the threshold shows the hint.
3. Saving without marking Low saves the line as not Low; no order line is
   added.
4. A product with no threshold shows no hint.
5. Works offline from the snapshot's resolved thresholds.

Constraints:
- Use the project's existing conventions and test framework.
- Snapshot additions (resolved thresholds per assigned Location and product)
  go through T-4.1.1's versioning and its owner's review; mind the size
  budget (MI-02).
- The hint never sets Low.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the stock check; stop and show them passing with the snapshot addition and
its size. Resume only on "Continue T-12.4.1".

Steps: 1. characterisation tests; 2. snapshot thresholds; 3. hint under the
Low tick; 4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-12.4.1-S; do not design
your own.

Definition of done: on a stock check, a count at or below the shop's resolved
threshold for that product shows "Below low-stock level (6)" under the Low
tick, and Low stays the rep's choice.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–5 demonstrated
- [ ] Snapshot version bumped and reviewed by its owner
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: per-Location threshold overrides (MI-22).
```

**Checkpoint**

Produces before pausing — characterisation tests of the stock check, passing, and the snapshot addition with its measured size.
Human reviews — Does the hint appear exactly at or below the resolved threshold, and is the snapshot still within budget?
Resume trigger — `Continue T-12.4.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | CD-006 (T-12.1.1, T-12.1.2), CD-002 (T-12.2.1, T-12.2.2), VP-001 (T-12.3.1), A1-009 (T-12.4.1) |
| Every task satisfies the three slice criteria | Pass | 6 of 6 |
| Every task carries a tier with a rationale citing dimensions | Pass | 6 of 6 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | T-12.3.1 (MI-22), T-12.4.1 (MI-02) |
| Tasks modifying existing behaviour order characterisation first | Pass | T-12.1.2, T-12.4.1 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | No Agent-Autonomous task in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-12.2.2 is Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | 6 of 6 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | None in this epic |
| Every scenario task precedes the task it gates | Pass | 6 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-02, 08, 22 |
