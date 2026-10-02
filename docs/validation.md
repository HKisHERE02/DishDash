# Validation results

Recorded on 2026-10-02. This record distinguishes implemented behavior from unverified deployment/infrastructure behavior.

## Environment

- Windows / PowerShell.
- Node 24.19.0; npm 11.5.2.
- Installed .NET SDKs: 9.0.203 and 10.0.401. The repository selects 10.0.401.
- Docker CLI 20.10.22 was present; a usable engine was not available.
- PostgreSQL was not found on PATH or as a local service.
- PostgreSQL remains the application provider. Local API and browser tests used isolated SQLite databases.

## Final executed results

| Check | Result |
| --- | --- |
| Backend Release build | PASS; 0 warnings, 0 errors |
| Backend unit tests | 21 passed, 0 failed, 0 skipped |
| API integration tests | 33 passed, 0 failed, 0 skipped; SQLite relational provider |
| Frontend unit/component/client tests | 15 passed across 5 files, 0 failed |
| Frontend ESLint | PASS; zero warnings allowed |
| TypeScript + Vite production build | PASS; 345.14 kB JavaScript / 107.34 kB gzip |
| Playwright final full run | 12 passed, 0 failed; production build, CI-like mode |
| Repeated keyboard regression | 5 consecutive passes |
| Repeated WCAG audit | 3 consecutive passes; final full run also passed |
| Response-contract/header negative controls | 5 expected failures with protections removed; restored protections passed in the full suite |
| Earlier ownership/CSRF negative controls | 2 expected failures with checks removed, then 2 passes with checks restored |
| npm audit | 0 known vulnerabilities |
| NuGet transitive vulnerability audit | No vulnerable packages reported for all 7 projects |
| EF migration consistency | No pending model changes |
| PostgreSQL idempotent SQL generation | PASS; actual server execution unavailable |
| Workflow YAML formatting/parsing | PASS; hosted execution not performed |

## Reproducing validation

See [Testing strategy](testing.md) for the test layers, commands, PostgreSQL configuration and security regression coverage.

## Requirement-to-evidence matrix

