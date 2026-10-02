# WI-018 — Link contacts to locations with one main contact each

Source T-2.4.1 / DIR-US-003. The user approved the plan and 14 Scenario Review
intents, then accepted the model checkpoint by answering its review question
“yes” on 2026-10-02. They confirmed that the same person may be Main at several
shops, while each shop has one Active Main. The checkpoint remains in
`contact-location-checkpoint.md` as the historical design snapshot.

## Delivered behavior

Head Office → Customers → Location now shows its Main contact and linked
Contacts, with Create contact, Link existing contact and Set Main actions.
Active Contacts appear by default; Show inactive reports and reveals the
retained Inactive people. Contact records show Type, details, global state and
every linked Location, with Customer/Town labels and Main markers.

Create contact saves one Active person and at least one initial Location link
in a single transaction. Locations can belong to different Customers. The
first Active person at a shop becomes Main automatically, with a displayed
note. Relinking the same person reports already linked, preserves Main, and
does not create another relationship. Empty shops remain valid.

Replacing a different Main names the outgoing person in the confirmation and
retains their ordinary link. Cancel is read-only. Main changes at one shop
leave the other shops' choices intact. Confirmed replacement carries both
the version and outgoing Main the user saw; a stale choice requires reload.

Names are required, trimmed, nonunique and limited to 200 characters. Contact
Type is required and must be Active on creation or a new assignment; an
unchanged archived Type survives edits and retains its labelled reference.
Phone/email are optional, trimmed, blank-to-null fields with format checks and
limits of 50/254 characters. Invalid forms retain the entered details/shops.

Status belongs to the person across all shops. A non-Main can become Inactive
without losing links. Becoming Inactive is blocked if Main anywhere, with
replacement guidance. There are no delete or unlink operations in this slice.

## Shared protection and persistence

`ContactStore` owns serializable create/edit/link/Main transactions and checks
SQL rowversions. Status edits load all linked shops. A no-op edit still checks
and advances the Contact version; roster-affecting edits advance Location
versions too. SQL deadlocks and stale versions give safe reload conflicts.

`20261002141232_AddContactsAndMainContact` adds Contacts and LocationContacts,
plus the optional MainContactId on existing Locations. Existing directory IDs,
Type/Town references, Eircodes and archived labels survive the additive upgrade.

- The link's composite primary key rejects duplicate Location/Contact pairs.
- Main is one scalar on the Location row, so two independent Main flags cannot
  be represented. A composite restrictive foreign key requires that choice to
  be one of the same shop's links; a Main link cannot be deleted.
- SQL guards reject an Inactive Main, clearing Main while Active links exist,
  and globally inactivating a person who is Main at any shop. First Active links
  fill empty slots, including multi-row SQL batches. Activation also fills an
  inactive-only legacy roster's empty slot without replacing another Main.
- Trigger-aware EF mapping disables direct OUTPUT for the affected tables.
  Contact/link insertion precedes the Main pointer; the store suppresses
  premature pointer updates and reloads trigger-updated Location versions.
- `ContactTypeUsageSource` counts real Contact rows, including Inactive people,
  rather than Location links. Usage and retirement share the scoped DirectoryDb
  transaction. A Buyer Type used by 14 people linked twice still reports 14,
  offers Archive only and keeps all labelled references. Existing fail-closed
  usage-provider checks remain in the predecessor suite.

The migration embeds immutable SQL guard definitions. The standalone
`contact-location-storage.sql` is the review companion, not a runtime dependency.
Future screens must use the shared store/model; storage independently protects
Main eligibility and retention. As reviewed, the minimum initial link on Contact
creation is enforced by the atomic store/domain operation, not a Contact insert
trigger. Empty and inactive-only legacy shop rosters may have no Main.

API operations require current Head Office access and staff API scope. Razor
forms reuse current-role checks, server-held access tokens and antiforgery.
Browser requests cannot bypass Main eligibility or replacement confirmation.

## Acceptance verification

1. **“Contact ‘Mary Walsh’, Type Pharmacist, linked to Rathdrum, Arklow and
   Wicklow Town appears as a Contact at all three.”** The multi-shop scenario
   reopens all three rosters and the Contact page, asserting one stable person,
   Type/details and links across two Customers.
2. **“Setting Mary as Main at Rathdrum and Arklow shows ‘Main contact: Mary
   Walsh’ there; Wicklow Town still has no Main.”** The user-approved source
   reconciliation takes precedence over the contradictory vacancy wording:
   Wicklow starts with another Main, Mary is ordinary there, and making her Main
   at Rathdrum/Arklow leaves Wicklow's existing choice unchanged. The website
   assertions check all three displayed Main labels. Source cards are preserved.
3. **“Linking the first Contact to a Location with none makes them Main
   automatically, with a note saying so.”** Website creation and linking to an
   empty shop both assert the saved Main and the automatic designation note.
4. **“Setting a second Main at Rathdrum asks ‘Replace Mary Walsh as main
   contact?’; after confirming, Mary remains a linked Contact.”** Website/API
   scenarios check the exact question, unchanged state on Cancel/omitted
   confirmation, explicit confirmation, retained outgoing link and independence
   at the other shop.
5. **“A Location with 3 Inactive Contacts lists only Active ones with
   ‘Show inactive (3)’.”** Both linked shops hide three globally Inactive people,
   display the count and reveal the same retained IDs on toggling.
6. **“No path ever leaves a Location with two Mains.”** Simultaneous first links,
   competing replacements and status/selection races retain one Active linked
   Main. Direct SQL tests also reject duplicate links, invalid status, unlinked
   or Inactive Main choices, Main clearing/deletion and global Main inactivation.

## Verification evidence

Before behavior changes, all 145 existing directory/reference scenarios passed
and both existing predecessor-upgrade checks passed at the model checkpoint.
The focused Contact suite now passes all 26 cases across the approved scenario
families, including invalid input, archived Types, real usage counts, races,
antiforgery/access removal, restart and the WI-017 schema upgrade.

- Focused SQL run: `.artifacts/wi018-contacts/wi018-contacts_net10.0_20261002153518.trx`.
- Build: zero warnings/errors, warnings treated as errors.
- JavaScript: 34 passed, zero failures/skips.
- Complete bounded .NET regression suite: **1,565 passed**, zero failures/skips
  (143 API, 1,046 identity administration, 376 website). TRX files are
  `.artifacts/wi018-full/wi018-full_net10.0_20261002153735.trx`,
  `wi018-full_net10.0_20261002154106.trx` and
  `wi018-full_net10.0_20261002154331.trx`.
- Final build after the test hosts exited: zero warnings/errors. An overlapping
  build attempt had Windows copy-lock errors; the sequential rerun succeeded.
- Plan validation against `1476def`: only WI-018's status is added as done;
  source card/spec content and all earlier statuses remain unchanged.

The only predecessor test edit is fixture registration for the production
Contact usage provider's new type. Its existing assertions remain unchanged.

## WI-020 handover and next work

WI-020 must add its explicit Replacement Needed gap/outgoing-contact reference
and coordinated replacement/retirement across shops. Extend the model and SQL
guards together, retaining IDs/history and ensuring reactivation does not restore
the outgoing Main. WI-018's inactive-only legacy repair is not that future gap
workflow. Tablet/sync, customer logins and coordinate lookup remain separate.

The next item is WI-019 — Default location coordinates from Eircode or Town.
WI-018 finishes on its feature branch; main integration is a separate request.
