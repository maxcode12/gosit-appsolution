# ADR-002 — Use ASP.NET Core Identity for password hashing only

## Status
Accepted.

## Context

`CLAUDE.md` specifies "ASP.NET Core Identity/JWT initially" for identity. ASP.NET Core
Identity ships two things GoSit could use:

1. A vetted password hasher (`PasswordHasher<TUser>`, PBKDF2-HMAC-SHA512).
2. A full user-management stack — `IdentityUser`, `IdentityDbContext`, `UserManager`,
   `SignInManager`, and the schema that goes with them.

Adopting the full stack conflicts with the architecture in `docs/ARCHITECTURE.md`.
`IdentityUser` is an Entity Framework type, so making it the account aggregate would put
a persistence type at the centre of the domain, and `GoSit.ArchitectureTests` explicitly
forbids the Domain project from referencing EF Core. `UserManager` would also become the
place account rules live, which is the "giant Service class" pattern `CLAUDE.md` lists as
an anti-pattern.

## Decision

Use Identity's password hasher, and nothing else.

- `GoSit.Domain.Users.User` is a plain aggregate with private setters and explicit
  methods for every state change.
- `IPasswordHasher` is declared in `GoSit.Application.Abstractions` and implemented in
  `GoSit.Infrastructure.Security.IdentityPasswordHasher`, which wraps
  `PasswordHasher<User>` from `Microsoft.Extensions.Identity.Core`.
- Tokens are issued by `JwtTokenService`, not by Identity.
- Refresh tokens are a domain concept owned by the `User` aggregate.

## Consequences

Positive:

- The hashing algorithm, iteration count, and upgrade path are maintained by Microsoft.
  `Verify` reports when a stored hash uses outdated parameters and login re-hashes it.
- The domain stays free of EF Core, so the architecture tests keep passing.
- Account rules live on the aggregate where they can be unit-tested without a database.

Trade-offs:

- Features that come free with full Identity — email confirmation tokens, lockout
  counters, two-factor, external login stores — must be built rather than configured.
  Email verification and lockout are already planned; `docs/PLAN.md` Phase 7 covers the
  trust and safety work that would otherwise lean on Identity.
- Anyone expecting `UserManager` will not find it. This ADR is the explanation.

## Related

- Brute-force protection is handled at the edge by ASP.NET Core rate limiting on
  `/api/v1/auth/*` rather than by Identity lockout. Per-account lockout is still open.
