# Penguin Plank — Business Application Specification for Kiro

Version 1.1 • October 6, 2026 • Owner: Bret Dowell

## 1. Purpose and implementation directive

Build a private application for running Penguin Plank, a small handmade hardwood home-goods business operated alongside full-time employment. Deliver it first through a responsive web browser. Provide an independent, documented API that a future native mobile application can consume without duplicating business rules.

The complete business release includes production tracking, market packing, event inventory, event profitability, product costing, photo/listing readiness, and wholesale follow-ups. Implement these in phases, not as an unprioritized collection of screens.

Do not build a customer application now. Build the shared product, piece, care, media, event, contact, and sales foundations now so customer features can be added without replacing the core schema. Customer login, ownership claims, public QR pages, shopping, reservations, notifications, and custom-order workflows are deferred.

This document is a requirements and architecture input for Kiro. Generate traceable requirements.md, design.md, and tasks.md for each implementation phase. Preserve requirement IDs and business invariants. Use EARS-style acceptance criteria. Do not expand scope to customer features. Implement external integrations only in the phases and directions explicitly defined below.

## 2. Decisions, assumptions, and scope boundaries

### Required by the owner

- C# and modern .NET (the successor to .NET Core).
- SQL Server backend using a free edition suitable for production.
- Browser delivery first; future mobile client supported by the architecture.
- All application database reads and writes served by the API.
- All seven previously proposed business feature areas included.
- Shared business/customer data designed and implemented now.

### Proposed implementation defaults

These are specification choices, not previously supplied owner requirements.

| Decision | Default |
|---|---|
| Runtime | .NET 10 LTS, latest supported servicing patch; ASP.NET Core 10 and EF Core 10 |
| Browser UI | Blazor WebAssembly, responsive; same-origin hosting with API |
| Architecture | Modular monolith: one API and relational database, with module boundaries |
| Production database | SQL Server 2025 Express on a supported Windows host |
| Development/test database | Express preferred for parity; Developer edition allowed only for development/test |
| Business scope | One Penguin Plank business; multiple internal users, no multitenant SaaS |
| Currency and time | USD initially; UTC timestamps and America/Los_Angeles business timezone |
| Connectivity | Online-first; no offline writes or synchronization engine in initial release |
| Integrations | Foundation in Phase A; Shopify and Square inbound connections first, then Faire/Etsy, then controlled inventory publishing and Meta social publishing. CSV remains a fallback. |
| Deployment | Private HTTPS application; Windows/IIS reference deployment; provider selected later |
| Identity | ASP.NET Core Identity; staff accounts only; no public registration |

SQL Server 2025 Express has a 50 GB relational database limit, a buffer pool limit of 1,410 MB, and compute limited to the lesser of one socket or four cores. It has no SQL Server Agent. These are sufficient as a starting point for the proposed small-business workload when media files are stored outside SQL Server. Monitor actual usage; this is a capacity assessment, not a guarantee. Developer edition is not licensed for production. LocalDB is for local development, not the shared production service. Free database licensing does not make hosting, backups, or file storage free.

### Explicitly excluded

- Native iOS/Android clients, offline mutation queue, customer UI or customer authentication.
- Payment processing, card storage, checkout, accounting ledger, payroll, or tax filing.
- Uncontrolled bidirectional synchronization, scraping, automated email sending, or calendar synchronization. Approved channel integrations are in scope as phased below.
- General manufacturing ERP, raw-lumber inventory optimization, purchasing, and machine control.
- AI-generated product descriptions or image editing.
- Unapproved changes to existing online listings or social posts; advertising campaign management and customer messaging.

## 3. Users and authorization

| Role | Capabilities |
|---|---|
| Owner | All business modules, financial information, users, configuration, imports, stock adjustments and reversals |
| Staff | Catalog, production, packing, event stock, sales capture, media/listing tasks and wholesale follow-ups; no cost/profit reports or user administration |

Enforce permissions in the API, including field-level financial projections. Hiding browser controls is insufficient. Staff need sale prices for market operations but must not receive unit costs, margins, or private financial reports. Record actor and timestamp for business mutations.

Future customer identities must be separate from staff permissions. A Contact is a business record, not a login. A future ownership relationship will link a customer identity to a piece through a verified claim workflow; possession of a product barcode must not grant ownership.

## 4. Functional requirements and acceptance criteria

### R01 — Catalog and shared product data

Maintain Product (design/family), ProductVariant (sellable SKU), and optional ProductPiece (one physical item). Each variant declares serialized or quantity-based tracking. Cutting boards and other distinctive pieces can be serialized; conditioner, bags, and repeated small goods can use quantity tracking.

- WHEN an owner creates a variant, THE SYSTEM SHALL require a unique SKU, tracking mode, unit of measure, active status, and valid product association.
- WHEN dimensions are recorded, THE SYSTEM SHALL store decimal dimensions and an explicit unit; round products may use diameter instead of length/width.
- WHEN a variant or piece contains multiple woods, THE SYSTEM SHALL support multiple WoodSpecies relationships rather than one wood text field.
- WHEN a piece is created, THE SYSTEM SHALL assign a stable ID and unique optional business piece code, and allow actual dimensions, finish, production date, story, and media distinct from its variant.
- WHEN master data is archived, THE SYSTEM SHALL preserve historical references and prevent new transactions against it.
- WHEN an external listing is recorded, THE SYSTEM SHALL retain channel, external identifier, URL, status, and last verified date/source; synchronization is enabled only for connected channels and fields approved under R11–R15.

Public-ready fields include title, description, dimensions, wood species, finish, care guidance and approved media. Costs, stock adjustments, production notes, customer contact information, and wholesale notes are internal. Store public descriptions separately from internal notes; publication status is Draft by default.

