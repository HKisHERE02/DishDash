# API reference

All routes are prefixed with `/api`. JSON enums are strings (`Cook`, `Restaurant`, `Confirmed`, `Preparing`, `OutForDelivery`, `Delivered`). Prices are numeric CAD estimates. Dates are UTC ISO timestamps. IDs are integers for dishes and GUIDs for orders and user-owned collections.

| Method | Route | Access / behavior |
| --- | --- | --- |
| GET | `/health` | Public minimal liveness only |
| GET | `/auth/csrf` | Public; returns request token and sets antiforgery cookie |
| POST | `/auth/register` | Email, password, displayName; creates persistent session |
| POST | `/auth/login` | Email/password; lockout and rate limited |
| POST | `/auth/logout` | Authenticated; removes session cookie |
| GET, PUT | `/profile` | Own preferences |
| GET | `/dishes` | Public; search, cuisine, diet, maxMinutes, budget, sort |
| GET | `/dishes/{id}` | Recipe and steps |
| GET | `/dishes/{id}/comparison?servings=2` | Lowest cost comparison and explanation |
| GET | `/dishes/{id}/grocery-options?servings=2` | Ingredient estimates and provider choices |
| GET | `/dishes/{id}/restaurant-options?servings=2` | Meal price, delivery, ETA and demo rating |
| GET | `/favorites` | Own saved dish IDs |
| PUT, DELETE | `/favorites/{dishId}` | Add/remove own favourite |
| GET | `/recommendations` | Dietary/allergen filtered, scored, explained dishes |
| GET, POST, DELETE | `/cart` | Read quoted basket, add selection, or clear own basket |
| DELETE | `/cart/{id}` | Remove owned line |
| GET, POST | `/orders` | Own history / demo checkout |
| GET | `/orders/{id}` | Owned order, items and timeline |
| POST | `/orders/{id}/advance-demo` | Advance owned order by one valid state |
| GET | `/notifications` | Own notifications |
| PUT | `/notifications/{id}/read` | Mark own notification read |
| GET, PUT | `/ratings` | Own ratings / upsert a delivered owned order rating |
| DELETE | `/ratings/{id}` | Delete own rating |

All unsafe methods require `X-CSRF-TOKEN` from `/auth/csrf` together with the associated cookie. Acquire a fresh token after sign-in/sign-out. Every non-public route also requires the session cookie. Missing/invalid authentication returns 401; unknown or other-user resource IDs return 404.

Protected responses use explicit contracts. Profile responses contain only display name and preferences. Order responses contain the public order ID, timestamp, total, status, item snapshots and timeline. Items expose `dishId`, `dishName`, `servings`, `kind`, `providerName` and `total`; timeline events expose `status` and `createdAt`. Notifications and ratings retain `orderId` for navigation. Ownership IDs, checkout keys, concurrency versions and nested persistence keys are not returned. These schemas are included in the development OpenAPI document.

Example add-to-cart body:

```json
{ "dishId": 1, "servings": 2, "kind": "Cook", "providerId": "market" }
```

Example checkout body:

```json
{ "requestKey": "abdf049c-8602-459b-b645-61e910aebc0e", "outcome": "success", "expectedTotal": 7.70 }
```

Use a new UUID for a new checkout and the same UUID when retrying an uncertain response. `outcome` is `success` or `decline`; it is a simulation, not a payment authorization. The server ignores client ownership IDs and computes prices from its own data.

Validation failures return 400 with a useful `title`. Rate limiting returns 429. Persistence conflicts return 409 when reported by EF Core. Unexpected failures return a generic 500 response. No endpoint exposes connection strings or stack traces. OpenAPI JSON is exposed at `/api/openapi/v1.json` only in Development.
