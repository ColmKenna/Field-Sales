# 04 — User stories: amendments and new stories

**Session:** Field Sales Management System design, 21 Sep 2026
**Derived from:** decisions settled in session, recorded in `01-tablet-day.md` (T7.1, T7.6–T7.12, T2.5) and `02-head-office.md` (H1.6, H1.7, H2.6–H2.8)
**Scope:** only decisions confirmed in conversation. Drafting calls in 00–03 that haven't been confirmed (e.g. T2.1 Today above Overdue, H1.1 Type column) are not turned into stories here.
**Numbering:** the source area documents are under `plan_docs/stories`. New cross-area stories retain their `US-NEW-###` placeholders until they are folded back into those sources; amendments reference the source story IDs.
**Folded into `plan_docs/stories` on 23 Sep 2026:** every amendment below has been added to its source story as "UX amendments (23 Sep 2026)" scenarios, keeping the AC IDs (e.g. `Scenario CV001-A`). Placeholders now live at: US-NEW-001 → Rep at a Location US-008 (scenarios 7–13); US-NEW-002 and US-NEW-004 → Rep at a Location US-012 (scenarios 7–18); US-NEW-003 → Rep at a Location US-003 (scenarios 8–11); US-NEW-005 → Head Office Order Processing **US-008**; US-NEW-006 → Visit Planning **US-016**. BR-NEW-001 to BR-NEW-005 are folded into the glossaries and design decisions of Head Office, Pricing, Promotions, Visit Planning and Customer Directory. "Pricing US-008" here maps to source Pricing **US-007** (Ask for a price override), and source US-008 (Decide a price override) is marked superseded. AC-PR009-B/C/D were reworded to the monthly per-rep allowance of BR-NEW-003. Keep this file as the design record; when amending further, update both.

---

## Summary

| ID | Type | Title | Surface |
|---|---|---|---|
| BR-NEW-001 | Business rule | Automatic order disposition | Head office |
| BR-NEW-002 | Business rule | Rep discount allowance and promotion interaction | Tablet, head office |
| BR-NEW-003 | Business rule | Free of charge on discontinuing stock only | Tablet |
| BR-NEW-004 | Business rule | Structured Visit Due Reason Type | Rep website, manager website, tablet |
| BR-NEW-005 | Business rule | Many Location Profiles, defaults resolved field by field | Manager website, rep website, tablet |
| BR-NEW-008 | Business rule | A chain's Agreed Ranges are catalogue Ranges assigned to it | Customer, rep, head office |
| BR-NEW-009 | Business rule | Orders wait as Pending until a cut-off, then are accepted automatically | Rep website, customer, head office |
| US-NEW-001 | New story | Quantity popover when adding a Low item | Tablet (T-06, T-07) |
| US-NEW-002 | New story | Low tab as a catch-up list | Tablet (T-07) |
| US-NEW-003 | New story | Rep prompted when a line couldn't be supplied | Tablet (T-02, T-08) |
| US-NEW-004 | New story | Out-of-pattern quantity marker on Review | Tablet (T-07) |
| US-NEW-005 | New story | Orders processed without head office acceptance | Head office (H-01, H-02) |
| US-NEW-006 | New story | Manage Visit Due Reason Types | Manager/admin website |
| US-NEW-007 | New story | Usual products landing page, marked when others have ordered | Customer (C-09, C-02) |
| Self-service US-004 | Amendment | Same-page catalogue scope control; one search with curated results first | Customer (C-03) |
| Self-service US-005, US-007, US-008 | Amendment | Placing confirms the order; own orders editable or cancellable until the cut-off, read-only after | Customer (C-04, C-06, C-07) |
| Self-service US-008 | Amendment | Chain submissions expand in history; a branch filter shows normal rows | Customer (C-06) |
| Self-service US-001 | Amendment | Rep sets up online ordering on the tablet from the contact on T-05; sent in the background; manager approves on M-17, prompted by email; a manager sets one up directly from M-07 | Customer (C-08), Tablet (T-05), Manager website (M-07, M-17) |
| Self-service US-002 | Amendment | Expired invitation: request a new one, approved by the rep or manager before it is sent | Customer (C-01) |
| Self-service US-009 | Clarification | Repeat is for one branch order, not a whole chain submission; any visible order can be repeated, whoever placed it; it adds to the current order, and the repeated quantity wins after one warning | Customer (C-07) |
| Self-service US-006 | Clarification | Predefined chain ranges and default dropdown in grid | Customer (C-05) |
| Pricing US-008 *(confirm)* | Amendment | Price override applied within allowance | Tablet |
| Pricing US-009 *(confirm)* | Amendment | Free of charge restricted and applied | Tablet |
| Promotions US-007 *(confirm)* | Amendment | Spend threshold qualifies before rep discounts | Tablet, customer |
| Visit Planning US-003 | Clarification | Planner horizon, views, responsive panel and Due Reason disclosure | Rep website (R-01) |
| Visit Planning US-004, US-005 | Clarification | Drag uses default duration; click scheduled visit to amend | Rep website (R-01) |
| Visit Planning US-009 | Clarification | Review-and-select bulk digest actions | Rep website (R-02) |
| Visit Planning US-010 | Amendment | By-rep default and compact coverage context | Manager website (M-01) |
| Visit Planning US-006, US-007 | Clarification | Absence save precedes affected-visit decisions | Manager website (M-02) |
| Visit Planning US-011 | Clarification | Four-step campaign creation wizard | Manager website (M-03) |
| Visit Planning US-012 | Clarification | Campaign detail defaults to Overall | Manager website (M-04) |
| Visit Planning US-014 | Clarification | Conflict ordering, rep filter and resolved history | Manager website (M-05) |
| Visit Planning US-015 | Amendment | Location-first one-off visit with a chosen rep | Manager website (M-16), tablet |
| Coverage US-001 | Amendment | Campaign creation can assign an unassigned Location's Primary Rep | Manager website (M-03) |
| Coverage US-001 | Amendment | Transfer Territory Assignments between reps (push, pull, County splitting) | Manager website (M-06) |
| Coverage US-002 | Amendment | Location page as the manager's home for a Location | Manager website (M-07) |
| Coverage US-003 | Amendment | Handover when the previous rep is leaving; inherited visits | Manager website (M-08), rep website, performance |
| Coverage US-004, US-005 | Amendment | Bulk reassign for long absence only; reversal restores and flags | Manager website (M-09) |
| Coverage US-006 | Amendment | Multi-condition specialist scopes; campaign routing when several match | Manager website (M-10, M-03) |
| Master & Branch US-001 | Clarification | Master Location view leads with "Start multi-branch order" | Tablet (T-11) |
| Master & Branch US-002, US-003 | Amendment | Agreed Ranges are assigned catalogue Ranges; a Range Review assigns or removes whole Ranges, confirmed per Range | Tablet (T-12), Head office (H-04) |
| Master & Branch US-006 | Amendment | Grid opens on recently ordered products; `All` keeps adjusted cells; range picker with several Agreed Ranges | Rep/manager website (M-11) |
| Targets & Performance US-001, US-002 | Amendment | Targets carried forward from the previous period | Manager website (M-12) |
| Coverage US-007 | Amendment | Unassigned count pushed to the Visit Planning overview | Manager website (M-15, M-01) |
| Coverage US-009 | Amendment | Cohort grants from the Restriction Group's page | Manager website (M-14) |
| Targets & Performance US-005 | Clarification | "Furthest behind" by points behind pace, re-sortable by money; value decides for dual targets | Manager website (M-13) |
| Targets & Performance US-004 | Amendment | Targeted Locations ordered by greatest target shortfall | Rep website (R-03) |
| Stock Allocation US-003, US-005 | Clarification | Manager-selected stock pools when re-proposing | Head office (H-10) |
| Area 1 US-014 | Amendment | Sent item shows removed lines and applied prices; rep edits own Pending orders on R-04 until the cut-off | Tablet (T-08), rep website (R-04) |
| Head Office US-001, US-002 | Superseded in part | Worklist no longer holds orders | Head office |
| Head Office US-003 | Superseded | Order detail becomes a record | Head office |
| Head Office US-006 | Amendment | Manual Hold and Reject before the cut-off; held orders listed on H-06 | Head office (H-02, H-06) |

---

## Business rules

### BR-NEW-001 — Automatic order disposition

Orders are never accepted by a person. Each order flag has one disposition:

| Disposition | Flags | Behaviour |
|---|---|---|
| Route | Stock Shortfall, Oversold | Order accepted; short quantity becomes outstanding and goes to allocation (H-10). |
| Auto-resolve | Unavailable Line | Order accepted; the unavailable line is removed with its reason recorded; the rep is prompted (US-NEW-003). |
| Annotate | Large, Watched Product, Rep-flagged, New Location, Prospect Conversion | Recorded on the order. No human step. |
| Divert | *(none)* | Price Override and Free of Charge no longer divert, because BR-NEW-002 and BR-NEW-003 enforce limits at capture. |

### BR-NEW-002 — Rep discount allowance and promotion interaction

A rep may lower a line's price by up to an allowance, expressed as a percentage and configured by a manager through a commercial policy profile. Assigning a product to an eligible profile makes that policy available on the tablet; products outside a policy cannot be discounted by the rep. An override inside the allowance is applied, not requested.

1. **Base price** is the best non-promotional price: tier, list or quantity break. Quantity breaks are bulk pricing, not an offer.
2. **No promotion on the line** — the rep can go down to the allowance off the base price.
3. **Promotion no larger than the allowance** — the rep's discount stacks on the promotion price.
4. **Promotion larger than the allowance** (measured as head office set it, against tier or list) — the rep can't stack on it, but can still go down to the allowance off the base price if that is lower than the promotion. Only if the promotion is lower than that too is the line blocked.
5. **Buy X get Y, bundle, mix and match** — lines in these promotions never take a rep discount, whatever the promotion's size.
6. **Spend threshold** — whether the order qualifies is checked on resolved prices, before rep discounts. *Assumed:* the threshold discount then applies to the order total as it finally stands (RC-NEW-006).

**Rationale:** rule 4's fallback keeps the system monotonic — running a promotion never lowers the best price a rep can reach. Rule 5 is the one accepted exception, traded for a rule a rep can state in one sentence.

**Note for wherever the allowance is set:** because of rule 3, the effective maximum discount is nearly twice the allowance (a 9% promotion plus a 10% rep discount is about 18% off).

**Data:** the applicable policy rules and product membership must be in the tablet snapshot (brief §11). "Profile" is a working model, not yet a settled entity: it may become explicit business rules and must not be conflated with the catalogue's existing one-per-product Product Profile without resolving that model first.

### BR-NEW-003 — Free of charge on discontinuing stock only

1. A free-of-charge line can only be a product in the Discontinuing state.
2. A manager assigns eligible products to a commercial policy profile and sets an FOC quantity allowance per rep per calendar month.
3. Products outside an eligible policy cannot be added free of charge. Inside the rep's remaining monthly allowance, the line is applied; no head office decision.
4. An FOC item is an ordinary order line whose resolved price is €0.00. Stock, availability, allocation, despatch and removal behave exactly as for any other product line.
5. The quantity counts against the rep's monthly allowance while the order line exists. Reducing or removing the line releases that quantity. If processing removes it through the Unavailable Line path (BR-NEW-001), the returned allowance reaches the tablet at Sync.

**Data:** the applicable policy, product membership, rep's month-to-date FOC use and remaining allowance must be in the tablet snapshot. The tablet derives its offline balance from the snapshot plus its local order lines.

### BR-NEW-004 — Structured Visit Due Reason Type

1. A Visit Due may continue to have no Due Reason.
2. When a manager supplies a Due Reason, they choose a structured Reason Type and may enter the free-text explanation carried by the existing model.
3. The manager/admin chooses the Reason Type's icon and colour from a controlled system set. The type supplies stable icon, colour, filter and reporting semantics; the explanation supplies the visit-specific context.
4. Colour is never the only indicator: the UI pairs it with the type icon and an accessible type label.
5. An authorised manager or administrator maintains the Reason Type vocabulary.
6. The Reason Type and explanation travel with the Visit Due to the rep website and tablet snapshot.

**Presentation:** colour is never offered as a free-form value and never acts as the only signal.

### BR-NEW-005 — Many Location Profiles, defaults resolved field by field

1. A Location may have **any number** of Location Profiles (for example "Large pharmacy", "Rural shop", "Customer campaign X").
2. Any profile may carry defaults for Visit Frequency, Visit Duration and per-product Low Stock Thresholds. A profile carrying none is purely a grouping, used for selection such as specialist scopes, campaign filters and reporting.
3. When several profiles supply the same default, each field resolves separately and the **most demanding value wins**:
   - **Visit Frequency** — the most frequent.
   - **Visit Duration** — the longest.
   - **Low Stock Threshold** (per product) — the highest, so the hint appears earliest.
4. A per-Location override beats every profile, as the source stories already allow.
5. Every resolved default is shown with its source, as prices and owners are: `Every 2 weeks (from Large pharmacy)`, `Warn at 12 (from Rural shop)`. When profiles tie, all of them are named.
6. A Location with no profile carrying a Visit Frequency generates no Recurring Visit Dues and appears on the No Visit Schedule list (Customer Directory, unchanged).

**Example:** Byrne's is Large pharmacy (every 2 weeks, 45 min, SPF30 warn at 6) and Rural shop (every 4 weeks, 20 min, SPF30 warn at 12). It resolves to every 2 weeks, 45 minutes, warn at 12.

**Trade-off accepted:** the resolved set can mix profiles, so it may match no single profile a manager set up. Showing the source of each value is the mitigation.

**Superseded:** "a Location carries a Location Profile" (one per Location) in the Customer Directory, Visit Planning and Rep at a Location glossaries.

**Affects:** Coverage US-006, where a Location Profile scope matches a Location holding that profile among any others; Visit Planning Visit Due generation; the tablet's Low Stock Hint (resolved thresholds must be in the snapshot); M-03 campaign filters.

### BR-NEW-007 — "As of sync" only when the last sync wasn't today

*Settled 24 Sep 2026 (T8.1, convention §3 rule 4).*

1. Server-derived facts on the tablet (order status, despatch figures, Run-out Remaining, "back in stock around" dates, availability) carry **no** "as of sync" wording when the tablet has synced today.
2. When the last sync was before today, each such fact carries "as of [day date]", e.g. "Accepted, partly sent — as of Mon 21 Sep"; "About 120 left as of Mon 21 Sep".
3. The chip still attaches to the fact, one per panel (T8.1), never to the page.

**Supersedes:** the always-on "as of HH:MM sync" wording in brief §2.1 and in the scenarios that quote it: Rep at a Location glossary (Run-out, Temporarily Unavailable) and US-010, US-014; Head Office Order Processing US-001 and the partial-release scenario; Range Lifecycle's Run-out scenario. Those scenarios hold when the last sync was before today, with the date in place of the time.

**Example:** Colm synced at 07:42 today: T-08 reads "Accepted, partly sent". Colm didn't sync today, last synced Mon 21 Sep: T-08 reads "Accepted, partly sent — as of Mon 21 Sep".

### BR-NEW-006 — Work is judged as captured

*Settled 24 Sep 2026 (T1.1).*

1. An Order or Call uploaded from the tablet is validated against the business rules **in force when it was captured**, as held in the rep's snapshot, not against the rules at upload.
2. A business-rule change after capture never rejects the work. Examples: the Location was reassigned to another rep, or archived; a price, tier or promotion changed (already the case: prices resolve from the morning snapshot); a commercial policy or allowance changed.
3. Needs Attention on T-01 therefore holds **only technical faults**, such as an incomplete or corrupt upload or a duplicate submission, never a business-rule rejection.
4. **Supply facts are not business rules.** Stock and availability are still handled after capture by the existing dispositions (BR-NEW-001: Route to allocation, Auto-resolve an unavailable line with the rep prompted). The order is accepted; what can be supplied is a separate question.

**Superseded:** US-017's example rejections "Location no longer exists" and T-01's "Rejected: this location is no longer assigned to you".

---

### BR-NEW-008 — A chain's Agreed Ranges are catalogue Ranges assigned to it

*Settled 26 Sep 2026 (C5.4). **Revised the same day:** Colm — "there are no real chain ranges, there are just ranges in general"; "for now a chain doesn't have its own … range, may be added in a later iteration".*