### R02 — Workshop production

Create production batches with variant-specific lines, planned quantities, target dates, priority and configurable workflow templates. Default board stages: planned, glue-up, flatten/dimension, CNC/detailing, sanding, grain raising, final sanding, oiling, conditioning, quality check, ready. Templates may omit stages for phone stands, trays or conditioner.

- WHEN a batch starts, THE SYSTEM SHALL snapshot its workflow so later template edits do not change that batch.
- WHEN pieces in a batch progress differently, THE SYSTEM SHALL allow a line to split into sublots with independent quantities/stages; total quantities must reconcile.
- WHEN a stage changes, THE SYSTEM SHALL record prior/new stage, actor, timestamp and optional note.
- WHEN drying or curing is required, THE SYSTEM SHALL record earliest next-stage time and show the wait; owner override requires a reason.
- WHEN a batch is completed, THE SYSTEM SHALL require a quality disposition for all quantities: accepted, rework, or scrap.
- WHEN accepted output becomes ready, THE SYSTEM SHALL create serialized pieces or quantity stock receipts exactly once, in a database transaction.
- WHEN an item is scrapped or reworked, THE SYSTEM SHALL preserve history; rejected output must not become saleable stock.
- WHEN a batch stage is corrected, THE SYSTEM SHALL keep the prior history and a correction reason.

The workshop board shows overdue work, work due before upcoming markets, blocked stages, remaining quantities, and next actions. Work-in-progress is separate from finished stock.

### R03 — Market planning and packing

Maintain events with name, venue, address, timezone, start/end, setup/load-out times, application status, fees, notes, and internal/public descriptions. Support reusable packing templates.

- WHEN a packing plan is created, THE SYSTEM SHALL include products, consumables (bags, tags, conditioner), and reusable equipment (display stand, canopy, checkout equipment).
- WHEN a product is added, THE SYSTEM SHALL distinguish planned quantity, allocated quantity, and actually packed quantity.
- WHEN stock is allocated across events, THE SYSTEM SHALL reject aggregate allocations exceeding eligible available stock.
- WHEN a checklist item is checked, THE SYSTEM SHALL not silently move inventory; packing a product invokes an explicit stock-transfer command.
- WHEN equipment is marked packed, THE SYSTEM SHALL track its checklist status without treating it as saleable inventory.
- WHEN an event is canceled, THE SYSTEM SHALL release unfulfilled allocations and require any physically transferred stock to be returned explicitly.

Provide a printable checklist and a phone-friendly packing view. Clearly identify shortages, missing equipment and unfinished products.

### R04 — Inventory and event reconciliation

Use an append-only stock movement ledger and balances updated atomically. Locations include Workshop and distinct event locations; quarantine is optional. Finished stock movements include receipt, transfer, sale, return, damage, donation, and correction.

- WHEN stock transfers to an event, THE SYSTEM SHALL decrease workshop stock and increase event stock in one transaction.
- WHEN a sale is recorded, THE SYSTEM SHALL require sale date, channel, line quantity, price, discount, tax, and optional event/external reference; cash/card payment method may be recorded without card details.
- WHEN a serialized item sells, THE SYSTEM SHALL permit exactly one active sale disposition for that piece.
- WHEN insufficient stock exists, THE SYSTEM SHALL reject the sale or transfer and leave all balances unchanged.
- WHEN a return occurs, THE SYSTEM SHALL link the original sale and require restock, quarantine, or scrap disposition.
- WHEN a sale is reversed, THE SYSTEM SHALL create linked compensating records rather than delete ledger history.
- WHEN a command is retried with the same idempotency key, THE SYSTEM SHALL return the original result without repeating movements.
- WHEN an event closes, THE SYSTEM SHALL reconcile opening/transferred-in stock with sales, returns received, transfers out, donations, damage and ending count.
- WHEN actual count differs from expected count, THE SYSTEM SHALL show the variance and require an owner-approved correction with a reason.

For each variant/location: expected ending quantity = opening + receipts + transfers in + restocked returns - sales - transfers out - donations - damage + signed corrections.

Partial return-to-workshop transfers are allowed. Closing an event does not invent missing stock or assume all remaining products were returned. Owner may close with a documented variance resolution. Allocations are planning records, never stock movements.

Support Code 128/SKU and piece-code lookup from keyboard-emulating scanners. Provide manual lookup on mobile; camera scanning is deferred. Scanning identifies an item; it must not sell it immediately.

### R05 — Event profitability

- WHEN event expenses are entered, THE SYSTEM SHALL support booth fees, mileage or transport, parking, lodging, processing fees, and other categorized costs.
- WHEN a sale posts, THE SYSTEM SHALL capture its assigned unit cost snapshot, selling price, discount, tax, and fees so later pricing changes do not rewrite results.
- WHEN cost is unavailable, THE SYSTEM SHALL label profitability incomplete rather than treat unknown cost as zero.
- WHEN expense data is incomplete, THE SYSTEM SHALL label the report provisional.
- WHEN comparing events, THE SYSTEM SHALL report units sold, net merchandise revenue, sales by product, costs, contribution, and notes.

Net merchandise revenue excludes sales tax and subtracts discounts/refunded merchandise. Event contribution = net merchandise revenue - recognized cost of goods sold - event expenses - processing fees. Do not count the same fee in sale fees and expense rows. A restocked return reverses the corresponding cost; damaged/nonrestocked returns require an explicit loss treatment. Label contribution as an operational estimate, not accounting net profit.

Snapshot cost distinguishes cash/material cost from imputed labor and allocated overhead. Show cash contribution and contribution after estimated labor separately when those components are supplied. Store event work hours optionally and calculate contribution per hour only with a valid denominator. Do not double-count product labor in event labor.

