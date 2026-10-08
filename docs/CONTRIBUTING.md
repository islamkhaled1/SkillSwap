# CONTRIBUTING

## Team Workflow

Welcome to the SkillSwap backend team. Please read this guide before making your first commit.

---

## Branching Strategy

```
main         <- Production-ready. Protected. Never push directly.
develop      <- Integration branch. All features merge here via PR.
```

### Feature Branches

Create feature branches from `develop`:

```bash
git checkout develop
git pull origin develop
git checkout -b feature/auth-profile
```

**Naming conventions:**

| Type | Pattern | Example |
|---|---|---|
| Feature | `feature/<name>` | `feature/auth-profile` |
| Bug fix | `fix/<name>` | `fix/wallet-hold-race` |
| Hotfix | `hotfix/<name>` | `hotfix/jwt-expiry` |
| Docs | `docs/<name>` | `docs/update-database-spec` |

**Planned feature branches:**

- `feature/auth-profile`
- `feature/skills-matching`
- `feature/swap-chat`
- `feature/session-wallet`
- `feature/reviews-platform`
- `feature/subscriptions`
- `feature/notifications`
- `feature/admin`

---

## Pull Request Rules

1. **No direct pushes to `main` or `develop`.**
2. All work goes through a PR into `develop`.
3. At least **one teammate must review and approve** before merge.
4. Resolve all review comments before merging.

### Before Opening a PR

```bash
# Update your branch from latest develop
git fetch origin
git rebase origin/develop

# Build must succeed
dotnet build

# All tests must pass
dotnet test

# No warnings should be introduced without justification
```

### PR Description Must Include

- What was implemented
- How to test it
- Any migration steps required
- Any deviations from DATABASE.md (must be approved by tech lead)

---

## EF Core Migrations

> ⚠️ Migrations are sensitive. Only one migration owner per sprint should manage them.

Rules:
- **Do NOT create migrations** until DATABASE.md has been reviewed and locked.
- Migrations must be reviewed by the tech lead before merging.
- Never squash existing migrations.
- Run `dotnet ef database update` in the dev environment before PR.

Migration command (from repo root):

```bash
dotnet ef migrations add <MigrationName> \
  --project src/SkillSwap.Infrastructure \
  --startup-project src/SkillSwap.API
```

---

## DATABASE.md Is the Source of Truth

> **All agents and developers MUST read `docs/DATABASE.md` before adding or modifying any entity.**

- Do NOT invent entity fields not documented there.
- Do NOT change relationship semantics without updating DATABASE.md first.
- If DATABASE.md conflicts with Business-Rules.md, raise it with the tech lead.

**Schema changes require:**
1. Update `docs/DATABASE.md`
2. PR reviewed + approved
3. Then create the EF migration

---

## Commit Message Format

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(auth): add JWT token generation service
fix(wallet): prevent negative balance on concurrent booking
docs(db): add Session entity spec to DATABASE.md
test(wallet): add concurrent booking integration test
chore: update EF Core to 10.0.x
```

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `perf`

---

## Code Style

- Use **nullable reference types** (`#nullable enable` is project-wide).
- Use **async/await** throughout; never block with `.Result` or `.Wait()`.
- All stored dates/times must be **UTC**: `DateTime.UtcNow` and named with `*Utc` suffix.
- Follow Clean Architecture boundaries — Domain must never reference Infrastructure or API.
- Use `IEntityTypeConfiguration<T>` for all EF Core entity mappings.
- Use `Result<T>` for service-layer return values; do not throw exceptions for expected failures.
- Use `DomainException` (or subtypes) for business rule violations.

---

## Questions and Escalation

- **Database design questions** → Open a discussion before coding; update DATABASE.md.
- **Business rule questions** → Raise with product owner; update Business-Rules.md.
- **Tech debt** → Create a `chore/` branch and PR with clear justification.
