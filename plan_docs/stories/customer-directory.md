# Customer Directory: UX & User Stories

**Generated:** 18 September 2026 (amended 23 September 2026 from the UX design sessions: a Location may hold many Location Profiles — see `../uxdocs/04-user-stories-amendments.md`, BR-NEW-005)
**Bounded context:** Customer Directory
**Primary users:** Head Office User; Field Salesperson (GPS capture and contact status in the field)
**Scope:** The website screens where head office maintains Customers, Locations, Contacts and geography, and the small tablet additions that keep them accurate from the field. Six screens: Customer Record, Location Record, Contact Record, Main Contact Replacement, Geography, Gap Lists.

---

## 1. Bounded Context & Scope

### Bounded context

- **Domain area:** Customer Directory. It owns who the customers are, where they are, and who to talk to. Every other area reads from it; only this area and the field GPS capture write to it.
- **Ubiquitous language:**
  - **Customer** — the buying organisation. Has many Locations. Not a unit of performance measurement.
  - **Location** — a physical place belonging to a Customer (or a Prospect, area 2). Carries a **Town** (required), an optional **Eircode**, coordinates, a **Location Type**, any number of **Location Profiles**, Contacts, and optionally a **Master Location**. The unit of performance measurement.
  - **Location Type** — what the place is: Pharmacy, Convenience store, Supermarket, Head office. Reference classification with a description. Optional.
  - **Location Profile** — how the place is serviced or grouped: Large pharmacy, Small rural shop, Customer campaign X. May carry defaults for **Visit Frequency**, **Visit Duration** and **Low Stock Thresholds** (per product); a profile carrying none is purely a grouping, used for selection (specialist scopes, campaign filters, reporting). **A Location may hold many profiles.** Where several supply the same default, each field resolves separately to the **most demanding value** (most frequent visits, longest duration, highest threshold), and a per-Location override beats every profile. Every resolved default is shown with its source ("Every 2 weeks (from Large pharmacy)"). Optional; a Location with no profile carrying a Visit Frequency generates no Recurring Visit Dues and appears on the **No Visit Schedule** list.
  - **Master Location** — a Location that other Locations of the same Customer report to (a head office or main shop). A Location with a master is a **Branch Location**. Recorded here; what a master does (range agreements, multi-branch orders, master-vs-branch call types) is the Master & Branch Ordering area.
  - **Contact** — a person. Has a **Contact Type**, contact details, and links to one or more Locations. States: **Active**, **Inactive** (left, or no longer to be asked for; never deleted because Calls reference them).
  - **Main Contact** — exactly one Active Contact per Location. Removing or unlinking a Main Contact requires naming a replacement, or marking the Contact Inactive, which leaves the Location flagged **Replacement Needed**.
  - **Contact Type** — reference classification with a description (Owner, Manager, Buyer, Pharmacist).
  - **Region / County / Town** — strict hierarchy. A Location's Town decides its territory (Coverage Management). Moving a Town to another County moves its Locations' territory.
  - **Coordinates** — latitude/longitude with a **Precision**: *Town* (defaulted from the Town), *Eircode* (defaulted from the Eircode), or *Confirmed on site* (captured from a rep's device). Previous values are kept so a bad capture can be reverted.
  - **GPS Capture** — a rep at the Location saving the device's position as its coordinates. Applies immediately; a reading far from the expected position asks the rep to confirm first. Works offline and syncs as unsent work.
  - **Temporarily Closed** — a date range on an otherwise active Location (refurbishment, seasonal shutdown) with an expected **Reopen Date**, which may be open-ended. The Location stays active: Visit Dues keep generating and orders are unaffected, but days inside the window cannot be scheduled — the Location-side equivalent of a rep's Planned Absence. Extendable, or shortened to reopen early, by head office or by the rep from the tablet.
  - **Closed** — a permanent Location state with a **Closed Date**. No new Visit Dues; open Visit Dues are cancelled (recorded as Cancelled, not Missed); no new Orders may be started; existing Orders untouched and In Progress ones still send. Stays on the Customer record and in searches labelled Closed; leaves Home and Suggested Lists on the tablet. Can be **Reopened**, restoring scheduling from the next Cycle Period.
  - **Gap Lists** — head office lists of Locations with no Profile, no rep (Unassigned, from Coverage Management), Replacement Needed, or unconfirmed coordinates.
- **Upstream contexts:**
  - **Eircode / address lookup** — coordinates for an Eircode (external service, assumed available).
  - **Rep at a Location** (area 1) — GPS Captures arriving via Sync.
- **Downstream contexts:**
  - **Coverage Management** — Locations, Towns and the hierarchy for territory resolution; Customers and Profiles as Specialist scopes.
  - **Visit Planning** — Profile defaults, coordinates and Precision for the planner map, Master/Branch for the Master & Branch Ordering area.
  - **Rep at a Location** — Location details, Main Contact and status, coordinates, in the Morning Snapshot.
  - **Prospecting** (area 2) — Prospect Locations convert into Customer Locations here.
  - **Self-service** (area 7) — Contacts become Customer Users.
- **Terms that mean something different elsewhere:**
  - **Profile** — Location Profile (servicing defaults) is unrelated to Product Profile (product classification).
  - **Master** — a Master Location is a Location; it is not the Customer and not a "master record" in the data sense.
  - **Inactive** — a Contact state. A Location is **Temporarily Closed** (still active) or **Closed** (permanent), never inactive or deleted.
  - **Territory** — derived from Town; never set on the Location directly.

### Scope

- **In scope:**
  - Customer create/edit with its Locations
  - Location create/edit: Town, Eircode, Type, Profile, Master Location, coordinates
  - Contact create/edit, Location links, Main Contact designation, replacement flow, Inactive state
  - Contact Type and Location Type reference lists (archive-not-delete)
  - Location Profile list with its defaults (frequency, duration, thresholds)
  - Region / County / Town maintenance, including moving a Town
  - Coordinate defaulting and GPS Capture (tablet amendment)
  - Temporary closures; closing and reopening a Location
  - Gap Lists
- **Out of scope:**
  - Range agreements, multi-branch ordering, master-vs-branch call types (Master & Branch Ordering area)
  - Assigning reps (Coverage Management)
  - Visit Due generation (Visit Planning)
  - Products, Ranges, prices
  - Customer User accounts and self-service sign-up (area 7)
  - Cold-call Prospect creation on the tablet (area 2)
  - Bulk import of customers from external systems
- **Assumptions:**
  - An Eircode lookup service is available online to head office; the tablet never geocodes.
  - Contact details (phone, email) are free fields; no validation beyond format.
  - A Location has exactly one Customer; moving a Location between Customers is a rare head-office edit with history kept.
  - The distance check threshold for GPS Capture is a system setting, assumed 2 km.
  - Location Type and Profile lists follow the archive-not-delete rule from Product Management.

---

## 2. Personas

### Head Office User

- **Role:** maintains the customer base; usually also a Sales Manager.
- **Responsibilities:** sets up new customers and shops; keeps contacts current as staff change; assigns Types and Profiles so visits are scheduled at the right rhythm; keeps geography tidy.
- **Context on arrival:** a new chain to set up with a head office and twelve branches; a rep reporting that the buyer has left; a Town that was put in the wrong County.
- **Goal:** "Keep the customer base accurate enough that reps arrive at the right place, ask for the right person, and get visits scheduled at the right rhythm."
- **Pain points:** shops nobody is scheduled to visit because a field was skipped; a contact who left months ago still shown as the person to ask for; hundreds of shops with no map position.

### Field Salesperson (field data)

- **Role:** the area 1 rep, correcting the directory from where they stand.
- **Responsibilities:** confirms the shop's position; notices when the main contact has gone.
- **Context on arrival:** standing in the shop, offline, one hand free.
- **Goal:** "Fix what I can see is wrong without it becoming a job."
- **Pain points:** a town-level pin that sends the sat-nav to the wrong end of a long street; being greeted by a stranger where the system still names the old owner.

---

## 3. Flow Sketch

```mermaid
flowchart TD
    Cust[Customer Record: Locations list] --> NewLoc[Create Location: Town required, Eircode, Type, Profile, Master]
    NewLoc --> Coord{Eircode given?}
    Coord -->|Yes| EirPin[Coordinates from Eircode, Precision: Eircode]
    Coord -->|No| TownPin[Coordinates from Town, Precision: Town]
    EirPin --> Loc[Location Record]
    TownPin --> Loc
    Loc --> Gaps{Profile? Main Contact? Rep?}
    Gaps -->|Missing| GapList[Gap Lists: No Visit Schedule / Replacement Needed / Unassigned / Unconfirmed position]
    Gaps -->|Complete| Loc

    Loc --> Contacts[Contacts at this Location, one Main]
    Contacts --> AddC[Link existing Contact or create new]
    Contacts --> RemoveMain[Remove or unlink Main Contact]
    RemoveMain --> Replace{Replacement known?}
    Replace -->|Yes| Name[Name replacement from this Location's Contacts or new] --> Contacts
    Replace -->|No| Inactive[Mark Contact Inactive; Location flagged Replacement Needed] --> GapList

    Loc --> Temp[Temporarily Closed: reopen date, scheduling blocked in window, extendable] --> Loc
    Loc --> Close[Mark Closed with date: open visits cancelled, branches reporting here flagged] --> Loc
    Tablet --> TempEdit[Extend or shorten a temporary closure] -. Sync .-> Temp

    Geo[Geography: Region > County > Town] --> MoveTown[Move Town to another County: impact on Locations and territories shown] --> Geo

    Tablet[Tablet: Location screen during a visit] --> GPS[Set location from GPS]
    GPS --> Far{Far from expected position?}
    Far -->|Yes| Confirm[Confirm: "4 km from Rathdrum — save anyway?"]
    Far -->|No| Save[Saved as unsent, Precision: Confirmed on site]
    Confirm -->|Yes| Save
    Confirm -->|No| Tablet
    Save -. Sync .-> Loc
```

---

## 4. Design Decisions

### Type and Profile are separate, and both optional

- **Chose:** Location Type says what the place is; Location Profile says how it is serviced and carries the scheduling and stock defaults; either may be blank, with gaps flagged on the No Visit Schedule list.
- **Over:** one combined classification; a required Profile at creation.
- **Because:** a large pharmacy and a large supermarket can share servicing; a head office is a different type from its shops but may share nothing; the person creating a Location often does not yet know how it will be serviced.
- **Trade-off accepted:** a Location can exist with no visits due until someone works the gap list; the list is shown beside Unassigned so both are cleared together.
- **Amended 23 Sep 2026:** a Location may hold many Location Profiles, and defaults resolve field by field to the most demanding value, each shown with its source. **Trade-off accepted:** the resolved set can mix profiles, so it may match no single profile a manager set up; showing each value's source is the mitigation.

### Main Contact is the one thing that cannot be left blank

- **Chose:** removing or unlinking a Main Contact requires naming a replacement; when none is known, the outgoing Contact is marked Inactive and the Location is flagged Replacement Needed; Contacts are never deleted.
- **Over:** allowing a blank Main Contact; deleting departed contacts.
- **Because:** a rep needs to know who to ask for, and "nobody" is worse than "Mary (inactive)"; historical Calls reference the person; the rule survives edits made from another Location's screen.
- **Trade-off accepted:** a small extra step on every contact change; Inactive contacts accumulate and lists must hide them by default.

### Coordinates: rough by default, exact through use

- **Chose:** default from Eircode, else Town, with the Precision recorded; a rep can capture the device GPS during a visit, applied immediately, with a confirmation only when the reading is far from the expected position; previous values kept for revert.
- **Over:** hand entry; address geocoding for every Location; head office approval of captures.
- **Because:** every Location gets a usable pin on day one with no data project; the rep standing in the shop is the only person who knows the pin is right; a bad fix is caught by the distance check and is reversible.
- **Trade-off accepted:** town-level pins are useless for navigation until confirmed; the planner map must show precision so reps do not trust them.

### Master Location is a relationship recorded here, used elsewhere

- **Chose:** a Location may name another Location of the same Customer as its master; the behaviour it enables lives in the Master & Branch Ordering area.
- **Over:** modelling the master as the Customer; designing master ordering inside this area.
- **Because:** the Customer is the legal buyer and a master is a place with a buyer in it; a chain's head office and its shops may fall to different reps by geography, which is a coverage decision not a directory one; multi-branch ordering is a journey of its own.
- **Trade-off accepted:** the directory knows the hierarchy but not what it means; two documents must stay consistent.

### Temporary closure is a window, not a state change

- **Chose:** a refurbishment or shutdown is a date range with a Reopen Date on an active Location; Visit Dues keep generating, days in the window cannot be scheduled, and the window is extended (or shortened) by head office or the rep.
- **Over:** using Closed and reopening; suspending Visit Due generation.
- **Because:** the shop is still a customer and the visit should be planned for after it reopens, not made to disappear and come back; the rep at the door usually learns the date first; Visit Planning already blocks days this way for Planned Absence.
- **Trade-off accepted:** an open-ended closure blocks scheduling indefinitely until someone updates it; a Due Window falling entirely inside the closure needs the manager's Extend.

### Closed, never deleted

- **Chose:** a Location is marked Closed with a date; it stops scheduling and new orders, keeps its history and existing orders, stays searchable and labelled, and can be Reopened.
- **Over:** deleting; hiding it entirely; leaving open visits to go Missed.
- **Because:** orders, calls and performance reference it; a rep arriving at a shut shop should see the system knows; a refurbishment is not a permanent closure.
- **Trade-off accepted:** a Closed master leaves its branches pointing at it until head office reassigns them; the gap list shows it.

### Geography edits show their reach

- **Chose:** moving a Town to another County (or a County to another Region) shows how many Locations move and which reps' territories change before saving; Towns, Counties and Regions follow archive-not-delete.
- **Over:** silent edits; blocking moves of non-empty Towns.
- **Because:** territory is derived from Town, so a geography edit silently reassigns shops; the impact-preview pattern already exists in Coverage Management.
- **Trade-off accepted:** a Town move can produce Handover Pending entries in Coverage Management.

---

## 5. User Stories

### US-001: Create a Customer with its Locations

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want to set up a Customer and add its Locations, each with its Town, so that reps and planning know where the customer's shops are |
| **Priority** | Must Have |
| **Status** | Ready |
| **Dependencies** | Geography (US-007) |

**Acceptance criteria:**

*Scenario 1: Create with first Location*
```
When I create Customer "Hickey's Pharmacies" and add Location "Hickey's Rathdrum", Town Rathdrum, Eircode A67 X123, Type Pharmacy
Then both are saved, the Location's coordinates are set from the Eircode with Precision "Eircode"
And it appears on the No Visit Schedule list (no Profile) and the Unassigned list until Coverage Management assigns it
```

*Scenario 2: Town required*
```
When I add a Location without a Town
Then it is rejected with "Choose a town"
```

*Scenario 3: No Eircode*
```
When I add a Location with Town Laragh and no Eircode
Then coordinates are set from Laragh with Precision "Town"
```

*Scenario 4: Duplicate Location name within a Customer*
```
When I add a second Location named "Hickey's Rathdrum" to the same Customer
Then I see "This customer already has a location with that name" and can continue or rename
```

---

### US-002: Set a Location's Type, Profile and Master Location

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want to classify a Location by what it is and how it is serviced, and record which Location it reports to, so that visits are scheduled at the right rhythm and chains are understood |
| **Priority** | Must Have |
| **Status** | Ready |
| **Dependencies** | US-001, US-006 |

**Acceptance criteria:**

*Scenario 1: Profile sets defaults*
```
When I set Profile "Large pharmacy" (every 4 weeks, 45 min)
Then the Location shows "Every 4 weeks (profile default)" and leaves the No Visit Schedule list
And Visit Planning generates its Recurring Visit Dues from the Cycle Start I am asked for
```

*Scenario 2: Type independent of Profile*
```
When I set Type "Head office" and no Profile
Then the Type is saved and the Location stays on the No Visit Schedule list with "No profile — no visits will be scheduled"
```

*Scenario 3: Master Location*
```
When I set Master Location "Hickey's Head Office" on "Hickey's Rathdrum"
Then Rathdrum shows "Reports to Hickey's Head Office" and Head Office lists Rathdrum under Branches
```

*Scenario 4: Master must be same Customer*
```
When I pick a Location belonging to another Customer as master
Then it is not offered
```

*Scenario 5: No circular masters*
```
Given Head Office reports to nothing
When I try to set Rathdrum as Head Office's master while Rathdrum reports to Head Office
Then it is rejected with "This would make the locations report to each other"
```

*Scenario 6: Profile changed*
```
When I change Profile from "Large pharmacy" to "Small rural shop" (every 8 weeks)
Then future Cycle Periods follow 8 weeks and the current open Visit Due is unchanged
```

**UX amendments (23 Sep 2026)** — many profiles per Location (BR-NEW-005 in `../uxdocs/04-user-stories-amendments.md`).

*Scenario 7: Several profiles, most demanding wins*
```
Given Byrne's has Profiles "Large pharmacy" (every 2 weeks, 45 min, SPF30 warn at 6) and "Rural shop" (every 4 weeks, 20 min, SPF30 warn at 12)
Then Byrne's resolves to every 2 weeks, 45 minutes, and warns at 12 for SPF30
And each value shows its source, e.g. "Every 2 weeks (from Large pharmacy)", "Warn at 12 (from Rural shop)"
```

*Scenario 8: Grouping-only profile*
```
When I add Profile "Customer campaign X", which carries no defaults, to Byrne's
Then Byrne's defaults are unchanged and the profile is available to specialist scopes, campaign filters and reporting
```

*Scenario 9: Override beats every profile*
```
Given Byrne's has a per-Location Visit Frequency override of every 3 weeks
Then it is visited every 3 weeks whatever its profiles supply
```

---

### US-003: Manage Contacts and their Location links

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want to create Contacts, link them to one or more Locations, and set one as Main Contact per Location so that reps know who to ask for at each shop |
| **Priority** | Must Have |
| **Status** | Ready |
| **Dependencies** | US-001 |

**Acceptance criteria:**

*Scenario 1: Contact at several Locations*
```
When I create Contact "Mary Walsh", Type Pharmacist, and link her to Rathdrum, Arklow and Wicklow Town
Then she appears at all three and each shows her as a Contact
```

*Scenario 2: Set Main Contact*
```
When I set Mary as Main Contact at Rathdrum and Arklow
Then each shows "Main contact: Mary Walsh"; Wicklow Town still needs one
```

*Scenario 3: First Contact becomes Main*
```
When I link the first Contact to a Location with no Contacts
Then they are set as Main Contact automatically, with a note
```

*Scenario 4: Two Mains not allowed*
```
When I set a second Contact as Main at Rathdrum
Then I am asked "Replace Mary Walsh as main contact?" and Mary remains a Contact after confirming
```

*Scenario 5: Inactive hidden by default*
```
Given 3 Inactive Contacts at a Location
Then the Contacts list shows Active ones with "Show inactive (3)"
```

---

### US-004: Replace or retire a Main Contact

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want removing a Main Contact to require a replacement, or to mark them Inactive with the gap flagged, so that no shop is left with nobody to ask for |
| **Priority** | Must Have |
| **Status** | Ready |
| **Dependencies** | US-003 |

**Acceptance criteria:**

*Scenario 1: Replacement named*
```
Given Mary Walsh is Main Contact at Rathdrum
When I unlink her from Rathdrum
Then I am asked to choose a replacement from Rathdrum's other Contacts or create one
And on choosing "Sean Byrne" he becomes Main and Mary is unlinked
```

*Scenario 2: No replacement known*
```
When I choose "No replacement yet"
Then Mary is marked Inactive (at all her Locations), stays linked, and Rathdrum is flagged "Main contact inactive — replacement needed" on the gap list
```

*Scenario 3: Inactive at several Locations*
```
Given Mary is Main at Rathdrum and Arklow
When she is marked Inactive
Then both Locations are flagged Replacement Needed
```

*Scenario 4: Resolve the gap*
```
When I set Sean Byrne as Main at Rathdrum
Then the flag clears for Rathdrum and Mary remains Inactive
```

*Scenario 5: Tablet shows status*
```
When a rep opens Rathdrum after syncing
Then they see "Main contact: Mary Walsh (inactive) — replacement needed"
```

*Scenario 6: Reactivate*
```
When I mark Mary Active again
Then she is a normal Contact; she does not automatically regain Main
```

---

### US-005: Capture a Location's position from the field *(area 1 amendment)*

| Field | Value |
|---|---|
| **Story** | As a Field Salesperson, I want to set a shop's map position from my device while I'm standing in it so that the planner map and sat-nav are right next time, without anyone typing coordinates |
| **Priority** | Should Have |
| **Status** | Ready |
| **Dependencies** | Area 1 Location screen and Sync; US-001 |

**Acceptance criteria:**

*Scenario 1: Capture close to expected*
```
Given Rathdrum Pharmacy has Precision "Town"
When I tap Set location from GPS while at the shop, 300 m from the Town position
Then coordinates are saved as unsent with Precision "Confirmed on site" and no confirmation is asked
```

*Scenario 2: Far from expected*
```
When the device reads 4 km from the expected position
Then I see "This is 4 km from Rathdrum — save anyway?" and can confirm or cancel
```

*Scenario 3: Offline*
```
Given no signal
When I capture
Then it is saved as unsent and uploads at next Sync; the Location shows "Confirmed on site (not yet sent)"
```

*Scenario 4: Already confirmed*
```
Given Precision is already "Confirmed on site"
Then the action is available but not highlighted, and capturing again asks "Replace the confirmed position?"
```

*Scenario 5: Revert*
```
When head office reverts a capture
Then the previous Eircode or Town position is restored with its Precision
```

**UX amendments (24 Sep 2026)** — T5.3; `../uxdocs/04-user-stories-amendments.md` (Customer Directory US-005). **Superseded:** Scenario 4 (action available but not highlighted, with "Replace the confirmed position?").

*Scenario 6: Shown only while unconfirmed*
```
Given Precision is "Confirmed on site" (sent or not yet sent)
When I open the Location on the tablet
Then Set location from GPS is not shown
And when head office reverts the capture, the action is shown again
```

**Non-functional notes:** the action is a quiet control on the Location screen, highlighted once for unconfirmed Locations; no prompt on every visit.

---

### US-006: Maintain Location Profiles and their defaults

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want to define Location Profiles with a visit frequency, visit duration and low-stock thresholds per product so that hundreds of shops are serviced consistently from a handful of settings |
| **Priority** | Must Have |
| **Status** | Ready |
| **Dependencies** | Products (Product Management) |

**Acceptance criteria:**

*Scenario 1: Create a Profile*
```
When I create "Large pharmacy" with Visit Frequency 4 weeks and Visit Duration 45 min
Then it is offered on Locations and Visit Planning uses it as the default
```

*Scenario 2: Thresholds per product*
```
When I set threshold 6 for "Cold & Flu Relief 16s" and 3 for "Vitamin D 1000IU 90s" on "Large pharmacy"
Then reps at Large-pharmacy Locations see the Low Stock Hint at or below those counts; products with no threshold show no hint
```

*Scenario 3: Change a default*
```
Given 40 Locations use "Large pharmacy"
When I change Visit Frequency to 3 weeks
Then I see "40 locations without an override will change to every 3 weeks" and on confirm their future Cycle Periods follow 3 weeks
```

*Scenario 4: Archive a Profile in use*
```
Given "Large pharmacy" is used by 40 Locations
Then Delete is not shown; Archive shows "Used by 40 locations — they keep their current defaults until reassigned"
```

**Open questions:** per-Location threshold overrides are still assumed out of scope; confirm.

*Amended 23 Sep 2026:* because a Location may hold several profiles, a changed default affects only Locations where this profile supplies the winning value; the impact message in scenario 3 counts those.

---

### US-007: Maintain geography and move a Town

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want to maintain Regions, Counties and Towns, and move a Town to the right County with the effect on territories shown so that a geography fix doesn't silently reassign shops |
| **Priority** | Should Have |
| **Status** | Ready |
| **Dependencies** | Coverage Management for territory impact |

**Acceptance criteria:**

*Scenario 1: Create hierarchy*
```
When I create Region "Leinster", County "Wicklow" under it, Town "Rathdrum" under Wicklow
Then Rathdrum is offered on Locations
```

*Scenario 2: Town must have a County*
```
When I create a Town with no County
Then it is rejected with "Choose a county"
```

*Scenario 3: Move a Town*
```
Given Rathdrum has 23 Locations and Wicklow is assigned to Colm
When I move Rathdrum to County Wexford (assigned to Brian)
Then I see "23 locations move to Wexford; Primary Rep changes from Colm to Brian for 23 locations" and Handover Pending is raised for their open visits on confirm
```

*Scenario 4: Archive a Town in use*
```
Given Laragh has 4 Locations
Then Delete is not shown; Archive is, with the count, and archived Towns are not offered on new Locations
```

---

### US-008: Work the gap lists

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want one place listing Locations with no Profile, no rep, an inactive Main Contact or an unconfirmed position so that new or changed shops are made whole together |
| **Priority** | Should Have |
| **Status** | Ready |
| **Dependencies** | US-002, US-004, US-005; Unassigned (Coverage Management) |

**Acceptance criteria:**

*Scenario 1: Combined view*
```
Given 5 Locations have no Profile, 3 are Unassigned (2 of them also without Profile), 1 has Replacement Needed, 60 are Town-precision
When I open Gaps
Then I see each Location once with its gaps as tags, filterable by gap type
```

*Scenario 2: Fix inline*
```
When I set a Profile from the list
Then the No Profile tag clears without leaving the list
```

*Scenario 3: Unassigned links out*
```
When I choose Assign on an Unassigned row
Then I am taken to Coverage Management for that Location
```

*Scenario 4: Position gaps are informational*
```
Then Town-precision Locations are listed under "Unconfirmed position" with no action, since reps confirm them on site
```

---

### US-009: Maintain Location Type and Contact Type lists

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want to maintain the Type lists with descriptions, archiving rather than deleting anything in use so that classification stays consistent and history intact |
| **Priority** | Could Have |
| **Status** | Ready |
| **Dependencies** | None |

**Acceptance criteria:**

*Scenario 1: Add a Type*
```
When I add Location Type "Head office — no stock held"
Then it is offered on Locations
```

*Scenario 2: Archive in use*
```
Given Contact Type "Buyer" is used by 14 Contacts
Then Delete is not shown; Archive keeps it on the 14 labelled Archived
```

*Scenario 3: Delete unused*
```
Given a Type with no references
Then Delete is offered with a plain confirmation
```

---

### US-010: Record a temporary closure

| Field | Value |
|---|---|
| **Story** | As a Head Office User or Field Salesperson, I want to record that a shop is shut until a given date so that nobody drives out there and the visit is scheduled for after it reopens |
| **Priority** | Should Have |
| **Status** | Ready |
| **Dependencies** | US-001; Visit Planning scheduling; area 1 Location screen and Sync |

**Acceptance criteria:**

*Scenario 1: Record from head office*
```
Given Byrne's Chemist is refurbishing
When I record Temporarily Closed from 28 September to 14 October 2026
Then the Location stays active, its Visit Dues keep generating
And its days from 28 Sep to 14 Oct cannot be scheduled in the rep's planner
```

*Scenario 2: Rep schedules after reopening*
```
Given a Visit Due with window 28 September – 25 October 2026
When the rep opens the planner
Then Byrne's shows "Closed until 14 Oct" and the earliest schedulable day is 15 October 2026
```

*Scenario 3: Window entirely inside the closure*
```
Given a Visit Due whose window ends 10 October 2026
Then it cannot be scheduled and appears for the manager as needing Extend, like an absence-affected visit
```

*Scenario 4: Rep extends from the tablet*
```
Given the rep arrives on 15 October and the shop is still shut until the end of the month
When they change the reopen date to 31 October 2026
Then it is saved as unsent and uploads at next Sync; the planner and Home update after it syncs
```

*Scenario 5: Open-ended*
```
When I record a temporary closure with no reopen date
Then scheduling is blocked until a date is set or the closure is removed, and the Location shows "Closed — reopen date unknown"
```

*Scenario 6: Reopen early*
```
When the shop reopens on 8 October and the date is changed to 8 October 2026
Then days from 8 October are schedulable again
```

*Scenario 7: Orders unaffected*
```
Then orders for Byrne's can still be created, sent and accepted throughout the closure
```

**Edge cases addressed:** a closure that repeatedly slips is handled by extending; no separate escalation.

---

### US-011: Close or reopen a Location

| Field | Value |
|---|---|
| **Story** | As a Head Office User, I want to mark a Location as permanently Closed so that it stops being scheduled and ordered for, while its history, existing orders and searchability remain |
| **Priority** | Must Have |
| **Status** | Ready |
| **Dependencies** | US-001, US-010; Visit Planning and Ordering honour the state |

**Acceptance criteria:**

*Scenario 1: Close*
```
Given Doyle's Shop has 1 open Visit Due, 1 Pending Order and 1 In Progress Order on the rep's tablet
When I mark it Closed from 30 September 2026
Then the Visit Due is Cancelled (not Missed), no new Visit Dues are generated, the Pending Order is unchanged
And the In Progress Order still sends at next Sync; no new Order can be started for Doyle's
```

*Scenario 2: Still findable*
```
When a rep searches "Doyle" on the tablet after syncing
Then Doyle's Shop appears labelled "Closed 30 Sep 2026" with Record Call and New Order not offered
And it no longer appears on Home or any Suggested List
```

*Scenario 3: Closed master*
```
Given Hickey's Head Office has 12 branches reporting to it
When I mark it Closed
Then I see "12 branches report to this location — reassign their master?" and can proceed; the 12 appear on the gap list as "Master location closed"
```

*Scenario 4: Reopen*
```
When I reopen Doyle's Shop
Then it returns to scheduling from the next Cycle Period, appears on Home again, and its Closed Date is kept in history
```

*Scenario 5: Coverage and performance*
```
Then Doyle's stays with its Primary Rep in Assignment History and is excluded from the rep's active Location count
```

---

## 6. Requires Clarification

1. **Location closure (resolved):** Closed state defined in US-010.
2. **Per-Location threshold overrides:** confirm they remain out of scope.
3. **GPS distance threshold:** 2 km assumed.
4. **Eircode lookup:** confirm the service and its coverage of rural addresses.
5. **Moving a Location between Customers:** rare; confirm history handling.
6. **Area 1 amendments (applied):** GPS capture on the Location screen and in Sync; Main Contact status shown; coordinates and Precision in the snapshot; Closed Locations searchable but not actionable; "Closed until" on Home and the Location, with the rep able to extend or shorten it.
9. **Self-service:** a Contact with a login is created by a rep or manager and approved by a manager; there is no self-signup. Marking a Contact Inactive suspends their login.
8. **Visit Planning amendments:** Cancelled as a Visit Due outcome when a Location closes permanently; Location-side blocked days and the Extend path for temporary closures.
7. **Master & Branch Ordering area:** range agreements (constraint vs default still open), multi-branch entry on tablet (product-at-a-time) and laptop (grid), Orders carrying "ordered by" and "for", master-vs-branch call types.

---

## 7. Recommended Next Steps

1. Apply the area 1 and Visit Planning amendments (items 6 and 8) with the next batch.
2. Take Master & Branch Ordering next; its open question on range agreements is the largest remaining design decision touching the tablet.
3. Then Pricing & Promotions, which shares the Order Pad with master ordering.