### R06 — Product costing and pricing

- WHEN an owner creates a cost estimate, THE SYSTEM SHALL capture material quantities/rates, waste allowance, consumables, labor minutes/rate, packaging, and optional overhead.
- WHEN an estimate is revised, THE SYSTEM SHALL create a new version and preserve prior versions.
- WHEN margin is displayed, THE SYSTEM SHALL distinguish margin (price-cost)/price from markup (price-cost)/cost and handle zero denominators.
- WHEN a proposed retail or wholesale price is calculated, THE SYSTEM SHALL show its assumptions, channel fees, and any case-pack minimum.
- WHEN production output is received, THE SYSTEM SHALL assign an approved cost estimate snapshot or explicit actual cost to that stock; unknown cost remains flagged.

Use decimal arithmetic; never floating-point for financial values. Costing is estimation and management reporting, not tax inventory valuation. Quantity stock uses documented weighted-average cost per variant/location; serialized stock uses assigned piece cost. Transfers carry cost unchanged. Returns use the original sale cost. Owner cost adjustments are audited and must not rewrite posted sales.

### R07 — Photo and listing readiness

- WHEN a product, variant, or piece needs marketing work, THE SYSTEM SHALL track photo, detail photo, scale photo, video, description, and channel readiness tasks.
- WHEN media is uploaded, THE SYSTEM SHALL associate it with an explicit entity, role, caption, sort order, and private/public-approved visibility.
- WHEN a listing task is completed, THE SYSTEM SHALL store channel, completion date and optional external listing URL.
- WHEN readiness is displayed, THE SYSTEM SHALL show missing assets separately for Shopify, Etsy and Faire.

Checklist templates are configurable; do not hardcode a marketplace photo minimum as a permanent rule. Videos may be attached; transcoding is deferred. Platform publishing is implemented only in Phase G with approval and platform-specific validation.

### R08 — Wholesale contacts and follow-ups

- WHEN a wholesale account is created, THE SYSTEM SHALL store company, contacts, website, stage, channel, notes and optional Faire Direct/external reference.
- WHEN an interaction is recorded, THE SYSTEM SHALL store date, type, summary, sample requests and next action.
- WHEN a follow-up is scheduled, THE SYSTEM SHALL require an assignee, due date and status.
- WHEN the dashboard loads, THE SYSTEM SHALL show due and overdue follow-ups.
- WHEN a staff user records a completed follow-up, THE SYSTEM SHALL not automatically send an email or message.

Support new lead, contacted, sample requested, active retailer, dormant and closed stages. Maintain minimal person information; no customer data collection merely to complete an anonymous market sale.

### R09 — Dashboard and search

Show next markets, production deadlines, stock/packing shortages, listing backlog and wholesale follow-ups. Owner also sees event contribution and costing gaps. Search by SKU, piece code, product name, event and wholesale company. Filter/sort/paginate on the server.

### R10 — Shared customer-ready foundation

Implement now: shared catalog hierarchy, individual piece records, mixed-wood composition, versioned CareProfile/CareProfileVersion, approved media metadata, publication states, public-safe descriptions, event publication fields, optional sale contact reference, and channel references.

Each piece may reference the care version applicable at production; editing guidance creates a new version. Do not publish a piece story automatically when production completes.

Design extension points for future CustomerIdentity, PieceOwnership, CareReminderPreference, Reservation, and CustomOrderRequest. Do not create speculative empty workflow tables or implement their endpoints now. Future customer DTOs must be explicit allowlists. A customer must never receive internal entities serialized wholesale.


### R11 — Integration foundation and operational controls

Build the integration foundation in Phase A, before concrete platform connections. Keep connectors disabled until credentials, mappings and permissions are configured. No connector is required to complete the first operational release.

- WHEN a platform is connected, THE SYSTEM SHALL record the business account/shop identity, granted scopes, API version, enabled capabilities, sync direction and connection health.
- WHEN credentials are supplied, THE SYSTEM SHALL store secrets encrypted or in the deployment secret store, never in source control, browser storage, logs or API responses; handle revocation and expiration explicitly.
- WHEN an external product, variant, order, line or location is mapped, THE SYSTEM SHALL preserve platform/account/external IDs and a unique association with internal records. SKU matches are suggestions, not silent authoritative matches.
- WHEN an inbound notification arrives, THE SYSTEM SHALL validate the platform's prescribed authentication/signature, durably record it before acknowledgment and process it asynchronously.
- WHEN notifications repeat or arrive out of order, THE SYSTEM SHALL deduplicate and reconcile authoritative resource state rather than apply blind repeated stock changes.
- WHEN notifications are unavailable or missed, THE SYSTEM SHALL support cursor-based incremental polling/backfill and periodic reconciliation with checkpoints.
- WHEN an external call fails transiently, THE SYSTEM SHALL use bounded retries with backoff/jitter and respect rate limits; permanent mapping/permission errors go to an actionable exception queue.
- WHEN an owner views sync history, THE SYSTEM SHALL show last success, backlog, failed items, affected resource, redacted error, retry/reconcile action and pause/resume controls.
- WHEN a connection is paused, THE SYSTEM SHALL stop new outgoing work without deleting receipts, mappings or history.
- WHEN a mutation reaches a platform but its acknowledgment is lost, THE SYSTEM SHALL reconcile the remote result before retrying; never assume every API supports idempotency keys.

Use focused application boundary interfaces and separate Shopify, Square, Faire, Etsy and Meta infrastructure adapters. Inject HTTP transport, clock and retry scheduling; use deterministic fixtures for unit tests and supported sandboxes/test accounts for adapter tests. Do not place provider-specific payloads in Domain. Follow coding-standards.md.

