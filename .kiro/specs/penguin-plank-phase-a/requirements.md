# Requirements Document

## Introduction

This document defines the Phase A requirements for the Penguin Plank private business application: the foundation and shared catalog release. Penguin Plank is a small handmade hardwood home-goods business operated alongside full-time employment. The application is delivered first through a responsive web browser, backed by an independent HTTPS JSON API, so that a future native mobile client can reuse the same business rules without duplication.

Phase A establishes the solution structure, data persistence, authentication and authorization, the shared product catalog and customer-ready foundation, the audit/concurrency/idempotency/media infrastructure, the integration foundation (with all concrete platform connectors disabled), the browser shell, and the deployment/backup/restore baseline. Phase A delivers requirements **R01** (Catalog and shared product data), **R10** (Shared customer-ready foundation), and **R11** (Integration foundation and operational controls), plus the foundational infrastructure described in plan items A1, A2, A4, A4a, A5, and A6.

The complete business domain data model (all entities in the specification, Section 6) is designed in Phase A so that later phases (R02–R09 operational, costing, marketing, wholesale, dashboard; and R12–R15 integrations) do not force a replacement schema. Where that is relevant, later-phase requirement IDs are referenced to indicate that the Phase A schema must leave room for them. This document does **not** define acceptance criteria for those unimplemented later-phase features.

The technology baseline is .NET 10 LTS, ASP.NET Core 10, EF Core 10, Blazor WebAssembly, and SQL Server 2025 Express, hosted as a private HTTPS application. All browser (and future mobile) business data operations are served exclusively by the API.

### Scope exclusions (no Phase A requirements are written for these)

- Customer UI, customer authentication, customer data collection, public QR pages, shopping, reservations, custom orders, and care reminders.
- Payment processing, card storage, checkout, accounting ledger, payroll, and tax filing.
- Offline write queue or synchronization engine.
- Concrete Shopify, Square, Faire, Etsy, and Meta connectors (these belong to Phases E–G); outgoing inventory publishing (Phase F); social publishing (Phase G).
- Native mobile client.
- AI-generated product descriptions or image editing.

## Glossary

- **System**: The Penguin Plank application as a whole (API, application/domain services, infrastructure, and Blazor WebAssembly browser client), unless a more specific component is named.
- **API**: The ASP.NET Core 10 HTTPS JSON service exposing business endpoints under `/api/v1` and the dedicated provider webhook endpoints under `/api/integrations/webhooks/{provider}`.
- **Browser_Client**: The Blazor WebAssembly responsive web client.
- **Owner**: A staff account role with access to all business modules, financial information, user administration, configuration, imports, and stock adjustments and reversals.
- **Staff**: A staff account role with access to catalog, production, packing, event stock, sales capture, media/listing tasks, and wholesale follow-ups, and with no access to unit cost, margin, profit information, financial reports, or user administration.
- **Actor**: The authenticated Owner, Staff user, or recorded system worker identity responsible for a business mutation.
- **Product**: A design or product family record (for example, "end-grain cutting board").
- **ProductVariant**: A sellable SKU belonging to a Product; declares a serialized or quantity-based tracking mode.
- **ProductPiece**: One physical individual item belonging to a ProductVariant, identified by a stable ID and an optional unique business piece code.
- **Tracking_Mode**: The per-variant declaration of either serialized tracking (individual pieces) or quantity-based tracking (counted units).
- **Unit_Of_Measure**: The explicit unit in which a variant's quantity is counted or measured.
- **WoodSpecies**: A configurable reference record for a species of wood (for example, walnut, maple, cherry).
- **VariantWood / PieceWood**: Many-to-many composition relationships that associate one or more WoodSpecies with a ProductVariant or ProductPiece, with optional proportions.
- **CareProfile**: A reusable care-guidance identity associated with a variant.
- **CareProfileVersion**: An immutable version of care guidance; editing care guidance creates a new version and preserves prior versions.
- **MediaAsset**: A stored media file's metadata record (storage key, MIME type, size, checksum, caption, role, sort order, and private/public-approved visibility). Media binary content is stored outside the relational database and outside the web root.
- **ChannelListing**: A record of an external listing for a variant, retaining channel, external identifier, URL, readiness/publication status, and last verified date/source.
- **ExternalReference**: A typed owner association recording a platform and external identifier, implemented with foreign keys per supported owner type rather than unchecked polymorphic identifiers.
- **Publication_State**: The lifecycle state controlling whether catalog content is public-ready; defaults to Draft.
- **Public_Ready_Field**: A field eligible for public exposure (title, description, dimensions, wood species, finish, care guidance, approved media).
- **Internal_Field**: A field that must never be exposed publicly or to unauthorized roles (costs, stock adjustments, production notes, customer contact information, wholesale notes).
- **IntegrationConnection**: A record of a connected (or connectable) external platform account, including shop/account identity, granted scopes, API version, enabled capabilities, sync direction, and connection health.
- **EntityMapping**: A record associating an external platform resource (product, variant, order, line, or location) with an internal record, preserving platform, account, and external identifiers under a unique association.
- **Credential_Reference**: A pointer to an external platform secret held encrypted or in the deployment secret store; never the secret value itself in source control, browser storage, logs, or API responses.
- **IntegrationInbox**: A SQL-backed durable store of inbound provider notifications, keyed by a unique delivery key, recorded before acknowledgment and processed asynchronously.
- **IntegrationOutbox**: A SQL-backed durable store of outgoing integration jobs created in the same database transaction as the business change that requires them.
- **Background_Worker**: An application worker or OS service that processes inbox and outbox items with leases, attempt history, and crash-safe processing; it does not rely on SQL Server Agent (unavailable in Express).
- **SyncCheckpoint**: A cursor record enabling incremental polling and backfill with periodic reconciliation.
- **SyncRun / SyncException**: Records of synchronization execution counts, last success, failures, and owner resolution history; the exception queue holds actionable permanent errors.
- **Idempotency_Key**: A caller-supplied key for a mutating command, persisted with caller, operation, and payload hash to prevent duplicate effects.
- **ETag / Row_Version**: The optimistic-concurrency token for a mutable aggregate, exposed as an HTTP ETag and required via `If-Match` for edits.
- **IFileStore**: The file-storage abstraction providing authorized upload, download, and storage-key management; local file storage initially, with a cloud object-store adapter possible later.
- **ProblemDetails**: The RFC-style structured error response carrying a stable business error code and a correlation identifier.
- **Reference_Environment**: A declared, documented hardware and dataset configuration used to measure performance targets.

