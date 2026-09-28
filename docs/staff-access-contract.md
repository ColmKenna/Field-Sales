# Staff website access contract (WI-002)

Agreed on 2026-09-28 for T-1.0.1 / IAM-US-001 and IAM-US-002. This is the contract for the first
staff identity slice. The human reviews each later implementation increment before it begins.

## Hosts and sessions

| Concern | Decision |
|---|---|
| Identity issuer | The adopted `FieldSales.Identity` host at `https://localhost:7201` in Development. It owns the username/password page, OIDC endpoints and `/Admin`. |
| Staff website | One .NET 10 / Aspire `FieldSales.Web` Razor Pages BFF on `https://localhost:7203`, containing rep, manager and head-office areas behind one session. |
| Business API | `FieldSales.Api` on `https://localhost:7204`; browser calls go through the BFF. |
| Browser state | A protected BFF session cookie. Access and refresh tokens remain server-side. |
| Staff credentials | I-01 sends the person to the identity host's existing username/password page. The BFF never collects the password. |
| Recovery | Staff contact a `SysAdmin`; the administrator uses the existing identity-admin password-reset page. No staff self-service reset or customer invitation flow is part of this slice. |

The approved I-01 and I-02 layouts are in
[`plan_docs/uxdocs/07-access-and-sign-in.md`](../plan_docs/uxdocs/07-access-and-sign-in.md).
I-01 provides staff entry, failure/retry and a signed-out return. I-02 presents only currently
permitted areas when several are held and no last-used permitted area is available. A valid
direct-link destination takes precedence over the default landing after sign-in.

## Identity and authorization

| Item | Decision |
|---|---|
| Business roles | ASP.NET Identity roles named exactly `Field Salesperson`, `Sales Manager`, `Head Office User`. |
| Security administration | Template `SysAdmin` remains separate and grants no business area by itself. A Head Office User is not a `SysAdmin` by implication. |
| OIDC client | Confidential `fieldsales-staff-web`, authorization code with PKCE and a secret supplied by AppHost parameter `staff-web-client-secret`. |
| Requested scopes | `openid`, `profile`, `roles`, `fieldsales.api`, `offline_access`. Role claims come from the `roles` identity resource. |
| API | Resource `fieldsales-api`, scope `fieldsales.api`, audience `fieldsales-api`. |
| Redirects | `https://localhost:7203/signin-oidc`, `https://localhost:7203/signout-callback-oidc` and front-channel logout `https://localhost:7203/signout-oidc`. |

The BFF guards each area at the request boundary. A denied direct link exposes neither protected
content nor its actions. One sign-in covers every held business role; switching areas does not
prompt again. The last-used area is chosen only if it is still held. An account with one remaining
area goes there; a multi-role account with no usable last-used area sees I-02.

Increment 2 replaces the template's Development sample-client seed with this client and API
resource/scope. Existing sample registrations in a persistent Development database are reviewed
and removed through `/Admin`; the seeder does not delete existing client records. Outside
Development, client administration continues through `/Admin`. Staff accounts and roles are
provisioned through the existing Admin Users/Roles pages; indefinite suspension represents an
inactive account for rejected sign-in. Storage of the last-used area is reviewed with its increment.

## Increment 3 implementation

`FieldSales.Web` stores each OIDC authentication ticket in its own SQL Server database. The
ticket contains the access and refresh tokens; it is encrypted with the website's Data Protection
key before storage. The `__Host-FieldSalesStaff` cookie contains only an opaque ticket key. The
website keeps its Data Protection key ring in `StaffWebDb` and requires a certificate to protect
that key ring outside Development. The `staff-web` migration bundle prepares the database there.

I-01 starts a single OIDC attempt per submit and redirects to the identity host's existing
password page. An invalid staff password returns to I-01 with a retry message; other clients'
login failures remain on the identity host. One BFF cookie covers the three business areas,
with exact role policies on their Razor Pages. Sign out uses a protected POST, removes the stored
BFF ticket, and starts OIDC sign-out at the identity host. After the front-channel notification,
the staff client returns to I-01 automatically, with the identity page's return link as fallback.
The first API surface, `/staff/session`, validates a bearer token for `fieldsales-api`, the
`fieldsales.api` scope and a business role. The BFF calls it with a server-held access token and
refreshes that token through the identity host as it nears expiration.

The current `/Staff` page lists held areas. Last-used routing, I-02 choice and switching are the
next increment; this page is a temporary entry for the protected landings.

## Scenario gate and boundary

The developer approved IAM-US-001 S1–S8 and IAM-US-002 S1–S2 as the Human-Led website scenario
matrix, including a direct protected read and action denial. The named tests and one-line intents
are recorded in [`WI-002.md`](../plan_docs/.agent-notes/WI-002.md). IAM-US-002 S3, current-role
enforcement on the next request after an in-session role removal, is WI-003.