Add a SQL-backed durable inbox/outbox and background worker; no broker is required. Outgoing jobs must be created in the same transaction as the business change that requires them. Use leases, attempt history and crash-safe processing; do not claim exactly-once network delivery. Express scheduling uses an application worker/OS service, not SQL Server Agent.

### R12 — Shopify and Square inbound connections

First active connectors: Shopify catalog/order import, then Square market sales.

- WHEN Shopify catalog data is imported, THE SYSTEM SHALL preview mappings and changes to products/variants, preserve external IDs and leave internal costing, production, care and piece data intact.
- WHEN a supported Shopify order/refund changes, THE SYSTEM SHALL import its economic state and map its lines without treating every order-created event as a completed sale.
- WHEN a Square transaction is imported, THE SYSTEM SHALL associate catalog lines, discounts, taxes, refunds and available fees with the correct sale and event.
- WHEN event matching is ambiguous, THE SYSTEM SHALL require review; location and local time window may suggest a match but cannot silently assign an overlapping event.
- WHEN Shopify or Square does not provide reliable fee/cost data, THE SYSTEM SHALL retain unknown/provisional status rather than fabricate profit.
- WHEN an external order lacks a matching SKU, sufficient eligible stock or a required serialized piece, THE SYSTEM SHALL queue it for reconciliation. External completed sales must remain visible even when stock posting is blocked.
- WHEN the same economic order arrives through CSV, webhook, polling, or another connected platform, THE SYSTEM SHALL use its origin/mapping to prevent duplicate sales and inventory decrements.
- WHEN an external refund arrives, THE SYSTEM SHALL not assume physical restock. Use the original sale cost and request the inventory disposition where necessary.

Model external orders independently from posted internal Sale records. Unfulfilled accepted orders reserve appropriate availability; a sale/fulfillment transition consumes stock and releases its reservation in one transaction. Document each connector's order/payment/fulfillment status mapping and cancellation behavior. Payment processing remains on the existing platform.

### R13 — Faire and Etsy inbound connections

- WHEN Faire orders are imported, THE SYSTEM SHALL map wholesale lines, case-pack quantities, retailer/account references and fulfillment status to production/availability needs.
- WHEN a retailer is matched to a wholesale account, THE SYSTEM SHALL preserve contact source and avoid overwriting manually curated notes or follow-ups.
- WHEN Etsy orders/listings are imported, THE SYSTEM SHALL map listing variations to internal variants and link listing-readiness tasks to external listings.
- WHEN a Faire/Etsy order is mirrored into Shopify or another connected service, THE SYSTEM SHALL reconcile it as one originating order using documented cross-channel identifiers.
- WHEN an API permission or required operation is unavailable, THE SYSTEM SHALL expose the limitation and retain validated CSV fallback.

Verify brand/shop access, allowed data use, supported endpoints, app registration and current API requirements before implementing each connector. Do not assume webhook availability for every platform; use supported polling when necessary.

### R14 — Controlled outgoing inventory and listing updates

Inbound-only is the default until an owner explicitly enables publishing per channel. Before enabling it, document existing Shopify/Faire/Etsy/Square bridges and disable or configure overlapping writers.

- WHEN stock publishing is enabled, THE SYSTEM SHALL designate this application as authority for eligible finished-stock availability in the configured inventory pool; avoid uncontrolled bidirectional updates.
- WHEN availability is calculated, THE SYSTEM SHALL account for quality status, location eligibility, unfulfilled order reservations, event allocations not yet packed, safety buffer and channel quota. Packing converts an allocation into physical transfer and must not deduct it twice.
- WHEN scarce or one-of-a-kind pieces are assigned, THE SYSTEM SHALL use exclusive channel allocation or quotas; never publish the same single unit independently to every channel.
- WHEN an external stock edit conflicts with authority, THE SYSTEM SHALL surface a discrepancy according to configured policy; do not silently overwrite manual edits.
- WHEN publishing updates, THE SYSTEM SHALL use an audited durable outbox, supported conditional/version checks and reconciliation to avoid feedback loops.
- WHEN a channel is stale or unavailable, THE SYSTEM SHALL show it clearly and apply the configured sales-buffer/pause policy. Do not promise cross-platform atomic stock updates or zero overselling.
- WHEN a description, price, photo or other listing change is proposed, THE SYSTEM SHALL require an owner-approved preview of the exact fields and destination. Approval does not authorize unrelated fields.
- WHEN data changes after approval, THE SYSTEM SHALL invalidate approval for the affected outgoing content.

Maintain field-level ownership: internal app controls production, costing and piece identity; imported storefront marketing/prices remain channel-controlled until explicitly selected for app publishing. Synchronize aggregate channel quantities while retaining serialized piece assignment internally. Remote adapters cannot mutate balances directly; use existing application commands and stock invariants.

### R15 — Facebook and Instagram marketing publishing

Implement after commerce connections are stable. Scope is Penguin Plank's Facebook Page and eligible Instagram professional account, not personal profiles, ads or automated customer messages.

- WHEN marketing content is prepared, THE SYSTEM SHALL link approved media, caption, destination account, platform format, planned time and related products/event.
- WHEN an owner approves publishing, THE SYSTEM SHALL capture an immutable content snapshot and per-destination approval; scheduling uses that approved snapshot.
- WHEN supported content publishes, THE SYSTEM SHALL record remote post ID/URL and per-platform success; a Facebook success must not hide an Instagram failure.
- WHEN a retry could duplicate a post, THE SYSTEM SHALL reconcile remote state or require review before repeating.
- WHEN content is unsupported or credentials expire, THE SYSTEM SHALL stop that destination and show an actionable error, retaining a manual-export option.
- WHEN a connector requires fetchable media, THE SYSTEM SHALL provide a narrowly scoped, time-limited delivery URL for only the approved asset; private files must not become broadly public.