## Requirements

### Requirement 1: R01 - Catalog and shared product data

**User Story:** As an Owner, I want to maintain products, variants, individual pieces, wood composition, and external listing references, so that the shared catalog accurately describes what Penguin Plank designs, makes, and sells across channels.

#### Acceptance Criteria

1. WHEN an Owner submits a ProductVariant creation request, THE System SHALL reject the request unless all of the following are present and valid: a SKU unique across existing ProductVariants, a Tracking_Mode equal to serialized or quantity, a non-empty Unit_Of_Measure, an active status that is true or false, and a Product association that references an existing non-archived Product.
2. IF a ProductVariant creation request supplies a SKU that matches an existing ProductVariant SKU, THEN THE System SHALL reject the request, leave all catalog records unchanged, and return an error response indicating a duplicate SKU conflict.
3. WHEN dimensions are recorded for a Product, ProductVariant, or ProductPiece, THE System SHALL store each dimension as a non-negative decimal value with up to four decimal places accompanied by an explicit unit, and SHALL reject the record if any dimension is negative or if the unit is absent.
4. WHERE a Product is round, THE System SHALL accept a single diameter dimension in place of length and width, applying the same non-negative decimal and explicit-unit rules defined in criterion 3.
5. WHEN a ProductVariant or ProductPiece composition is recorded, THE System SHALL support one or more WoodSpecies relationships, each with an optional proportion, and SHALL NOT require a single wood text field; IF any supplied proportion is less than 0 or greater than 100 percent, THEN THE System SHALL reject the composition and leave the record unchanged.
6. WHEN a ProductPiece is created, THE System SHALL assign a stable identifier that is unique and immutable, accept an optional business piece code that is unique across existing ProductPieces when supplied, and allow actual dimensions, finish, production date, story, and MediaAsset associations that are stored independently of the ProductPiece's ProductVariant values.
7. IF a ProductPiece is created with a business piece code that matches an existing ProductPiece business piece code, THEN THE System SHALL reject the request, leave all catalog records unchanged, and return an error response indicating a duplicate piece code conflict.
8. WHEN an Owner archives Product, ProductVariant, or ProductPiece master data, THE System SHALL retain all existing historical references to that record so they remain readable, and SHALL reject any attempt to record a new transaction against the archived record with an error response indicating the record is archived.
9. WHEN a ChannelListing is recorded for a ProductVariant, THE System SHALL retain the channel, external identifier, URL, status, last verified timestamp, and last verified source, and SHALL reject the record if the channel, external identifier, or status is absent.
10. WHILE no connected channel is configured and enabled under R11, THE System SHALL treat a ChannelListing as a stored reference only and SHALL NOT perform any channel synchronization for it.
11. THE System SHALL store Public_Ready_Field values separately from Internal_Field values for every Product, ProductVariant, and ProductPiece record so that neither set overwrites the other.
12. WHEN catalog content is created for a Product, ProductVariant, or ProductPiece, THE System SHALL set its Publication_State to Draft.
13. IF a Tracking_Mode change is requested for a ProductVariant that already has recorded stock history and no designed migration is applied, THEN THE System SHALL reject the change, leave the Tracking_Mode and stock history unchanged, and return an error response indicating the change is not permitted.