0. **There are no chain-specific product lists in this iteration.** A chain's **Agreed Ranges** are ordinary catalogue Ranges (Range Lifecycle; built on H-18), such as "Everyday" and "2026 Christmas gift packs", **assigned** to the Master Location as Ranges are assigned to any customer. A Range Review agrees which Ranges the chain takes, as whole Ranges (T12.2). Chain-owned ranges may come in a later iteration.
1. A Master Location may be assigned several Ranges. A product may belong to more than one of them (C5.10), which is ordinary for catalogue Ranges.
2. The customer multi-branch grid offers the chain's Agreed Ranges in a dropdown; the signed-in buyer's personal default appears automatically on opening when they have set one (C5.3, C5.6). Until then, the first dropdown option is shown as a provisional fallback (C5.7), without saving a personal default. Entered product rows and quantities remain visible at the top of the grid when the buyer changes ranges (C5.2, C5.8).
3. As before, Agreed Ranges guide ordering rather than restricting it; permitted products outside them remain reachable.
4. A buyer with access to the chain grid may designate their own default range (C5.5–C5.6). Changing it affects only that buyer.
5. *Settled 26 Sep 2026, revised:* company head office creates Ranges, as it already does for the catalogue (H-18, Range Lifecycle). Reps never create a Range. On a Range Review they propose **assigning or removing whole Ranges** for the chain (T12.2); they never change a Range's products, because a Range is shared with every customer and rep assigned it. Rejected: a rep proposing a new range; product-level adds and drops on a chain's range (the original Master & Branch US-002/US-003 model and T12.1).

**Supersedes:** Master & Branch Ordering's assumption that a chain has one Agreed Range across all branches, **and its model of an Agreed Range as a chain-specific product list changed product by product.** "Agreed Range" now means a catalogue Range assigned to the chain.

**Open impacts:** Range Review proposals assign or remove whole Ranges (T12.2), confirmed per Range on H-04; the branch Order Pad is settled as stacked range sections oldest range first, which puts the everyday range first by convention (T7.15–T7.18); the rep/manager multi-branch grid uses a range picker (M11.4); how the master Location view and tablet snapshot present several ranges and overlapping membership. Dropdown sort order is deferred; the current first option is the initial fallback.

---

### BR-NEW-009 — Orders wait as Pending until a cut-off, then are accepted automatically

*Settled 26 Sep 2026.* Colm: orders "shouldn't be accepted by default" on receipt. Chosen over a person accepting every order, and over holding flagged orders for a person.

1. A synced rep order, or a placed customer order, is **Pending** until the next **order cut-off**.
2. At the cut-off it is accepted automatically, with BR-NEW-001's dispositions applied then (shortfall routed to allocation, unavailable lines removed, annotations recorded). Still no person accepts orders; H1.6 stands.
3. Until the cut-off, the rep can edit a Pending order on the rep website (R-04). Whether a customer can edit their own Pending order is open (C4.1 made placement final).
4. *Settled 26 Sep 2026:* the company sets a cut-off time **for each day of the week**, and a day may have none (for example 4pm Monday to Thursday, 1pm Friday, none at weekends). An order synced or placed after a day's cut-off, or on a day without one, waits for the next cut-off ("You can change this until 4pm Monday"). Rejected: one daily time for the company; different times by region or delivery run. Which head office screen holds the setting is open (not yet drawn).

**Supersedes:** "accepted on receipt" and "when it is received … accepted" in BR-NEW-001, US-NEW-005, Head Office Order Processing US-008, and Self-service (glossary, US-005, US-006, US-009): read "at the cut-off".

---

## New stories

### US-NEW-001: As a Field Salesperson, I want to enter a quantity when I add a Low item to the order so that the line reflects what the customer just agreed, not a number the system guessed.

> **Context:** extends the Low-marking behaviour in Area 1 US-006 to US-011 *(confirm which)* and order entry in Area 1 US-012. Suggested quantities were considered and dropped: the data model holds a low-stock *threshold*, not a target level, and a pre-filled number anchors the rep.

**Acceptance Criteria:**

**AC-NEW-001-1:**
- **Given** I have ticked Low on a stock-check line for an orderable product
- **When** I tap Add to order
- **Then** a quantity popover opens showing the product name, the count I recorded, the resolved price with its source, and an empty quantity field

**AC-NEW-001-2:**
- **Given** the quantity popover is open
- **When** I enter a quantity that brings a quantity break into reach
- **Then** the break prompt appears beneath the price and updates as I type

**AC-NEW-001-3:**
- **Given** I have entered a valid quantity
- **When** I tap Add to order
- **Then** the line is added to the order at that quantity and the item shows as ADDED with its quantity on the Low tab

**AC-NEW-001-4:**
- **Given** the quantity popover is open
- **When** I tap Cancel
- **Then** nothing is added and the item remains not added

**AC-NEW-001-5:**
- **Given** the product is measure-based
- **When** I enter a quantity below the minimum or off the step
- **Then** the existing measure validation message is shown and Add to order is unavailable until corrected

**AC-NEW-001-6:**
- **Given** an item on the Low tab is not added or REMOVED
- **When** I tap Add
- **Then** the same quantity popover opens

**AC-NEW-001-7:**
- **Given** any route into the quantity popover
- **When** it opens
- **Then** the quantity field is empty — it is never pre-filled

**Recommended Acceptance Tests:**

- `Should_OpenQuantityPopover_When_AddToOrderTappedOnLowItem`
  → Verifies Add to order on a Low line opens the popover rather than adding immediately.
- `Should_OpenWithEmptyQuantity_When_PopoverOpens`
  → Verifies no suggested quantity is ever pre-filled.
- `Should_UpdateBreakPromptLive_When_QuantityApproachesBreak`
  → Verifies the break prompt reacts to the typed quantity.
- `Should_AddLineAndMarkAdded_When_QuantityConfirmed`
  → Verifies the line is added and the Low tab state becomes ADDED.
- `Should_LeaveItemNotAdded_When_PopoverCancelled`
  → Verifies cancel has no side effects.
- `Should_UseSamePopover_When_AddTappedOnLowTab`
  → Verifies both entry points share one component.

---

### US-NEW-002: As a Field Salesperson, I want the Low tab to show what happened to every item I marked Low so that I can see at a glance whether anything the customer is short of hasn't made it onto the order.

> **Context:** extends order entry, Area 1 US-012 / US-013. Most Low items are added at tick-time (US-NEW-001); the Low tab is a catch-up list for anything skipped. Presented as a tab beside Order pad and Search all.

**Acceptance Criteria:**

**AC-NEW-002-1:**
- **Given** the order follows a stock check with items marked Low
- **When** I open the Low tab
- **Then** every Low item is listed with exactly one state: not added; ADDED with its quantity; REMOVED; REPLACED naming the replacement and its quantity; or CAN'T ADD with the availability reason

**AC-NEW-002-2:**
- **Given** the Low tab is shown in the tab strip
- **When** any items are not added or CAN'T ADD
- **Then** the tab label shows a count of those items only; ADDED, REMOVED and REPLACED are not counted

**AC-NEW-002-3:**
- **Given** a Low item was added to the order
- **When** I remove its line from the order
- **Then** the item shows REMOVED with an Add action

**AC-NEW-002-4:**
- **Given** I marked Low on an unavailable product and added a replacement
- **When** I open the Low tab
- **Then** the item shows REPLACED with the replacement product and quantity

**AC-NEW-002-5:**
- **Given** a Low item is unorderable and no replacement was added
- **When** I open the Low tab
- **Then** it shows CAN'T ADD with the reason and a Find replacement action, and no quantity control

**AC-NEW-002-6:**
- **Given** there are several not added items
- **When** I view the Low tab
- **Then** there is no action that adds them all at once

**AC-NEW-002-7:**
- **Given** a Low item's product is on the order
- **When** I untick Low on the stock-check line
- **Then** the order line is unchanged

**Recommended Acceptance Tests:**

- `Should_ShowExactlyOneState_When_LowTabListsItem`
  → Verifies every item resolves to one of the five states.
- `Should_CountOnlyNotAddedAndCantAdd_When_LowTabLabelRendered`
  → Verifies the tab count excludes handled items.
- `Should_ShowRemoved_When_AddedLineRemovedFromOrder`
  → Verifies a deliberate removal is distinguished from never-added.
- `Should_ShowReplacementDetail_When_ReplacementAdded`
  → Verifies REPLACED names what went in instead.
- `Should_HideQuantityControl_When_ItemCantBeAdded`
  → Verifies unorderable items offer Find replacement only.
- `Should_NotOfferAddAll_When_MultipleItemsNotAdded`
  → Verifies there is no bulk add.
- `Should_KeepOrderLine_When_LowUnticked`
  → Verifies unticking Low doesn't remove an order line.

**Additional Acceptance Criteria (24 Sep 2026 — RC-NEW-007 resolved, T7.13):**

> **Context:** an order started with no Call of its own (e.g. T-05 → New order for a phoned-in order). The tab stays, and carries the gaps from the Location's most recent Call.