Target supported Instagram photos, carousels and Reels and Facebook Page posts/photos; confirm exact formats, permissions, account linkage, token lifecycle, app review/access level and video/media requirements against official documentation before implementation. Keep platform restrictions in adapters/configuration, not permanent domain assumptions. Social publishing approval is a product workflow, not automatic permission to send unrelated messages.

## 5. Architecture and solution structure

Browser -> HTTPS JSON API -> application/domain services -> EF Core -> SQL Server.

Media endpoints use an independent file-store abstraction. Only metadata is relational. Authentication belongs to the API. No browser database driver, connection string, EF context, or SQL access. Future mobile clients use the same API and application services.

Suggested projects:

- PenguinPlank.Api: endpoints/controllers, identity, authorization, ProblemDetails and OpenAPI.
- PenguinPlank.Application: use cases, validation, authorization policies and orchestration.
- PenguinPlank.Domain: entities, invariants, costing and reconciliation rules.
- PenguinPlank.Infrastructure: EF Core, migrations, identity persistence, file store and audit persistence.
- PenguinPlank.Contracts: versioned request/response DTOs; no persistence entities.
- PenguinPlank.Web: Blazor WebAssembly client and typed API client.
- UnitTests, IntegrationTests, and BrowserTests.

A modular monolith is sufficient. Do not add microservices, Kubernetes, event sourcing infrastructure, a message broker, or a generic repository layer without a concrete need. The stock ledger is a business audit model, not a mandate for whole-system event sourcing.

Modules: Catalog, Production, Inventory, Markets, Costing, Marketing, Wholesale, Integrations, Identity/Administration. Modules share a database but use explicit services for cross-module business commands.

## 6. Relational data model

Use uniqueidentifier IDs with an index strategy avoiding avoidable fragmentation; unique business codes separately. Mutable aggregates use SQL rowversion for optimistic concurrency. CreatedAtUtc/UpdatedAtUtc use datetimeoffset. Financial values use decimal(19,4), rates decimal(19,6), dimensions decimal(12,4), and USD amounts round to cents at transaction boundaries. Stock quantities are whole units initially; costing material quantities may be fractional.

| Entity / group | Main fields and relationships |
|---|---|
| Product | ID, name, category, public description, internal notes, publication state, active flag |
| ProductVariant | ProductId, unique SKU, barcode, tracking mode, dimension fields/unit, finish, retail/wholesale price, case-pack, CareProfileId |
| WoodSpecies / VariantWood | Species reference; many-to-many variant composition with optional proportions |
| ProductPiece / PieceWood | VariantId, unique piece code, actual dimensions/finish, production batch line, applicable care version, story, status; actual wood composition |
| CareProfile / CareProfileVersion | Reusable care guidance identity and immutable versions |
| MediaAsset / explicit join tables | Store key, MIME type, size, checksum, caption, approval state; ProductMedia, VariantMedia, PieceMedia, EventMedia |
| ChannelListing | VariantId, channel, external ID, URL, readiness/publication status, verified timestamp |
| ExternalReference | Typed owner association, platform and external ID; implement foreign keys per supported owner rather than unchecked polymorphic IDs |
| ProductionWorkflow / WorkflowStage | Ordered template stages and expected wait durations |
| ProductionBatch / BatchLine / StageHistory | Planned/completed quantities, workflow snapshot, priority, target date, sublot parent, stage transitions, dispositions |
| Location | Type and optional EventId; unique location code |
| InventoryMovement / InventoryMovementLine | Type, date, actor, reason, related command/sale, source/destination; variant, optional piece, quantity, carried cost |
| InventoryBalance | Unique VariantId + LocationId, quantity, cost components and rowversion; ledger-reconcilable projection |
| InventoryAllocation | EventId, variant/piece, allocated quantity, fulfilled/released state; separate from physical balance |
| Event | Internal/public descriptions, venue, timezone, dates, status, setup/load-out, optional published state |
| PackingTemplate / PackingTemplateItem | Product, consumable or equipment reference, planned quantities, instructions |
| EventPackingItem / Equipment | Plan/actual/check state and reusable equipment catalog |
| Sale / SaleLine | Channel, EventId optional, external reference, ContactId optional, prices/discounts/tax/fees, cost components snapshot, movement reference |
| SaleReturn / ReturnLine | Original sale line, quantity, refund amounts, restock disposition and compensating movement |
| EventExpense / EventReconciliation | Categories, amounts, receipt media, work hours; count, expected balance, variance and closure resolution |
| CostEstimate / CostEstimateLine | Version, variant/piece scope, quantities/rates, labor, waste, overhead, approval and totals |
| ListingTask | Product/variant/piece reference, channel, task type, assignee, due date, state, completion |
| WholesaleAccount / Contact | Company and stage; reusable contact with optional company association |
| Interaction / FollowUp | Account/contact, date/type/summary, assignee, due date, status |
| AuditEntry / IdempotencyRecord | Actor, action, entity, timestamp, permitted change summary; request key/hash/result reference and expiry |
| IntegrationConnection / EntityMapping | Platform/account, capabilities/scopes/version, secret reference, health and directional field ownership; constrained external mappings |
| ExternalOrder / ExternalOrderLine / OrderReservation | Origin and mirrored IDs, payment/fulfillment state, normalized lines, posting status, stock/piece reservations and reconciliation errors |
| IntegrationInbox / IntegrationOutbox / SyncCheckpoint | Unique delivery/operation keys, payload references, cursor, lease, status, retries and retention |
| SyncRun / SyncException | Counts, last success, failures and owner resolution history |
| ChannelStockPolicy / ChannelAllocation | Pool eligibility, buffer, quotas, exclusive piece assignment and authority policy |
| PublishingDraft / PublishingApproval / PublishingResult | Content version/snapshot, destination, approved fields/media, schedule and remote result |
| Identity tables / BusinessSettings | Staff identity/roles; timezone, default rates, units, currency and configuration |