### Requirement 2: R10 - Shared customer-ready foundation

**User Story:** As an Owner, I want the shared catalog, piece, care, media, and channel foundation designed and implemented now with explicit public-safe boundaries, so that future customer features can be added without replacing the core schema or leaking internal data.

#### Acceptance Criteria

1. THE System SHALL implement a shared catalog hierarchy comprising Product, ProductVariant, and ProductPiece records, where each ProductPiece references exactly one ProductVariant and each ProductVariant references exactly one Product.
2. THE System SHALL support representing more than one WoodSpecies relationship on a single ProductVariant and on a single ProductPiece, rather than a single wood text field.
3. THE System SHALL implement a reusable CareProfile identity that owns one or more CareProfileVersion records, and once a CareProfileVersion is created THE System SHALL NOT modify or delete that CareProfileVersion.
4. WHEN a ProductPiece is produced, THE System SHALL record a reference to the CareProfileVersion in effect at the time of production, and THE System SHALL continue to resolve that same referenced CareProfileVersion for the ProductPiece after any later care guidance edit.
5. WHEN care guidance for a CareProfile is edited, THE System SHALL create a new CareProfileVersion and SHALL retain every prior CareProfileVersion unchanged.
6. WHEN production of a ProductPiece completes, THE System SHALL leave that ProductPiece story in an unpublished publication state and SHALL NOT transition it to a published state without an explicit publish action.
7. THE System SHALL set the publication state of newly created Product, ProductVariant, or ProductPiece public-facing content to Draft by default, and SHALL only expose such content externally after it is explicitly transitioned to a public-approved state.
8. THE System SHALL implement within the shared schema: approved MediaAsset metadata, publication state values, public-safe descriptions stored separately from internal notes, event publication fields, an optional sale Contact reference, and channel references.
9. THE System SHALL provide schema extension points for future CustomerIdentity, PieceOwnership, CareReminderPreference, Reservation, and CustomOrderRequest records, and SHALL NOT create empty workflow tables for those records nor expose any endpoint for those records in Phase A.
10. WHERE a response is destined for a future customer client, THE System SHALL serialize only fields on an explicit per-response allowlist and SHALL NOT serialize any internal entity wholesale.
11. WHEN public-approved content is produced for a Product, ProductVariant, or ProductPiece, THE System SHALL exclude Internal_Field values — including costs, internal notes, production notes, customer contact information, and wholesale notes — from that content.

### Requirement 3: R11 - Integration foundation and operational controls

**User Story:** As an Owner, I want the integration foundation — connection records, mappings, secret references, durable inbox/outbox, checkpoints, sync history, and an exception queue — built now with all concrete connectors disabled, so that platform connections can be added later without reworking the core and without any uncontrolled synchronization in the first release.

#### Acceptance Criteria

