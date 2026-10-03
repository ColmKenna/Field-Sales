# WI-020 — Replace or retire a Main contact

Source T-2.5.1 / DIR-US-004. Approved plan, Scenario Review matrix and explicit
Continue T-2.5.1 were supplied together on 2026-10-03. The prerequisite
characterisation/design checkpoint is in main-contact-replacement-checkpoint.md.

## Behaviour and attachment

H-27's Contact record has an Unlink action for every linked shop. For an outgoing
Main or retained retired Main, it opens an inline named question: an existing
Active linked contact, Add a new contact, or No replacement yet. Existing/new
replacement selects the new Main and unlinks the outgoing person only at that
shop. Ordinary Set Main retains the outgoing link, as WI-018 did. Contacts have
no delete operation; even a person with no remaining links keeps their record.

No replacement yet marks the person globally Inactive, retains every link, and
flags every shop where they were Main. An explicit Mark inactive across all
locations action provides the same global retirement with a named impact preview.
Generic contact edits continue to reject implicit Main retirement. Reactivation
keeps the person ordinary and leaves unresolved gaps. Explicitly choosing a Main
clears only that shop's flag. The same person can be Main at several shops.

H-26 shows "Main contact inactive — replacement needed" and the outgoing person's
name even while ordinary inactive contacts are hidden. After reactivation it
shows "Main contact replacement needed" without calling the person inactive.
Invalid input is retained; unused new-contact fields do not block another choice.
Cancel/read operations do not write anything.

## Storage and shared protection

Migration 20261003104659_AddMainContactReplacement adds nullable
RetiredMainContactId to Location, a composite restrictive foreign key to the
same-shop link, and an index. It is exclusive with the existing MainContactId.
Active Main and retired outgoing identity are separate, so current Contact status
cannot accidentally clear a gap. Legacy Locations remain unflagged with stable
IDs, links, Main choices and positions. No coordinate or identity configuration
changes are included.

Location/Contact rules and the three SQL guards extend together. A new gap must
retain the previous Main, and a gap cannot clear without a linked Active Main.
First-Main repair skips flagged shops on creation/link/reactivation; unflagged
empty/legacy shops retain the existing first-Active behaviour. Restrictive
foreign keys prevent unlinking either selected reference prematurely. Contacts
cannot be silently made Inactive while still selected as Active Main.

Every command uses the existing serializable DirectoryDb transaction. Global
retirement locks the Contact and all linked Location rosters in stable order,
checks the Contact version and complete Location/version set shown to the user,
records gaps, and changes status atomically. Replacement/unlink checks Contact
and target Location versions. New contact and link are saved before their Main
reference; Main changes before outgoing link deletion, all inside that transaction.
A test induces a failure after the new contact and Main saves and verifies complete
rollback. Concurrent, incomplete, stale, malformed or invalid input saves nothing.

The new authenticated Head Office operations behind the BFF are:

- POST /directory/locations/{locationId}/contacts/{contactId}/remove
- POST /directory/contacts/{contactId}/retire

They retain API audience/scope/current-role checks and website antiforgery.
LocationContactsPage includes ReplacementNeeded and RetiredMainContact;
ContactLocationSummary includes ReplacementNeeded and IsRetiredMain, all optional
trailing additions. WI-144 can query `Locations.Where(x => x.RetiredMainContactId
!= null)`; the reference is indexed. WI-036 can use the outgoing identity/status.
The tablet display, gap-list screen and online-login suspension remain separate.

## Acceptance evidence

| Quoted criterion | Evidence |
| --- | --- |
| "Unlinking Mary Walsh, Main at Rathdrum, asks who replaces her; choosing Sean Byrne makes him Main and unlinks Mary." | Inline existing-choice website scenario; other shop unchanged and Mary remains Active. |
| "No replacement yet" marks Mary Inactive at all her Locations, keeps her linked, and flags Rathdrum "Main contact inactive — replacement needed". | Inline none-choice website scenario, global retirement API/UI cases and hidden-inactive H-26 display. |
| "If Mary is Main at Rathdrum and Arklow, marking her Inactive flags both." | Two/five-shop theory; linked non-Main shop retains its other Main. |
| "Setting Sean Byrne as Main at Rathdrum clears Rathdrum's flag; Mary stays Inactive." | ClearOnlyChosenGap scenario verifies second shop remains flagged and Mary's global status stays Inactive. |
| "Marking Mary Active again makes her a normal Contact; she does not regain Main anywhere." | Reactivation/new-link scenario leaves Main null and both gaps intact until explicit choice. |
| "The flag is queryable for a later gap list (E17), per Location." | FlagEveryMainShop query counts stored outgoing references; per-Location API/read contracts expose the flag and identity. |

All approved scenario families are represented in MainContactReplacementTests,
MainContactCharacterisationTests and the unchanged WI-018 contact assertions:
first Main; independent shops; confirmed replacement; inactive/unlinked rejection;
protected implicit removal; existing/new/no replacement; several-shop retirement;
local gap resolution; reactivation; non-Main unlink; invalid/cancelled input;
stale/concurrent changes; authorization/antiforgery; restart and predecessor upgrade.

## Verification

Before domain changes: 26 predecessor contact cases and 2 new characterisation
cases passed. After implementation: 53 focused cases passed; the final suite also
adds global-retirement website and late-failure rollback checks.
Warning-as-error solution build: zero warnings/errors.
API: 192/192; website: 434/434; JavaScript: 34/34. Identity results pending.
Results are under .artifacts/wi020/. No tests were weakened, skipped or deleted.
The old contact fixture changed only cleanup for the new guarded FK cycle, restoring
the SQL guard before each scenario. Two initial new-test assumptions were corrected:
HTML attribute order for antiforgery extraction, and affected-shop display order.

WI-020 remains active until the final regression results are green and recorded.
Main integration is a separate user action. Next by console order is WI-021:
Assign territories and resolve each shop's owner with its source.