Enforce foreign keys, unique SKU/piece/barcode where supplied, nonnegative balances, positive transaction quantities, valid date ranges, and legal state transitions. Variant tracking mode cannot change after stock history exists without a designed migration. A serialized piece has one current location and cannot be simultaneously allocated or sold twice. Do not cascade-delete ledger, sale, costing, or production history.

Keep external media outside the web root. Use explicit foreign-key join tables instead of arbitrary EntityType/EntityId links for operational data. Index stock variant/location, stage/due date, event/date, follow-up assignee/status/date and external-reference uniqueness.

## 7. API contract

All business endpoints under /api/v1. OpenAPI documents verbs, DTOs, permissions, errors, examples and paging. No generic execute-SQL or unrestricted database CRUD endpoints. API coverage includes settings, reports, imports and file metadata, not just core edit forms.

| Area | Representative endpoints |
|---|---|
| Catalog | GET/POST /products; GET/PATCH /products/{id}; /variants; /pieces; /wood-species; /care-profiles and versions |
| Production | /production-batches; /production-batches/{id}/lines; POST /.../split, /transition, /complete |
| Stock | GET /inventory/balances, /inventory/movements; POST /inventory/receipts, /transfers, /adjustments |
| Events | /events; /events/{id}/allocations; /packing-items; POST /pack, /return-stock, /reconcile, /close |
| Sales | POST /sales; GET /sales/{id}; POST /sales/{id}/returns and /reversal |
| Costing/reports | /cost-estimates and /approve; GET /reports/events and /reports/events/{id} |
| Marketing | /media uploads/downloads; /listing-tasks; /channel-listings |
| Wholesale | /wholesale-accounts; /contacts; /interactions; /follow-ups |
| Integrations | /integrations/connections; /mappings; /sync-runs; /exceptions; POST /sync, /retry, /pause, /resume; /publishing-drafts and /approve |
| Administration | /settings; /users; /audit; /imports/preview and /imports/commit; /exports |

Conventions:

- GET lists: filters, stable sorting, page size default 50/max 200 and total count.
- POST creates: 201 and Location; commands: documented 200/201 result. PATCH uses purpose-specific DTOs and an expected version.
- Errors: 400 validation, 401 unauthenticated, 403 forbidden, 404 not found, 409 business conflict/idempotency mismatch, 412 stale version, 413 too-large upload, 429 throttled, 500 unexpected failure. Use ProblemDetails with stable business error code and correlation ID.
- Mutating stock, sale, completion and import commands require Idempotency-Key. Persist key + caller + operation + payload hash; a changed payload under the same key is a conflict. Same-key concurrent requests must serialize through database uniqueness.
- Expose rowversion as ETag; require If-Match for mutable aggregate edits. Show refresh/retry UX, never silent overwrite.
- Use transactions and appropriate locking/conditional updates for stock and allocations; rowversion alone is not sufficient for aggregate stock constraints.
- Return timestamps with offsets and explicit units/currency. Do not serialize EF navigation graphs.

Provider callbacks use dedicated /api/integrations/webhooks/{provider} endpoints with provider authentication, payload limits and durable deduplication. These are explicit exceptions to staff-cookie authentication; never expose business-read endpoints anonymously. Credential/setup, retry and publishing controls are owner-only. Workers use application services with recorded system actor context.

Authentication: browser uses secure HttpOnly same-origin cookies with CSRF protection for mutations; do not store bearer tokens in browser local storage. Configure narrowly scoped CORS only when required. Define an authentication abstraction so a future mobile client can use a standards-based OIDC authorization-code flow with PKCE and the same staff policies. Implement mobile authentication when the mobile client is in scope, not a custom token protocol now.

## 8. Browser experience

Navigation: Dashboard, Workshop, Products & Stock, Markets, Costing (owner), Photos & Listings, Wholesale, Integrations (owner), Settings (owner).

Use a restrained Penguin Plank presentation: clear typography, neutral backgrounds, wood photography where useful, large action targets and few decorative elements. At narrow widths use cards/detail sheets instead of wide tables; preserve keyboard access and accessible labels. Do not make drag-and-drop the only way to advance production.

Market workflow: select event -> review allocations/shortages -> pack and transfer -> find/scan item -> record sale -> count remaining stock -> return/resolve discrepancies -> close -> review contribution.

Workshop workflow: create batch -> select workflow -> progress/split -> wait timers -> quality check -> receive finished stock -> attach photos/listing tasks.

Connectivity loss shows a clear unsaved/error state; never indicate a successful sale until the API confirms it. Initial release may cache static UI assets, but must not queue business mutations or serve stale inventory as current. Printing/export must not require a desktop-only plugin.

## 9. Nonfunctional requirements and operations