1. WHEN a platform IntegrationConnection is recorded, THE System SHALL store the business account or shop identity, granted scopes, API version, enabled capabilities, sync direction, and connection health.
2. WHEN external credentials are supplied, THE System SHALL store a Credential_Reference to a secret held encrypted or in the deployment secret store, and SHALL NOT store the secret value in source control, browser storage, logs, or API responses.
3. WHEN an external credential is revoked or expired, THE System SHALL record the revocation or expiration state explicitly and SHALL stop using that credential.
4. WHEN an external product, variant, order, line, or location is mapped, THE System SHALL preserve the platform, account, and external identifiers in an EntityMapping with a unique association to the internal record.
5. WHEN a SKU match between an external resource and an internal record is detected, THE System SHALL present the match as a suggestion for review and SHALL NOT apply it as a silent authoritative mapping.
6. WHEN an inbound provider notification is received, THE System SHALL validate the provider's prescribed authentication or signature, durably record the notification in the IntegrationInbox before acknowledgment, and process it asynchronously.
7. IF an inbound provider notification fails authentication or signature validation, THEN THE System SHALL reject the notification and SHALL NOT record it as an authoritative event.
8. WHEN provider notifications repeat or arrive out of order, THE System SHALL deduplicate them by delivery key and reconcile authoritative resource state rather than apply repeated stock changes.
9. WHERE provider notifications are unavailable or missed, THE System SHALL support cursor-based incremental polling and backfill using a SyncCheckpoint with periodic reconciliation.
10. IF an external call fails transiently, THEN THE System SHALL retry within a bounded limit using backoff with jitter while respecting provider rate limits.
11. IF an external operation fails with a permanent mapping or permission error, THEN THE System SHALL route the item to the actionable SyncException exception queue.
12. WHEN an Owner views sync history, THE System SHALL show the last success, backlog, failed items, affected resource, redacted error, available retry or reconcile action, and pause and resume controls.
13. WHEN an Owner pauses an IntegrationConnection, THE System SHALL stop new outgoing work and SHALL preserve existing receipts, EntityMapping records, and history.
14. WHEN a business change requires an outgoing integration job, THE System SHALL create the IntegrationOutbox job in the same database transaction as the business change.
15. THE Background_Worker SHALL process IntegrationInbox and IntegrationOutbox items using leases, attempt history, and crash-safe processing, without relying on SQL Server Agent.
16. THE System SHALL expose integration capabilities through focused application boundary interfaces with injected HTTP transport, clock, and retry scheduling, and SHALL keep provider-specific payloads out of the Domain layer.
17. WHILE Phase A is the deployed release, THE System SHALL keep all concrete Shopify, Square, Faire, Etsy, and Meta connectors disabled.

### Requirement 4: A1 - Solution structure and conventions

**User Story:** As a developer, I want a consistent solution structure, build/CI pipeline, configuration approach, and error conventions, so that all later phases build on stable, testable foundations.

#### Acceptance Criteria

1. THE System SHALL be organized into the projects PenguinPlank.Api, PenguinPlank.Application, PenguinPlank.Domain, PenguinPlank.Infrastructure, PenguinPlank.Contracts, PenguinPlank.Web, and separate UnitTests, IntegrationTests, and BrowserTests projects.
2. THE PenguinPlank.Domain project SHALL NOT depend on EF Core, ASP.NET Core, UI, HTTP, or file I/O infrastructure.
3. THE PenguinPlank.Contracts project SHALL contain versioned request and response DTOs and SHALL NOT contain persistence entities.
4. THE Browser_Client SHALL NOT contain a database driver, connection string, EF context, or direct SQL access, and SHALL perform all business data operations through the API.
5. THE System SHALL expose module boundaries for Catalog, Integrations, and Identity/Administration and SHALL perform cross-module business commands through explicit services rather than shared mutable state.
6. WHEN a build runs in CI, THE System SHALL treat compiler and analyzer warnings as errors for authored projects and SHALL verify formatting with `dotnet format --verify-no-changes`.
7. THE System SHALL provide a committed root `.editorconfig` and central build configuration enabling nullable reference types, .NET analyzers, and the project naming rules.
8. WHEN an API operation produces an error, THE System SHALL return a ProblemDetails response carrying a stable business error code and a correlation identifier.

### Requirement 5: A2 - Persistence, identity, and authorization foundation

**User Story:** As an Owner, I want an Express-compatible database schema, migrations, staff identity, a secure owner bootstrap, and enforced role policies, so that the application has a safe, correctly constrained data and access foundation.

#### Acceptance Criteria

