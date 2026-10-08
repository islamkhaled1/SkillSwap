## Description
<!-- Provide a brief summary of the changes introduced in this PR. -->

## Associated Module & Member Ownership
<!-- Check your assigned module ownership -->
- [ ] Member 1 -- Auth & Profile (`feature/auth-profile`)
- [ ] Member 2 -- Skills, Availability & Matching (`feature/skills-matching`)
- [ ] Member 3 -- Swap Request & Chat (`feature/swap-chat`)
- [ ] Member 4 -- Session & Wallet (`feature/session-wallet`)
- [ ] Member 5 -- Platform Features (`feature/reviews-platform`)
- [ ] Cross-Cutting / Shared Infrastructure

## PR Quality Checklist
Before requesting review, ensure the following are satisfied:
- [ ] **Target Branch:** This PR targets `develop` (never `main` directly).
- [ ] **Feature Branch:** Branched from the latest `develop`.
- [ ] **Scope Isolation:** Only modified files within assigned module scope (no drive-by formatting in other files).
- [ ] **Build Health:** `dotnet build SkillSwap.sln` succeeds with 0 errors and 0 warnings.
- [ ] **Test Health:** `dotnet test SkillSwap.sln` passes 100% (minimum 74 passing tests; new tests added for new logic).
- [ ] **Architecture Boundaries:** Clean Architecture rules respected (`SkillSwap.Application` does NOT reference Infrastructure or API).
- [ ] **Database & Migrations:** If modifying entities, followed migration policy (coordinated with Member 4; no independent migrations added).
- [ ] **Business Contracts:** Respected core platform invariants (non-negative wallets, integer minutes, Requester=Learner, Receiver=Teacher, UTC timestamps).
- [ ] **Contract Documentation:** Any changes to DTOs, interfaces, or endpoints are documented in `docs/team/`.
- [ ] **Security:** No secrets, connection passwords, or credentials committed.
