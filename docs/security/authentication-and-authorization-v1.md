# Authentication and Authorization V1

Status: accepted contract; `AS-ID-01` and `AS-ID-02` implemented locally.

## Authority and boundaries

Apologia Studio is the single authority for human accounts. Document Manager
does not maintain a second user directory. Machine-to-machine consumer,
notification and replay credentials remain separate service identities and are
never used as human sessions.

Authentication proves who the user is. Authorization independently decides
what that authenticated user may do. Hiding a control is never an authorization
boundary: every sensitive command must enforce its permission server-side.

## Public registration workflow

Public registration is enabled, but registration never grants access by itself:

```text
registration
    -> pending_email
    -> pending_approval
       -> active (initial Reader role)
       -> rejected
```

1. A visitor registers an e-mail address, display name and password.
2. The account remains `pending_email` until the address is confirmed with a
   single-use, time-limited token.
3. Confirmation moves the account to `pending_approval`; it does not create an
   authenticated application session.
4. An administrator explicitly approves or rejects the request. Rejection
   requires a reason.
5. Approval activates the account and assigns only the `Reader` role.
6. Any sensitive role or permission is granted by a separate, explicit
   administrative action.

Pending, rejected and suspended accounts cannot sign in. Rejected accounts are
retained so the decision can be audited and repeated abusive registrations can
be recognized. They can later be reconsidered by an administrator.

## Authorization model

The target assignment chain is:

```text
account -> groups -> roles -> permissions
```

Permissions are a closed, versioned catalog defined by the application. The
administration UI may assign known permissions to roles but cannot invent free
text permissions. Direct per-user permission exceptions are deliberately
excluded from V1. Effective permissions are the union of roles inherited from
the user's groups.

Initial system roles are `Reader`, `Editor`, `DocumentOperator` and
`Administrator`. Four protected system groups map one-to-one to those roles:
`Readers`, `Editors`, `Document Operators` and `Administrators`. Custom groups
may combine several roles. Custom roles may be created and composed from the
closed permission catalog. The `Administrator` role and the system-group to
system-role mappings are immutable safeguards.

Legacy direct role assignments are migrated once into their corresponding
system groups and then removed. Runtime authorization therefore follows the
documented inheritance chain rather than maintaining two competing assignment
models. A change to a group or role updates the security stamp of affected
accounts so an existing session is revalidated and loses obsolete rights.

## Account administration

The administration surface must support:

- viewing pending and existing accounts;
- creating an account manually, either through the normal e-mail verification
  workflow or, for controlled local testing, with verification bypassed;
- approving or rejecting a verified request;
- suspending and reactivating an account;
- resetting lockout and, later, issuing a password-reset invitation;
- managing group membership, roles and role permissions;
- opening an account detail from the account list to inspect its effective
  roles and permissions and change its group memberships;
- viewing security administration events.

There must always be at least one active administrator. An administrator cannot
remove or suspend the final active administrator, including itself.

Every approval, rejection, suspension, reactivation, role change and group
change records the actor, target, timestamp and reason where applicable.
The administration screen exposes the 100 most recent security events.

An account created with the administrative e-mail-verification bypass is
immediately active but receives only the `Reader` role. The bypass is recorded
as an explicit `account.create.verified` event with the reason
`email-verification-bypassed`. It grants no sensitive permission and is not a
replacement for the production e-mail workflow.

### Administration UI workflow

The authenticated account menu exposes one `Administration` entry. Hovering it
opens the `Accounts` and `Groups and permissions` submenu; selecting the parent
entry directly opens `Accounts`. Both administration screens link to each other
and use the close control to return to Studio. All labels inherit the user's
Apologia Studio interface language.

From `Accounts`, an administrator can:

1. create an account through the standard verification workflow, or explicitly
   bypass e-mail verification for controlled local tests;
2. select an account name to open its access detail;
3. add or remove group memberships;
4. inspect the roles and permissions that will be inherited from the selected
   groups before saving.

The detail does not allow direct per-account role or permission assignments.
Groups remain the only supported assignment point, so the effective access
model stays deterministic. Saving membership changes records an
`account.groups.update` administration event, updates the user's security stamp
and therefore causes existing sessions to be revalidated. Removing the final
active administrator's last administrator-bearing group is rejected.

From `Groups and permissions`, an administrator can create custom groups and
roles, manage group members, associate roles with groups and compose roles from
the closed application permission catalog. System role mappings remain
protected.

## Credentials and session security

Passwords are processed only by ASP.NET Core Identity and stored as adaptive
password hashes. E-mail confirmation and password-reset tokens are single-use
Identity tokens. Authentication uses an HTTP-only, secure-in-production,
same-site cookie with a bounded lifetime. Repeated failed sign-ins cause
temporary lockout.

No production deployment may use the development e-mail sink or a known
bootstrap password. Production must configure a real e-mail delivery adapter
and bootstrap the first administrator with an explicit secret. Public account
creation is rate-limited before internet exposure.

## Bootstrap and compatibility

The first administrator is created only when the identity store has no users
and bootstrap is explicitly enabled. Local development adopts the historical
demo user identifier so existing conversations and editorial audit references
remain owned by the initial administrator. Bootstrap is idempotent and cannot
overwrite an existing account.

## Delivery sequence

`AS-ID-01` establishes Identity persistence, registration, e-mail confirmation,
approval/rejection, login/logout, the first administrator and the initial
account administration screen.

`AS-ID-02` adds groups, editable role-permission composition and its complete
administration UI.

`AS-ID-03` applies named permission policies to every Apologia page and command,
replacing development feature flags for sensitive editorial operations.

`AS-ID-04` shares the authenticated session with the embedded Manager UI and
enforces Manager operation, replay and permanent-custody-deletion permissions
while retaining independent service credentials for backend integration.

## Implemented administration surfaces

- `/administration/accounts`: pending registrations, approval, rejection,
  suspension, reactivation and lockout reset;
- `/administration/access`: group membership, group-to-role composition,
  custom roles, role-to-permission composition and recent security events;
- the account menu exposes those screens only when the current principal owns
  the corresponding permission, under a single nested `Administration` entry;
- both administration screens inherit the user's Apologia Studio interface
  language and use a close control to return to Studio;
- the two administration screens provide direct navigation to each other;
- each administration service reloads the actor and effective permissions from
  PostgreSQL before changing state. The UI is therefore not the security
  boundary.

## Deliberately unresolved before production exposure

- connect a transactional e-mail provider; the development adapter only logs
  confirmation links locally;
- add rate limiting and anti-abuse controls to public registration and login;
- add the password-reset invitation workflow;
- complete `AS-ID-04` so the embedded Document Manager consumes the same human
  session and enforces its own operation, replay and custody-purge policies.
