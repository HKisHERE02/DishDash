# Testing strategy

Tests cover behavior and boundaries rather than mirroring implementation details. A green SQLite run does not substitute for a PostgreSQL run.

## Test layers

| Layer | Location | Coverage |
| --- | --- | --- |
| Domain/application | `backend/tests/DishDash.UnitTests/MealRulesTests.cs` | Serving bounds and scaling, monetary rounding, dietary compatibility, allergen exclusions, comparison differences, provider refusal, recommendation filtering, sequential/terminal transitions, rating ownership/state/limits |
| HTTP integration | `backend/tests/DishDash.IntegrationTests/ApiTests.cs` | Session creation/removal, password hashing, public catalog, CSRF, protected routes, owner-scoped resources, quotes, both checkout paths, decline recovery and repeated keys |
| Additional boundaries | `backend/tests/DishDash.IntegrationTests/ApiBoundaryTests.cs` | Forged sessions, profile validation, empty checkout, notification ownership, allergy-invalidated baskets, lockout and idempotent demo seeding |
| Response contracts and headers | `backend/tests/DishDash.IntegrationTests/ResponseContractTests.cs` | Exact public fields across protected responses, nested order snapshots, checkout replay, OpenAPI response schemas and safe headers on success/error responses in Production |
| Component/client | `frontend/src/**/*.test.*` | Accessible comparison controls, cost/time display, loading/error feedback, constrained authentication forms, successful/refused HTTP responses, CSRF headers and network errors |
| Favourites state | `frontend/src/features/dishes/DiscoverPage.test.tsx` | Delayed favourites show loading until data or a confirmed empty list arrives; failures show retry; normal discovery remains visible during favourites loading/failure |
| Production browser | `frontend/e2e/journeys.spec.ts` | Full account and meal journeys, both carts, checkout decline/success, ratings, notifications, recommendations, saved cooking progress/timers, error recovery, mobile layout and keyboard-only registration |
| Accessibility audit | `frontend/e2e/accessibility.spec.ts` | axe-core WCAG 2 A/AA and 2.1 AA rules on discovery, dish comparison and registration |

## Commands

```sh
dotnet restore backend/DishDash.slnx
dotnet build backend/DishDash.slnx --no-restore --configuration Release
dotnet test backend/DishDash.slnx --no-build --configuration Release
dotnet list backend/DishDash.slnx package --vulnerable --include-transitive

cd frontend
npm ci
npm run lint
npm test
npm run build
npm audit --audit-level=high
npx playwright install chromium
npm run test:e2e
```

The Playwright configuration starts port 5142 for the test API and port 4173 for Vite preview. Stop other processes using those ports first. Browser tests require an existing `npm run build`. Tests run with one worker and zero automatic retries; failures remain visible. Trace and screenshot evidence is retained on failure.

## PostgreSQL integration mode

Set `DISHDASH_TEST_POSTGRES` to a connection string for a disposable PostgreSQL server before running `dotnet test`. The test role must be allowed to create databases. Each fixture creates and later removes only its own random `dishdash_test_` database. Migrations, rather than `EnsureCreated`, initialize those databases. When this setting is absent, integration tests use per-factory temporary SQLite files.

GitHub Actions configures PostgreSQL 17 for the backend suite. This configuration has not yet executed remotely. Local validation used SQLite because PostgreSQL was unavailable.

## Security negative controls

Security regression tests were deliberately proven to fail when the corresponding protections were removed:

1. Removing the user-ID predicate from the order-detail query caused `OrdersEnforceOwnershipAndRatingState` to fail: expected 404, received 200.
2. Bypassing antiforgery validation caused `CsrfIsRequiredForRegistration` to fail: expected 400, received 200.
3. Returning the profile persistence entity caused `ProfileResponsesExposeOnlyPreferences` to fail because the response contained an extra `userId` field.
4. Removing `Referrer-Policy` caused all four `ProductionResponsesIncludeSafeHeadersEvenOnErrors` cases to fail on the missing header.

The protections were restored and the full suite passed. No bypass remains in the repository. Do not run deliberate mutations against a deployed process or keep modified binaries running.

## Browser failure investigation

The initial keyboard test failed against the production build with `CI=true`. The retained screenshot showed focus on the hero link instead of the skip link. The application was focusing main content on initial mount. Focus now moves to main only after route changes. The affected test passed five consecutive repeats, then the full browser suite passed.

The initial accessibility audit identified text contrast below 4.5:1. The affected foreground colors were darkened. The unchanged WCAG rule set passed three consecutive repeats after rebuilding. No contrast rule or failing selector was excluded.

## Scope of evidence

- Automated accessibility checks cannot certify complete accessibility. Keyboard and mobile checks supplement them; a screen-reader review remains useful.
- Images are remote and illustrative. Core meal journeys do not rely on image downloads.
- Neither SQLite tests nor migration SQL generation prove PostgreSQL runtime behavior or concurrency semantics.
- CI workflow syntax can be checked locally; a successful hosted run requires pushing the repository.
- Browser artifacts can include synthetic account details. They are ignored; only reviewed, anonymous product screenshots belong in documentation.