- Target 10 concurrent internal users, 5,000 variants, 50,000 piece records and 250,000 stock lines as a planning envelope. Validate representative data on reference hardware, not an in-memory mock.
- Target p95 API reads under 500 ms and mutations under one second on a declared reference environment, excluding media transfer/external network. Document hardware and test conditions; tune measured bottlenecks.
- Authentication required for every business endpoint; provider callbacks have dedicated signature/token validation and no business read access. Rate-limit login and costly operations. Bootstrap owner with a one-time setup process; no default production password.
- TLS, secure cookies, explicit DTO validation, parameterized EF queries, secrets outside source control, least-privilege database account, structured logs without secrets or unnecessary contact details.
- Upload images/videos with explicit allowlist and signature validation, randomized storage keys, bounded sizes (proposed 20 MB image/200 MB video), no executable/HTML uploads, and authorized downloads. Public-approved metadata does not make a file anonymously accessible; Phase G may issue narrowly scoped time-limited URLs solely for approved publishing delivery.
- Use local file storage behind IFileStore initially, with a cloud-object-store adapter possible later. Back up files and relational metadata together; detect orphan/missing files.
- Schedule nightly SQL backups and media backups through the OS scheduler, since Express has no SQL Server Agent. Keep an off-host protected copy. Initial goals: maximum 24-hour data loss and restore within four hours; demonstrate a restore and document dependencies.
- EF migrations are versioned, reviewed and applied as a deployment step with backup; do not automatically migrate production on every startup.
- Monitor disk space, database size, failed backups, failed writes and application health. Warn at 70%/85% of edition size limit. Support export and documented upgrade to a paid SQL Server edition if needed.
- Use UTC for instant timestamps; event/follow-up date-only fields retain intended local dates. DST handling must use timezone rules.
- Support Chrome, Edge, Safari desktop and current iOS/Android browsers; aim for WCAG 2.2 AA for core workflows.

## 10. Initial data and import policy

Seed configurable wood species (walnut, maple, cherry, white oak, ash, purpleheart, jatoba, iroko, zebrawood, canarywood), categories, channels, workflow templates, locations and expense categories. These are editable references, not hardcoded enums where business changes are likely.

Use example catalog fixtures for end-grain cutting boards, crumb-catching bread boards, cheese boards, trays, phone stands, keychains and conditioner. Mark example data clearly and keep it out of production by default. Do not treat previously discussed inventory counts, event dates or prices as verified opening balances.

CSV import initially supports catalog/variants, opening stock, wholesale contacts and manual external sales. Require preview, row-level validation, duplicate detection, explicit mappings, and owner commit. Opening stock creates ledger receipts. Sales imports share the same external-order reconciliation pipeline as connectors and use channel/account/origin-order reference uniqueness and either create stock movements or reconcile a previously posted matching sale; never double-decrement stock. Reject unmatched variants/pieces and insufficient stock instead of guessing. Make a committed import atomic per documented batch and retry-safe.

## 11. Verification and release acceptance

Unit-test meaningful business rules: cost math, margin/markup, workflow output, event reconciliation and authorization policies. Integration-test against real SQL Server, including constraints, rowversion, transaction rollback and concurrency. Browser smoke tests cover critical end-to-end tasks.

Required acceptance scenarios:

1. Complete a mixed-output board batch: receive accepted pieces once, leave rework outside saleable stock, record scrap.
2. Split a batch line and advance its parts independently without changing total planned quantity.
3. Allocate stock to two markets; conflicting allocations are rejected.
4. Transfer stock to an event and sell its last unit simultaneously from two clients: exactly one sale succeeds.
5. Retry a sale/completion/import request: one economic result and one set of movements.
6. Reverse a sale and process a partial return with restock/quarantine dispositions; balances and reports reconcile.
7. Pack, sell, donate, count, return and close an event; a discrepancy requires a documented resolution.
8. Revise product cost and price after a sale; historical sale cost and event results stay unchanged.
9. Import the same external sales twice; stock and revenue are counted once.
10. Staff cannot obtain unit costs or profits by directly requesting owner endpoints or fields.
11. Unauthenticated callers cannot fetch private product, contact, media or event data.
12. Care/profile edits preserve a piece's referenced version; public-approved content excludes internal notes.
13. Stale browser edit produces a conflict and refresh prompt.
14. Restore database and media from backups to a fresh environment and complete a read/write smoke check.
15. Complete core packing/sale flows in a narrow mobile browser using accessible controls.
16. Deliver duplicated/out-of-order provider events and replay a CSV mirror: one economic sale and one stock effect.
17. Simulate timeout after remote acceptance, rate limit, revoked credentials and worker crash; recover without silent data loss or duplicate publishing.
18. Import an unmatched/oversold external order; show it in review without inventing stock or losing the commercial record.
19. Reserve, pack and publish scarce stock across channels; no double allocation deduction and no multiply advertised exclusive piece.
20. Approve a content snapshot, edit its source, and simulate partial social success; require correct approval and destination-specific recovery.
21. Reject forged provider callbacks; verify staff cannot access credentials or owner publishing controls.

Definition of done: requirements mapped to passing tests; deployable release; OpenAPI and setup guide; migration scripts; backup/restore instructions; no secrets in repository; all planned workflows functional through API; explicit known limitations.

## 12. Phased Kiro implementation plan

### Phase A — Foundation and shared catalog

- [ ] A1 Create solution, module boundaries, build/CI, configuration and error conventions.
- [ ] A2 Configure Express-compatible EF schema/migrations, Identity, owner bootstrap and role policies.
- [ ] A3 Implement products, variants, pieces, wood composition, care versions and publication fields (R01/R10).
- [ ] A4 Implement audit, ETags, idempotency foundation, storage adapter and authorized media access.
- [ ] A4a Implement connector contracts, external mappings/orders/reservations, credential references, inbox/outbox, checkpoints and exception queue (R11); concrete connectors remain disabled.
- [ ] A5 Build browser shell, login, catalog editing and API client.
- [ ] A6 Deliver deployment, backup and restore baseline; verify permissions and SQL constraints.

### Phase B — Workshop, stock and market operations

- [ ] B1 Implement stock ledger/balances and atomic command services before packing or sales UI.
- [ ] B2 Implement workflow templates, batches, split lines, quality dispositions and retry-safe completion (R02).
- [ ] B3 Implement event records, packing templates, allocations and transfers (R03).
- [ ] B4 Implement sales, returns, reversal, lookup/scanning and event reconciliation (R04).
- [ ] B5 Implement dashboard operational priorities and printable packing lists (R09).
- [ ] B6 Pass concurrency, reconciliation and mobile-browser acceptance scenarios.