**AC-NEW-002-8:**
- **Given** Carey's most recent Call on Tue 15 Sep left Nappy Wipes not added and Aftersun CAN'T ADD, and Sudocrem not added
- **And** Sudocrem was ordered at Carey's on 18 Sep
- **When** I start an order at Carey's with no Call and open the Low tab
- **Then** it reads "Still open from your call at Carey's - Tue 15 Sep" and lists Nappy Wipes (not added) and Aftersun (CAN'T ADD) only

**AC-NEW-002-9:**
- **Given** the Low tab shows carried items
- **When** the tab label renders
- **Then** the count includes them under AC-NEW-002-2, and Add and Find replacement behave as for any Low item

**AC-NEW-002-10:**
- **Given** the most recent Call at the Location was 10 weeks ago
- **When** I start an order with no Call
- **Then** its gaps are still carried, labelled with that Call's date

**AC-NEW-002-11:**
- **Given** the Location has no earlier Call, or its most recent Call has no gaps left
- **When** I open the Low tab on an order with no Call
- **Then** it shows Low (0) and "No stock check with this order." with a Record call link

**AC-NEW-002-12:**
- **Given** the Low tab on an order with no Call shows carried items
- **When** I use Record call and save a Call at the Location
- **Then** the order is linked to that Call and the Low tab shows that Call's Low items in place of the carried ones

**Recommended Acceptance Tests:**

- `Should_CarryGapsFromMostRecentCall_When_OrderHasNoCall`
  → Verifies only not added and CAN'T ADD items carry.
- `Should_ExcludeCarriedItem_When_ProductOrderedSinceCall`
  → Verifies later orders by any route close a gap.
- `Should_CarryRegardlessOfAge_When_LastCallIsOld`
  → Verifies there is no cut-off; the date label is the guard.
- `Should_ShowEmptyState_When_NoEarlierCallOrNoGaps`
  → Verifies the tab stays with an explanation and a Record call link.
- `Should_ReplaceCarriedRows_When_CallRecordedDuringOrder`
  → Verifies a new Call supersedes the carry-over.

---

### US-NEW-003: As a Field Salesperson, I want to be told on Home when a line was removed from one of my orders so that I can tell the customer before their delivery arrives short.

> **Context:** follows from BR-NEW-001 auto-resolve. For most shops the rep is the only channel to the customer, and the order total has changed since the rep quoted it. Touches T-02 (Area 1 US-003) and T-08 (Area 1 US-014).

**Acceptance Criteria:**

**AC-NEW-003-1:**
- **Given** a line on one of my orders was removed because the product was unavailable
- **When** the tablet next syncs
- **Then** Home shows a "not supplied" count in the exception strip alongside the unsent, cycle-decision and conflict counts

**AC-NEW-003-2:**
- **Given** the "not supplied" count is above zero
- **When** I tap it
- **Then** I see each affected order with its location, the removed line or lines, and the reason

**AC-NEW-003-3:**
- **Given** I open an affected order
- **When** T-08 renders
- **Then** the removed line reads "Not supplied — [reason]. Removed from the order. Let the customer know."

**AC-NEW-003-4:**
- **Given** no lines have been removed from my orders
- **When** Home renders
- **Then** no "not supplied" count is shown

**Recommended Acceptance Tests:**

- `Should_ShowNotSuppliedCount_When_LineAutoRemovedAfterSync`
  → Verifies the prompt appears after the sync that brings the removal down.
- `Should_ListAffectedOrdersWithReason_When_CountTapped`
  → Verifies the count leads to the orders and reasons.
- `Should_PromptToTellCustomer_When_SentItemHasRemovedLine`
  → Verifies T-08 wording on the removed line.
- `Should_HideCount_When_NoLinesRemoved`
  → Verifies the strip doesn't show a zero counter.

**Additional Acceptance Criteria (24 Sep 2026 — RC-NEW-001 resolved):**

> **Context:** reps phone the shop the same day, so the rep declares the call was made. The next Call at the Location is a backstop for a skipped tap. The count counts orders, because one call covers a whole order. See T2.6 and T8.3 in `01-tablet-day.md`.

**AC-NEW-003-5:**
- **Given** an order in the "not supplied" list or open on T-08 has removed lines not yet marked told
- **When** I tap "Told them"
- **Then** every such removed line on that order is recorded as told with the time, and the order no longer counts toward the "not supplied" count

**AC-NEW-003-6:**
- **Given** I have just marked an order told in the "not supplied" list
- **When** the list is still open
- **Then** the order stays in the list, greyed, showing "Told [time]" with "Undo", and leaves the list only once I leave it

**AC-NEW-003-7:**
- **Given** I marked an order told by mistake
- **When** I tap "Undo" in the list or on T-08
- **Then** the told record is removed and the order counts toward the "not supplied" count again

**AC-NEW-003-8:**
- **Given** one or more orders at a Location have removed lines not yet marked told
- **When** I log a Call at that Location, by visit or by phone
- **Then** those lines are recorded as told by that Call, and the orders no longer count

**AC-NEW-003-9:**
- **Given** an order whose removed lines are all marked told
- **When** a later sync removes a further line from it
- **Then** the order counts again, only the new line is flagged, and the earlier lines keep their told record

**AC-NEW-003-10:**
- **Given** I open an affected order on T-08
- **When** I leave it without tapping "Told them"
- **Then** the order still counts

**Recommended Acceptance Tests:**

- `Should_MarkAllRemovedLinesTold_When_ToldThemTappedOnOrder`
  → Verifies the tap is per order and records every open removed line.
- `Should_KeepToldOrderInListWithUndo_When_ListStillOpen`
  → Verifies mis-tap recovery until the rep leaves the list.
- `Should_RestoreCount_When_ToldUndone`
  → Verifies Undo reverses the told record.
- `Should_ClearNotSuppliedAtLocation_When_CallLoggedThere`
  → Verifies the Call backstop covers every affected order at the Location.
- `Should_RaiseOrderAgainWithNewLineOnly_When_LineRemovedAfterTold`
  → Verifies a later removal is treated as a new issue.
- `Should_KeepCount_When_OrderOpenedWithoutTold`
  → Verifies viewing is not a completion signal.

> ~~**Open:** what clears the count (RC-NEW-001).~~ **Resolved (24 Sep 2026)** — AC-NEW-003-5 to -10.

---

### US-NEW-004: As a Field Salesperson, I want lines with an unusually high quantity for this shop marked when I review an order so that I catch a mistyped quantity before it's sent.

> **Context:** extends Area 1 US-012 / US-013. With no human acceptance at head office (BR-NEW-001), Review is the last point a slip (48 typed as 480) can be caught. Large stays an annotate flag.

**Acceptance Criteria:**

**AC-NEW-004-1:**
- **Given** a line's quantity is well above what this location ordered of that product on its last 3 accepted orders
- **When** I view Review order
- **Then** the line shows "Usually about [N] here"

**AC-NEW-004-2:**
- **Given** the location has no accepted order containing the product
- **When** I view Review order
- **Then** no marker is shown on that line

**AC-NEW-004-3:**
- **Given** a line's quantity is well below the location's usual
- **When** I view Review order
- **Then** no marker is shown

**AC-NEW-004-4:**
- **Given** one or more lines are marked
- **When** I tap Mark Ready to Send
- **Then** the order is marked Ready to Send without any confirmation or acknowledgement step

**AC-NEW-004-5:**
- **Given** the tablet has no connection
- **When** I view Review order
- **Then** markers are still calculated, from the order history already in the snapshot

**Recommended Acceptance Tests:**

- `Should_MarkLine_When_QuantityWellAboveLastThreeOrders`
  → Verifies the marker and its "usually about" figure.
- `Should_NotMarkLine_When_NoOrderHistoryForProductAtLocation`
  → Verifies first-time products are never marked.
- `Should_NotMarkLine_When_QuantityBelowUsual`
  → Verifies high-only behaviour.
- `Should_NotBlockReadyToSend_When_LinesMarked`
  → Verifies the marker is a non-blocking fallback.
- `Should_CalculateMarkerOffline_When_NoConnection`
  → Verifies it uses snapshot data.

> **Note:** the threshold for "well above" is an implementation detail, not a design decision.

---

### US-NEW-005: As a Head Office user, I want orders that are within policy to go through without my acceptance so that my worklist only holds things that genuinely need a decision.

> **Context:** replaces the order-decision parts of Head Office US-001 to US-003. Implements BR-NEW-001. *Amended 26 Sep 2026 (BR-NEW-009):* in AC-NEW-005-1 to 3, "When it is received" reads "At the next order cut-off"; until then the order is Pending and the rep can edit it on R-04.

**Acceptance Criteria:**

**AC-NEW-005-1:**
- **Given** a synced order has no flags, or only annotate flags
- **When** it is received
- **Then** it is accepted and released to the warehouse without a human step, and its flags are recorded on the order

**AC-NEW-005-2:**
- **Given** a synced order has a line flagged Stock Shortfall or Oversold
- **When** it is received
- **Then** the order is accepted, what's available is released, and the short quantity becomes outstanding and appears in allocation (H-10)

**AC-NEW-005-3:**
- **Given** a synced order has a line that is now Unavailable
- **When** it is received
- **Then** the order is accepted, that line is removed with its reason recorded, and the capturing rep is prompted (US-NEW-003)

**AC-NEW-005-4:**
- **Given** I open the worklist
- **When** it renders
- **Then** it contains only range proposals and duplicate matches, each labelled with its type *(customer account requests moved to M-17, 26 Sep 2026)*

**AC-NEW-005-5:**
- **Given** there are no range proposals or duplicate matches
- **When** I open the worklist
- **Then** it reads "Nothing needs a decision." with no action offered

**AC-NEW-005-6:**
- **Given** I open an order's detail
- **When** H-02 renders
- **Then** it shows the lines as captured, any line removed automatically with its reason, applied rep prices and free-of-charge lines with their working, the annotations as plain sentences, and "Total as captured" and "Total accepted" labelled separately

**AC-NEW-005-7:**
- **Given** I am viewing an order's detail
- **When** I look for decision controls
- **Then** there are no Accept, Partial release or per-line override and free-goods decisions

**Recommended Acceptance Tests:**

- `Should_AcceptAndRelease_When_OrderHasOnlyAnnotateFlags`
- `Should_RouteShortQuantityToAllocation_When_LineShort`
- `Should_RemoveLineAndPromptRep_When_LineUnavailable`
- `Should_ExcludeOrders_When_WorklistRendered`
- `Should_ShowGoodNewsEmptyState_When_NothingNeedsDecision`
- `Should_ShowBothTotals_When_LineRemovedAutomatically`
- `Should_OfferNoDecisionControls_When_OrderDetailViewed`

---

### US-NEW-006: As an authorised manager or administrator, I want to maintain Visit Due Reason Types so that planners use consistent reasons that can be recognised, filtered and reported.

> **Context:** extends Visit Planning's free-text Due Reason with a managed structured type (BR-NEW-004). The manager/admin chooses its icon and colour from a controlled system set.

**Acceptance Criteria:**

**AC-NEW-006-1:**
- **Given** I am authorised to maintain Reason Types
- **When** I create a type with a unique name
- **Then** it becomes available when a manager supplies a Due Reason

**AC-NEW-006-2:**
- **Given** a Reason Type is referenced by existing Visit Dues
- **When** I rename it
- **Then** those visits show the new name while retaining their original reason explanation

**AC-NEW-006-3:**
- **Given** a Reason Type is referenced by existing or historical Visit Dues
- **When** I retire it
- **Then** it is archived rather than deleted, remains visible on those visits and is unavailable for new reasons

**AC-NEW-006-4:**
- **Given** another active Reason Type already has the same name
- **When** I try to save a duplicate
- **Then** the save is rejected with a message naming the existing type

**AC-NEW-006-5:**
- **Given** I create or edit a Reason Type
- **When** I choose its presentation
- **Then** I select an icon and colour from the controlled system set
- **And** the UI previews them together with the type name

**Recommended Acceptance Tests:**

- `Should_AddReasonType_When_NameIsUnique`
  → Makes the type available for new Visit Dues.
- `Should_PreserveExistingReasons_When_TypeRenamed`
  → Historical references remain intact.
- `Should_ArchiveInsteadOfDelete_When_TypeIsReferenced`
  → Existing and historical visits retain their type.
- `Should_RejectReasonType_When_ActiveNameAlreadyExists`
  → Prevents indistinguishable options.
- `Should_SaveReasonType_When_IconAndColourChosenFromControlledSet`
  → Keeps planner indicators configurable but visually constrained.

---

### US-NEW-007: As a Customer User, I want to land on the products I usually order for this shop, marked when someone else has already ordered them, so that I can build an order without hunting and without ordering twice.

> **Context:** settled 24–25 Sep 2026 (C-09, C2.1–C2.2, `06-customer.md`). Customers buy the same products but rarely the same order, so the landing page is built around frequent products, not past orders. Folded into Self-service US-003, US-004 and US-005 as UX amendment scenarios.

**Acceptance Criteria:**

**AC-NEW-007-1:**
- **Given** I am linked to Hickey's Rathdrum and Hickey's Arklow
- **When** I sign in, on any visit
- **Then** I am asked which shop the order is for, and my previous choice is not preselected

**AC-NEW-007-2:**
- **Given** I have ordered Hand Cream 75ml for Rathdrum on two of my own orders in the last six months, and Arnica Gel once
- **When** I open Rathdrum's page
- **Then** Hand Cream is under "Your usual products" and Arnica Gel is not

**AC-NEW-007-3:**
- **Given** the rep has ordered Sudocrem 125g for Rathdrum many times, and I never have
- **When** I open Rathdrum's page after my 3rd order
- **Then** Sudocrem is not under "Your usual products"

**AC-NEW-007-4:**
- **Given** I have placed fewer than 3 orders for Rathdrum
- **When** I open Rathdrum's page
- **Then** a separate section "Often ordered for Hickey's Rathdrum" lists products ordered at least twice in the last six months on any order for the Location, from any source

**AC-NEW-007-5:**
- **Given** I have just submitted my 3rd order for Rathdrum
- **When** I next open Rathdrum's page
- **Then** the "Often ordered for" section is no longer shown

**AC-NEW-007-6:**
- **Given** the rep ordered 6 Sudocrem for Rathdrum on 2 Oct and it has not been despatched
- **When** I view Sudocrem on the page
- **Then** it shows "Ordered 2 Oct by your rep · 6 on the way", and I can still add it without any confirmation

**AC-NEW-007-7:**
- **Given** that order was despatched on 4 Oct
- **When** I view Sudocrem within the company's expected-delivery period after despatch
- **Then** it shows "Despatched 4 Oct · expected soon"; after that period, no mark is shown

**AC-NEW-007-8:**
- **Given** a colleague's order for Rathdrum has Hand Cream with 24 of 36 despatched
- **When** I view Hand Cream on the page
- **Then** the mark shows both what was sent and what is outstanding

**AC-NEW-007-9:**
- **Given** I am on Rathdrum's page
- **When** I tap Add on Hand Cream 75ml
- **Then** a quantity popover opens with an empty field; on Add it closes, I stay on the page, and the row reads "In order · 12"

**AC-NEW-007-10:**
- **Given** I am on Rathdrum's page
- **Then** there is no summary of open orders on it; order history is reached by a link

**AC-NEW-007-11:**
- **Given** the rep ordered a product for Rathdrum that is neither in my usual products nor in the starting-point section
- **When** I find it through search or the catalogue
- **Then** its row carries no mark (C9.9)

**AC-NEW-007-12:**
- **Given** I am a buyer at Hickey's Head Office and can order for its head office location and 12 branches
- **When** I open the location choice on any visit
- **Then** "Order for several branches" is the primary action and opens C-05 directly
- **And** the head office location and eligible branches remain below for single-location orders, with none preselected

**Recommended Acceptance Tests:**

- `Should_AskForShop_When_UserHasSeveralLocations_EveryVisit`
  → Verifies the shop choice is never remembered.
- `Should_ListProduct_When_OwnOrdersContainItTwiceInSixMonths`
  → Verifies the frequency rule and the own-orders scope.
- `Should_NotListProduct_When_OnlyOthersOrderedIt`
  → Verifies other people's orders don't feed the usual list.
- `Should_ShowShopRegulars_When_FewerThanThreeOwnOrders`
  → Verifies the starting-point section and its removal.
- `Should_MarkProduct_When_AnotherUserOrderedItForTheLocation`
  → Verifies the on-the-way and despatched stages, and that adding isn't blocked.
- `Should_ClearDespatchedMark_When_ExpectedDeliveryPeriodPasses`
  → Verifies the second stage ends on the company setting.
- `Should_OpenEmptyPopoverAndStay_When_AddTapped`
  → Verifies no prefill and no navigation.
- `Should_LeadWithChainOrder_When_HeadOfficeBuyerChoosesOrderingPath`
  → Verifies the chain buyer can reach C-05 without first selecting one location.

> **Note:** the expected-delivery period is a company setting. Mark wording (C9.8) was confirmed 26 Sep 2026: "your rep", a colleague's name, or the chain's head office by name.

---

## Amendments to existing stories

### Self-service US-001 — creating the account from the tablet

> **Context:** settled 26 Sep 2026 (C8.1). The rep sets up online ordering on the tablet, from the contact reached through T-05, usually with the contact present. The request uses the background channel settled for customer requests (C1.5, C1.7), so it reaches the manager's approval queue as soon as the tablet has signal. Orders and calls still sync manually (T1.3).

**AC-SS001-A:**
- **Given** I am at Hickey's Rathdrum with the tablet
- **When** I open Mary Walsh from the Location screen and choose "Set up online ordering"
- **Then** I can confirm the invitation email and see "Mary will be able to order for: Hickey's Rathdrum"
- **When** I confirm
- **Then** the account is Pending approval and reaches my manager's queue as soon as the tablet has signal, without a manual sync

**AC-SS001-B:**
- **Given** Mary Walsh already has a login
- **When** I open Mary from the Location screen
- **Then** I see "Online ordering: Active" instead of "Set up online ordering"

> **Context:** settled 26 Sep 2026 (C8.2, M17.1). "My manager's queue" in US-001 S1 is a new manager page, **M-17 Online ordering approvals**. The manager is emailed with a link to it whenever a rep sets up an account, or a re-invitation arrives for a Location with no assigned rep.

**AC-SS001-C:**
- **Given** Colm has set up online ordering for Mary Walsh
- **Then** Colm's manager is emailed with a link to Online ordering approvals
- **And** the page lists Mary's account with who set it up, when, the email and "Will be able to order for: Hickey's Rathdrum"
- **When** the manager chooses Approve
- **Then** the invitation is sent to Mary and the item leaves the list
- **When** the manager chooses Decline instead
- **Then** a reason is required before the decline is saved, and Colm sees it

**AC-SS001-D:**
- **Given** Tom Kelly at Byrne's Londis, which has no assigned rep, has requested a new invitation
- **Then** the manager who sees Byrne's Londis as Unassigned is emailed, and the request appears on Online ordering approvals with "Send new invitation"

**AC-SS001-E:** *(C8.3, settled 26 Sep 2026)*
- **Given** the manager declined Mary Walsh's account with "Contact has left the business"
- **When** Colm's tablet has signal
- **Then** T-02 Home's Customer requests section shows "Mary Walsh: online ordering declined: Contact has left the business" without Colm syncing
- **When** Colm chooses OK
- **Then** the notice is removed

**AC-SS001-F:** *(C8.4, settled 26 Sep 2026)*
- **Given** I am a manager on M-07 for Hickey's Rathdrum
- **When** I choose "Set up online ordering…" and pick Mary Walsh
- **Then** I see "Mary Walsh will be able to order for: Hickey's Rathdrum" and confirm the email
- **When** I save
- **Then** the invitation is sent immediately, with no approval step

---

### Self-service US-002 — requesting a new invitation

> **Context:** settled 26 Sep 2026 (C1.1). An expired invitation page offers "Request a new invitation". The request is not self-approving: it goes to the rep or manager, and a new invitation is sent only when one of them approves it. This replaces US-002 S2's "can request a new one" with an approved request, and removes any ambiguity with the "contact your sales representative" wording.

**AC-SS002-A:**
- **Given** Mary Walsh's invitation has expired
- **When** Mary follows the link
- **Then** Mary sees that the invitation has expired and a "Request a new invitation" action
- **When** Mary requests one
- **Then** Mary is told the request has been sent and that a new invitation will follow once it is approved
- **And** no new invitation is sent until the rep or manager approves the request

**AC-SS002-B:**
- **Given** Mary has already requested a new invitation that is awaiting approval
- **When** Mary follows the expired link again
- **Then** Mary sees that the request is awaiting approval, and no duplicate request is created

> **Context:** settled 26 Sep 2026 (C1.2). The account was approved when it was created (US-001 S1), so a re-invitation needs only the rep responsible for the contact's Location. With no assigned rep, the request goes to the manager who sees that Location as Unassigned (M15.2); confirmed 26 Sep 2026.

**AC-SS002-C:**
- **Given** Mary Walsh at Hickey's Rathdrum has requested a new invitation
- **Then** the rep responsible for Hickey's Rathdrum receives the request
- **When** the rep approves it
- **Then** a new invitation is sent to Mary, with no manager approval step

> **Context:** settled 26 Sep 2026 (C1.3). The rep acts on the request from T-02 Home on the tablet. Because the tablet syncs when the rep chooses, the request arrives at sync and the invitation leaves at the following sync. How it sits on Home (a fifth exception counter or its own section) is open.

> **Context:** settled 26 Sep 2026 (C1.4–C1.6). Requests sit in their own collapsed "Customer requests" section on Home, shown only when one is waiting; the exception strip is unchanged. They are delivered in the background whenever the tablet has signal — a narrow exception to T1.3 and Area 1 US-002's manual sync, which still governs all other work. The rep (or the manager, for an unassigned Location) is also emailed.

**AC-SS002-D:**
- **Given** Mary requested a new invitation this morning
- **Then** the Location's rep is emailed about the request
- **And** when the rep's tablet has signal, T-02 Home shows a "Customer requests (1)" section without the rep syncing
- **And** the request shows Mary's name, Hickey's Rathdrum, the request date and "Send new invitation"
- **When** the rep chooses "Send new invitation"
- **Then** a new invitation is sent to Mary as soon as the tablet has signal, without the rep syncing (C1.7)
- **And** if there is no signal, the request shows "Will send when in range" until it goes

**AC-SS002-E:**
- **Given** the rep has 3 orders waiting to send and no customer requests
- **When** the tablet regains signal
- **Then** no "Customer requests" section appears, and the 3 orders stay unsent until the rep syncs

---

### Self-service US-009 — repeat scope

> **Context:** settled 26 Sep 2026 (C7.1). A chain submission appears as one expandable group in history, but Repeat is offered only on an individual branch order. It starts a new, unsubmitted single-location order for that branch, using the existing full or selected-line repeat rules. Repeating a whole chain submission may be considered later.

**Additional Acceptance Criteria:**

**AC-SS009-A:**
- **Given** one chain submission created orders for Arklow and Bray
- **When** I view its expandable entry in history
- **Then** the group header has no Repeat action
- **When** I open the Arklow order and choose Repeat all or Repeat selected
- **Then** a new, unsubmitted single-location order for Arklow is prefilled from that branch order only
- **And** the Arklow and Bray originals remain unchanged

> **Context:** settled 26 Sep 2026 (C7.2). Repeat is offered on every order visible in history for the customer's Locations, whoever placed it. Prices resolve fresh, so a rep's discount on the original is not copied.

**AC-SS009-B:**
- **Given** my rep placed an order for Arklow with a rep discount on Hand Cream
- **When** I open that order from history
- **Then** Repeat all and Repeat selected are available, as on my own orders
- **When** I repeat it
- **Then** a new, unsubmitted Arklow order is prefilled at today's prices, without the rep discount
- **And** the rep's original order is unchanged

> **Context:** settled 26 Sep 2026 (C7.3). Each Location has one unplaced order. Repeat adds to it rather than replacing it; a new order is started only when none is in progress. This refines "a new order" in US-009 S1 and AC-SS009-A/B.

**AC-SS009-C:**
- **Given** Arklow's current unplaced order has 12 Hand Cream
- **And** a past Arklow order has 8 Sudocrem
- **When** I repeat that past order
- **Then** 8 Sudocrem is added to Arklow's current order
- **And** the 12 Hand Cream already there is kept
- **And** no second order for Arklow is started

> **Context:** settled 26 Sep 2026 (C7.4). A repeated product already in the current order stays on one line and takes the repeated order's quantity. One warning, before anything is applied, lists every quantity that will change; it never appears once per product.

**AC-SS009-D:**
- **Given** Arklow's current order has 6 Hand Cream and 4 Sudocrem
- **And** the order I repeat has 12 Hand Cream, 8 Sudocrem and 5 other lines
- **When** I choose Repeat all
- **Then** one warning lists Hand Cream 6 → 12 and Sudocrem 4 → 8
- **When** I choose Continue
- **Then** Hand Cream is 12 and Sudocrem is 8, each on one line, and the 5 other lines are added
- **When** I choose Cancel instead
- **Then** Arklow's current order is unchanged

**AC-SS009-E:**
- **Given** none of the repeated products is in Arklow's current order
- **When** I repeat an order
- **Then** no warning appears and the lines are added

---

### Self-service US-005, US-007 and US-008 — customer confirmation and post-submit access

> **Context:** settled 26 Sep 2026 (C4.1–C4.2). The customer reviews and corrects the order before choosing “Place order”. That action confirms the order. Automatic acceptance on receipt means there is no useful customer Pending edit window. After submission, the customer can view the order and status but cannot edit, cancel a line, or cancel the whole order in self-service. The placed-order detail shows the company phone number and email address beside the order reference so they can request a change; company-side amendment and cancellation mechanics require a separate operational decision. This supersedes the customer Pending edit/cancel scenarios in Self-service US-007. The read-only/contact rule also applies to orders placed through C-05. *Amended the same day (C4.3, BR-NEW-009):* orders now wait as Pending until the order cut-off, so the customer can edit or cancel their own order until then; the read-only and contact rules apply after the cut-off. AC-SS007-A is amended accordingly; see AC-SS007-C/D.

**Additional Acceptance Criteria:**

**AC-SS005-A:**
- **Given** I have an in-progress order for one Location
- **When** I review and correct its lines and quantities, then choose “Place order”
- **Then** that action confirms and submits the order
- **And** the placed order is shown with its current status

**AC-SS007-A:**
- **Given** I have placed an order and the order cut-off has passed *(amended by C4.3)*
- **When** I view it in history or detail
- **Then** I see no customer edit, line-cancel, or whole-order cancel action
- **And** I am directed to contact the company if I need to request a change

**AC-SS007-B:**
- **Given** I view a placed order that I need to change, including an individual branch order placed through C-05
- **Then** its order reference, the company's phone number, and the company's email address appear together
- **And** I am told to quote the reference when contacting the company

**AC-SS007-C:** *(C4.3, settled 26 Sep 2026)*
- **Given** I placed an order at 10:12 and the cut-off is 4pm
- **When** I open it before 4pm
- **Then** it reads "Pending" and "You can change this until 4pm today", with Edit and Cancel order
- **When** I choose Edit, change a quantity and choose Save changes
- **Then** the order stays Pending with the change, without placing it again

**AC-SS007-D:** *(C4.3)*
- **When** I choose Cancel order and confirm "Cancel order #12345? This can't be undone."
- **Then** the order is Cancelled and shows as Cancelled in history
- **And** an order placed by a colleague or my rep shows neither Edit nor Cancel order

**AC-SS007-E:** *(C4.4, settled 26 Sep 2026)*
- **Given** the cut-off is 4pm and I am editing my order at 3:55pm
- **Then** I see "This order locks at 4pm — 5 minutes left to save"
- **When** I save at 4:01pm
- **Then** my changes are not saved and the order stands as accepted at 4pm
- **And** I am told to contact the company, quoting the order reference, with its phone and email
- **And** if I only view the Pending order at 3:55pm without choosing Edit, no warning appears

**AC-SS008-A:**
- **Given** I can view orders placed by me, colleagues, or my rep for my Locations
- **When** I open any of their details
- **Then** the order and its fulfilment status are read-only to me

**AC-SS008-B:**
- **Given** one chain placement created orders for Arklow and Bray
- **When** I open order history
- **Then** I see one expandable entry for that chain submission
- **And** expanding it shows the distinct Arklow and Bray orders, each with its own reference, status and value
- **And** each branch row opens that branch's read-only order detail

**AC-SS008-C:**
- **Given** an Arklow order came from a chain submission with other branch orders
- **When** I filter order history to Arklow
- **Then** that Arklow order appears as an ordinary chronological row with its own reference, status and value
- **And** it is not wrapped in the expandable chain entry

---

### Self-service US-004 — curated browsing and grouped search

> **Context:** settled 26 Sep 2026 (C3.1–C3.3). One search returns the Customer's curated matches first and other products they may buy underneath. The buyer does not make a separate wider-search choice. Browsing starts with the curated catalogue; a “Your catalogue” / “All products” control switches scope on the same page and provides the way back. Restricted products remain hidden, and existing availability rules still apply. Exact control styling remains a drafting call.

**Additional Acceptance Criteria:**

**AC-SS004-A:**
- **Given** “Honey & Lemon Lozenges” belongs to my curated catalogue and “Sugar Free Lozenges” is outside it but permitted for me to buy
- **When** I search for “lozenge”
- **Then** the curated match appears under “From your catalogue”
- **And** the outside match appears below under “Other products you can buy” in the same results, without another search action

**AC-SS004-B:**
- **Given** my curated catalogue has no match but another permitted product does
- **When** I search for that product
- **Then** I see the other permitted match without widening or repeating the search
- **And** Restricted products never appear, and a product matching multiple assigned Ranges appears only once

**AC-SS004-C:**
- **Given** I have not entered a search query
- **When** I browse by category
- **Then** I initially see products from my Customer's assigned Ranges and unranged products
- **And** a “Your catalogue” / “All products” control lets me widen browsing to other products I am permitted to buy on the same page
- **And** Restricted products never appear in either browse view

**AC-SS004-D:**
- **Given** I am browsing my curated catalogue in a selected category
- **When** I choose “All products”
- **Then** the catalogue widens on C-03 without navigating away
- **When** I choose “Your catalogue”
- **Then** I return to the curated view in that category, where the category exists in both views
- **And** a product search continues to show both result groups regardless of the browsing control's selection

---

### Self-service US-006 — predefined products in the customer multi-branch grid

> **Context:** settled 25–26 Sep 2026 (C5.1–C5.16, BR-NEW-008). The chain buyer expects predefined products in distinct Agreed Ranges, such as “2026 Christmas gift packs”. A product may belong to several ranges. Each buyer may set their own default; until then, the first dropdown option is shown. Entered rows stay visible at the top as the buyer changes the dropdown, with no second row in the selected range. All eligible branches start selected; a branch with quantities in the current chain order cannot be removed until those quantities are cleared, and the blocked removal opens that branch's entries with an explanation. Clearing the last quantity does not remove the branch automatically. The desktop hierarchy is accepted for the first design round; implementation may refine the layout.

**Additional Acceptance Criteria:**

**AC-SS006-A:**
- **Given** Hickey's Head Office has a confirmed Agreed Range
- **When** I start a multi-branch order
- **Then** the grid's predefined products come from that Agreed Range, with no quantities prefilled
- **And** I can still find and order a permitted product outside it

**AC-SS006-B:**
- **Given** I have entered quantities for products in “2026 Christmas gift packs”
- **When** I switch to everyday products
- **Then** the entered Christmas product rows remain visible in the same grid with their quantities

**AC-SS006-C:**
- **Given** the chain has several named Agreed Ranges and I have set one as my default
- **When** I open a new multi-branch grid
- **Then** the range dropdown selects my default automatically and offers the other ranges
- **And** my default range's products are shown for entry with empty quantities

**AC-SS006-D:**
- **Given** Hickey's Head Office holds a default Agreed Range and a separate “2026 Christmas gift packs” Agreed Range
- **When** I choose the Christmas range from the dropdown
- **Then** its confirmed products appear for entry in the same chain order
- **And** entered rows from the default range remain visible with their quantities

**AC-SS006-E:**
- **Given** Hickey's has several Agreed Ranges
- **When** I designate “Everyday” as my default
- **Then** my next customer multi-branch grid opens with “Everyday” selected and its products available for entry

**AC-SS006-F:**
- **Given** I and another Hickey's buyer can both start a chain order
- **When** I set “2026 Christmas gift packs” as my default
- **Then** only my grid starts on that range; the other buyer's starting range is unchanged

**AC-SS006-G:**
- **Given** I have not set a personal default Agreed Range
- **When** I open a new multi-branch grid
- **Then** the first range in the dropdown is selected and its products are shown for entry
- **And** that automatic selection does not save a personal default for me

**AC-SS006-H:**
- **Given** I have entered quantities for products in “2026 Christmas gift packs”
- **When** I change the range dropdown to “Everyday”
- **Then** the entered product rows and their quantities remain at the top of the grid
- **And** the Everyday products available for entry appear below them

**AC-SS006-I:**
- **Given** I am placing a multi-branch order on a phone
- **When** I choose a product from the selected Agreed Range
- **Then** I can enter one quantity for the selected branches and adjust individual branches
- **And** a running summary shows the products and branch quantities already entered before review
- **And** the order reaches the same branch-order review as the laptop or larger-tablet grid

**AC-SS006-J:**
- **Given** Hand Cream 75ml is a confirmed member of both Everyday and “2026 Christmas gift packs” for Hickey's, and it is not yet in my chain order
- **When** I select either range
- **Then** Hand Cream is available from that range for entry into the chain order

**AC-SS006-K:**
- **Given** I have already entered branch quantities for Hand Cream 75ml and it belongs to the selected Agreed Range
- **When** I view the multi-branch order
- **Then** Hand Cream appears once in “Entered in this order” at the top
- **And** it does not appear again among the selected range's products below
- **And** I can edit its existing branch quantities from the top row

**AC-SS006-L:**
- **Given** Hickey's has 12 eligible branches, one Closed branch, and one Temporarily Closed branch
- **When** I start a customer multi-branch order
- **Then** all 12 eligible branches are selected, including the Temporarily Closed branch with its closure date shown
- **And** the Closed branch is excluded
- **And** I can deselect eligible branches before entering products

**AC-SS006-M:**
- **Given** Arklow has 24 Hand Cream in my current in-progress chain order
- **When** I try to deselect Arklow from the branch selection
- **Then** Arklow stays selected and its quantities are unchanged
- **And** I see a message that an active order exists for Arklow and its quantities must be cleared before removing the branch
- **And** once all Arklow quantities in this chain order are cleared, I can deselect it

**AC-SS006-N:**
- **Given** I tried to remove Arklow but it has quantities on several products in this chain order
- **When** the removal is blocked
- **Then** the view opens directly on the products and quantities entered for Arklow in this chain order, with the reason shown
- **And** I can edit or clear those quantities there
- **And** Arklow remains selected while any quantity remains

**AC-SS006-O:**
- **Given** I reached Arklow's quantities after a blocked removal
- **When** I clear Arklow's last quantity in this chain order
- **Then** Arklow remains selected
- **And** I can return to branch selection and deliberately deselect it

**Recommended Acceptance Tests:**

- `Should_OfferAgreedProducts_When_ChainBuyerStartsGrid`
  → Verifies the chain's confirmed list is available without searching or copying quantities from a previous order.
- `Should_KeepEnteredRowsVisible_When_ChainBuyerChangesRange`
  → Verifies changing the products offered for entry does not hide work already entered.
- `Should_ShowDefaultRange_When_ChainBuyerOpensGrid`
  → Verifies the buyer can start from the designated range without choosing it each time.

**Open:** phone composition details are deferred to implementation feedback. The first-option fallback (C5.7) and the dropdown's sort order may be revisited later. Cross-surface impacts of multiple Agreed Ranges are listed in BR-NEW-008.

---

### Visit Planning US-012 — campaign detail view hierarchy

> **Context:** the campaign is the primary object; rep performance within it is a drill-down rather than the default framing.

**Additional Acceptance Criteria:**

**AC-VP012-A:**
- **Given** I open or return to a campaign
- **When** its detail page loads
- **Then** `Overall` is selected
- **And** I see total progress, the outcome breakdown and the visit list

**AC-VP012-B:**
- **Given** I switch to `By rep`
- **When** the detail table changes
- **Then** each rep shows done of due for this campaign
- **And** the campaign's overall progress and outcome breakdown remain visible above it

**AC-VP012-C:**
- **Given** the outcome breakdown is visible
- **When** I select Ordered, Declined, Follow up, Not yet visited or Cancelled
- **Then** the visit list shows only records behind that count
- **And** the selected outcome is identified by text/state, not colour alone

**AC-VP012-D:**
- **Given** an outcome filter is active
- **When** I select it again or choose `All visits`
- **Then** the complete visit list returns
- **And** all headline counts continue to show campaign totals while filtered

**AC-VP012-E:**
- **Given** a campaign has open visits
- **When** I choose `Extend remaining...`
- **Then** a review opens with every open visit selected by default
- **And** I can exclude individual visits before applying the change
- **And** I see the proposed new campaign window

**AC-VP012-F:**
- **Given** I apply an extension from the review
- **When** the change is saved
- **Then** only the selected open visits move into the new window
- **And** excluded open visits retain their existing dates
- **And** completed and cancelled visits remain unchanged

---

### Visit Planning US-014 — conflict list ordering

> **Context:** schedule conflicts are time-sensitive operational exceptions, so the affected visit date is more useful than the time the conflict was detected.

**Additional Acceptance Criteria:**

**AC-VP014-A:**
- **Given** more than one unresolved Schedule Conflict exists
- **When** I open the conflict list
- **Then** conflicts for overdue affected visits appear first
- **And** conflicts affecting today appear next
- **And** future conflicts follow in ascending affected-visit date order

**AC-VP014-B:**
- **Given** a rep row on the Visit Planning overview shows one or more conflicts
- **When** I select that conflict count
- **Then** the Schedule Conflicts page opens filtered to that rep
- **And** the active filter identifies the rep in text
- **And** I can clear it with `View all conflicts`

**AC-VP014-C:**
- **Given** I open the Schedule Conflicts page directly
- **When** the page loads
- **Then** it shows unresolved conflicts for all reps in the default earliest-first order

**AC-VP014-D:**
- **Given** either party amends a visit and thereby clears its Schedule Conflict
- **When** the conflict leaves the unresolved list
- **Then** it remains accessible to a manager in resolved-conflicts history
- **And** the history retains both conflicting versions
- **And** it identifies the clearing amendment, who made it and when

**AC-VP014-E:**
- **Given** I open Schedule Conflicts
- **When** the page loads
- **Then** `Open` is selected by default
- **And** `Resolved` is available as a separate adjacent view

**AC-VP014-F:**
- **Given** I entered Schedule Conflicts from a rep row
- **When** I switch between `Open` and `Resolved`
- **Then** the active rep filter is retained
- **And** I can still clear it with `View all conflicts`

**AC-VP014-G:**
- **Given** more than one cleared Schedule Conflict exists
- **When** I open `Resolved`
- **Then** the most recently cleared conflict appears first
- **And** each record identifies its affected visit date separately from its clearing date and time

---

### Visit Planning US-015 — Location-first one-off visit with a chosen rep

> **Context:** a one-off visit is triggered by something at a Location, so the flow starts there. The manager picks who goes, based on the reason or on who will be nearby, and confirms with that rep by phone. Sending a rep other than the Primary Rep is a convenience for this visit only, as with campaign routing (Coverage US-001 amendment). Settled in `05-manager.md` M16.1–M16.5 and M16.7.

**Additional Acceptance Criteria:**

**AC-VP015-A:**
- **Given** I am viewing a Location
- **When** I choose to add a one-off visit
- **Then** the form opens with that Location already filled in

**AC-VP015-B:**
- **Given** the add form is open for a Location with a Primary Rep
- **When** it renders
- **Then** the Primary Rep is selected as the rep who will make the visit

**AC-VP015-C:**
- **Given** the add form is open
- **When** I open the rep picker
- **Then** reps with any assignment at the Location are listed first, followed by the rest of the team

**AC-VP015-D:**
- **Given** Byrne's Primary Rep is Colm
- **When** I choose Aoife to make the one-off visit
- **Then** the form states "Aoife will make this visit. Colm remains Byrne's Primary Rep."
- **And** saving leaves Byrne's assignments and Assignment History unchanged

**AC-VP015-E:**
- **Given** Colm has a visit scheduled at Byrne's on Thu 24 Sep
- **When** I add a one-off visit with a due window of 21–25 Sep
- **Then** the form shows "Colm is scheduled at Byrne's on Thu 24 Sep."
- **And** the notice asks for no choice and does not prevent saving

**AC-VP015-F:**
- **Given** Aoife holds a one-off Visit Due at Byrne's and Colm holds an open Recurring Visit Due there
- **When** Aoife records a Call at Byrne's
- **Then** the one-off Visit Due is complete
- **And** Colm's Recurring Visit Due remains open and unchanged

**AC-VP015-F2:**
- **Given** Colm holds both a one-off Visit Due and an open Recurring Visit Due at Byrne's
- **When** he records a Call for the one-off visit
- **Then** the one-off Visit Due is complete
- **And** the Recurring Visit Due remains open

**AC-VP015-G:**
- **Given** I have saved a one-off Visit Due
- **When** the confirmation and the saved visit are shown
- **Then** neither shows whether the visit has reached the rep's tablet

**Superseded:** "the effective rep follows the Location assignment rules" as the only way to set the visit's rep. "One Call clears all" gains the exception in AC-VP015-F and F2: a Call for a one-off visit never completes a Recurring Visit Due.

**Recommended Acceptance Tests:**

- `Should_PrefillLocation_When_AddOneOffStartedFromLocation`
  → The trigger is a Location, so the form must not make the manager find it again.
- `Should_DefaultToPrimaryRep_When_AddOneOffFormOpens`
  → The usual answer is pre-selected; choosing someone else is the exception.
- `Should_ListAssignedRepsFirst_When_RepPickerOpens`
  → The rep sent is usually one already assigned to the Location.
- `Should_LeaveAssignmentUnchanged_When_OneOffSentToNonPrimaryRep`
  → Choosing a rep for one visit never changes who owns the Location.
- `Should_ShowScheduledVisitNotice_When_OtherVisitWithinAWeekOfWindow`
  → The manager needs to know about the nearby visit so they can talk to its rep afterwards.
- `Should_AllowSave_When_ScheduledVisitNoticeShown`
  → The notice is information only, not a gate.
- `Should_LeaveOtherRepsRecurringVisitOpen_When_OneOffCallRecorded`
  → What happens to the other visit is decided later by the manager and reps, not by the Call.
- `Should_LeaveOwnRecurringVisitOpen_When_SameRepRecordsOneOffCall`
  → The same rep may need a second visit with a different contact.

---

### Visit Planning US-011 — four-step campaign creation wizard

> **Context:** the candidate set, per-campaign details and routing summary have genuine dependencies and should not compete on one long screen.

**Additional Acceptance Criteria:**

**AC-VP011-A:**
- **Given** I start creating a Visit Campaign
- **When** the flow renders
- **Then** it has four labelled steps: `Filter`, `Review`, `Details`, `Confirm`

**AC-VP011-B:**
- **Given** I move backward or forward between completed steps
- **When** a step reopens
- **Then** my filters, selected Locations and campaign details are preserved

**AC-VP011-C:**
- **Given** the current filter matches no Locations
- **When** the Filter step renders
- **Then** it shows `No Locations match` and I cannot advance to Review

**AC-VP011-D:**
- **Given** my reviewed selection and campaign details are valid
- **When** I reach Confirm
- **Then** it states the visit count, rep count and number routed to specialists before creation

**AC-VP011-E:**
- **Given** a selected Location has no Primary Rep
- **When** I choose its campaign rep
- **Then** Review states that this rep will also become the Location's permanent Primary Rep

**AC-VP011-F:**
- **Given** a selected Location already has a Primary Rep
- **When** I choose another rep for this campaign
- **Then** only the campaign Visit Due is routed to that rep
- **And** the existing Primary Rep remains unchanged

**AC-VP011-G:**
- **Given** one or more selected Locations will receive a Primary Rep
- **When** I reach Confirm
- **Then** those permanent assignments are counted and listed separately from campaign-only routing

**AC-VP011-H:**
- **Given** I confirm campaign creation with a Primary Rep assignment for an unassigned Location
- **When** creation succeeds
- **Then** the direct Primary Rep assignment and its Assignment History entry are saved atomically with the campaign
- **And** future recurring visits, tablet access and performance attribution use that assignment normally

**AC-VP011-I:**
- **Given** multiple selected Locations are unassigned
- **When** I choose a bulk Primary Rep default
- **Then** that rep is applied to all selected unassigned rows
- **And** I can override the rep on any individual row before advancing

**AC-VP011-J:**
- **Given** one or more rows override the bulk Primary Rep default
- **When** Review and Confirm show assignment counts
- **Then** they use each row's effective rep and list every permanent assignment accurately

**Amendment to Coverage US-001:** campaign creation is an additional route for creating a direct Location assignment where the Location was previously unassigned. It must produce the same effective ownership and append-only Assignment History as the Coverage Management screen.

---

### Coverage US-001 — transfer Territory Assignments between reps

> **Context:** the most common trigger for territory work is a new rep, often replacing one who is leaving. A permanent replacement moves the **assignments themselves**, so Locations keep resolving through the territory and new Locations follow the new rep. Bulk reassign (US-004) stays for temporary moves, because it creates direct Location assignments. Settled in `05-manager.md` M6.1–M6.5.

**Additional Acceptance Criteria:**

**AC-CV001-A:**
- **Given** I am on Colm's Territory assignment page
- **When** I filter his assignments and choose `Transfer...`
- **Then** a review opens with every assignment in the filtered set selected
- **And** I can remove individual rows before choosing the receiving rep

**AC-CV001-B:**
- **Given** Colm holds Wicklow (County)
- **When** I view his assignments
- **Then** Wicklow can be expanded to its Towns, each with its Location count
- **And** Towns carved out to another rep are marked with that rep

**AC-CV001-C:**
- **Given** Colm holds Wicklow (County)
- **When** I transfer the whole County to Niamh
- **Then** the Wicklow County assignment moves to Niamh
- **And** its Locations show "Niamh (via Wicklow)", while carve-outs keep their owners

**AC-CV001-D:**
- **Given** Colm holds Wicklow (County)
- **When** I transfer only Bray, Greystones and Wicklow Town to Niamh
- **Then** each becomes a Town assignment to Niamh
- **And** the Wicklow County assignment stays with Colm

**AC-CV001-E:**
- **Given** Bray, Greystones and Wicklow Town have already been transferred to Niamh
- **When** I transfer every remaining Town Colm holds through Wicklow to Ciara
- **Then** the Wicklow County assignment moves to Ciara as well
- **And** the impact preview states "Wicklow (County) moves to Ciara with its last Towns."

**AC-CV001-E2:**
- **Given** Colm holds South East (Region), and Wicklow (County) has already been transferred to Ciara
- **When** I transfer Wexford, the last County Colm holds through South East, to Ciara
- **Then** the South East Region assignment moves to Ciara as well
- **And** the impact preview states "South East (Region) moves to Ciara with its last Counties."

**AC-CV001-F:**
- **Given** I am on Niamh's page and Rathdrum (Town) is assigned to Aoife
- **When** I choose `Add assignment`
- **Then** Rathdrum is listed as "Rathdrum — Aoife's"
- **And** choosing it asks "Transfer Rathdrum from Aoife to Niamh?" and continues to the impact preview and handover

**AC-CV001-G:**
- **Given** I am on Niamh's page
- **When** I choose `Take over from another rep...` and pick Colm
- **Then** the transfer review opens on Colm's assignments with Niamh already set as the receiving rep

**AC-CV001-H:**
- **Given** any transfer
- **When** I confirm it after the impact preview
- **Then** open visits with the previous rep go to Handover (US-003)
- **And** each Location whose owner changes gets an Assignment History entry

**Superseded:** US-001 scenario 3's dead-end message ("remove that assignment first") when adding an area already held at the same level. The transfer offer in AC-CV001-F replaces it.

---

### Coverage US-003 — handover when the previous rep is leaving; inherited visits

> **Context:** the most common reason for handover is a rep leaving and being replaced. "Leave with Colm" is meaningless once Colm has gone, and the new rep's schedule is built fresh anyway. Late visits handed over shouldn't count against the rep who inherits them. Settled in `05-manager.md` M8.1–M8.2.

**Additional Acceptance Criteria:**

**AC-CV003-A:**
- **Given** I start a transfer of Colm's assignments
- **When** the transfer begins
- **Then** I am asked "Is Colm leaving?"

**AC-CV003-B:**
- **Given** I answered that Colm is leaving
- **When** I reach the impact preview
- **Then** Leave is not offered and there is no per-visit handover list
- **And** the preview states how many open visits move to the new owner as visits needed, including how many are Overdue

**AC-CV003-C:**
- **Given** I confirm a transfer where Colm is leaving
- **When** it is saved
- **Then** every open Visit Due at a Location changing owner moves to that Location's new Primary Rep with its Scheduled Day cleared and its due window unchanged
- **And** none becomes Handover Pending

**AC-CV003-D:**
- **Given** I answered that Colm is not leaving
- **When** I reach the impact preview
- **Then** the US-003 handover applies unchanged: Move or Leave per visit, Apply to all, and undecided visits become Handover Pending

**AC-CV003-E:**
- **Given** Byrne's recurring visit was Overdue when it moved from Colm to Niamh
- **When** it appears on Niamh's planner, the Visit Planning overview or the visit detail
- **Then** it is marked "inherited from Colm"
- **And** it counts in Niamh's Overdue figure on the overview

**AC-CV003-F:**
- **Given** Niamh inherited Byrne's open Visit Due
- **When** it is completed or marked Missed
- **Then** its outcome is excluded from Niamh's performance
- **And** the inherited marker ends when the visit closes

**AC-CV003-G:**
- **Given** Niamh inherited Byrne's
- **When** a new Visit Due is generated there after the handover
- **Then** it is not marked inherited and counts in her performance normally

**AC-CV003-H:**
- **Given** Arklow is carved out from Colm to Niamh and Colm is not leaving
- **When** I choose Move for Carey's Pharmacy's open Visit Due in the handover
- **Then** it moves to Niamh with its due window unchanged, marked "inherited from Colm"
- **And** it is excluded from Niamh's performance until it closes, as in AC-CV003-F

**Recommended Acceptance Tests:**

- `Should_AskIfPreviousRepLeaving_When_TransferStarts`
  → One question removes a whole class of stranded visits.
- `Should_HideLeave_When_PreviousRepLeaving`
  → Leave would strand the visit with nobody.
- `Should_MoveAllOpenVisitsUnscheduled_When_PreviousRepLeaving`
  → The new rep's schedule is built fresh.
- `Should_KeepDueWindow_When_VisitInheritedOnHandover`
  → The shop's real Overdue state stays visible.
- `Should_ExcludeInheritedVisitFromPerformance_When_ClosedByNewRep`
  → The new rep isn't penalised for debt they didn't cause.
- `Should_CountNormally_When_VisitGeneratedAfterHandover`
  → The marker ends with the visit, not a time window.
- `Should_MarkInherited_When_VisitMovedInCarveOutHandover`
  → The new rep didn't set the window, whoever chose to move it.

---

### Coverage US-004, US-005 — bulk reassign for long absence; reversal

> **Context:** short leave never changes ownership. It's handled by the absence flow (Visit Planning US-006/US-007: Extend, Keep or Cover per visit), and the rep's schedule restarts on return. Bulk reassign is for long absence, where the batch is the record that makes reversal possible. On reversal both reps are working, and the returning rep may be part-time. Settled in `05-manager.md` M9.1–M9.4.

**Additional Acceptance Criteria:**

**AC-CV004-A:**
- **Given** Colm is on short leave
- **When** I handle his affected visits
- **Then** I use the absence decisions (Extend, Keep, Cover), and no ownership changes and no batch is created

**AC-CV004-B:**
- **Given** I bulk-reassign 23 of Colm's Locations to Aoife as "Maternity cover"
- **When** I reach the impact preview
- **Then** Leave is not offered and there is no per-visit handover
- **And** on save every open visit at those Locations moves to Aoife with Scheduled Day cleared, marked "inherited from Colm"

**AC-CV005-A:**
- **Given** I reverse "Maternity cover"
- **When** I reach the impact preview
- **Then** Aoife's open visits at the returning Locations are listed for Move or Leave, with Apply to all
- **And** undecided visits become Handover Pending
- **And** visits that Move are marked "inherited from Aoife"

**AC-CV005-B:**
- **Given** Colm still holds Wicklow (County)
- **When** a Location from the batch is reversed
- **Then** Aoife's direct assignment is removed and the Location shows "Colm (via Wicklow)"

**AC-CV005-C:**
- **Given** Wicklow (County) was transferred to Niamh during the batch
- **When** I reverse the batch
- **Then** the preview states "3 Locations would resolve to Niamh via Wicklow"
- **And** each of those rows offers "Assign to Colm directly" or "Leave with Niamh"

**Superseded:** US-005's open question ("restore the derived owner or create a direct assignment"): the owner is restored and exceptions are flagged (AC-CV005-B, C).

**Recommended Acceptance Tests:**

- `Should_NotCreateBatch_When_ShortLeaveHandledByAbsenceFlow`
  → Short leave keeps ownership; the schedule restarts on return.
- `Should_MoveAllOpenVisitsAsInherited_When_BulkReassignSaved`
  → The absent rep can't finish anything.
- `Should_OfferMoveOrLeave_When_BatchReversed`
  → Both reps are working and the return may be part-time.
- `Should_RestoreDerivedOwner_When_TerritoryUnchanged`
  → Reversal leaves no direct-assignment clutter.
- `Should_FlagLocation_When_RestoredOwnerIsNotReturningRep`
  → A territory change during the batch never silently hands shops to another rep.

---

### Coverage US-006 — multi-condition specialist scopes and campaign routing

> **Context:** the common specialist need is "every Location with profile X in an area", which the source's single-condition scopes can't express. Richer scopes make it more likely that several specialists match one campaign visit. Settled in `05-manager.md` M10.1–M10.2.

**Additional Acceptance Criteria:**

**AC-CV006-A:**
- **Given** I create a Specialist Assignment for Brian
- **When** I add the conditions Profile "Customer campaign X" and Area "Wicklow"
- **Then** Brian is Specialist on every Location holding that profile in Wicklow, and only those
- **And** before saving I see the scope as a sentence with the number of Locations it matches

**AC-CV006-B:**
- **Given** the default builder is open
- **When** I choose `Advanced`
- **Then** I can combine conditions with AND and OR, and group them

**AC-CV006-C:**
- **Given** Brian's scope is Profile "Customer campaign X" in Wicklow
- **When** a Location in Wicklow gains that profile
- **Then** Brian becomes its Specialist without any further action

**AC-CV006-D:**
- **Given** a specialist scope has no Brand condition
- **When** the specialist syncs
- **Then** no products are added to their Order Pad because of that scope

**AC-CV006-E:**
- **Given** a SunCo campaign and a Location where Ciara (Brand: SunCo) and Brian (Profile: Customer campaign X · in Wicklow) are both Specialists
- **When** I review the campaign's visits
- **Then** Ciara is preselected for that Location's visit and the row notes "also matches Brian"

**AC-CV006-F:**
- **Given** two Specialists at a Location both match the campaign's link, or the campaign has no link and two Specialists are at the Location
- **When** I review the campaign's visits
- **Then** the row shows "2 specialists match — choose" with no preselection
- **And** I can't advance until I have chosen

**AC-CV006-G:**
- **Given** no Specialist at a Location matches the campaign's link
- **When** I review the campaign's visits
- **Then** the visit starts with the Location's Primary Rep

**Superseded:** US-006's single-condition Scope (Customer **or** Location Profile **or** Brand). Scenario 3's routing is refined by AC-CV006-E to G.

**Recommended Acceptance Tests:**

- `Should_MatchOnlyLocationsMeetingAllConditions_When_ScopeHasProfileAndArea`
  → The default builder is AND-only, so it reads as one sentence.
- `Should_ShowSentenceAndMatchCount_When_ScopeSaved`
  → The manager can predict who the rule catches.
- `Should_JoinSpecialist_When_LocationGainsMatchingProfile`
  → Scopes stay live.
- `Should_PreselectLinkMatchingSpecialist_When_SeveralSpecialistsAtLocation`
  → The campaign's own link is the most relevant signal.
- `Should_RequireChoice_When_LinkDoesNotSettleRouting`
  → Never an arbitrary pick.
- `Should_RouteToPrimaryRep_When_NoSpecialistMatchesLink`
  → Source behaviour is retained.

---

### Master & Branch US-001 — the Master Location view leads with the chain order

> **Context:** settled 26 Sep 2026 (T11.1). A rep at a chain's head office is mostly there to take the chain order. The frame's layout was confirmed the same day.

**AC-MB001-A:**
- **When** I open Hickey's Head Office on the tablet
- **Then** "Start multi-branch order" is the one primary action
- **And** "Record call", where Range Review is a Purpose, is secondary

**AC-MB001-B:**
- **Given** Hickey's Wicklow Town is the chain's master and also a shop
- **When** I open it on the tablet
- **Then** "New order for this shop" is offered as well, for the shop's own order
- **And** for Hickey's Head Office, which holds no stock, that action is absent

---

### Master & Branch US-002 and US-003 — a Range Review assigns or removes whole Ranges

> **Context:** settled 26 Sep 2026, revised the same day (BR-NEW-008 rules 0 and 5, T12.2). A chain's Agreed Ranges are catalogue Ranges assigned to it. A Range Review proposes assigning or removing whole Ranges for the chain; it never changes a Range's products, because a Range is shared with everyone assigned it. Head office confirms or rejects each Range on H-04. **Superseded:** US-002 Scenario 1's product adds and drops, US-002 Scenario 3 (Unavailable product proposed), US-003's per-product confirmation, and the earlier AC-MB002-A/B/D (product-level drops, "Review another range"), which followed T12.1.

**AC-MB002-C:**
- **When** I look for a way to create a new range on the Range Review
- **Then** there is none; Ranges are created by head office

**AC-MB002-E:**
- **Given** Hickey's is assigned "Everyday" and "Sun care 2026"
- **When** I record a Range Review, add "2026 Christmas gift packs" and remove "Sun care 2026"
- **Then** the Call saves a Range Proposal: add 1 range, remove 1, state Proposed
- **And** the Call review reads "Range review: add 1 range, remove 1 — subject to head office confirmation"
- **And** no Range's products change

**AC-MB002-F:**
- **Given** that proposal reaches H-04
- **When** head office confirms the Christmas gift packs and rejects removing Sun care 2026 with a reason
- **Then** the proposal is Partly Confirmed; Hickey's is assigned Everyday, Sun care 2026 and 2026 Christmas gift packs
- **And** I see the reason on the Call

**AC-MB002-G:** *(T12.2 details, confirmed 26 Sep 2026)*
- **When** I choose "Add a range"
- **Then** only Active catalogue Ranges not already assigned to Hickey's are offered, each with its product count
- **When** I add "2026 Christmas gift packs"
- **Then** its row reads "Proposed: add" with Undo, until the Call syncs

---

### Master & Branch US-006 — grid rows and the `All` cell

> **Context:** a chain may have 140 agreed products, but a session orders about 30. The grid opens near that size, and a bulk fill never destroys deliberate exceptions. The grid layout itself is deferred to usage feedback (M11.2). Settled in `05-manager.md` M11.1 and M11.3.

**Additional Acceptance Criteria:**

**AC-MB006-A:**
- **Given** Hickey's branches have Accepted Orders
- **When** I start a Multi-Branch Order on the website
- **Then** the rows are the products on any branch's last 3 Accepted Orders, with every cell empty

**AC-MB006-B:**
- **Given** the grid is open
- **When** I search for a product that isn't listed
- **Then** I can add it as a row

**AC-MB006-C:**
- **Given** the grid is open
- **When** I choose `Show full Agreed Range`
- **Then** the rest of Hickey's agreed products appear as empty rows

**AC-MB006-D:**
- **Given** SPF30's `All` is 24 and I changed Rathdrum to 12 and Arklow to 0
- **When** I change `All` to 36
- **Then** the other 8 branches become 36, Rathdrum stays 12 and Arklow stays 0
- **And** the row shows "36 × 8 branches, 2 adjusted" with the adjusted cells marked

**AC-MB006-E:**
- **Given** a row has adjusted cells
- **When** I choose `Clear adjustments`
- **Then** every branch cell on the row takes the `All` value

**Superseded:** US-006 scenario 2's "every branch cell on that row becomes 24", when the row already has hand-edited cells.

> **Context:** settled 26 Sep 2026 (M11.4, BR-NEW-008). When a chain has several Agreed Ranges, a range picker beside the grid replaces `Show full Agreed Range`; its options are the chain's ranges, oldest first (T7.18). AC-MB006-C still describes a chain with one range.

**AC-MB006-F:**
- **Given** Hickey's has Agreed Ranges "Everyday" and "2026 Christmas gift packs"
- **When** I open the grid
- **Then** it shows recently ordered products, as in AC-MB006-A, and a range picker listing Everyday then 2026 Christmas gift packs
- **When** I pick "2026 Christmas gift packs"
- **Then** that range's products not already in the grid appear as empty rows

**AC-MB006-G:** *(M11.4 details, confirmed 26 Sep 2026)*
- **Given** I entered quantities on three Everyday rows
- **When** I pick "2026 Christmas gift packs"
- **Then** those three rows stay at the top with their quantities
- **And** Hand Cream 75ml, already a row and also in the gift packs, is not added a second time
- **When** I pick "Recently ordered"
- **Then** the grid returns to its opening rows, with the three entered rows still at the top

**Recommended Acceptance Tests:**

- `Should_OpenWithRecentlyOrderedProducts_When_GridStarts`
  → The grid starts near a typical session's size.
- `Should_LeaveCellsEmpty_When_GridStarts`
  → Rows are proposed; quantities never are.
- `Should_KeepAdjustedCells_When_AllChanged`
  → A bulk fill never destroys deliberate exceptions.
- `Should_ResetAdjustedCells_When_ClearAdjustmentsChosen`
  → An explicit way to mean "every branch".

---

### Targets & Performance US-001, US-002 — targets carried forward from the previous period

> **Context:** targets usually change a little from one period to the next, so retyping every figure costs more than the anchoring risk. This is a deliberate exception to *propose, don't impose*. Settled in `05-manager.md` M12.1–M12.2.

**Additional Acceptance Criteria:**

**AC-TP001-A:**
- **Given** Colm's Q4 2026 Rep Target was €40,000 and Brian had none
- **When** I choose period Q1 2027
- **Then** Colm's row shows €40,000, editable, and Brian's row is blank
- **And** the running total starts from the carried figures

**AC-TP001-B:**
- **Given** I changed 2 of 8 rows and left 6 as carried
- **When** I save
- **Then** all 8 figures are saved as Q1 2027 targets and dated in history as set by me today
- **And** the confirmation states "6 of 8 targets were carried forward from Q4 unchanged. You can update them later." and names those reps

**AC-TP002-A:**
- **Given** Aoife had a Rep–Range Target for "Summer 2026" in Q2 2026
- **When** I set targets for the same Range for Q3 2026
- **Then** her row is pre-filled with that figure on the same terms

**Recommended Acceptance Tests:**

- `Should_PrefillPreviousPeriodTarget_When_PeriodChosen`
  → Incremental changes are the norm.
- `Should_KeepBlank_When_NoPreviousTarget`
  → Blank still means no target; a carry never invents one.
- `Should_ReportCarriedCount_When_SavedWithUntouchedRows`
  → An interrupted review is visible without blocking the save.

---

### Targets & Performance US-005 — what "furthest behind" means

> **Context:** the sort decides who the manager looks at first. Percentage points behind pace ranks each rep against their own expectation; money behind pace shows the biggest holes in the team's number. Settled in `05-manager.md` M13.1–M13.3.

**Additional Acceptance Criteria:**

**AC-TP005-A:**
- **Given** we are 75% through Q4, Colm is at 66% of €40,000 and Aoife is at 50% of €12,000
- **When** I open the overview
- **Then** Aoife (25 pts behind pace) is listed before Colm (9 pts behind pace)

**AC-TP005-B:**
- **Given** the overview is open
- **When** I choose `Sort by € behind pace`
- **Then** Colm (€3,600 behind) is listed before Aoife (€3,000 behind)
- **And** every rep remains listed

**AC-TP005-C:**
- **Given** Colm has a value target 8 pts ahead of pace and a units target 15 pts behind
- **When** the overview is ordered
- **Then** Colm is ranked by value (8 pts ahead)
- **And** his row still shows the units figure and its pace gap

**AC-TP005-D:**
- **Given** a rep has a units-only target
- **When** the overview is ordered by percentage points
- **Then** that rep is ranked by units

**AC-TP005-E:**
- **Given** Niamh has a units-only target 14 pts behind pace
- **When** I choose `Sort by € behind pace`
- **Then** Niamh is listed under a separate "Units targets" heading below the reps ranked by €, ordered by points behind pace

**Recommended Acceptance Tests:**

- `Should_OrderByPointsBehindPace_When_OverviewOpens`
  → Ranks people against their own expectation.
- `Should_OrderByMoneyBehindPace_When_ManagerResorts`
  → The team-total view is one click away.
- `Should_RankByValue_When_RepHasValueAndUnitsTargets`
  → One predictable rule; units stay visible on the row.
- `Should_GroupUnitsOnlyRepsSeparately_When_SortedByMoney`
  → A rep with no € figure isn't implied to be least behind.

---

### Coverage US-009 — grant permissions to a cohort from the group's page

> **Context:** permissions have two triggers: onboarding one rep (per-rep view, the source) and a cohort completing training (per-group view, added). Both views share one record. Settled in `05-manager.md` M14.1; removal states its consequence first (M14.2).

**Additional Acceptance Criteria:**

**AC-CV009-A:**
- **Given** I open the Restriction Group "Pharmacy-only medicines"
- **When** the page loads
- **Then** I see the reps with Granted / Not granted and each one's last change

**AC-CV009-B:**
- **Given** I select Colm, Aoife, Ciara, Brian and Niamh on the group's page
- **When** I grant with reason "Completed training 15 Sep 2026"
- **Then** each of the five has a separate grant record with the date, my name and that reason

**AC-CV009-C:**
- **Given** I granted a permission from the group's page
- **When** I open that rep's permissions
- **Then** the grant appears there as Granted with the same record

**AC-CV009-D:**
- **Given** I am a Sales Manager who does not manage Ciara and not a Head Office User
- **When** I open the group's page
- **Then** Ciara is listed but can't be selected for a change

**AC-CV009-E:**
- **Given** Colm is granted Pharmacy-only medicines and has 2 lines from that group on In Progress orders
- **When** I remove the permission and save
- **Then** I am first told "Colm will lose Pharmacy-only medicines at his next Sync. 2 lines already on In Progress orders will still send."

**AC-CV009-F:**
- **Given** I have selected Colm, Aoife and Niamh on the Pharmacy-only medicines group page
- **When** I remove the permission from all three and save
- **Then** I am first told once how many reps lose it at their next Sync and how many In Progress lines will still send

**Recommended Acceptance Tests:**

- `Should_RecordEachGrantSeparately_When_CohortGrantedFromGroupPage`
  → The per-rep audit record is unchanged by the bulk entry.
- `Should_ShowSameState_When_ViewedPerRepOrPerGroup`
  → One record, two views.
- `Should_PreventChange_When_RepNotManagedAndNotHeadOffice`
  → Source permission rule applies in the new view.
- `Should_StateConsequence_When_PermissionRemoved`
  → The effect is delayed and lands on the rep's tablet; the manager learns it before saving, not never.

---

### Coverage US-007 — unassigned Locations pushed to the overview

> **Context:** an unassigned Location generates no visits, so it never becomes Overdue and never shows on the overview's exception columns. A list that has to be opened can't stop it being silently uncovered. Settled in `05-manager.md` M15.1–M15.3; row actions follow M7.2.

**Additional Acceptance Criteria:**

**AC-CV007-A:**
- **Given** 6 Locations in my area have no Primary Rep
- **When** I open the Visit Planning overview
- **Then** its header shows "6 Locations unassigned in your area" linking to the Unassigned list

**AC-CV007-B:**
- **Given** every Location has a Primary Rep
- **When** I open the Visit Planning overview
- **Then** no unassigned line appears

**AC-CV007-C:**
- **Given** Walsh's Shop and 4 other Laragh Locations are unassigned
- **When** I view the Unassigned list
- **Then** Walsh's row leads with `Assign Laragh (Town) to...` and states the Town's unassigned count, with `Assign just this shop to...` beside it

**AC-CV007-D:**
- **Given** Niamh reports to me and holds Arklow and Rathdrum (Towns), and no one holds Wicklow (County)
- **When** a Location in Aughrim, a Town nobody holds, becomes unassigned
- **Then** it counts in the unassigned line on my overview

**AC-CV007-E:**
- **Given** no rep holds any Town in the same County as an unassigned Location, but one of my reps holds a Territory Assignment in its Region
- **When** I open the Visit Planning overview
- **Then** that Location counts in my unassigned line

**AC-CV007-F:**
- **Given** no rep holds anything in an unassigned Location's County or Region
- **When** a Head Office User opens the Visit Planning overview
- **Then** that Location counts as "with no nearby team" and appears on no Sales Manager's line

**AC-CV007-G:**
- **Given** my reps and another manager's reps both hold Towns in Wicklow (County)
- **When** a Location in Wicklow becomes unassigned
- **Then** it counts on both managers' overviews

**AC-CV007-H:**
- **Given** my overview shows "4 Locations unassigned in your area" and 6 more are unassigned elsewhere
- **When** I follow the link
- **Then** the Unassigned list opens showing only my 4
- **And** `All unassigned` shows all 10

**Recommended Acceptance Tests:**

- `Should_ShowUnassignedCountOnOverview_When_AnyLocationUnassigned`
  → Unassigned shops produce no other signal.
- `Should_HideUnassignedLine_When_CountIsZero`
  → No decoration without meaning.
- `Should_ScopeUnassignedCountToNearestTeam_When_ManagerViewsOverview`
  → A count everyone sees is owned by no one; the nearest team owns it, with nothing to set up.
- `Should_ShowToHeadOffice_When_NoTeamHoldsCountyOrRegion`
  → No unassigned shop falls outside every count.

---

### Coverage US-002 — the Location page as the manager's home for a Location

> **Context:** M-07 answers "who covers this shop, and why?" and carries the Location's manager actions, so a manager never has to remember which screen holds which fact about a shop. It stays about coverage: visits are monitored in Visit Planning. Settled in `05-manager.md` M7.1–M7.5.

**Additional Acceptance Criteria:**

**AC-CV002-A:**
- **Given** I open Murphy's Pharmacy
- **When** the page loads
- **Then** I see its Primary Rep with source and its Specialists first
- **And** I can add a one-off visit and open History from the same page

**AC-CV002-B:**
- **Given** Walsh's Shop in Laragh is unassigned and 4 other Laragh Locations are too
- **When** I open Walsh's Shop
- **Then** `Assign Laragh (Town) to...` is offered first, with "4 other Locations in Laragh are unassigned"
- **And** `Assign just this shop to...` is offered beside it

**AC-CV002-C:**
- **Given** Murphy's Pharmacy shows "Aoife (via Rathdrum)"
- **When** I choose to change its owner
- **Then** `Change just this shop to...` is offered first and creates a direct Location assignment
- **And** `Transfer Rathdrum (Town, 23 Locations) to...` is offered beside it and continues to the transfer's impact preview

**AC-CV002-D:**
- **Given** I open a Location page
- **When** it renders
- **Then** it shows no list of open Visit Dues and no last-Call line

**Recommended Acceptance Tests:**

- `Should_LeadWithTownAssignment_When_LocationUnassigned`
  → Fixes the reason the shop is uncovered, not just this shop.
- `Should_ShowOtherUnassignedCountInTown_When_LocationUnassigned`
  → The cause is visible before the manager picks a level.
- `Should_LeadWithJustThisShop_When_ChangingCoveredLocation`
  → A single exception is the likely intent once a working assignment exists.
- `Should_OfferAddOneOffVisit_When_LocationPageOpens`
  → The complaint call starts from the Location (M16.1).

**Recommended Acceptance Tests:**

- `Should_SelectAllFilteredAssignments_When_TransferReviewOpens`
  → The manager starts from everything and removes exceptions.
- `Should_MoveCountyAssignment_When_WholeCountyTransferred`
  → A replacement keeps derived ownership instead of creating direct assignments.
- `Should_CreateTownAssignments_When_SomeTownsTransferred`
  → Dividing a County uses the existing carve-out mechanics.
- `Should_MoveCountyWithLastTowns_When_TransferTakesAllRemainingTowns`
  → No empty County is left with a departed rep to catch new Towns.
- `Should_OfferTransfer_When_AddingAreaHeldByAnotherRep`
  → Adding from the new rep's page never dead-ends.
- `Should_PresetReceivingRep_When_TakeOverStartedFromNewRepPage`
  → Pull and push produce the same transfer.

---

### Visit Planning US-006 and US-007 — two-stage absence flow

> **Context:** calendar blocking is urgent and factual; Extend / Keep / Cover decisions may require review and are already allowed to remain pending.

**Additional Acceptance Criteria:**

**AC-VP006-A:**
- **Given** I enter a valid planned absence
- **When** I save it
- **Then** the absence is committed and the affected calendar time is blocked immediately
- **And** any visits scheduled on those days return to Unscheduled as specified by US-006

**AC-VP006-B:**
- **Given** the saved absence affects one or more Visit Dues
- **When** the save completes
- **Then** I see the affected count and can choose `Decide affected visits` or `Return to overview`

**AC-VP006-C:**
- **Given** I return without deciding
- **When** the rep or manager views an affected visit
- **Then** its Due Window is unchanged and it shows `Absence — decision pending`
- **And** the saved absence remains in effect

**AC-VP006-D:**
- **Given** the saved absence affects no Visit Dues
- **When** the save completes
- **Then** I see `No visits affected` and no decision step is offered

**AC-VP006-E:**
- **Given** a new absence genuinely overlaps an existing absence for the same rep
- **When** the overlap is detected
- **Then** I see the existing absence, the proposed combined range and an `Extend existing absence` option
- **And** its reason remains editable before save

**AC-VP006-F:**
- **Given** two absences are adjacent but do not overlap in date/time
- **When** I save the new absence
- **Then** they remain separate entries and no extension prompt is shown

**Superseded:** US-006 scenario 4's `merge` wording; the action is `Extend existing absence` and applies only to genuine overlap.

**AC-VP007-A:**
- **Given** an absence affects multiple Visit Dues
- **When** I choose `Apply to all: Cover`
- **Then** a review opens with all eligible visits selected
- **And** I can Select all, Clear all, or unselect individual rows

**AC-VP007-B:**
- **Given** I selected one covering rep for the batch
- **When** I apply Cover
- **Then** that rep covers only the selected visits
- **And** excluded visits remain available for Extend, Keep or a different covering rep

**AC-VP007-C:**
- **Given** the selected covering rep is over capacity during the affected period
- **When** the review renders
- **Then** it shows the existing capacity warning and still allows confirmation

**AC-VP007-D:**
- **Given** the covering rep belongs to another manager
- **When** I apply Cover
- **Then** cover takes effect immediately and the other manager receives the source-story notification without an approval step

---

### Visit Planning US-010 — by-rep default with coverage context

> **Context:** managers normally plan for a rep. Coverage context helps distinguish patches but should not duplicate the full assignment screens.

**Additional Acceptance Criteria:**

**AC-VP010-A:**
- **Given** I have never opened the Visit Planning overview
- **When** it loads
- **Then** it opens in `By rep`

**AC-VP010-B:**
- **Given** I previously selected `By rep` or `By region`
- **When** I return to the overview
- **Then** it opens in my last-selected view

**AC-VP010-C:**
- **Given** a rep has effective Location coverage
- **When** their summary row renders
- **Then** it shows the broadest meaningful effective area, any carve-out or overflow count, and the effective Location count
- **And** fragmented coverage with no coherent territory shows up to two representative area names followed by `+ N areas`
- **And** detailed assignment rules remain available through the Coverage Management screens

---

### Stock Allocation US-003 and US-005 — re-propose from selected stock pools

> **Context:** an Incoming delivery may be too far away to be useful even though it is technically allocatable. Stock changes preserve the current draft until the manager explicitly chooses to re-propose.

**Additional Acceptance Criteria:**

**AC-SA003-A:**
- **Given** an allocation draft uses On Hand and one or more Incoming deliveries
- **When** I choose `Re-propose...`
- **Then** I see each stock pool with its quantity and availability/expected date
- **And** pools currently included in the draft start selected

**AC-SA003-B:**
- **Given** an Incoming delivery is too far away to use for current orders
- **When** I exclude it and apply Re-propose
- **Then** the complete-what-you-can proposal uses only the selected stock pools

**AC-SA003-C:**
- **Given** I am reviewing the pool selection
- **When** the page describes the effect
- **Then** it states that Re-propose replaces current draft allocations across all waiting orders

**AC-SA003-D:**
- **Given** I cancel the pool-selection step
- **When** I return to H-10
- **Then** the existing draft remains unchanged

---

### Targets & Performance US-004 — target-led Location ordering

> **Context:** R-03 is a target-attainment view. Ordering by highest contribution foregrounds Locations already performing well rather than those needing attention.

**Replacement Acceptance Criterion for Scenario 3:**

**AC-TP004-A:**
- **Given** two or more Locations have targets in the selected period
- **When** I open the per-Location target breakdown
- **Then** targeted Locations are ordered by lowest percentage attainment first

**AC-TP004-B:**
- **Given** one or more Locations have no target in the selected period
- **When** the Location breakdown uses the default `All` filter
- **Then** those Locations appear in a separate `No target set` section beneath targeted Locations
- **And** each shows its actual contribution and Accepted Order count without a percentage

**AC-TP004-C:**
- **Given** the Location breakdown is open
- **When** I choose `With targets` or `No target`
- **Then** only Locations matching that target-status filter are shown
- **And** choosing `All` restores both sections

**AC-TP004-D:**
- **Given** I have a target for the selected period
- **When** headline progress renders
- **Then** its progress bar includes a labelled straight-line pace marker
- **And** exact percentage attainment and time remaining are shown in text

**AC-TP004-E:**
- **Given** a target may be naturally end-weighted
- **When** actual progress is behind the straight-line marker
- **Then** the rep view does not infer `off track`, apply warning colour, or change ordering from that difference

**AC-TP004-F:**
- **Given** a manager creates or edits a target
- **When** they define its period and amount
- **Then** no pacing model is requested or inferred
- **And** reps and managers use their commercial context to interpret the neutral reference marker

**Superseded:** US-004 scenario 3's `highest contribution first` ordering for the target breakdown.

---

### Visit Planning US-003 — four-week panel and selectable scheduler views

> **Context:** resolves the source story's mismatch between its four-week example and two-week empty state, and replaces the fixed-week assumption in the first R-01 wireframe.

**Additional / Replacement Acceptance Criteria:**

**AC-VP003-A:**
- **Given** I open the planner
- **When** the scheduler loads
- **Then** I can select Day, Week or Month view and navigate the corresponding timeframe

**AC-VP003-B:**
- **Given** an open Visit Due is Overdue or due within the next four weeks and has no Scheduled Day
- **When** I view the unscheduled panel
- **Then** it appears in the Town list and map where coordinates exist

**AC-VP003-C:**
- **Given** an unscheduled Visit Due is due later than four weeks from today and is not Overdue
- **When** I view the unscheduled panel
- **Then** it does not appear until it enters the four-week horizon

**AC-VP003-D:**
- **Given** there are no Overdue or unscheduled Visit Dues due within the next four weeks
- **When** I view the panel
- **Then** it shows "Nothing unscheduled due in the next 4 weeks"

**AC-VP003-E:**
- **Given** I change the scheduler between Day, Week and Month
- **When** the calendar timeframe changes
- **Then** the unscheduled panel continues to use the same four-week horizon

**AC-VP003-F:**
- **Given** I have never opened the planner before
- **When** it loads
- **Then** the scheduler opens in Week view

**AC-VP003-G:**
- **Given** I previously selected Day, Week or Month view
- **When** I return to the planner
- **Then** it opens in my last-selected view

**AC-VP003-H:**
- **Given** I select Month view
- **When** the month renders
- **Then** each day shows its number of scheduled visits and does not render individual visit cards
- **And** it shows town names only when the day cell has room

**AC-VP003-I:**
- **Given** a day in Month view contains one or more scheduled visits
- **When** I select that day
- **Then** the scheduler switches to Day view with that date active
- **And** the day's individual visits are shown there

**AC-VP003-J:**
- **Given** I am using Day, Week or Month view and there is enough horizontal room
- **When** the planner renders
- **Then** the unscheduled panel remains visible beside the calendar

**AC-VP003-K:**
- **Given** available width is too limited for both usable calendar cells and the open panel
- **When** the planner renders
- **Then** the panel collapses to an `Unscheduled (n)` control
- **And** I can reopen it without losing my selection or changing its four-week horizon

**AC-VP003-L:**
- **Given** a scheduled visit has a Due Reason
- **When** I view it in Week view
- **Then** its card shows the Reason Type's icon and colour without the free-text explanation
- **And** the icon has an accessible type label

**AC-VP003-M:**
- **Given** a scheduled visit has a Due Reason
- **When** I view it in Day view
- **Then** the reason text is shown when space permits
- **And** opening the visit always shows the Reason Type and full explanation

> **Dependency:** BR-NEW-004 defines the structured Reason Type. Its vocabulary and ownership are still open.

**Superseded:** Visit Planning US-003 scenario 5's “next 2 weeks” empty-state wording.

---

### Visit Planning US-004 and US-005 — duration behavior when scheduling

> **Context:** distinguishes the immediate drag accelerator from the explicit `Schedule on...` route while preserving the source stories' default-duration and warning rules.

**Additional Acceptance Criteria:**

**AC-VP004-A:**
- **Given** an unscheduled Visit Due has a resolved default duration
- **When** I drag it onto an available day
- **Then** it is scheduled immediately with that duration and the day's load updates

**AC-VP004-B:**
- **Given** a scheduled visit uses its default duration
- **When** I select the visit and amend its duration
- **Then** the visit and day load update immediately

**AC-VP004-C:**
- **Given** a normal drop takes the day over its Working Day
- **When** the visit is scheduled
- **Then** the drop succeeds and the day shows `Over by` with the amount

**AC-VP004-D:**
- **Given** the dropped date is after the Visit Due's due date
- **When** I drop the visit
- **Then** the existing US-004 confirmation is shown before scheduling, including the Due Reason when present

**AC-VP004-E:**
- **Given** the destination is blocked by Planned Absence
- **When** I try to drop the visit
- **Then** the drop is refused as specified by US-004

---

### Visit Planning US-009 — review-and-select bulk decisions

> **Context:** replaces immediate `Apply to all` behavior. The rep chooses an outcome first, reviews every candidate across both digest sections, and may exclude individual visits before applying it.

**Additional / Replacement Acceptance Criteria:**

**AC-VP009-A:**
- **Given** the digest contains visits in `Still open from earlier weeks` and `This week`
- **When** I choose `Review Keep Overdue...` or `Review Mark Missed...`
- **Then** a dedicated review page lists all candidate visits under those same section headings
- **And** every candidate starts selected

**AC-VP009-B:**
- **Given** the bulk review is open
- **When** I use Select all, Clear all, or change an individual checkbox
- **Then** the selected count and the set to be changed update immediately

**AC-VP009-C:**
- **Given** I excluded one or more visits
- **When** I apply the chosen outcome
- **Then** only selected visits receive it
- **And** excluded visits remain undecided in the digest

**AC-VP009-D:**
- **Given** no visits are selected
- **When** the review renders
- **Then** the apply action is unavailable and the review explains that at least one visit must be selected

**AC-VP009-E:**
- **Given** I apply the chosen outcome successfully
- **When** processing completes
- **Then** I return to the Cycle End digest
- **And** excluded or otherwise undecided visits remain visible there

**AC-VP009-F:**
- **Given** I leave the review without applying
- **When** I return to the digest
- **Then** no visit decisions have changed

**AC-VP009-G:**
- **Given** I am reviewing `Mark Missed` for selected visits
- **When** I enter an optional shared reason
- **Then** that reason is applied to every selected visit that has no individual override

**AC-VP009-H:**
- **Given** a selected visit has a different cause
- **When** I override or clear its reason
- **Then** that visit keeps its individual value while the other selected visits retain the shared reason

**AC-VP009-I:**
- **Given** I change the shared reason after adding an individual override
- **When** the review updates
- **Then** the override is preserved and only non-overridden selected visits receive the new shared value

**AC-VP009-J:**
- **Given** I apply `Mark Missed` to selected Visit Dues
- **When** processing succeeds
- **Then** each selected Visit Due is closed and recorded as Missed with its applicable optional reason
- **And** no replacement visit, notification or automatic follow-up workflow is created
- **And** the next fixed-cycle Visit Due is unchanged

**AC-VP009-K:**
- **Given** a missed visit should be attempted again
- **When** the rep or a reviewing manager decides to reschedule it
- **Then** they create a one-off Visit Due through the normal planning flow

**AC-VP009-L:**
- **Given** I want to make different decisions for individual visits
- **When** I use a row's `Keep Overdue / Missed` controls on the digest
- **Then** I can save those decisions without entering either bulk-review page

**AC-VP009-M:**
- **Given** the digest first loads
- **When** I have not made a decision for a visit
- **Then** neither row outcome is preselected

**AC-VP009-N:**
- **Given** I select `Missed` on one digest row
- **When** the row expands
- **Then** an optional reason field appears directly beneath that visit

**AC-VP009-O:**
- **Given** I entered a row-level Missed reason
- **When** I switch that row to `Keep Overdue`
- **Then** the reason field is hidden and its value is not saved

**Superseded:** Visit Planning US-009 scenario 3's immediate `Apply to all` behavior.

---

### Pricing US-008 *(confirm)* — price override applied within allowance

> **Context:** overrides change from *requested* (head office decides) to *applied* within the rep's allowance (BR-NEW-002). The override sheet has four states: stacks, falls back, blocked by promotion, blocked by multi-buy.

**Additional Acceptance Criteria:**

**AC-PR008-A:**
- **Given** I open the override sheet on a line
- **When** it renders
- **Then** it shows the resolved price with its source and the lowest price I can offer, before I type anything

**AC-PR008-B:**
- **Given** I enter a price no lower than the lowest I can offer, with a reason
- **When** I tap Apply
- **Then** the price is applied to the line immediately and no head office decision is created

**AC-PR008-C:**
- **Given** I enter a price below the lowest I can offer
- **When** I try to apply it
- **Then** Apply is unavailable

**AC-PR008-D:**
- **Given** the line has a promotion no larger than my allowance
- **When** the sheet renders
- **Then** the lowest price is my allowance off the promotion price, and the sheet says so

**AC-PR008-E:**
- **Given** the line has a promotion larger than my allowance, and my allowance off the tier or break price is lower than the promotion price
- **When** the sheet renders
- **Then** the lowest price is my allowance off that base, and the sheet names the base ("10% off the bulk price")

**AC-PR008-F:**
- **Given** the line has a promotion larger than my allowance, and the promotion price is lower than anything my allowance can reach
- **When** the sheet renders
- **Then** it explains that no further discount can be added, with no price field and a Close action

**AC-PR008-G:**
- **Given** the line is part of a buy X get Y, bundle or mix-and-match promotion
- **When** the sheet renders
- **Then** it names the promotion and explains a rep discount can't be added, with no price field

**AC-PR008-H:**
- **Given** a rep price has been applied to a line
- **When** I open the price provenance sheet
- **Then** the applied price is shown as a calculation: the base or promotion price, my discount, and my reason

**Superseded:** any criterion in which head office accepts or declines an override ("accept at €9.00" / "accept at resolved €11.20"), and any status on T-08 describing an override's outcome.

**Recommended Acceptance Tests:**

- `Should_ShowLowestPriceBeforeInput_When_OverrideSheetOpens`
- `Should_ApplyWithoutHeadOfficeDecision_When_PriceWithinAllowance`
- `Should_StackOnPromotion_When_PromotionWithinAllowance`
- `Should_FallBackToBasePrice_When_PromotionExceedsAllowanceButBaseAllowanceLower`
- `Should_ExplainBlock_When_PromotionBelowAnyReachablePrice`
- `Should_BlockRepDiscount_When_LineInMultiBuy`
- `Should_ShowCalculation_When_ProvenanceOpenedOnDiscountedLine`

---

### Pricing US-009 *(confirm)* — free of charge restricted and applied

> **Context:** BR-NEW-003.

**Additional Acceptance Criteria:**

**AC-PR009-A:**
- **Given** I choose Add free-of-charge line
- **When** the picker opens
- **Then** it lists only products in the Discontinuing state, and states that only discontinuing products can be given free

**AC-PR009-B:**
- **Given** the picker is open
- **When** it renders
- **Then** it shows the order's free-of-charge cap and how many units are already used

**AC-PR009-C:**
- **Given** adding the quantity would exceed the cap
- **When** I try to add it
- **Then** it can't be added

**AC-PR009-D:**
- **Given** the free-of-charge line is within the cap
- **When** I add it
- **Then** it is applied to the order and no head office decision is created

**Superseded:** free-of-charge lines on any product with a free-text reason; head office "with or without the free line" decisions.

**Recommended Acceptance Tests:**

- `Should_ListOnlyDiscontinuingProducts_When_FocPickerOpens`
- `Should_ShowCapAndUsage_When_FocPickerOpens`
- `Should_PreventAdd_When_FocCapWouldBeExceeded`
- `Should_ApplyWithoutHeadOfficeDecision_When_FocWithinCap`

---

### Promotions US-007 *(confirm)* — spend threshold qualifies before rep discounts

**Additional Acceptance Criteria:**

**AC-PM007-A:**
- **Given** an order qualifies for a spend-threshold promotion on resolved prices
- **When** rep discounts bring the order total below the threshold
- **Then** the order still qualifies for the threshold discount

**Recommended Acceptance Tests:**

- `Should_KeepThresholdDiscount_When_RepDiscountDropsTotalBelowThreshold`

---

### Area 1 US-014 — sent item view

**Additional Acceptance Criteria:**

**AC-A1014-A:**
- **Given** a line on the order was removed as unavailable
- **When** I open the sent item
- **Then** the line reads "Not supplied — [reason]. Removed from the order. Let the customer know."

**AC-A1014-B:**
- **Given** the order has a rep price or a free-of-charge line
- **When** I open the sent item
- **Then** they are shown as facts of the order ("SPF30 at your price €9.25"), not as the outcome of a request

**Superseded:** "outcomes of any override or free-goods request".

> **Context:** settled 26 Sep 2026 (BR-NEW-009, R4.1). "Open on website" lands on R-04. Until the order cut-off, a rep can edit or cancel a Pending order they took themselves; customer-placed and other reps' orders open read-only. R-04's layout and first-iteration defaults were confirmed the same day.

**AC-A1014-C:**
- **Given** I took an Order at Carey's Pharmacy at 10:40 and the cut-off is 4pm
- **When** I open it on the website at 2pm
- **Then** I can change its lines, or cancel it, until 4pm
- **And** an order Mary placed online for Hickey's Rathdrum opens read-only, even while Pending

---

### Area 1 US-006, US-007 — "Stock mentioned" on a phone Call

> **Context:** settled 24 Sep 2026 (T6.5). A rep can't count stock down a phone line; the shopkeeper most likely says "we're low on X" or "we're out of Y". That is unconfirmed, so counts stay possible at one extra tap.

**Additional Acceptance Criteria:**

**AC-A1006-A:**
- **Given** I am recording a Call at Doyle's with a suggested list of 16 products
- **When** I choose Channel "Phone"
- **Then** the stock section reads "Stock mentioned", lists the same 16 products with a filter box, and each row offers Low, Out, Add to order and "Add count" with no stepper

**AC-A1006-B:**
- **Given** Channel "Phone"
- **When** I type "sudo" in the filter box
- **Then** the list narrows to matching products, and if none match I can search the catalogue and add the product as a row

**AC-A1006-C:**
- **Given** Channel "Phone"
- **When** I mark Sudocrem 125g Out
- **Then** it is also marked Low and recorded with a count of 0

**AC-A1006-D:**
- **Given** Channel "Phone", SPF30 marked Low, Sudocrem marked Out and 14 rows untouched
- **When** I tap Review and save
- **Then** the review reads "Phone · 2 marked Low (1 out)", only the 2 marked products are recorded, and the 14 untouched rows are not saved as Not checked

**AC-A1006-E:**
- **Given** Channel "Phone"
- **When** I tap "Add count" on a row and enter 3
- **Then** the row records a count of 3, as it would in person

**AC-A1006-F:**
- **Given** I have entered counts or marks
- **When** I switch Channel between In person and Phone
- **Then** nothing I entered is lost: counts show as the row's count on Phone, and Out rows show a count of 0 in person

**Recommended Acceptance Tests:**

- `Should_ShowStockMentionedWithoutSteppers_When_ChannelPhone`
  → Verifies the section adapts to the channel mode.
- `Should_FilterSuggestedRowsThenFallToCatalogue_When_TypingOnPhone`
  → Verifies the filter box covers both recognised and unlisted products.
- `Should_RecordLowWithZeroCount_When_OutMarked`
  → Verifies Out stores the same data as an in-person count of 0.
- `Should_NotSaveUntouchedRowsAsNotChecked_When_ChannelPhone`
  → Verifies a phone Call doesn't pollute the next suggested list.
- `Should_KeepEnteredValues_When_ChannelSwitched`
  → Verifies switching mode never discards input.

**Superseded:** US-006 Scenario 2's "a Stock Check of 4 products" on Phone is now four products mentioned (marked or counted), not a counted stock check.

---

### Area 1 US-009 — Low Stock Hint wording

> **Context:** settled 24 Sep 2026 (T6.3). The hint fires from a threshold, which is a floor; no "usual level" exists in the data (T7.7).

**AC-A1009-A:**
- **Given** the resolved threshold for "Cold & Flu Relief 16s" at Murphy's Pharmacy is 6
- **When** I enter a count of 3
- **Then** I see "Below low-stock level (6)" under the Low tick, and Low stays unticked

**Recommended Acceptance Tests:**

- `Should_ShowThresholdWording_When_CountBelowResolvedThreshold`
  → Verifies the hint names the floor, never a "usual" level.

**Superseded:** Scenario 1's "Below usual level" beside the count.

---

### Customer Directory US-005 — Set from GPS only while unconfirmed

> **Context:** settled 24 Sep 2026 (T5.3). Once a rep has confirmed the position on site, the action has no job; a wrong confirmation is a head-office revert.

**AC-CD005-A:**
- **Given** Quinn's Centra has Precision "Town" or "Eircode"
- **When** I open the Location on the tablet
- **Then** I see "Map position approximate" with Set from GPS, highlighted on my first visit only

**AC-CD005-B:**
- **Given** Precision is "Confirmed on site" (sent or not yet sent)
- **When** I open the Location
- **Then** Set from GPS is not shown

**AC-CD005-C:**
- **Given** head office reverts a confirmed capture
- **When** my tablet next syncs
- **Then** Set from GPS is shown again

**Recommended Acceptance Tests:**

- `Should_ShowSetFromGps_When_PrecisionIsDefaulted`
  → Verifies the action appears while the position is approximate.
- `Should_HideSetFromGps_When_PositionConfirmedOnSite`
  → Verifies the action disappears once confirmed, including before sync.
- `Should_ShowSetFromGpsAgain_When_CaptureReverted`
  → Verifies the revert path restores the action.

**Superseded:** Scenario 4 (available but not highlighted, with "Replace the confirmed position?").

---

### Area 1 US-023 — the chain's agreed range as a section on a branch order

> **Context:** settled 24 Sep 2026 (T7.14). A branch order works through the agreed range as a set, so it sits together at the top; each product appears once.

**AC-A1023-A:**
- **Given** Hickey's Pharmacy has an Agreed Range of 30 products
- **When** I open an Order at Hickey's Rathdrum
- **Then** the pad opens with a section "Hickey's agreed range (30)" above the normal pad, including products outside my ranges

**AC-A1023-B:**
- **Given** SPF30 Sun Lotion 200ml is in both the agreed range and my ranges
- **When** the pad renders
- **Then** SPF30 appears only in the agreed range section, not again under Sun care

**Recommended Acceptance Tests:**

- `Should_ShowAgreedRangeSectionFirst_When_OrderAtBranch`
  → Verifies the agreed range reads as a set at the top.
- `Should_ListProductOnce_When_InAgreedRangeAndRepRange`
  → Verifies no duplicate rows between the section and the pad.

**Superseded:** Scenario 5's per-row "In Hickey's agreed range" marker within the pad.

> **Context:** settled 26 Sep 2026 (T7.15, BR-NEW-008). With several Agreed Ranges, each gets its own stacked section at the top of the pad, all expanded and each collapsible like a category header. One product, one place holds across sections. Section order is open.

**AC-A1023-C:**
- **Given** Hickey's Pharmacy has Agreed Ranges "Everyday" (30) and "2026 Christmas gift packs" (12)
- **When** I open an Order at Hickey's Arklow
- **Then** the pad opens with a section for each range above the normal pad, both expanded

**AC-A1023-D:**
- **Given** Hand Cream 75ml is in both of Hickey's ranges and in my ranges
- **When** the pad renders
- **Then** Hand Cream appears once, in only one range section, and not again in its category

**AC-A1023-E:**
- **When** I collapse "2026 Christmas gift packs"
- **Then** its products are hidden until I expand it again, and any quantities already entered are kept

> **Context:** settled 26 Sep 2026 (T7.16). The chain's everyday range is the first section; seasonal and other ranges follow. The first section a shared product belongs to is where it appears. *T7.17:* no flag or date identifies the everyday range; this is a convention the rep knows, not a rule the system enforces. *T7.18:* the pad orders sections oldest range first, which puts the everyday range first in practice; a product in several ranges appears in the oldest.

**AC-A1023-G:**
- **Given** Hickey's "Everyday" range was created in 2024 and "2026 Christmas gift packs" in September 2026
- **When** I open an Order at Hickey's Arklow
- **Then** the Everyday section comes first, then the gift-pack section
- **And** Hand Cream, in both, appears only under Everyday

**AC-A1023-F:**
- **Given** Hand Cream 75ml is in Hickey's Everyday and in 2026 Christmas gift packs
- **When** I open an Order at Hickey's Arklow
- **Then** "Hickey's Everyday (30)" is the first section and Hand Cream appears there
- **And** the gift-pack section shows "(11)" and does not list Hand Cream

---

### Area 1 US-017 — Needs Attention holds technical faults only

> **Context:** settled 24 Sep 2026 (BR-NEW-006). Work is judged as captured, so a later business-rule change never bounces it.

**Additional Acceptance Criteria:**

**AC-A1017-A:**
- **Given** I captured an Order at Quinn's Centra while it was assigned to me
- **When** Quinn's is reassigned to Aoife before I sync
- **Then** the Order uploads and is accepted, and nothing appears in Needs Attention

**AC-A1017-B:**
- **Given** an upload arrived incomplete
- **When** the Sync finishes
- **Then** the item appears in Needs Attention with "Couldn't be sent - the upload was incomplete" and Open and Delete

**AC-A1017-C:**
- **Given** the server can't say anything the rep can act on
- **When** the item appears in Needs Attention
- **Then** it reads "This couldn't be sent - contact the office", with the raw reason kept for support and not shown by default (T1.2)

**Recommended Acceptance Tests:**

- `Should_AcceptOrder_When_LocationReassignedAfterCapture`
  → Verifies work is judged against the rules at capture.
- `Should_AcceptOrder_When_LocationArchivedAfterCapture`
  → Verifies an archived Location doesn't bounce captured work.
- `Should_ShowPlainReasonWithOpenAndDelete_When_UploadIncomplete`
  → Verifies Needs Attention holds technical faults with a plain sentence.

**Superseded:** Scenario 1's "Location no longer exists" as a rejection reason.

---

### Head Office US-001, US-002 — worklist *(superseded in part)*

Superseded: the Flagged Orders and Routine Orders sections, and the "Accept all routine" action. Orders no longer appear on the worklist (US-NEW-005).
Retained: range proposals, duplicate matches and account requests, each labelled with its type.

### Head Office US-003 — order detail decisions *(superseded)*

H-02 becomes a record rather than a decision screen (US-NEW-005, AC-6 and AC-7). Partial Release is replaced by routing short quantities to allocation. *Amended 26 Sep 2026 (H2.9):* Hold and Reject return as manual actions on a Pending order, before the cut-off.

### Head Office US-006 — held orders *(amended 26 Sep 2026)*

> **Context:** settled 26 Sep 2026 (H2.9, resolves RC-NEW-003). Orders wait as Pending until the order cut-off (BR-NEW-009). Before then, head office may Hold or Reject an order by hand on H-02; nothing flags orders for this. Held orders are listed on H-06 until released or rejected. Short stock no longer holds an order, so US-006 S2 is superseded. Visibility, editing and H-06's layout were confirmed the same day.

**AC-HO006-A:**
- **Given** order O-10412 is Pending and the cut-off is 4pm
- **When** I choose Hold at 2pm with the note "Credit stop - check with accounts"
- **Then** it is not accepted at 4pm and appears on Held orders with its note and time held
- **When** I choose Release
- **Then** it is accepted at once, with BR-NEW-001's dispositions applied then

**AC-HO006-B:**
- **Given** an order is Pending
- **When** I choose Reject with the reason "Account on hold"
- **Then** the order is Rejected, and the rep and customer see "Rejected — Account on hold"

**AC-HO006-C:**
- **Given** an order has been accepted
- **When** I open it on H-02
- **Then** neither Hold nor Reject is offered

---

## Edge cases

- **EC-NEW-001 — promotion versus bulk price.** SPF30 at 24 units: promotion €9.99 (11% off tier) exceeds a 10% allowance, but 10% off the €10.08 break is €9.07. The rep can offer €9.07 (BR-NEW-002 rule 4).
- **EC-NEW-002 — weak multi-buy.** A buy 10 get 1 free (about 9%) blocks the rep's discount although a price promotion of the same size wouldn't. Accepted (BR-NEW-002 rule 5).
- **EC-NEW-003 — free-of-charge stock runs out.** The line is removed as unavailable and the rep is prompted (BR-NEW-003 rule 3).
- **EC-NEW-004 — first order of a product at a location.** No out-of-pattern marker, since there's nothing to compare against (US-NEW-004).
- **EC-NEW-005 — Low unticked after adding.** The order line stays (US-NEW-002).
- **EC-NEW-006 — one-off and recurring visit held by different reps.** Aoife's one-off Call at Byrne's on Tue 22 Sep leaves Colm's recurring visit open. The manager and Colm decide whether to keep it, move it or cancel it as covered (Visit Planning US-015, AC-VP015-F).
- **EC-NEW-007 — second removal on a told order.** Tue: Quinn's order loses SPF30; the rep phones and marks it told. Thu: Hand Cream on the same part-despatched order goes unavailable and is removed. The order counts again with only Hand Cream flagged; SPF30 keeps "Told Tue 14:20" (US-NEW-003, AC-NEW-003-9).
- **EC-NEW-008 — product ordered once from the starting-point section.** Mary orders Arnica Gel once from "Often ordered for Hickey's Rathdrum", then places her 3rd order. The section is removed and Arnica Gel isn't yet in her usual list, so she finds it through search until she has ordered it twice (US-NEW-007, C9.4).

---

## Open clarifications

- ~~**RC-NEW-001** — What clears the "not supplied" count: opening the order, an explicit "Told them", or the next call at that location?~~ **Resolved (24 Sep 2026):** "Told them", marked per order on the list and on T-08, with Undo; the next Call logged at the Location is the backstop; a later removal re-raises the order with only the new line (US-NEW-003, EC-NEW-007).
- ~~**RC-NEW-002** — Are the rep discount allowance and the free-of-charge cap one figure for everyone, set per rep, or per category?~~ **Resolved at UX level:** a manager groups eligible products into a commercial policy profile with rules such as 10% rep discount and X FOC units per rep per month.
- ~~**RC-NEW-003** — Do Hold and Reject survive on H-02 with no flag driving them?~~ **Resolved 26 Sep 2026 (H2.9):** yes, by hand, before the cut-off; held orders on H-06.
- ~~**RC-NEW-004** — Do orders appear on H-01 at all, e.g. as a read-only feed, or only via search and the customer record?~~ **Resolved 26 Sep 2026 (H1.8):** no feed; a secondary "View orders" link on the Worklist opens H-31 Order list.
- ~~**RC-NEW-005** — Does the quantity popover dismiss on add, or advance to the next not added Low item?~~ **Resolved (24 Sep 2026):** it always dismisses on add, wherever it was opened.
- **RC-NEW-006** — Does the spend-threshold discount apply to the final order total (assumed) or the pre-discount total?
- ~~**RC-NEW-007** — What does the Low tab show on an order that doesn't follow a stock check (a phone order, say)?~~ **Resolved (24 Sep 2026):** a phone Call now has "Stock mentioned" (Area 1 US-006/US-007). An order with no Call carries the gaps (not added, CAN'T ADD) from the Location's most recent Call, minus products ordered since, dated, with no age limit (US-NEW-002 AC-8–12).
- ~~**RC-NEW-008** — If a Product is outside a commercial policy profile, is the corresponding action unavailable?~~ **Resolved:** yes; product membership explicitly enables the action.
- **RC-NEW-009** — Does the commercial policy profile replace or extend the catalogue's existing one-per-product Product Profile, or become a separate rule/membership model? The UX name is provisional until the business rules are firmer.
- ~~**RC-NEW-010** — When an offline rep adds an FOC line, does it consume the monthly allowance immediately, and how is that reservation reconciled?~~ **Resolved:** FOC is an ordinary €0.00 order line; its quantity counts while the line exists and normal line removal releases it. The tablet combines the synced balance with local lines.
- ~~**RC-NEW-011** — Does the manager/admin choose the icon and colour for each Visit Due Reason Type, or does the system assign them?~~ **Resolved:** the manager/admin chooses both from a controlled system set.
- ~~**RC-NEW-012** — What happens after a rep marks a Visit Due Missed?~~ **Resolved:** close and record only. No automatic replacement or workflow; a rep or manager may manually create a one-off Visit Due.
- ~~**RC-NEW-013** — When the one-off's rep also holds the open recurring visit at the Location, does their Call complete both or only the one-off?~~ **Resolved:** only the one-off. The same rep may need two visits with different contacts. When one is enough, the manager moves the existing visit forward in the planner instead of adding a one-off.
