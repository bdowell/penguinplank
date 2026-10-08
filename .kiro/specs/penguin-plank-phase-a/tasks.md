# Implementation Plan: Penguin Plank — Phase A (Foundation and Shared Catalog)

## Overview

This plan converts the Phase A design into an incremental, test-driven C# (.NET 10 / ASP.NET Core 10 / EF Core 10 / Blazor WebAssembly / SQL Server 2025 Express) implementation. It implements **only** Phase A deliverables (R01, R10, R11 foundation, and plan items A1–A9) while modeling the **complete Section 6 schema** so later phases extend rather than replace it. Later-phase entities (Production, Inventory, Markets, Sales, Costing, Marketing, Wholesale, concrete connectors, outgoing/social publishing) are **designed-only**: tables are configured and migrated, but no services, endpoints, or UI exercise them.

The sequence is: solution/CI foundation (A1) → persistence/identity (A2) → cross-cutting runtime foundations (A4: Result, ProblemDetails, idempotency, ETag, audit, IFileStore) → catalog domain + use cases + endpoints (A3/R01/R10) → integration foundation with connectors disabled (A4a/R11, A7, A8) → browser shell (A5) → deployment/backup/restore + performance verification (A6/A9).

Testing follows coding-standards §7: xUnit for unit/integration, hand-written fakes + a maintained mocking library for boundaries, injected `TimeProvider` for time, **real SQL Server with actual migrations** for integration tests (never EF InMemory for relational correctness), FsCheck/CsCheck for the 26 correctness properties at ≥100 iterations, bUnit for Blazor components, and Playwright for core browser workflows. Analyzer/compiler warnings are treated as errors for authored projects.

Conventions used below:
- Tasks marked with `*` are optional (tests/validation) and may be skipped for a faster MVP; non-`*` tasks are required.
- Property-test sub-tasks reference the design's **Property {n}** and the requirement clause each validates.
- Checkpoints ensure incremental validation; run the full build + `dotnet format --verify-no-changes` + tests at each.

---

## Tasks