Phase B is the first useful operational release. It is not completion of the full requested business scope.

### Phase C — Costing and event analysis

- [ ] C1 Implement versioned costing, cost assignment and weighted-average/serialized cost rules (R06).
- [ ] C2 Implement event expenses, cost completeness indicators and contribution reports (R05).
- [ ] C3 Add owner-only financial UI/API policies and historical-cost tests.
- [ ] C4 Add catalog/opening-stock/sales imports and report exports.

### Phase D — Marketing and wholesale

- [ ] D1 Implement media roles, listing tasks and channel readiness (R07).
- [ ] D2 Implement wholesale accounts, reusable contacts, interactions and follow-ups (R08).
- [ ] D3 Extend dashboard and wholesale import/export.
- [ ] D4 Complete regression, restore demonstration and full business release acceptance.

### Phase E — Inbound commerce integrations

- [ ] E1 Implement Shopify connection, catalog preview/mapping and order/refund reconciliation (R12).
- [ ] E2 Implement Square sales/refund import and reviewed event matching (R12).
- [ ] E3 Implement Faire wholesale order/account mapping and Etsy order/listing mapping (R13).
- [ ] E4 Verify current provider capabilities; test duplicate/mirrored orders, out-of-order events, polling/backfill and credential failures.
- [ ] E5 Retain manual/CSV workflows and release connectors independently.

### Phase F — Outgoing stock and approved listing updates

- [ ] F1 Inventory existing channel bridges and confirm authority/pool/field policies.
- [ ] F2 Implement availability, reservations, quotas, exclusive piece allocation and conflict handling (R14).
- [ ] F3 Enable audited inventory publishing per channel only after reconciliation tests pass.
- [ ] F4 Add owner-approved listing previews and platform-specific supported field updates.

### Phase G — Approved social publishing

- [ ] G1 Verify Facebook Page/Instagram professional account API eligibility and permissions.
- [ ] G2 Implement drafts, immutable approval snapshots, schedule and approved-media delivery (R15).
- [ ] G3 Implement platform-specific publishing, partial-success reporting and safe retry/reconciliation.
- [ ] G4 Deliver manual export fallback and test approval invalidation, token expiry and unsupported formats.

Phases E–G are requested extensions to the business app. They do not block Phase B or completion of the original seven-feature release in Phase D.

### Later releases — separate specifications

Native staff mobile client and its authentication; offline writes if justified; additional integrations beyond Phases E–G; customer experience and identity; verified piece ownership/QR pages; reservations; custom orders; care reminders and consent. Reuse shared data and application rules but add separate customer-safe endpoint contracts and authorization.

## 13. Kiro kickoff prompt

> Use this document as the authoritative baseline for Penguin Plank's private business application. Start with Phase A and create requirements.md, design.md, and tasks.md in a feature spec. Use .NET 10, ASP.NET Core, EF Core, Blazor WebAssembly, and SQL Server 2025 Express. All browser and future mobile business data operations must use the API. Preserve requirement IDs, the stock and financial invariants, and explicit customer scope exclusions. Design the complete business domain before implementing Phase A so later phases do not force a replacement schema. Keep each phase independently reviewable and trace requirements to tests. Identify a blocking contradiction before changing scope; otherwise use the stated defaults. Build the R11 integration foundation in Phase A; implement concrete platform connections only in Phases E–G. Do not build customer screens, payment processing, or offline synchronization. Preserve inbound-first rollout, external-order deduplication, inventory authority and owner-approved publishing. Do not treat demo inventory as real stock. Produce runnable setup instructions and SQL Server-backed integration tests, then implement the phase tasks.

## 14. Decisions to confirm before production deployment

These do not block the specification or local implementation.

1. Hosting machine/provider and budget; owner-approved storage/backup location.
2. Whether staff should see any additional financial information beyond sale prices.
3. Preferred labor/overhead rates and opening stock/cost records.
4. Whether online-only market usage is sufficient given actual cellular coverage.
5. Which external CSV formats are worth supporting first, current platform account/API eligibility, and existing channel bridges.
6. Whether the future mobile app will use .NET MAUI or another client framework.
7. Inventory authority, channel pools/buffers/quotas and owner-approved publication permissions before outgoing sync.

## 15. Technical references

Verified October 6, 2026. The architecture choices above are recommendations; these sources support product capabilities and tooling conventions.

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- SQL Server 2025 editions, licensing distinctions and limits: https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025?view=sql-server-ver17
- SQL Server 2025 changes: https://learn.microsoft.com/en-us/sql/sql-server/what-s-new-in-sql-server-2025?view=sql-server-ver17
- Kiro specs: https://kiro.dev/docs/specs/
- Kiro spec practices: https://kiro.dev/docs/specs/best-practices/

- Shopify APIs: https://shopify.dev/docs/apps/build/apis
- Shopify webhooks: https://shopify.dev/docs/api/webhooks/latest
- Square orders: https://developer.squareup.com/reference/square/orders-api
- Square webhook events: https://developer.squareup.com/docs/webhooks/v2webhook-events-tech-ref
- Faire brand API: https://faire.github.io/external-api-v2-docs/
- Etsy Open API: https://developers.etsy.com/
- Meta Instagram publishing: https://developers.facebook.com/docs/instagram-platform/content-publishing/
- Meta Pages API: https://developers.facebook.com/docs/pages-api/

Platform capabilities must be rechecked at connector implementation; unverified access or formats remain explicit capability gates.

End of specification.
