# Standing decisions

Human decisions that apply beyond the item that raised them. Each entry names who decided, when,
and where it came up. Don't change an entry without a new human decision; add a dated amendment.

## Running items back to back with assumed approval (2026-11-06, developer, after WI-061)

The developer: "yes [open the PR, merge after CI, update local main], and then do the next 3 items, assume approval instead of
asking". For WI-062, WI-063 and WI-064: each plan, its decisions (the recommended option wherever a question would have been asked)
and its scenarios are assumed approved; Apply and checkpoint triggers are taken as given; each item finishes with push, PR, merge
after CI (merge commit, branch kept) and local main updated before the next starts. Assumed decisions are marked "(assumed)" in
each item's notes. Work still stops for CI failures, failing suites, or anything destructive outside an item's own footprint.

## WI-064 customer prices on the tablet (2026-11-06, assumed approval)

Full text in `WI-064.md`.

- **Snapshot 1.6** carries the tiers held by the customers of the rep's assigned Locations, their terms for included products,
  and each included product's quantity breaks. (A contract change normally reviewed by its owner; assumed approved.)
- **Every price on the tablet comes from the engine:** the Order Pad (at the smallest orderable quantity), the quantity popover (at
  the quantity typed) and a new line's captured price and source.
- **A list-price winner is captured as before** ("Base price"); tier and break winners carry their own source.
- **Later quantity changes keep the captured price** (WI-059's rule, kept); WI-066's break prompts will need this revisited.

## WI-063 tiers on a customer (2026-11-06, assumed approval)

Full text in `WI-063.md`.

- **The customer–tier link** is stored in the catalogue database beside the tiers; the customer is checked against the Directory.
- **Every Location prices against its customer's tiers**, through one service the sample (and later the snapshot) uses.
- **The sample** is the customer's 5 most-ordered products from its last 20 accepted orders, plus a search, priced by the engine
  with the ticked tiers before saving.
- **Removing a tier** changes prices from then on only; captured orders keep their prices.

## WI-062 quantity breaks on a product (2026-11-06, assumed approval)

Full text in `WI-062.md`.

- **Breaks belong to the product** and are saved as a set; not dated.
- **A threshold** is an orderable quantity of the product (whole for Each, a multiple of the step for measures) above the
  smallest orderable quantity; no two at the same quantity; prices more than €0.00, to the cent.
- **Never-applies warnings** save anyway: a break above a cheaper break at a smaller quantity, or above today's base price.
- **Unit changes** that would leave a break off the new rules are refused.

## WI-061 price-list tiers and tier warnings (2026-11-06, developer, plan approval)

The developer answered the WI-061 questions, replied **Approved** to the plan with D1–D9, then **Apply**. Full text in `WI-061.md`.

- **Shape changes** go through a confirm step that says what stops applying; nothing changes until Confirm.
- **Percentage → price list:** fixed overrides become listed prices; percentage overrides are removed, named in the confirm step.
- **Price list → percentage:** a new default percentage is entered; listed prices stay as fixed overrides.
- **Above list price:** a fixed price saves straight away; its row says "This is above list price and will never apply" while it is
  above today's list price, worked out when read and never stored.
- **Finish:** as WI-060 if the developer says so at the time.

## WI-060 percentage price tiers (2026-11-06, developer, plan approval)

The developer answered the WI-060 questions, replied **Approved** to the plan with D1–D11, then `Continue T-6.1.1` (taken as
**Apply**, since the checkpoint hadn't been reached). Full text in `WI-060.md`.

- **Storage holds terms, never prices:** a tier's name and default percentage; an override's product and either a percentage or a
  fixed price, one per product per tier. Prices are worked out through the engine when needed.
- **Limits:** percentages more than 0 and less than 100, to 2 decimal places; fixed prices more than €0.00, to the cent; tier names
  unique ignoring case.
- **Editing:** rename, change the percentage, add, change and remove overrides; tiers aren't deleted.
- **Override rows** show the term, the list price and today's price; products are picked by search.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-059 future-dated prices on the tablet (2026-11-06, developer, plan approval)

The developer answered the WI-059 questions, replied **Approved** to the plan with D1–D8, then proceeded (Apply). Full text in
`WI-059.md`.

- **Business day (MI-53):** "today" for prices is the date in Europe/Dublin, on the tablet and the server alike, from one shared
  `BusinessCalendar` in `FieldSales.Pricing`. Call and order dates are not changed by this.
- **Clock:** the tablet trusts its clock but never prices on a day before its snapshot's build date.
- **Midnight:** open pages refresh their prices when the price day changes, keeping drafts.
- **Captured lines** keep the price they were added at; a quantity change never re-prices; the server never re-prices.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-058 the price engine (2026-11-05, developer, plan approval)

The developer answered the WI-058 questions, replied **Approved** to the plan with D1–D10, then **Apply**. Full text in `WI-058.md`.
These rules bind every later pricing item (tiers, breaks, display, promotions, allowances, self-service).

- **Rounding (MI-48):** a unit price (per item or per kg) is rounded to the cent, halves away from zero, as soon as a percentage is
  applied; a line's value is the rounded unit price × quantity, rounded the same way.
- **Overrides (MI-47):** per product only. A percentage tier has a default percentage and product overrides (a percentage or a fixed
  price); a price-list tier has fixed prices, and a product it doesn't list gets no tier price. No category overrides.
- **Ties:** on an equal price the rep sees the tier, then the break, then the list price; equal tiers by name.
- **Breaks:** every break at or below the quantity is reached and the lowest of them applies, so a rising break never does.
- **One engine** in `FieldSales.Pricing`, shared by tablet and server: `Resolve(product, tiers, quantity, date, extraCandidates)`.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-057 competitor notes on a call (2026-11-05, developer, plan approval)

The developer answered the WI-057 questions, replied **Approved** to the plan with D1–D10, then **Apply**. Full text in `WI-057.md`.

- **Notes:** several free-text notes per Call, each with one link type (None, Products, one Range, one Category); links keep their
  captured name. A note counts as recorded; the save review ends "3 competitor notes".
- **Picker:** this Call's products with a count or a Low mark come first, then search of everything downloaded.
- **Range** is offered with nothing to pick ("No ranges on this tablet yet") until E9 brings Ranges.
- **Edit Call** changes a note's text and removes notes; links stay; nothing is added. This completes US-015 S2.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-056 follow-up calls (2026-11-05, developer, plan approval)

The developer answered the WI-056 questions, replied **Approved** to the plan with D1–D8, then **Apply**. Full text in `WI-056.md`.

- **The link:** a follow-up Call carries `followUpOf` (the Call it follows up); the server stores it. Version 1 of the Call contract.
- **Current stock level:** one shared function, the latest count per product by the time the Call was saved; counted entries only.
  No screen shows it yet.
- **Suggested List:** a follow-up's products join its original's Stock Check; a follow-up never becomes the last check itself.
- **Follow-up mode** starts with an empty stock section and is opened from Edit Call and the call page.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-055 correcting a saved call before it syncs (2026-11-05, developer, plan approval)

The developer answered the WI-055 questions, replied **Approved** to the plan with D1–D8, then **Apply**. Full text in `WI-055.md`.

- **Call page:** a read-only page for a saved call, opened from Sync's Unsent review. Unsent: Edit call and Record a follow-up call.
  Sent: Record a follow-up call only. Sent calls get a way in with WI-075.
- **Edit Call changes, never adds:** channel, a counted line's count (not cleared), Low marks, notes only if the call had notes, and
  removing lines. No new products, no count on a Not checked line, no new notes, no Add to order, no Delete.
- **Record a follow-up call** sits in place of the add actions and opens Record call at the shop until WI-056 adds the link.
- **Upload claims calls** as it does orders, so a call can't change while it is being sent.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-054 the out-of-pattern quantity marker (2026-11-05, developer, plan approval)

The developer chose the marker rule, replied **Approved** to the WI-054 plan with D1–D7, then **Apply**. Full text in `WI-054.md`.

- **Rule (MI-49):** the product's quantity on whichever of the shop's last 3 accepted orders contained it; at least 2 such orders,
  else no marker. Usual = the median (two: their average). Marked when the line is more than 3× usual. No absolute floor. High only.
- **Text:** "Usually about 48 here", rounded to a whole number for Each and to the step for measured products, with the unit.
  Quantities in another unit are ignored.
- **Snapshot 1.5** adds optional `quantities` to each `recentOrderLines` row; the tablet stores them in local format 5.
- **Where:** order-entry lines while In Progress or Ready to Send. Never blocks Mark Ready to Send; nothing to acknowledge.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-053 the last call's gaps on an order with no call (2026-11-04, developer, plan approval)

The developer replied **Approved** to the WI-053 plan with the recommended D1–D7, then **Apply**. Full text in `WI-053.md`.

- **Snapshot 1.4** adds one family, `LastCallLowLines`: each Location's most recent Call (any channel, by anyone), its Low products,
  minus products on any received order there taken on or after the Call's date. "Ordered since" is worked out on the server.
- **"Ordered since" (owner's ruling at the checkpoint, 2026-11-05):** "any order since the date" — every order at the Location dated
  on or after the Call's date, whatever its status or route. No status filter on the server or the tablet.
- **The tablet** also drops gaps on its own orders at the Location since the Call (except the order on screen, where they read ADDED),
  and a newer unsynced Call at the Location replaces the snapshot's.
- **Recording a Call from the Low tab** links the order to it on save and returns to order entry for that Call.
- **Out of scope:** CAN'T ADD (E8), REPLACED (E10), customer online / chain orders until they exist.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-052 the Low tab in order entry (2026-11-04, developer, plan approval)

The developer replied **Approved** to the WI-052 plan with the recommended D1–D9, then **Apply**. Full text in `WI-052.md`.

- **Tabs:** `Order pad` (today's screen, search included) and `Low (n)` only; "Search all" waits for Ranges.
- **From a call:** Record call's **Open order** opens the Call's order (or a new one) with **Back to call**; a new order opened from a
  Call is linked to it on its first line.
- **One source for each Low item's state:** ADDED whenever the product is on the order; REMOVED only when not on the order and the
  Call's draft recorded the line was taken off; not added otherwise. "Taken off" lives on the draft only, so after the Call is saved a
  removed item reads not added.
- **Count** is not added only; unticking Low drops the item from the tab and never touches the order.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-051 Low to order through a quantity popover (2026-11-04, developer, plan approval)

The developer replied **Approved** to the WI-051 plan with the recommended D1–D10, then **Apply**. Full text in `WI-051.md`.

- **Call → Order link — amended 2026-11-04 (developer, after Apply):** the link is the Order's existing reserved `callId`, not a new
  `orderId` on the Call. A new order from a Call gets `callId`; an In Progress order with none gets it on the first add from a Call; an
  order already linked to another Call keeps that link. No contract, server or schema change. (Originally approved: the Call carries
  `orderId` with a server column; dropped once the existing `callId` came to light.)
- **A Call with only order lines isn't saved** (developer, 2026-11-04): the S5 rule stands; the lines stay on the order.
- **Find-or-create:** the call's linked order if still In Progress, else the rep's latest In Progress order at the shop, else a new
  one; chosen and added in one step under the per-rep order lock. The link moves if the linked order left In Progress.
- **Low tick In person on every row;** unticking keeps the count. Phone's Add to order works on every row without marking Low.
- **"On order (N)"** replaces Add to order when the product is on the call's order; unticking Low never removes a line.
- **Quantity popover** (`Components/Shared/QuantityPopover.razor`) is shared with the future Low tab; quantity never pre-filled;
  "list price" on screen, "Base price" stays in the contract. Lines added stay on the order if the call is left unsaved.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-050 Stock mentioned on a phone call (2026-11-04, developer, plan approval)

The developer replied **Approved** to the WI-050 plan with the recommended D1–D9, then **Apply**. Full text in `WI-050.md`.

- **Low on a stock entry:** `CallStockEntry` gains optional `low` (true or absent); Call stays version 1. Out is Low with a count of 0,
  not a field of its own.
- **Phone saves only what was mentioned:** rows marked Low/Out or given a count. Untouched rows are never saved as Not checked.
  A Low mark counts as recording something on either channel.
- **Review:** "Phone · 2 marked Low (1 out)" ("(… out)" omitted at 0), plus "· 1 counted" for counted rows without Low. In person keeps
  its stock summary and adds "· 2 Low".
- **Channel switch keeps everything;** Low marks are saved In person too (read-only note there until WI-051).
- **Server:** accepts `low`; refuses a Phone entry with neither a count nor Low as incomplete. Phone is never the last stock check.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-049 the suggested stock-check list (2026-11-04, developer, plan approval)

The developer replied **Approved** to the WI-049 plan with the recommended D1–D11, then **Apply**. Full text in `WI-049.md`.

- **Accepted orders:** `OrderLifecycleStatus.Accepted` exists with no transition until WI-067; "Ordered recently" reads Accepted orders
  only, so production lists show only "Last visit" products until then.
- **MI-12 stopgap:** the order count is API configuration `TabletSnapshot:SuggestedListOrderCount` (default 3, 1–10). No settings screen.
- **Snapshot 1.3** adds two optional (locationId, productId) families: last stock check lines and products on the last N Accepted orders,
  filtered to the rep's Locations and visible products. The tablet merges them in one function (`SuggestedList`).
- **Received Calls** get server tables (`ReceivedCalls`, `ReceivedCallStockEntries`). Last stock check = the Location's latest In-person
  Call with stock entries by saved time. A Phone call saves no stock entries until T-5.3.2.
- **Stock entries** keep product id, code and name as captured, the unit, and a count (absent = Not checked). A Call whose stock lines are
  all Not checked records nothing. Counts: "Enter 0 or more"; Each needs "Enter a whole number"; measures up to 6 decimals.
- **Finish:** push, PR, merge after CI (merge commit, branch kept), update local main.

## WI-047 the Call body and Record Call (2026-11-03, developer, plan approval)

The developer replied **Approved** to the WI-047 plan with the recommended D1–D10, then **Apply** with the remaining stops lifted, and
answered D11: **push, open a PR, merge once CI passes, then update local main**. Full text in `WI-047.md`.

- **Call body v1** (`FieldSales.Calls.Contracts`): `schema "field-sales-call"`, `version`, `id`, `locationId` (top level, read by shop
  retention), `capturingRep` (sign-in subject; names resolve from identity), `calledOn`, `savedAt`, `channel` InPerson|Phone,
  `snapshot {version, builtUtc}`, `pitchNotes` (absent when none; trimmed; at most 4,000 characters).
- **Extension points are reserved optional sections within version 1**, absent meaning empty: `stockEntries[]` (T-5.4.1, T-5.3.2, with
  a per-entry `low` for T-5.5.1), `competitorNotes[]` (T-5.8.1), `followUpOf` (T-5.7.1), `campaignOutcomes[]` (E20), `rangeReview{}`
  (E23). Call Purposes are derived from the sections present. Visit Due completion (E13) derives from location, rep and date.
- **A saved Call is immutable:** `CallStore` has Save only. Corrections (WI-055) add their own operation behind the WI-046 upload claim.
- **Checks shared by tablet and server:** no channel → "Choose In person or Phone"; nothing recorded (either channel) → "Add pitch notes,
  a stock check or a competitor note". The server refuses a malformed Call as `unsupported` / `incomplete` / `not-yours`; it never checks
  the directory, so a Call at a shop reassigned since the snapshot is accepted. Calls stay in `ReceivedWork` only (no server table yet).
- **Review dialog:** "Save this call?", the one-line summary joined with " · ", **Back** and **Save** (story wording over the T-06 lines).
- **Unsaved drafts** live in app memory per rep, session and shop, so a resume rebuild restores them; leaving with content asks first.

## WI-046 editing unsent orders and the upload claim (2026-11-03, developer, plan approval)

The developer replied **Approved** to the WI-046 plan with the recommended D1–D6, then **Apply**. Full table and scenarios in `WI-046.md`.

- **No change after an upload starts.** Before sending an item, the uploader claims it (`upload_attempts`) under the same per-rep gate
  order changes use, and only if the saved body is still the queued body. Reopen, edit and delete refuse a claimed order. Confirmation
  (Sent) and a technical refusal (Needs Attention) clear the claim; so do 401/403 and a request that provably never reached the server.
  A lost connection mid-request, a timeout, a 5xx or an unreadable receipt keep it, so the order stays locked until a later Sync gets an
  answer. This settles WI-039 D1's open question for orders: an id the server may hold is never re-sent with a changed body or deleted.
- **D1:** additive local migration allowed: Unsent DB format 4 adds `upload_attempts`; upgrades keep all work and Sent items.
- **D2:** Needs Attention orders cannot be reopened or edited; WI-042's Open, re-mark and Delete remain.
- **D3:** deleting an order removes only the order; a saved Call stays and still uploads (WI-047 decides any Call-side display).
- **D4 (inferred):** at a shop no longer assigned, reopen/change/remove/delete are allowed; adding a product still needs assignment.
- **D5 (inferred):** a line whose product is missing from today's snapshot can be removed but its quantity can't change.
- **D6 (inferred):** a quantity change keeps the captured price (breaks are E6).
- The claim covers **orders only** (Calls can't be changed by the rep yet), and lives in its own table, not on `UnsentItem`, so captured
  work compares equal whether or not it is claimed. **WI-055 (Call corrections) should extend the claim to Calls** before letting a saved
  Call change. Consequence: a Sync whose upload of a Ready to Send order fails without a definite answer now writes the claim to the
  Unsent database (the byte-for-byte "failed Sync leaves the file unchanged" tests use Calls and In Progress orders, which aren't claimed).

## WI-043 restricted-product retention (2026-10-31, developer)

The developer explicitly selected **A — restrict active reads; keep recovery snapshots**.

- New snapshot payloads and all active tablet catalogue/search/direct product reads exclude products
  the rep lacks permission to see. Archived groups remain hidden from every rep (MI-45).
- Preserve the existing two-completed-generation recovery policy. A previously authorized product
  can remain in an older local recovery generation; WI-043 does not purge those copies on removal.
- Captured order lines retain their stored name, quantity, price and snapshot reference and remain
  valid when captured. They are not rewritten by current catalogue visibility or recovery cleanup.
- The approved T1 test asserts active-read exclusion, not physical erasure of historical snapshots.
  No additional retention/schema change or storage scope amendment is needed for WI-043.

## WI-040 sync failure causes (2026-10-30, developer, plan approval)

The developer replied **Approved** to the WI-040 plan (decisions D1–D8, full text in `WI-040.md`).

- **Causes, decided on the tablet only (no API or contract change):** *sign-in expired* (no token, a refused refresh, or a 401)
  stops everything, so nothing uploads and the day doesn't download; *no connection* means nothing in that Sync got an answer;
  *poor connection* means the connection failed during uploads after something got through (an item confirmed, or the day
  downloaded afterwards); *server error* (5xx, unexpected answer, receipt not naming the item) stops the uploads and the download
  is still tried. Items refused as technical faults keep WI-039's sentence until **WI-042** gives them Needs Attention.
- **Wording:** "2 items not sent — poor connection. They'll send next Sync." (US-001 S2 wins over the T-01 wireframe's "Try again
  when you have signal."); "Sign in again to send 3 items" counts the finished work still waiting (saved Calls + Ready to Send
  Orders), "Sign in again to sync." when nothing waits; server error "2 items not sent — Field Sales didn't respond. They'll send
  next Sync."; when uploads went but the day didn't download, "3 items sent." plus why the day didn't download. No message may
  say saved work is unchanged once an item was sent in that Sync.
- **Sign in on T-01:** a Sign in button replaces Sync now in the expired state and starts the entry screen's connected sign-in.
  **WI-041** owns the automatic resume, I-03's "After sign-in, this Sync resumes automatically." and Sign in without signal.
- **Resume point for WI-041:** confirmed items move to Sent immediately and every Sync reads the queue fresh, so a repeated or
  resumed Sync never re-sends a confirmed item; a lost confirmation is re-sent and answered `AlreadyReceived` (WI-039).
- **Amendment 2026-10-31 (device check, developer chose "keep it in the app"):** `MainLayout` rebuilds the routed page on every
  resume, and returning from the sign-in browser is a resume. The Sync screen's notice (report, Sign in state, sign-in answer)
  therefore lives in `TabletApp.SyncNotices` (Tablet.Core `SyncNotices`), keyed by rep and session version, and the page restores
  it. **WI-041** should build its interrupted-Sync state on the same store rather than on page fields.

## WI-039 upload protocol and MI-52 (2026-10-29/30, developer, plan approval)

The developer replied **Approved** to the WI-039 plan (decisions D1–D11, full text in `WI-039.md`), then on 2026-10-30
accepted the A5/A6 stand-ins, asked for the directory and ordering migration bundles to be added, and asked for a PR at the end.

- **Identity:** the id the tablet gives an item at capture is its identity across retries. The server stores a SHA-256 of
  the body, but it is not part of identity. One `PUT` per item (`TabletUploadContract` in `FieldSales.Snapshot.Contracts`;
  the snapshot document and version are unchanged). 201 `Received` / 200 `AlreadyReceived`; the tablet marks Sent on either,
  immediately after each confirmation. A lost confirmation is recovered by re-sending. That is the whole protocol.
- **Same id, different body:** the first version is kept, the re-send is counted on the ledger row, and it is confirmed.
  Today's tablet can't change an item once finished. **WI-046 (edit/reopen) and Call corrections must decide** what happens
  to an item changed after an upload attempt whose confirmation was lost.
- **MI-52 is decided — the technical faults** (`UploadFaults` codes in the problem response's `fault` extension):
  `corrupt`, `unsupported`, `incomplete`, `not-yours` (400), `id-in-use` (409: another rep's item or another kind already
  has the id), `too-large` (413, over 1 MiB). Nothing else is ever refused: the receiver never reads the catalogue or
  directory (BR-NEW-006). A duplicate is not a fault. **WI-042** turns these codes into rep sentences (T1.2).
- **Server store:** `OrderingDb` (API `Ordering` folder): `ReceivedWork` ledger for every kind, body verbatim, and `Orders`
  with status **Pending** in the same transaction. Order lines stay in the body; **WI-067** decides line tables. Calls live in
  the ledger only until Sales Operations has a store. **WI-047** defines the Call body, its structure checks and its
  snapshot reference.
- **Upload order:** Calls, then Ready to Send Orders, oldest saved first. No server-side link check between a Call and its Order.
- **Tablet Sent state:** the Unsent database (v2) moves a confirmed item from `work` to a `sent` table in one transaction.
  Sent items no longer count as unsent and no longer keep their shop's details on the tablet.
- **Sync sequence:** token, uploads, space check, download. The download runs whatever the upload outcome; a 401/403 stops
  everything; a dropped connection or 5xx stops the remaining uploads only. **WI-040** owns the cause wording.

## WI-021 reporting-line source (2026-10-03, developer, planning)

- The developer selected **Store reporting lines in Field Sales business data,
  managed by Head Office** in response to the WI-021 source question.
- This settles the source of rep → Sales Manager relationships. Identity
  administration remains responsible for accounts and roles.
- The developer then replied **Approved** on 2026-10-03, accepting the concrete
  reporting-line integration, ownership/history policies and Human-Led scenario
  matrix in `WI-021.md`. That approval authorizes increment 1 only.
- Compute effective owners on read through a single pure resolver. Assigned new
  Locations receive initial Unassigned → inherited-owner history atomically;
  unassigned creation writes no ownership entry. Source-only changes with an
  unchanged rep write no ownership-change entry. Preserve historical labels and
  reserve the `geography change` cause for ownership changes through geography.

## WI-021 trusted history labels — sequencing amendment (2026-10-03, developer)

- The developer replied **Approved** to
  `docs/territory-history-plan-amendment.md` after checkpoint `65695b6`.
- Bring the trusted read-only staff directory contract, Identity lookup and API
  client forward from increment 5 into increment 4, so history captures real
  actor/rep display labels. Use existing account data and current bearer/role
  authorization; keep identity network reads outside the business transaction.
- Missing or invalid required identity data fails the write. History reads retain
  captured labels through account/geography renames and need no identity lookup.
  Assignment/reporting management and final acceptance remain increment 5;
  the mandatory increment review stops still apply.

## WI-019 Town-first coordinates (2026-10-02, developer)

- Town coordinates are the first default. Keep Eircode lookup optional and
  disabled initially; all lookups and credentials remain server-side.
- Allow saving with Position needed when no coordinates are available. Head
  Office supplies Town coordinates through CSV or manual entry.
- Changed Town/Eircode recalculates only unconfirmed positions and retains
  history. Automatic defaulting always preserves Confirmed on site positions.
- These decisions supersede the source's Eircode-first and guaranteed-position
  assumptions. The complete WI-019 plan and 12 Scenario Review cases are approved;
  Human Tight-Loop increment reviews still apply. See WI-019.md and
  docs/location-coordinate-delivery.md.

## Placeholders resolved (2026-09-28, developer, during WI-001)

| Placeholder | Value |
|---|---|
| `{{APPLICATION_REPOSITORY}}` | `https://github.com/ColmKenna/Field-Sales`, checked out at `D:\repos\Field-Sales`. The planning docs live in this same repo under `plan_docs/`. |
| `{{TEMPLATE_REVISION}}` | `6395488d1beeeaa57b0825a214a1859c0c045c93` (upstream `main`, 2026-09-07). Evidence is in `docs/template-adoption.md`. |
| `{{INTEGRATION_BRANCH}}` | `main` ("for now"; the developer may revisit this) |
| `{{TEMPLATE_PREFIX}}` | `FieldSales.Identity` (see Naming) |

Still open and owned by later items: `{{TABLET_CLIENT_STACK}}` (MI-01) and the other
`missingInformation` entries in `plan-data.js`. They don't block WI-001.

**Amendment (2026-10-23, developer, WI-032 1C):** `{{TABLET_CLIENT_STACK}}` = **.NET MAUI Blazor
Hybrid on .NET 10**. See § WI-032 tablet runtime and storage (1C).

## Naming

The top level is `FieldSales`, and every project is `FieldSales.<Name>`:

| Before | After |
|---|---|
| `Sales.slnx` | `FieldSales.slnx` |
| `AppHost/AppHost.csproj` | `FieldSales.AppHost/FieldSales.AppHost.csproj` |
| `ServiceDefaults/ServiceDefaults.csproj` | `FieldSales.ServiceDefaults/FieldSales.ServiceDefaults.csproj` |
| `IdentityServerProject/src/IdentityServerProject` | `FieldSales.Identity/src/FieldSales.Identity` |
| `IdentityServerProject.Admin.Services` | `FieldSales.Identity.Admin.Services` |
| `IdentityServerProject.Admin.Tests` | `FieldSales.Identity.Admin.Tests` |

The template rename script handles the `IdentityServerProject*` → `FieldSales.Identity*` part and
names the solution `FieldSales.Identity.slnx`. Renaming the solution to `FieldSales.slnx` and the
AppHost/ServiceDefaults projects is done by hand. Future application projects follow the same
pattern: `FieldSales.Web` (staff web/BFF), `FieldSales.Api`, and so on, with names agreed per card.

## Local ports

The developer wants every project on its own port, so Field Sales can run alongside other
template-based solutions. Block 7200–7299 is reserved for Field Sales:

| Resource | Port |
|---|---|
| Identity host HTTPS / HTTP | 7201 / 7202 |
| Staff web/BFF (reserved) | 7203 |
| Field Sales API (reserved) | 7204 |
| Customer web (reserved) | 7205 |
| Aspire dashboard HTTPS / HTTP | 7210 / 7211 |
| Aspire OTLP HTTPS / HTTP | 7212 / 7213 |
| Aspire resource service HTTPS / HTTP | 7214 / 7215 |

The reserved ports are proposals until the item that adds the project confirms them.

## Delivery console

- Work-item status is stored in `plan_docs/field-sales-delivery/plan-data.js` as a top-level
  `workItemStatus` map, and the console reads it from there (developer's decision, 2026-09-28).
- A status chip clicked in the browser only creates an unsaved draft in localStorage. The draft
  records the file value it was made against, and is dropped once the file changes that item.
  The developer picked "file wins, local drafts".
- Agents update `workItemStatus` directly: set the item to `active` when implementation starts and
  to `done` when it completes. They change nothing else in `plan-data.js`.
- The logic is in `status-store.js`, with tests in `tests/status-store.test.js`.

## Repository

- `plan_docs/` is committed to the application repository (developer's decision, 2026-09-28).
- The template's one-shot rename tooling (`scripts/rename-template.ps1`, `scripts/tests/test-rename.ps1`,
  and the `rename-smoke-test` CI job) is removed once the rename is applied, because it only works
  on an un-renamed tree (developer confirmed, 2026-09-28).

## Identity client boundary (2026-09-28, developer, WI-001 Increment 3)

- WI-001 records the contract, and **WI-002 implements it**, replacing the sample
  `razorclient`/`blazorclient` development seeds.
- Staff web/BFF client id `fieldsales-staff-web`: confidential, authorization code with PKCE, and a
  secret from an AppHost parameter. URIs on `https://localhost:7203`: `/signin-oidc`,
  `/signout-callback-oidc`, `/signout-oidc` (front-channel).
- API resource `fieldsales-api`, scope `fieldsales.api`, served on `https://localhost:7204`.
- Still open for WI-002: project names for the web/BFF and API, whether `roles` and
  `offline_access` are requested, the AppHost secret parameter name, and the business-role mapping.
- The full contract is in `docs/template-adoption.md` § Client boundary for WI-002.

## WI-002 staff access contract (2026-09-28, developer, Increment 1)

- One staff website hosts the rep, manager and head-office areas. The new .NET 10 / Aspire projects
  are `FieldSales.Web` (Razor Pages BFF, HTTPS 7203) and `FieldSales.Api` (HTTPS 7204). The
  proposed I-01 sign-in and I-02 area-choice layouts are approved for this item.
- Staff authenticate with the adopted identity host's username/password page. I-01 redirects to
  that page; it does not collect a password in the BFF. A staff member needing recovery contacts a
  `SysAdmin`, who uses the existing identity-admin password-reset page. Customer invitations and
  recovery remain separate.
- The business roles are the exact ASP.NET Identity role names `Field Salesperson`, `Sales Manager`
  and `Head Office User`. `SysAdmin` remains a distinct security-administration role and grants no
  staff area by itself. The BFF requests the `roles` identity scope to receive role claims.
- The BFF requests `offline_access` for server-held refresh tokens. The browser holds only the
  protected BFF session cookie. The AppHost secret parameter is `staff-web-client-secret`. The
  WI-001 client id, API resource, scope and redirect/logout URIs remain as recorded in
  `docs/template-adoption.md`.
- The developer approved the Human-Led scenario gate: IAM-US-001 S1–S8 and IAM-US-002 S1–S2,
  including direct-link denial of protected content and actions. Role removal during an open
  website session is WI-003. The named scenario matrix is in `WI-002.md`.

### Increment 2 (2026-09-28, developer)

- Reuse **Admin → Roles** and **Admin → Users** to provision the three business roles and staff
  accounts; no new provisioning UI or automatic staff-account seed is added.
- An inactive staff account is represented by the existing **Suspend** action, which sets an
  indefinite Identity lockout. The existing **Unlock** action reverses it.
- Development seeding registers `fieldsales-staff-web`, `fieldsales.api` and `fieldsales-api`
  instead of the template sample clients and API. The seeder remains additive: existing sample
  registrations in a persistent Development database require SysAdmin review and removal through
  `/Admin`, rather than automatic deletion of potentially edited records.

### Increment 3 (2026-09-28, developer)

- The BFF persists encrypted OIDC tickets and Data Protection keys in dedicated SQL Server
  `StaffWebDb`; the browser receives an opaque, secure, HttpOnly cookie. Production requires a
  certificate for the website key ring and the `staff-web` migration bundle.
- The first API surface is authenticated `GET /staff/session`. It checks the signed bearer token,
  `fieldsales-api` audience, `fieldsales.api` scope and at least one business role. The BFF calls
  it with a server-held access token and refreshes that token using its server-held refresh token.
- I-01 returns rejected staff credentials to a retry state. Protected landings exist for rep,
  manager and head office; the sign-out POST ends the BFF and identity-host sessions. The
  last-used destination, I-02 and area switching remain in Increment 4.

### Increment 4 (2026-09-28, developer)

- Persist each staff member's last-used area in `StaffWebDb`, keyed by their identity `sub` claim.
  Resolve stored keys only through the known area map and select them only while the principal
  still has the mapped business role. Never take a destination or role from browser state.
- Use I-02 for first use when more than one area is permitted and no usable last-used area exists.
  Show only areas held by the current principal. Single-area users go directly to their area.
  Area navigation switches destinations inside the existing staff session. A valid permitted
  direct link has priority over the saved default; protected request endpoints deny an unheld
  area before protected page content or actions are reached.
- IAM-US-002 S3, immediate role removal during an open session, remains WI-003.

## WI-003 current staff-role boundary (2026-09-29, developer)

- The identity host's current ASP.NET Identity role assignments are authoritative for staff area access. Its bearer-protected `/staff/current-roles` endpoint returns only the caller's own business roles, using the existing `fieldsales-api` audience and `fieldsales.api` scope.
- The staff BFF refreshes business-role claims from that source before each protected request; the API checks the same source for authenticated staff requests. A lookup failure denies access without falling back to stale token or cookie roles. A removed last-used area is cleared while remaining permitted areas stay available in the same session.
- A denied request to an area whose role was removed opens I-05. A denied write says the change was not saved; a denied read does not. I-05 offers only current areas, or sign-out and the existing administrator-contact guidance if none remain. An area never held keeps the generic access-denied page.
- WI-003's protected Head Office POST is a Testing-only representative write. WI-004 owns production catalogue forms and persistence. No administrator contact URL was approved, so the existing guidance supplies the help text without a new destination.

## WI-004 category contract (2026-09-29, developer, plan approval)

- Use SQL Server and EF Core with stable GUID category identities and optional parent identities. Category depth is unlimited, with six levels as a display and verification target. Derive breadcrumbs from the current parent chain on read.
- Category names are unique among siblings; duplicate leaf names under different parents are valid. A rejected or expired create/rename form changes nothing and is not persisted or replayed after sign-in.
- The Human-Led scenario matrix is recorded in `WI-004.md`. Because Product, Order and Call records do not yet exist, WI-004 proves changing descendant category paths and stable category identities; direct assertions over those later record types follow their introduction.
- At the Increment 1 review, the developer approved an API-owned `CatalogueDb` separate from website session storage. The BFF calls its business endpoints with server-held tokens. See `docs/catalogue-category-decisions.md`.

## WI-011 archived Restriction Groups (2026-10-01, developer, plan approval)

- **MI-45 is decided.** While a Restriction Group is archived, its products are hidden from every rep,
  whether or not the rep holds its permission. The rule fails closed. Permissions are kept untouched,
  so un-archiving restores each rep's previous visibility exactly. Products with no group are
  unaffected.
- The rule lives in one place: `RestrictedProductVisibility.VisibleToRep(products, groups,
  permittedGroupIds)` in `FieldSales.Api/Catalogue/`. **WI-043** (snapshot builder) must apply it
  rather than re-deriving visibility, and must pass the rep's current permissions from **WI-029**.
- A product cannot be newly assigned to an archived group, but an unchanged existing link is kept.
  **WI-012**'s editor inherits this through `Product.SetRestrictionGroup` /
  `ProductReferenceAssignments.SetRestrictionGroupAsync`.
- **WI-029** must register an `IReferenceUsageSource` with key
  `RestrictionGroupListStore.PermissionSource` (`"restriction-permissions"`, labels
  permission/permissions) for the `restriction-groups` list, and add that key to the list's required
  sources. Until then the S5 archive message ("N permissions will be kept but have no effect while
  archived") cannot appear in production.
- Archive confirmation wording: "N permissions will be kept but have no effect while archived"
  (singular "1 permission will be kept but has no effect while archived") and "N products will be
  hidden from every rep while archived". The un-archive confirmation says "N permissions will take
  effect again".
- An In Progress order line for such a product (valid when captured) is a WI-043 obligation: no
  orders or tablet exist yet.

## WI-031 removal consequence (2026-10-19, developer)

The developer selected **keep the rep count, omit the order-line count, and say existing unsent
lines will still send**, plus **a review step with Confirm removal and Cancel**. They then replied
**Approved** to the implementation plan and 17 Human-Led scenarios. This authorizes the single-rep
increment only; stop for review before cohort removal, and stop again after that increment.

On 2026-10-20 the developer replied **Continue WI-031**, accepting the pushed single-rep checkpoint
`0ded5cc` and authorizing the planned cohort increment. Stop for review after its tests/previews;
the final verification/completion increment follows acceptance of that checkpoint.

The developer's subsequent **Continue WI-031** on 2026-10-20 accepts the pushed cohort checkpoint
`948e68f` and authorizes final full-suite verification, acceptance evidence, status done and final
commit/push. No PR creation or integration was requested. The reviewed single-rep copy uses neutral
“their next Sync” because the directory has no pronoun data.

- MI-44 is settled for WI-031's wording only. The order-line-count portions of CV009-E/F are
  superseded by this decision. No guessed/last-known count and no new tablet/order telemetry.
- Single: "Colm will lose Pharmacy-only medicines at his next Sync. Any lines already on unsent
  orders will still send." Cohort: "3 reps will lose Pharmacy-only medicines at their next Sync.
  Any lines already on their unsent orders will still send."
- Review writes nothing. Confirm uses a protected BFF review binding actor, group, reps, expected
  last-change ids and optional reason; lifetime 30 minutes. Changed permissions require fresh review.
  Existing API authorization, record and writer remain authoritative; confirmation is the website flow.
- Archived-group wording explains that it already grants no access and restoration will not restore
  the removed permission. Inactive holders remain removable. Reason rules and grant behavior remain.
- Cohort removal will reuse the existing rule/writer and be atomic, with Grant/Remove modes on the
  group page. It is a later increment; the single-rep checkpoint does not authorize it.

## WI-030 cohort grants (2026-10-19, developer, plan approval)

The developer replied **approved** to the WI-030 implementation plan and its 15 Scenario Review
cases. Implement through the T-3.4.2 checkpoint; continuation requires **Continue T-3.4.2**.

- Reuse WI-029's rule, writer and record. All grants in one action succeed or fail together.
- Forbidden, invalid, already-granted or stale selections reject the entire action; no partial
  grants or duplicate history entries. Empty, duplicate and malformed selections are validation errors.
- Already-granted, outside-team and self rows cannot be selected. Head Office may grant across
  teams but cannot grant to themselves. Inactive current holders remain visible without grant controls.
- Active group chooser; direct archived group pages are read-only. Shared reason follows WI-029.
- The five named reps in CV009-B are granted by another authorized actor, preserving the no-self-change rule.
- WI-031 owns removal and consequence messaging; WI-043 owns tablet behavior. No schema change here.

### WI-030 review checkpoint accepted (2026-10-19, developer)

The developer replied **Continue T-3.4.2** after the group page, five-rep grant and outside-team
denial evidence were presented and the authorization rule was explained. This accepts the checkpoint
and authorizes the remaining approved scenarios, full verification and completion on the feature
branch. PR creation and integration remain separate actions.

## WI-029 Restriction Permissions (2026-10-19, developer, plan approval)

The developer replied **Approved** to the WI-029 plan, accepting decisions D0–D9 and the Human-Led
scenario matrix as written in `WI-029.md`.

- **Who may change:** the rep's own Sales Manager (current reporting line) or any Head Office User,
  for any rep holding Field Salesperson, including a Sales Manager who also sells. **Any** Sales
  Manager or Head Office User may view any rep's permissions. **Nobody changes their own
  permissions**, Head Office included. WI-030's cohort grants must reuse this rule, not a second check.
- The reason is optional on grant and removal, with the Assignment History rules (trimmed, at most
  1000 characters, no control characters).
- **Archived groups:** no new grant; removal stays allowed and is recorded; the per-rep view lists
  archived groups the rep holds, with Remove only.
- **Storage:** CatalogueDb, code under `FieldSales.Api/Coverage`. `RestrictionPermissionChanges` is
  the append-only record (captured rep, actor and group names; no FK). `RestrictionPermissions`
  holds current grants, with a restrictive FK to `RestrictionGroups`, written in the same
  serializable catalogue transaction. The reporting line is read in a serializable DirectoryDb
  transaction kept open until the catalogue write commits. This satisfies the WI-009/WI-011
  obligation that another area's references join the retirement transaction.
- A stale or repeated change is refused with "This permission changed. Reload before saving." and
  nothing is recorded.
- A group whose permissions were all removed may be deleted when no product uses it; the record
  keeps its captured name. A group with a current grant offers Archive only.
- Grants need an active Field Salesperson; removals also work for a rep who has since become
  inactive. A failed identity lookup saves nothing.
- **Hand-offs:** the `restriction-permissions` usage source counts current grants.
  `RestrictionPermissionReader.CurrentGroupIdsAsync(repSubject)` gives **WI-043** the rep's current
  permissions for `VisibleToRep`. WI-043 owns S1's "appears on his tablet after Sync" and S2.
- **WI-031** owns the removal consequence statement (M14.2, CV009-E/F). Until it lands, Remove
  saves directly.

## WI-015 geography retirement (2026-10-02, developer, plan approval)

- Archive applies to the selected geography row. An archived County or Region
  excludes descendant Towns from new choices without rewriting child flags.
  Existing links/IDs remain; archived children still count as usage.
- Un-archive restores choices only under an active hierarchy. New children
  beneath archived geography are rejected. CSV preserves existing archival
  state; an invalid addition rolls back the entire import.
- WI-015 delivers reference contracts and the shared archive controls.
  Production Location storage/forms remain WI-016; the Laragh four-reference
  scenario uses a test provider. WI-016 must replace the keyed `directory`
  `EmptyLocationUsageSource` with real Location counts and protect assignments
  in the same serializable DirectoryDb transaction. See
  `docs/directory-geography-retirement.md` for the concrete connection steps.

## WI-016 first Location requirement (2026-10-02, developer)

- The developer chose **Require a first Location when creating a Customer**,
  then approved the implementation plan and Scenario Review matrix.
- Customer and first Location must save in one transaction. A missing or
  invalid first Location saves neither record; additional Locations can be
  added from the Customer record afterwards.
- Customer names are nonunique. Required names are trimmed and capped at 200
  characters. Same-Customer Location duplicate warnings ignore case and apply
  to both creation and renaming, with proceed/rename allowed.
- The model checkpoint remains required before persistence/UI implementation;
  resume trigger `Continue T-2.3.1`. The reviewed artifact is
  `docs/customer-location-checkpoint.md`.
  The developer accepted it with that exact trigger on 2026-10-02, authorizing
  the remaining implementation and verification plan without another routine
  approval pause. Feature-branch delivery includes its push; main integration
  remains a separate request.

## WI-032 tablet runtime and storage (1C) (2026-10-23, developer)

The developer chose to decide 1C on the physical Samsung evidence before the iPad check, then
picked the recommended option for each decision in `docs/tablet-offline-1c-decision.md`. This
applies to WI-032's production slice and the items it blocks (WI-033, -034, -037, -039, -041,
-043, -048 and later tablet work).

- **Runtime (MI-01):** the tablet app is .NET MAUI Blazor Hybrid on .NET 10.
- **Storage (MI-02):** SQLite through Microsoft.Data.Sqlite with pinned native 3.53.3 and a preflight
  that refuses another native build, in WAL mode with synchronous FULL. Each signed-in rep has one
  bound snapshot; a staged import is fully verified before a single-transaction switch, and the
  previous snapshot stays until then. Unsent work lives in a separate database that refresh never
  rewrites. Storage work runs off the UI thread. **Old-copy cleanup runs after the switch** in
  bounded batches and resumes on the next open.
- **Targets:** at target scale (25,000 products / 500 Locations / 120,000 history lines) on
  SM-P610-class hardware: offline open under 3 s, refresh under 90 s, and 500-Location search under
  1 s (source requirement). Target scale is the planning assumption until real volumes are known.
- **iPad:** the iPad mini/Mac check is deferred until before any iPad use. An iPad failure reopens
  this decision for iOS only.
- **Still proposals, to confirm in the WI-032 production plan:** the free-space check before refresh
  (twice the snapshot DB size plus 512 MiB), backup exclusion with Android file-based encryption, and
  snapshot contract details (field list, schema ownership, version compatibility, cross-database
  consistency, clock rollback).
- The spike tool stays isolated under `tools/FieldSales.TabletSpike*`; production code needs its
  own approved plan and Human-Led scenarios. Console `missingInformation` entries are not edited.

## WI-032 production slice (2026-10-24, developer, plan approval)

The developer replied **Approved** to `docs/tablet-sync-production-plan.md`, covering decisions
P1–P13, contract v1 fields, five increments with review stops and 25 Human-Led scenarios (MI-62
undecided).

- **Snapshot contract owner: the developer (ColmKenna)**, as T-4.1.1's owner. One snapshot version
  change at a time, each reviewed by the owner. Every later card changing
  `FieldSales.Snapshot.Contracts` states its version bump: minor for additive fields, major for
  anything removed or changed.
- **Tablet identity client** `fieldsales-tablet`: public, authorization code with PKCE, system
  browser, `offline_access`. **Refresh-token absolute lifetime 24 hours** (developer, replacing the
  proposed 12). Refreshing never extends the 24-hour local window (I3.1).
- **1C defaults confirmed:** free-space check before Sync (twice the snapshot DB plus 512 MiB),
  snapshot and Unsent databases excluded from Android backup, Android file-based encryption.
- The developer will install the ASP.NET development certificate on the Samsung in increment 4.

## WI-032 increment 4b (2026-10-26, developer)

- **Scenario 18 (24 hours elapsed) on the Samsung:** the developer changes the tablet date forward in
  Settings and back afterwards. No test hook is added to the app.
- **Scenario 25 (budgets at target scale):** a dev-only DemoData `scale` command (seed batch
  `scale-v1`) adds 500 shops for Colm in Swords and 25,000 products with 800 categories and 75,000
  prices. The app logs `open`, `sync` and `cleanup` durations under `FieldSalesTiming`. Recorded in
  `docs/tablet-sync-production-plan.md`.

## WI-032 open-time budget (2026-10-27, developer)

- The D3 budget "open under 3 s at target scale" means the rep-felt open: Android starting the app to the shops list on screen (not only the
  data open the spike measured). The Home list is drawn with `Virtualize` so only visible rows render. Measured on the Samsung: 2.82 to 2.99 s
  over five cold opens (thin margin); startup cost, not the list, is now the main part.

## WI-032 completion and startup speed (2026-10-27, developer)

- WI-032 accepted and marked `done`. The cold-open speed is deferred: "If I find the speed an issue later I will tackle it then." No further startup work is planned in this card.