1. THE System SHALL define the EF Core schema and migrations to run on SQL Server 2025 Express with media files stored outside the relational database.
2. WHEN migrations are applied, THE System SHALL apply them as a reviewed deployment step and SHALL NOT automatically migrate the production database on application startup.
3. THE System SHALL assign uniqueidentifier primary keys, maintain unique business codes separately, use rowversion for mutable aggregates, and record CreatedAtUtc and UpdatedAtUtc as datetimeoffset values.
4. THE System SHALL store financial values as decimal(19,4), rates as decimal(19,6), and dimensions as decimal(12,4), and SHALL round USD amounts to cents at transaction boundaries.
5. THE System SHALL store stock quantities as whole units and SHALL allow fractional costing material quantities.
6. THE System SHALL enforce foreign keys, unique SKU, piece code, and barcode where supplied, nonnegative balances, positive transaction quantities, valid date ranges, and legal state transitions.
7. THE System SHALL NOT cascade-delete ledger, sale, costing, or production history.
8. WHEN the application is first provisioned, THE System SHALL create the Owner account through a one-time setup process without a default production password.
9. THE System SHALL authenticate staff accounts only and SHALL NOT provide public registration.
10. THE System SHALL enforce Owner and Staff permissions in the API, including field-level financial projections, independently of any browser control visibility.
11. WHEN a Staff user requests unit cost, margin, profit, or financial report data through any endpoint or field, THE System SHALL deny access to that data.
12. WHEN a business mutation is recorded, THE System SHALL record the Actor and timestamp.
13. THE System SHALL keep future customer identities separate from staff permissions and SHALL treat a Contact as a business record rather than a login.
14. WHEN a singleton service is registered, THE System SHALL NOT allow it to capture a scoped database context.

### Requirement 6: A4 - Audit, concurrency, idempotency, and media access

**User Story:** As an Owner, I want audit records, optimistic concurrency, idempotent commands, and an authorized media store, so that business mutations are safe, traceable, and resistant to duplication and stale overwrites.

#### Acceptance Criteria

1. WHEN an API GET list is requested, THE System SHALL support filters and stable sorting, apply a default page size of 50 and a maximum page size of 200, and return a total count.
2. WHEN a mutating API command is requested, THE System SHALL require an Idempotency_Key and SHALL persist the key with the caller, operation, and payload hash.
3. IF a request reuses an existing Idempotency_Key with a changed payload hash, THEN THE System SHALL reject the request as a conflict.
4. WHEN the same Idempotency_Key is reused with an identical payload, THE System SHALL return the original result without repeating the effect.
5. THE System SHALL expose the Row_Version of a mutable aggregate as an HTTP ETag.
6. WHEN an edit to a mutable aggregate is requested, THE System SHALL require an `If-Match` ETag and SHALL reject a stale version with a conflict response rather than silently overwriting state.
7. THE System SHALL record an AuditEntry capturing Actor, action, affected entity, timestamp, and a permitted change summary for business mutations.
8. THE System SHALL store MediaAsset binary content through the IFileStore abstraction outside the web root, with local file storage initially and a cloud object-store adapter possible later.
9. WHEN media is uploaded, THE System SHALL validate the file against an allowlist and signature, assign a randomized storage key, and reject executable or HTML uploads.
10. WHEN media is uploaded, THE System SHALL enforce bounded sizes of 20 MB for images and 200 MB for videos.
11. WHEN media is downloaded, THE System SHALL authorize the request, and public-approved metadata SHALL NOT make a media file anonymously accessible.
12. WHEN a MediaAsset is associated with an entity, THE System SHALL use an explicit foreign-key join table and SHALL NOT use a polymorphic EntityType/EntityId link.
13. WHEN an ExternalReference is recorded, THE System SHALL associate it to its owner through a typed foreign key per supported owner type rather than an unchecked polymorphic identifier.

### Requirement 7: A5 - Browser shell, authentication flow, and catalog editing

**User Story:** As an Owner or Staff user, I want to log in to a responsive browser shell and edit the catalog through a typed API client, so that I can manage products, variants, and pieces securely from a browser.

#### Acceptance Criteria

1. WHEN a user authenticates in the Browser_Client, THE System SHALL establish a secure HttpOnly same-origin session cookie and SHALL NOT store a bearer token in browser storage.
2. WHEN the Browser_Client submits a mutation, THE System SHALL require CSRF protection for that request.
3. THE System SHALL define an authentication abstraction permitting a future mobile client to use a standards-based OIDC authorization-code flow with PKCE under the same staff policies.
4. WHEN an unauthenticated caller requests a private product, contact, media, or event resource, THE System SHALL deny access.
5. THE Browser_Client SHALL provide a navigation shell, a login view, and catalog editing views for Product, ProductVariant, and ProductPiece records through a typed API client.
6. WHEN a Browser_Client edit targets a mutable aggregate whose version has changed, THE System SHALL surface a conflict and a refresh prompt rather than overwrite the current state.
7. WHERE the browser displays catalog data at a narrow width, THE System SHALL present cards or detail sheets with preserved keyboard access and accessible labels.
8. WHEN connectivity is lost, THE Browser_Client SHALL show a clear unsaved or error state and SHALL NOT queue business mutations for later submission.