| Requirement | Implementation | Automated test / evidence | Status |
| --- | --- | --- | --- |
| Layered backend and feature frontend | `backend/src/*`, `frontend/src/features/*` | Release solution build; TypeScript/Vite build | PASS |
| Registration, login, logout, persistent session | `AuthEndpoints.cs`, `AuthPage.tsx`, Identity configuration | API auth, hash, lockout and forged-session tests; browser account journey | PASS |
| Protected endpoints and routes | API authorization groups, `App.tsx` | Seven anonymous-route cases and ownership negatives | PASS |
| Profile preferences | `AuthEndpoints.cs`, `ProfilePage.tsx` | Invalid-profile refusal; recommendation and mobile profile journeys | PASS |
| Protected public response contracts | `ResponseContracts.cs`, auth/commerce endpoints, frontend models | Exact-field assertions for profile, checkout/replay, order history/detail/progression, nested items/events, notifications and rating create/edit/list; OpenAPI schema assertions; TypeScript build | PASS |
| Safe API response headers | `Program.cs` | Production-environment assertions for 200, 400, 401 and 404 responses; missing-header negative control | PASS |
| Search, cuisine/diet/time/budget filters and sorting | `CatalogEndpoints.cs`, `DiscoverPage.tsx` | Combined diet/cuisine/time/budget test, ingredient search, ascending price/time assertions; browser search/empty/cuisine journey | PASS |
| Dish detail, ingredients and instructions | `DishDetailPage.tsx`, `DemoSeed.cs` | API catalog test; browser dish/servings journey | PASS |
| Dynamic servings | `MealRules.cs`, provider adapters | Four scaling cases and three invalid-serving cases; browser quantity/cost assertions | PASS |
| Cook vs Order comparison | `MealComparison.cs`, `ComparisonPanel.tsx` | Deterministic cost/time unit test; component and browser comparison tests | PASS |
| Grocery and restaurant options | `DemoProviders.cs`, provider contracts | Both API/browser checkout paths; unavailable-option refusal | PASS |
| Grocery basket and restaurant cart | `CheckoutService.cs`, `CartPage.tsx` | Both checkout tests; invalid quantity/provider/ownership tests | PASS |
| Safe demo checkout and decline | `CheckoutService.cs`, demo outcome selector | No card-input assertion; decline then success; repeated request key and changed-total refusal | PASS |
| PostgreSQL schema and migrations | `DishDashDbContext.cs`, `Migrations/*` | Migration generation, idempotent SQL generation, pending-model check | PARTIAL |
| Live PostgreSQL behavior | PostgreSQL test mode and CI service | No local server; remote workflow not run | BLOCKED |
| Guided cooking, progress and timers | `CookingPage.tsx` | Browser next/previous, timer reload, reset and completion journey | PASS |
| Favourites | API favorite composite key; `DishCard.tsx`, `DiscoverPage.tsx` | Private favourites integration test; save/un-save browser journey; delayed data/empty, error/retry and unchanged discovery component tests | PASS |
| Explainable recommendations and hard allergen exclusion | `Recommendations`, preferences, checkout quote validation | Domain exclusion including favourite; API and browser allergy negatives | PASS |
| Order history and deterministic tracking | `Order.Advance`, commerce endpoints, order pages | Sequential/terminal tests; history and three progression steps in both browser checkout journeys | PASS |
| In-app notification read/unread | `Notification`, commerce endpoints, notification page | Ownership/read-state API test; browser count/read assertions | PASS |
| Delivered-order ratings and ownership | `MealRules.ValidateRating`, rating endpoints/form | Invalid owner/state/stars/comment tests; create/edit/delete browser journey | PASS |
| Realistic deterministic seed data | `DemoSeed.cs` | 20-dish catalog; repeatable account/history seed test | PASS |
| Polished desktop/mobile UI | `styles.css`, feature pages | Browser screenshots and mobile overflow assertions | PASS |
| Accessibility | Semantic controls, focus management, error/status regions | WCAG AA automated audits, repeated skip-link regression, keyboard-only registration | PASS (tested scope) |
| Failure paths | API/client errors and validation | Missing dish, no results, invalid auth, provider refusal, invalid/empty cart, payment decline, price mismatch, ownership, ratings and network failure tests | PASS (tested scope) |
| Safe health output | `/api/health` | Exact JSON assertion | PASS |
| OpenAPI | Development mapping in `Program.cs` | Runtime document-path assertions in Development and 404 outside Development | PASS |
| Security-sensitive tests turn red | CSRF and owned order read checks | Two deliberate failures; restored two passes; final full suite | PASS |
| No secrets or real payment storage | Config example, ignore rules, Identity, demo-only checkout | Source/config review and repository audit | PASS |
| CI configuration | `.github/workflows/validate.yml` | Includes npm and transitive .NET vulnerability checks; equivalent local commands passed; no hosted run | PARTIAL |
| Professional documentation | README and `docs/*` | Source and claim review | PASS |
| Docker/container validation | Kept independent of app setup | Engine unavailable; no container files claimed | NOT IMPLEMENTED (optional) |
| Live deployment and demo video | Deferred | No deployment/video produced | NOT IMPLEMENTED |

## Known limits

The implementation is suitable for local human review and demo exploration with the test host. It is **NO-GO for final portfolio publication** until PostgreSQL runtime validation and hosted CI succeed and the remaining evidence gaps are closed. There is no deployed service or real provider/payment integration.

Automated tests cover the listed behavior, not every filter combination or concurrency schedule. Package audits report known advisories at the time of execution, not a guarantee of security. UI images are illustrative and remotely loaded. Container packaging, account recovery/deletion and production deployment are intentionally deferred.