- [x] 1. Establish solution structure, build configuration, and CI (A1)
  - [x] 1.1 Create the solution and project skeleton with enforced references
    - Create `PenguinPlank.sln` and projects: `PenguinPlank.Domain`, `PenguinPlank.Application`, `PenguinPlank.Infrastructure`, `PenguinPlank.Api`, `PenguinPlank.Contracts`, `PenguinPlank.Web` (Blazor WASM), plus `UnitTests`, `IntegrationTests`, `BrowserTests`.
    - Wire project references to enforce the dependency rule: Domain depends on nothing; Application → Domain; Infrastructure → Application + Domain; Api → Application + Contracts + Infrastructure (composition root only); Web → Contracts; Contracts standalone.
    - Target `net10.0`; set `LangVersion` to a stable (non-preview) version.
    - _Requirements: A1 (4.1, 4.2, 4.3, 4.4, 4.5)_
  - [x] 1.2 Add root `.editorconfig` and `Directory.Build.props` central configuration
    - Encode naming rules from coding-standards §4 (`_camelCase` private fields, `s_camelCase` private static, `Async` suffix, PascalCase types/methods, camelCase locals/params).
    - Four-space indent, spaces not tabs, Allman braces, file-scoped namespaces, sorted usings, no unused imports.
    - Enable `<Nullable>enable</Nullable>`, `.NET analyzers` (`EnableNETAnalyzers`, `AnalysisLevel=latest`), and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` for authored projects; scope generated-code exclusions correctly.
    - _Requirements: A1 (4.6, 4.7)_
  - [x] 1.3 Create module-boundary namespace/folder placeholders and composition-root scaffold
    - Create namespace/folder placeholders for all modules: Catalog, Integrations, Identity/Administration (implemented), and Production, Inventory, Markets, Costing, Marketing, Wholesale (designed-only).
    - Add an empty DI composition-root extension in `PenguinPlank.Api` (`AddPenguinPlankServices`) that later tasks populate; no business code resolves services via `IServiceProvider`/service locator.
    - _Requirements: A1 (4.5)_
  - [x] 1.4 Add CI pipeline enforcing build, format, and analyzer gates
    - Pipeline steps: restore, `dotnet format --verify-no-changes`, build with warnings-as-errors, run UnitTests + IntegrationTests (SQL Server service) + BrowserTests.
    - _Requirements: A1 (4.6, 4.7)_

- [x] 2. Define cross-cutting primitives shared across layers (A1, A4)
  - [x] 2.1 Implement typed `Result`/`Result<T>` and stable business error codes in Domain
    - Immutable `Result`/`Result<T>` with a stable `ErrorCode` enum/value and message; success/failure factory methods; no exceptions for expected business failures.
    - Define the Phase A business error-code catalog (duplicate SKU, duplicate piece code, archived record, invalid dimension, invalid proportion, tracking-mode locked, idempotency conflict, stale version, upload rejected, forbidden, mapping conflict).
    - _Requirements: A1 (4.8), A4 (6.3, 6.6)_
  - [x] 2.2 Define `ActorContext` and `IClock`/`TimeProvider` usage conventions in Application
    - `ActorContext` (user id, role Owner/Staff, system-worker flag) constructed at the API boundary and passed into use cases; Domain never touches `HttpContext`.
    - Standardize on injected `TimeProvider`; forbid direct `DateTime.UtcNow` in business decisions (A8).
    - _Requirements: A2 (5.12), A7 (9.6), A8 (10.1)_
  - [x] 2.3 Write unit tests for `Result<T>` semantics and error-code stability
    - Verify success/failure carries the correct stable code; failure never exposes stack/secret.
    - _Requirements: A1 (4.8)_

- [x] 3. Model the full Section 6 schema and create the initial migration (A2)
  - [x] 3.1 Implement global column/type conventions and base entity configuration
    - GUID PKs; `rowversion` on mutable aggregates; `CreatedAtUtc`/`UpdatedAtUtc` as `datetimeoffset`; money `decimal(19,4)`, rates `decimal(19,6)`, dimensions `decimal(12,4)`; whole-unit stock quantities, fractional costing quantities; USD rounding at transaction boundaries centralized in a `MoneyRounding` policy type.
    - Enforce FK constraints and **no cascade-delete** for ledger/sale/costing/production history.
    - _Requirements: A2 (5.3, 5.4, 5.5, 5.6, 5.7)_
  - [x] 3.2 Configure the implemented Catalog entities (R01/R10)
    - Product, ProductVariant (unique SKU, filtered-unique Barcode, TrackingMode, UnitOfMeasure, dimensions+unit/diameter, prices, CasePack, CareProfileId), ProductPiece (stable immutable Id, filtered-unique PieceCode, actual dims/finish/story/status, CareProfileVersionId, designed-only ProductionBatchLineId link, PublicationState default Draft), WoodSpecies, VariantWood/PieceWood (composite PK, optional Proportion), CareProfile, CareProfileVersion (immutable), ChannelListing, ProductExternalRef/VariantExternalRef (typed, unique per owner).
    - Separate Public_Ready_Field columns from Internal_Field columns so neither overwrites the other.
    - _Requirements: R01 (1.1, 1.6, 1.9, 1.11, 1.12), R10 (2.1, 2.2, 2.3, 2.8)_
  - [x] 3.3 Configure cross-cutting + integration + identity/admin entities
    - AuditEntry, IdempotencyRecord; MediaAsset + ProductMedia/VariantMedia/PieceMedia explicit FK joins (no polymorphic links); IntegrationConnection, EntityMapping (unique Platform/Account/ExternalId), IntegrationInbox (unique DeliveryKey), IntegrationOutbox (unique OperationKey), SyncCheckpoint, SyncRun/SyncException, ExternalOrder/ExternalOrderLine/OrderReservation; BusinessSettings (timezone America/Los_Angeles, rates, units, USD, size-limit thresholds).
    - _Requirements: A4 (6.7, 6.12, 6.13), R11 (3.1, 3.4), A2 (5.3)_
  - [x] 3.4 Configure designed-only later-phase entities (schema only)
    - Production (ProductionWorkflow/WorkflowStage, ProductionBatch/BatchLine/StageHistory), Inventory (Location, InventoryMovement/Line, InventoryBalance, InventoryAllocation), Markets (Event, PackingTemplate/Item, EventPackingItem/Equipment, EventMedia), Sales (Sale/SaleLine, SaleReturn/ReturnLine, EventExpense/EventReconciliation), Costing (CostEstimate/Line), Marketing/Wholesale (ListingTask, WholesaleAccount/Contact, Interaction/FollowUp), ChannelStockPolicy/ChannelAllocation, PublishingDraft/Approval/Result.
    - Provide additive extension seams (nullable optional-FK columns, documented join seams) for future CustomerIdentity/PieceOwnership/CareReminderPreference/Reservation/CustomOrderRequest — **no empty workflow tables, no endpoints**.
    - _Requirements: R10 (2.9), A2 (5.1)_
  - [x] 3.5 Create index strategy and the initial EF Core migration for SQL Server 2025 Express
    - Unique/filtered-unique indexes (SKU, Barcode, PieceCode, EntityMapping triple, Inbox DeliveryKey, Outbox OperationKey, external-ref uniqueness); covering indexes for hot Phase A lookups; GUID key strategy avoiding avoidable fragmentation.
    - Generate the initial migration; configure migrations to apply as an explicit **deployment step** (no auto-migrate on startup).
    - _Requirements: A2 (5.1, 5.2, 5.6)_
  - [x] 3.6 Write integration tests for schema constraints against real SQL Server
    - Against isolated SQL Server 2025 Express/Developer using **actual migrations** (not EF InMemory): unique SKU/piece-code/barcode, no-cascade-delete of history, FK enforcement, `rowversion` presence, decimal precision.
    - _Requirements: A2 (5.1, 5.3, 5.6, 5.7)_

- [x] 4. Implement Identity, owner bootstrap, and role policies (A2)
  - [x] 4.1 Configure ASP.NET Core Identity (staff accounts only) and Owner/Staff roles
    - Identity tables + AppUser; no public registration; two roles (Owner, Staff).
    - _Requirements: A2 (5.9, 5.13)_
  - [x] 4.2 Implement one-time Owner bootstrap with no default production password
    - Idempotent provisioning path creating the Owner only when none exists; refuses a baked-in default password.
    - _Requirements: A2 (5.8)_
  - [x] 4.3 Implement API authorization policy handlers and field-level allowlist projection
    - Policy handlers for Owner-only vs Staff-accessible operations; application use cases build DTOs from explicit **public/role field allowlists** so financial projections are stripped server-side (not merely hidden in UI).
    - Validate DI lifetimes: no singleton captures a scoped DbContext.
    - _Requirements: A2 (5.10, 5.11, 5.14)_
  - [x] 4.4 Write use-case tests for authorization projection and bootstrap
    - Staff projection strips unit cost/margin/profit; bootstrap creates Owner once and is safe to re-run.
    - _Requirements: A2 (5.8, 5.11)_

- [x] 5. Checkpoint — foundation builds and schema verified
  - Ensure the solution builds with warnings-as-errors, `dotnet format --verify-no-changes` passes, migrations apply to a fresh SQL Server, and all tests pass. Ask the user if questions arise.
  - _Requirements: A1, A2_

- [x] 6. Implement A4 cross-cutting runtime foundations (A4, A1)
  - [x] 6.1 Implement ProblemDetails mapping with stable error code + correlation ID
    - Exception/Result → RFC ProblemDetails middleware mapping statuses (400/401/403/404/409/412/413/429/500); never leak stack traces, SQL messages, or secrets; attach correlation ID; structured logging without secrets/PII.
    - _Requirements: A1 (4.8), A6 (8.5)_
  - [x] 6.2 Write property test for ProblemDetails error shape
    - **Property 24: Every error response is a ProblemDetails with a stable code**
    - **Validates: Requirements 4.8**
  - [x] 6.3 Implement the idempotency pipeline and `IIdempotencyStore`
    - `BeginAsync`/`CompleteAsync` with key + caller + operation + payload hash persisted; same key + same hash replays stored result with no repeated effect; same key + different hash → 409; concurrent same-key serialized via DB uniqueness.
    - _Requirements: A4 (6.2, 6.3, 6.4)_
  - [x] 6.4 Write property test for idempotent replay
    - **Property 13: Idempotent command replay**
    - **Validates: Requirements 6.2, 6.3, 6.4**
  - [x] 6.5 Implement ETag/If-Match optimistic concurrency helpers
    - Expose `rowversion` as ETag on reads; require `If-Match` on mutable-aggregate edits; conditional `UPDATE ... WHERE rowversion = @ifMatch`; stale → 412 refresh prompt, never silent overwrite.
    - _Requirements: A4 (6.5, 6.6)_
  - [x] 6.6 Write property test for stale-edit protection
    - **Property 14: Stale edit never overwrites**
    - **Validates: Requirements 6.5, 6.6, 7.6**
  - [x] 6.7 Implement `IAuditSink` + EF SaveChanges interceptor
    - Record AuditEntry (actor, action, entity, timestamp, permitted change summary) within the same transaction as the mutation, including background-worker system-actor mutations; never store secrets/unnecessary PII.
    - _Requirements: A2 (5.12), A4 (6.7), A7 (9.6)_
  - [x] 6.8 Write property test for audit coverage
    - **Property 15: Every business mutation is audited**
    - **Validates: Requirements 5.12, 6.7, 9.6**
  - [x] 6.9 Implement `IFileStore` local adapter with media validation and authorized access
    - Local file store **outside the web root**; randomized unique storage keys; upload validation: MIME allowlist + magic-byte signature match, reject executable/HTML, enforce 20 MB image / 200 MB video bounds; authorized downloads only — public-approved metadata never grants anonymous access.
    - _Requirements: A4 (6.8, 6.9, 6.10, 6.11)_
  - [x] 6.10 Write property test for media upload validation
    - **Property 17: Media upload validation**
    - **Validates: Requirements 6.9, 6.10**
  - [x] 6.11 Implement generic paging/filter/sort primitives for GET lists
    - `Page<T>` with default size 50, max 200 clamp, stable sort, total count.
    - _Requirements: A4 (6.1)_
  - [x] 6.12 Write property test for paging clamp and total count
    - **Property 16: Paging clamp and total count**
    - **Validates: Requirements 6.1**
  - [x] 6.13 Write integration tests for idempotency uniqueness, ETag concurrency, and audit transactionality on real SQL Server
    - Idempotency-key DB uniqueness under concurrent same-key requests; `If-Match` 412 path; audit written iff mutation commits (rollback leaves no audit).
    - _Requirements: A4 (6.2, 6.5, 6.7)_

- [x] 7. Implement A3 catalog pure domain policies (R01, R10)
  - [x] 7.1 Implement `SkuPolicy` and piece-code uniqueness validation
    - Pure validation of a new variant/piece against existing business codes; rejects collisions with duplicate-conflict code; accepts non-colliding valid records; leaves state unchanged.
    - _Requirements: R01 (1.1, 1.2, 1.7)_
  - [x] 7.2 Write property test for unique business code enforcement
    - **Property 1: Unique business code enforcement for catalog records**
    - **Validates: Requirements 1.1, 1.2, 1.7**
  - [x] 7.3 Implement `DimensionPolicy`
    - Accept iff every supplied dimension is a non-negative decimal (≤4 dp) with an explicit unit; round products may supply diameter in place of length/width.
    - _Requirements: R01 (1.3, 1.4)_
  - [x] 7.4 Write property test for dimension validation
    - **Property 2: Dimension validation**
    - **Validates: Requirements 1.3, 1.4**
  - [x] 7.5 Implement wood-composition proportion validation
    - Accept iff every supplied proportion is within 0–100 inclusive; support multiple WoodSpecies relationships; reject otherwise leaving record unchanged.
    - _Requirements: R01 (1.5), R10 (2.2)_
  - [x] 7.6 Write property test for wood proportion validation
    - **Property 3: Wood composition proportion validation**
    - **Validates: Requirements 1.5, 2.2**
  - [x] 7.7 Implement publication-default and no-auto-publish rule
    - New Product/Variant/Piece default to Draft; a produced piece's story stays unpublished absent an explicit publish action.
    - _Requirements: R01 (1.12), R10 (2.6, 2.7)_
  - [x] 7.8 Write property test for Draft default and no automatic publication
    - **Property 4: Draft default and no automatic publication**
    - **Validates: Requirements 1.12, 2.6, 2.7**
  - [x] 7.9 Implement `TrackingModeChangePolicy`
    - Permit change iff no stock history or a designed migration applied; otherwise reject leaving tracking mode and stock history unchanged.
    - _Requirements: R01 (1.13)_
  - [x] 7.10 Write property test for tracking-mode change guard
    - **Property 5: Tracking-mode change guard**
    - **Validates: Requirements 1.13**
  - [x] 7.11 Implement archived-master-data transaction guard
    - Reject new transactions against archived Product/Variant/Piece with an archived-record error; keep existing historical references readable.
    - _Requirements: R01 (1.8)_
  - [x] 7.12 Write property test for archived rejection
    - **Property 6: Archived master data rejects new transactions**
    - **Validates: Requirements 1.8**
  - [x] 7.13 Implement `CareVersionResolver` and append-only versioning rule
    - Each care edit creates a new immutable CareProfileVersion; prior versions never modified/deleted; a produced piece always resolves the version in effect at its production time.
    - _Requirements: R10 (2.3, 2.4, 2.5)_
  - [x] 7.14 Write property tests for care versioning
    - **Property 7: CareProfileVersion immutability and append-only versioning** — Validates 2.3, 2.5
    - **Property 8: Produced-piece care-version preservation** — Validates 2.4
  - [x] 7.15 Implement `PublicProjectionPolicy`
    - Produce a public-safe projection containing only explicit-allowlist fields and never any Internal_Field (costs, internal/production notes, contact info, wholesale notes); never serialize an internal entity wholesale.
    - _Requirements: R01 (1.11), R10 (2.10, 2.11)_
  - [x] 7.16 Write property test for public projection safety
    - **Property 9: Public projection excludes internal fields**
    - **Validates: Requirements 1.11, 2.10, 2.11**

- [x] 8. Implement A3 catalog use cases and persistence (R01, R10)
  - [x] 8.1 Define Application boundary interfaces for catalog
    - `ICatalogWriter`, `ICatalogReader`, `ICareProfileStore` (append-only versions); all I/O methods take/propagate `CancellationToken`, return typed `Result`/`Result<T>`, and use injected `TimeProvider`.
    - _Requirements: R01 (1.1, 1.6), R10 (2.3)_
  - [x] 8.2 Implement catalog use cases orchestrating domain policies
    - Create/archive variant, create piece, record wood composition, record ChannelListing (require channel/external id/status; reference-only, no sync while no channel enabled), add care-profile version — each applying the pure policies, writing audit, and enforcing idempotency/ETag on mutations.
    - _Requirements: R01 (1.1, 1.6, 1.8, 1.9, 1.10, 1.13), R10 (2.3, 2.4, 2.5)_
  - [x] 8.3 Implement Infrastructure EF Core persistence for the catalog contracts
    - Implement `ICatalogWriter`/`ICatalogReader`/`ICareProfileStore` with parameterized EF queries; never leak `IQueryable`/DbContext/EF entities across the boundary (DTOs only).
    - _Requirements: R01 (1.1), A2 (5.1)_
  - [x] 8.4 Write use-case tests with controlled substitutes
    - Success/rejection/boundary/cancellation cases using fakes + injected `TimeProvider`.
    - _Requirements: R01 (1.1, 1.2, 1.8, 1.13), R10 (2.4, 2.5)_
  - [x] 8.5 Write integration tests for catalog persistence on real SQL Server
    - Create/archive/version flows via actual migrations; duplicate-code DB rejection; care-version immutability enforced at the row level.
    - _Requirements: R01 (1.1, 1.2, 1.7, 1.8), R10 (2.3, 2.5)_

- [ ] 9. Implement A3 catalog, media, and administration API endpoints (R01, R10, A2, A4)
  - [x] 9.1 Implement `/api/v1` catalog endpoints with Contracts DTOs
    - `GET/POST /products`, `GET/PATCH /products/{id}`, `/variants`, `/pieces`, `/wood-species`, `/care-profiles` + versions; thin endpoints mapping to use cases; paging/filter/stable-sort/total-count; `Idempotency-Key` on mutations; ETag/`If-Match` on edits; versioned request/response DTOs with no persistence entities; timestamps with offsets, explicit units/currency, no EF graph serialization.
    - _Requirements: R01 (1.1, 1.6, 1.9), A4 (6.1, 6.2, 6.5), A8 (10.3), A1 (4.3)_
  - [x] 9.2 Implement media endpoints
    - `POST /media` (validated upload via IFileStore), `GET /media/{id}` (authorized download), metadata endpoints; reject anonymous access to public-approved files.
    - _Requirements: A4 (6.8, 6.9, 6.10, 6.11)_
  - [x] 9.3 Implement administration/auth endpoints and wire the composition root
    - `/settings`, `/users`, `/audit`; login/logout, CSRF token, one-time Owner bootstrap; register all catalog/media/foundation implementations in `AddPenguinPlankServices`.
    - Browser auth: secure HttpOnly same-origin cookie, CSRF on mutations, no bearer token in browser storage; define an auth abstraction enabling future OIDC+PKCE for mobile.
    - _Requirements: A2 (5.8, 5.9), A5 (7.1, 7.2, 7.3)_
  - [x] 9.4 Enforce field-level Owner/Staff authorization and deny unauthenticated private reads
    - Apply policy handlers + allowlist projection on every catalog/media/admin response; deny unauthenticated access to private product/contact/media/event resources regardless of publication metadata.
    - _Requirements: A2 (5.10, 5.11), A5 (7.4)_
  - [x] 9.5 Write property tests for authorization and unauthorized access
    - **Property 11: Staff never receives financial data or owner/credential controls** — Validates 5.10, 5.11, 9.5
    - **Property 12: Unauthorized callers are denied private data** — Validates 6.11, 7.4, 9.4
  - [ ] 9.6 Write integration tests for acceptance scenarios #10, #11, #12
    - #10 Staff cannot obtain unit costs/profits via any endpoint or field; #11 unauthenticated callers denied private data; #12 care-version preserved + public content excludes internal notes.
    - _Requirements: A2 (5.11), A5 (7.4), R10 (2.4, 2.11)_

- [ ] 10. Checkpoint — catalog vertical slice complete
  - Ensure all unit/property/integration tests pass, build is clean with warnings-as-errors, and format verification passes. Ask the user if questions arise.
  - _Requirements: R01, R10, A2, A4_

- [ ] 11. Implement A4a integration foundation stores (R11) — connectors disabled
  - [ ] 11.1 Implement `IIntegrationConnectionStore` and `ICredentialStore` indirection
    - Record connection (platform, account/shop identity, scopes, API version, enabled capabilities, sync direction, health, `Enabled=false`); credential **reference** only — secret never stored in DB/logs/responses/browser; record revocation/expiration and stop credential use.
    - _Requirements: R11 (3.1, 3.2, 3.3)_
  - [ ] 11.2 Write property test for credential secrecy
    - **Property 21: Credential secrets are never exposed**
    - **Validates: Requirements 3.2, 3.12, 8.5**
  - [ ] 11.3 Implement `IEntityMappingStore` with uniqueness and SKU-match-as-suggestion
    - Preserve platform/account/external identifiers with unique association to one internal record; reject duplicate triple; surface detected SKU matches as reviewable suggestions, never silent authoritative mappings.
    - _Requirements: R11 (3.4, 3.5)_
  - [ ] 11.4 Write property tests for mapping
    - **Property 10: EntityMapping uniqueness and identifier preservation** — Validates 3.4
    - **Property 22: SKU matches are suggestions, not silent mappings** — Validates 3.5
  - [ ] 11.5 Implement durable `IInboxStore` with dedupe-by-delivery-key
    - `TryRecordAsync` dedupes by unique DeliveryKey; durable record before acknowledgment; repeated/out-of-order keys reconcile authoritative state rather than apply repeated stock changes.
    - _Requirements: R11 (3.6, 3.8)_
  - [ ] 11.6 Write property test for inbox dedupe
    - **Property 18: Inbox dedupe by delivery key is idempotent**
    - **Validates: Requirements 3.8**
  - [ ] 11.7 Implement transactional `IOutboxStore`
    - Enqueue an outbox job in the **same transaction** as the business change that requires it (transactional outbox); job exists iff the change committed.
    - _Requirements: R11 (3.14)_
  - [ ] 11.8 Write property test for outbox atomicity
    - **Property 19: Transactional outbox atomicity**
    - **Validates: Requirements 3.14**
  - [ ] 11.9 Implement `ISyncCheckpointStore`, SyncRun/SyncException queue, and pause/resume
    - Cursor read/advance for incremental polling/backfill; exception queue for permanent mapping/permission errors; sync history exposes last success, backlog, failed items, affected resource, redacted error, retry/reconcile actions; pause stops new outgoing work while preserving receipts/mappings/history.
    - _Requirements: R11 (3.9, 3.11, 3.12, 3.13)_
  - [ ] 11.10 Write integration tests for inbox/outbox/mapping on real SQL Server
    - Delivery-key uniqueness, operation-key uniqueness, transactional outbox rollback behavior, mapping-triple uniqueness via actual migrations.
    - _Requirements: R11 (3.4, 3.8, 3.14)_

- [ ] 12. Implement Background_Worker and retry policy (R11, A7, A8)
  - [ ] 12.1 Implement bounded retry backoff policy (pure)
    - Pure function computing attempt schedule: at most configured bounded attempts, base delay non-decreasing in attempt (before jitter), respects configured rate limits; jitter and rate limits injected.
    - _Requirements: R11 (3.10)_
  - [ ] 12.2 Write property test for retry backoff
    - **Property 23: Retry backoff is bounded and increasing**
    - **Validates: Requirements 3.10**
  - [ ] 12.3 Implement the Background_Worker hosted service
    - Hosted service (not SQL Server Agent) leasing inbox/outbox items with attempt history and crash-safe lease reclaim; injected `TimeProvider`, HTTP transport abstraction, and retry scheduler; runs through application services with a recorded system `ActorContext`; connectors remain disabled so no job dispatches to a real provider.
    - _Requirements: R11 (3.15, 3.16, 3.17), A7 (9.6), A8 (10.1)_
  - [ ] 12.4 Write use-case tests for worker leasing and crash-safe reclaim
    - Lease acquisition, attempt-history increment, expired-lease reclaim, system-actor audit on worker mutations — all with injected `TimeProvider`.
    - _Requirements: R11 (3.15), A7 (9.6)_

- [ ] 13. Implement A7 webhook endpoints and integration management API (R11, A7)
  - [ ] 13.1 Implement `/api/integrations/webhooks/{provider}` endpoints
    - Validate provider signature/auth and payload size **before** acknowledgment; durably record to inbox (dedupe); reject forged/invalid-signature callbacks without recording them as authoritative; expose no business-read data anonymously.
    - _Requirements: R11 (3.6, 3.7), A7 (9.1, 9.2, 9.3, 9.4)_
  - [ ] 13.2 Write property test for forged callbacks
    - **Property 20: Forged provider callbacks are rejected**
    - **Validates: Requirements 3.7, 9.2, 9.3**
  - [ ] 13.3 Implement owner-only integration management endpoints
    - `GET/POST /integrations/connections`, `/mappings`, `/sync-runs`, `/exceptions`, `POST .../pause`, `.../resume`, `.../retry`; deny Staff access to credential-reference data and owner integration/publishing controls; wire into the composition root.
    - _Requirements: R11 (3.12, 3.13), A7 (9.5)_
  - [ ] 13.4 Write integration test for acceptance scenario #21
    - #21 Forged provider callback rejected and not processed; Staff cannot access credentials or owner integration controls.
    - _Requirements: A7 (9.3, 9.5)_

- [ ] 14. Implement A8 time/localization behavior and verify (R11, A8)
  - [ ] 14.1 Implement date-only local-date preservation across DST
    - Store/resolve event and follow-up date-only fields preserving the intended local date using America/Los_Angeles timezone rules (including DST); API timestamps returned with offsets and explicit units/currency.
    - _Requirements: A8 (10.1, 10.2, 10.3)_
  - [ ] 14.2 Write property test for date-only preservation
    - **Property 26: Date-only local-date preservation across DST**
    - **Validates: Requirements 10.2**

- [ ] 15. Checkpoint — integration foundation complete, connectors disabled
  - Ensure all tests pass, build is clean, and verify no concrete Shopify/Square/Faire/Etsy/Meta adapter exists and every connection defaults to `Enabled=false`. Ask the user if questions arise.
  - _Requirements: R11 (3.17), A7, A8_

- [ ] 16. Implement A5 Blazor WebAssembly browser shell (A5)
  - [ ] 16.1 Implement the typed API client aligned with Contracts
    - Same-origin typed HTTP client consuming `/api/v1`; no database driver/connection string/EF/SQL in Web; carries CSRF token and relies on the HttpOnly cookie session.
    - _Requirements: A5 (7.1, 7.2), A1 (4.4)_
  - [ ] 16.2 Implement login view and navigation shell
    - Login establishing the HttpOnly cookie session; nav shell with Dashboard, Products & Stock, Integrations (owner), Settings (owner); Workshop/Markets/Costing/Photos & Listings/Wholesale as Phase A placeholders.
    - _Requirements: A5 (7.1, 7.5)_
  - [ ] 16.3 Implement catalog editing views with stale-edit and connectivity-loss UX
    - Product/ProductVariant/ProductPiece editing via the typed client; changed `rowversion` surfaces a conflict + refresh prompt (If-Match 412) instead of silent overwrite; connectivity loss shows a clear unsaved/error state and **never queues business mutations**; responsive cards/detail sheets at narrow widths with keyboard access and accessible labels (aim WCAG 2.2 AA).
    - _Requirements: A5 (7.5, 7.6, 7.7, 7.8)_
  - [ ] 16.4 Write bUnit component tests
    - Catalog editing behavior and the stale-edit conflict/refresh flow (acceptance scenario #13 at the component layer).
    - _Requirements: A5 (7.6, 7.7)_
  - [ ] 16.5 Write Playwright core-workflow smoke tests
    - Login, create/edit a variant, authorized media upload/download, narrow-width catalog flow; acceptance scenario #13 end-to-end.
    - _Requirements: A5 (7.5, 7.6, 7.7), A4 (6.11)_

- [ ] 17. Implement A6 deployment, backup/restore, and A9 performance verification (A6, A9)
  - [ ] 17.1 Implement orphan/missing media detection
    - Detector reporting exactly MediaAsset metadata whose backing file is missing and exactly stored files with no referencing metadata (orphans), and nothing else.
    - _Requirements: A6 (8.7)_
  - [ ] 17.2 Write property test for orphan/missing media detection
    - **Property 25: Orphan and missing media detection**
    - **Validates: Requirements 8.7**
  - [ ] 17.3 Author deployment, backup, and restore baseline (config + scripts + runbook)
    - Private HTTPS deployment with secure cookies and a least-privilege DB account (no `db_owner` at runtime); nightly SQL + media backup via the **OS scheduler** (no SQL Server Agent) with an off-host protected copy; back up relational metadata and media together; document ≤24h RPO / ≤4h RTO; capacity warnings at 70%/85% of the Express size limit; rate limiting on login and costly operations.
    - _Requirements: A6 (8.1, 8.2, 8.3, 8.5, 8.6, 8.7)_
  - [ ] 17.4 Write restore-to-fresh-environment smoke test (acceptance scenario #14)
    - Restore DB + media to a fresh environment via actual migrations and complete a read/write smoke check.
    - _Requirements: A6 (8.3, 8.4)_
  - [ ] 17.5 Implement a seedable catalog performance dataset and latency verification harness
    - Representative seed (toward 5,000 variants / 50,000 pieces) and a repeatable harness measuring catalog API read/mutation latency under 10 concurrent users against real SQL Server; document the Reference_Environment hardware, dataset, workload, and measurement method; assert documented targets (p95 reads <500 ms, mutations <1 s, excluding media/external network).
    - _Requirements: A9 (11.1, 11.2, 11.3, 11.4)_

- [ ] 18. Final checkpoint — Phase A acceptance
  - Ensure the full solution builds with warnings-as-errors, `dotnet format --verify-no-changes` passes, all unit/property/integration/component/E2E tests pass, acceptance scenarios #10–#14 and #21 are covered, migrations apply cleanly to a fresh SQL Server, and the restore smoke check succeeds. Ask the user if questions arise.
  - _Requirements: R01, R10, R11, A1–A9_

## Notes

- Tasks marked with `*` are optional (tests/validation) and can be skipped for a faster MVP; all non-`*` tasks are required implementation/configuration.
- Each task references the specific requirement clauses it satisfies for traceability.
- All 26 correctness properties from the design are mapped to dedicated property-test sub-tasks (Properties 1–26), each validated by a single FsCheck/CsCheck test at ≥100 iterations tagged `Feature: penguin-plank-phase-a, Property {n}`.
- Integration tests run against **real SQL Server 2025 Express/Developer using actual migrations** — EF InMemory is never used to prove relational correctness.
- Phase A implements only Catalog, Integrations (connectors disabled), and Identity/Administration use cases; all other Section 6 entities are designed-only schema (configured + migrated, no services/endpoints/UI).
- Acceptance scenarios covered: #10 (task 9.6), #11 (task 9.6), #12 (task 9.6), #13 (tasks 16.4/16.5), #14 (task 17.4), #21 (task 13.4).

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4"] },
    { "id": 2, "tasks": ["2.1", "2.2"] },
    { "id": 3, "tasks": ["2.3", "3.1"] },
    { "id": 4, "tasks": ["3.2", "3.3", "3.4"] },
    { "id": 5, "tasks": ["3.5"] },
    { "id": 6, "tasks": ["3.6", "4.1"] },
    { "id": 7, "tasks": ["4.2", "4.3"] },
    { "id": 8, "tasks": ["4.4"] },
    { "id": 9, "tasks": ["6.1", "6.3", "6.5", "6.7", "6.9", "6.11"] },
    { "id": 10, "tasks": ["6.2", "6.4", "6.6", "6.8", "6.10", "6.12", "6.13"] },
    { "id": 11, "tasks": ["7.1", "7.3", "7.5", "7.7", "7.9", "7.11", "7.13", "7.15"] },
    { "id": 12, "tasks": ["7.2", "7.4", "7.6", "7.8", "7.10", "7.12", "7.14", "7.16"] },
    { "id": 13, "tasks": ["8.1"] },
    { "id": 14, "tasks": ["8.2", "8.3"] },
    { "id": 15, "tasks": ["8.4", "8.5"] },
    { "id": 16, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 17, "tasks": ["9.4"] },
    { "id": 18, "tasks": ["9.5", "9.6"] },
    { "id": 19, "tasks": ["11.1", "11.3", "11.5", "11.7", "11.9"] },
    { "id": 20, "tasks": ["11.2", "11.4", "11.6", "11.8", "11.10", "12.1", "14.1"] },
    { "id": 21, "tasks": ["12.2", "12.3", "14.2"] },
    { "id": 22, "tasks": ["12.4", "13.1", "13.3"] },
    { "id": 23, "tasks": ["13.2", "13.4"] },
    { "id": 24, "tasks": ["16.1"] },
    { "id": 25, "tasks": ["16.2", "16.3"] },
    { "id": 26, "tasks": ["16.4", "16.5", "17.1", "17.3", "17.5"] },
    { "id": 27, "tasks": ["17.2", "17.4"] }
  ]
}
```