### Requirement 8: A6 - Deployment, backup, and restore baseline

**User Story:** As an Owner, I want a documented deployment, backup, and restore baseline with verified permissions and SQL constraints, so that the application can be run privately and recovered after a failure.

#### Acceptance Criteria

1. THE System SHALL be deployable as a private HTTPS application with secure cookies and a least-privilege database account.
2. THE System SHALL schedule nightly SQL and media backups through the OS scheduler, since SQL Server Express has no SQL Server Agent, and SHALL retain an off-host protected copy.
3. THE System SHALL target a maximum of 24 hours of data loss and restoration within four hours, and SHALL demonstrate a backup restore to a fresh environment.
4. WHEN a database and media backup is restored to a fresh environment, THE System SHALL support completion of a read and write smoke check.
5. THE System SHALL log structured events at appropriate boundaries without recording secrets or unnecessary contact details.
6. WHEN login or a costly operation is invoked, THE System SHALL apply rate limiting.
7. THE System SHALL back up media files and relational metadata together and SHALL detect orphan or missing media files.

### Requirement 9: A7 - Provider webhook endpoints and credential protection

**User Story:** As an Owner, I want dedicated provider webhook endpoints with provider authentication and durable deduplication, and strict protection of credentials and owner controls, so that no inbound integration surface can be forged or expose private data in Phase A.

#### Acceptance Criteria

1. THE System SHALL expose provider callbacks only at dedicated `/api/integrations/webhooks/{provider}` endpoints with provider authentication, payload size limits, and durable deduplication.
2. WHEN a provider callback arrives, THE System SHALL validate the provider's authentication or signature before recording the callback.
3. IF a provider callback presents an invalid or forged signature, THEN THE System SHALL reject the callback and SHALL NOT process it.
4. THE System SHALL NOT expose business read endpoints to anonymous callers through the provider webhook surface.
5. WHEN a Staff user requests Credential_Reference data or owner publishing or integration-setup controls, THE System SHALL deny access.
6. WHEN an integration background job runs, THE System SHALL execute it through application services with a recorded system Actor context.

### Requirement 10: A8 - Time, localization, and browser support

**User Story:** As an Owner, I want correct time handling, localization, and supported browsers, so that timestamps and date-only fields are accurate and the application works on the devices used for the business.

#### Acceptance Criteria

1. THE System SHALL record instant timestamps in UTC and SHALL obtain time-dependent business behavior through TimeProvider rather than reading the system clock directly in business decisions.
2. THE System SHALL retain the intended local date for event and follow-up date-only fields and SHALL apply timezone rules, including daylight saving time, using the America/Los_Angeles business timezone.
3. THE System SHALL return API timestamps with offsets and explicit units and currency and SHALL NOT serialize EF navigation graphs.
4. THE System SHALL support Chrome, Edge, and Safari on desktop and current iOS and Android browsers, and SHALL aim for WCAG 2.2 AA for core workflows.

### Requirement 11: A9 - Phase A performance and capacity verification

**User Story:** As an Owner, I want the catalog API to meet the documented latency targets under representative load, so that Phase A performance is verified without benchmarking unimplemented later-phase workflows.

#### Acceptance Criteria

1. THE System SHALL document the planning envelope targets of 10 concurrent internal users, 5,000 variants, 50,000 ProductPiece records, and 250,000 stock lines as system targets.
2. THE System SHALL document the latency targets of p95 API reads under 500 ms and mutations under one second on a declared Reference_Environment, excluding media transfer and external network time.
3. WHEN Phase A performance is verified, THE System SHALL measure catalog API read and mutation latency with representative seeded data and 10 concurrent users and SHALL document the hardware, dataset, workload, and measurement method.
4. WHERE a workflow belongs to Phase B through Phase G, THE System SHALL defer its performance and full-system capacity validation to a later release and SHALL NOT introduce performance thresholds beyond those stated above.
