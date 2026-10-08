using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Channel;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Costing;
using PenguinPlank.Domain.IdentityAdministration;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Domain.Inventory;
using PenguinPlank.Domain.Marketing;
using PenguinPlank.Domain.Markets;
using PenguinPlank.Domain.Media;
using PenguinPlank.Domain.Production;
using PenguinPlank.Domain.Publishing;
using PenguinPlank.Domain.Sales;
using PenguinPlank.Domain.Wholesale;
using PenguinPlank.Infrastructure.IdentityAdministration;

namespace PenguinPlank.Infrastructure.Persistence;

/// <summary>
/// The single EF Core <see cref="DbContext"/> for the Penguin Plank modular monolith. It
/// establishes the global column/type conventions and the no-cascade-delete default that
/// every entity configuration inherits.
/// </summary>
/// <remarks>
/// <para>
/// EF Core lives in Infrastructure only — the Domain layer stays persistence-ignorant
/// (coding-standards §3). The Catalog entities (R01/R10) are registered as the
/// <c>DbSet</c>s below and mapped by <see cref="IEntityTypeConfiguration{TEntity}"/> types in
/// the Infrastructure Catalog folder, which <see cref="OnModelCreating"/> discovers from this
/// assembly. The cross-cutting (audit, idempotency), media, integration foundation, and
/// identity/administration entities are registered as the <c>DbSet</c>s below (task 3.3);
/// the remaining designed-only later-phase entities are added by task 3.4. The context derives
/// from <see cref="IdentityDbContext{TUser,TRole,TKey}"/> with <see cref="AppUser"/>,
/// <see cref="IdentityRole{TKey}"/>, and a <see cref="Guid"/> key, so the ASP.NET Core Identity
/// staff-account and role tables live in this same database and migration chain (task 4.1). Every
/// configuration builds on the shared
/// <em>conventions</em> established here:
/// </para>
/// <list type="bullet">
///   <item><description>GUID primary keys and <c>datetimeoffset</c> audit timestamps on every <see cref="Entity"/>.</description></item>
///   <item><description>A SQL <c>rowversion</c> concurrency token on every mutable aggregate (<see cref="VersionedEntity"/>).</description></item>
///   <item><description>Decimal precision defaults aligned with <see cref="DecimalPrecision"/> so money never falls back to a lossy scale.</description></item>
///   <item><description><see cref="DeleteBehavior.Restrict"/> as the model-wide default so ledger, sale, costing, and production history is never cascade-deleted (invariant 5 / requirement 5.7).</description></item>
/// </list>
/// <para>
/// The context does <b>not</b> migrate on startup; migrations apply as an explicit
/// deployment step (task 3.5). Register it in the Api composition root with a connection
/// string drawn from configuration — never a hardcoded secret.
/// </para>
/// </remarks>
public class PenguinPlankDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// Creates the context with externally supplied options (connection string, provider,
    /// and lifetime are configured in the Api composition root).
    /// </summary>
    /// <param name="options">The EF Core options for this context.</param>
    public PenguinPlankDbContext(DbContextOptions<PenguinPlankDbContext> options)
        : base(options)
    {
    }

    // ---------------------------------------------------------------------
    // Catalog (R01, R10). Mapping details live in the IEntityTypeConfiguration
    // types in Infrastructure/Catalog; these sets expose the aggregates to the
    // model and to query roots.
    // ---------------------------------------------------------------------

    /// <summary>Product family records.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Sellable SKUs belonging to products.</summary>
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    /// <summary>Individual physical pieces of serialized variants.</summary>
    public DbSet<ProductPiece> ProductPieces => Set<ProductPiece>();

    /// <summary>Configurable wood-species reference data.</summary>
    public DbSet<WoodSpecies> WoodSpecies => Set<WoodSpecies>();

    /// <summary>Variant-to-wood composition links (composite key).</summary>
    public DbSet<VariantWood> VariantWoods => Set<VariantWood>();

    /// <summary>Piece-to-wood composition links (composite key).</summary>
    public DbSet<PieceWood> PieceWoods => Set<PieceWood>();

    /// <summary>Reusable care-guidance identities.</summary>
    public DbSet<CareProfile> CareProfiles => Set<CareProfile>();

    /// <summary>Immutable, append-only care-guidance versions.</summary>
    public DbSet<CareProfileVersion> CareProfileVersions => Set<CareProfileVersion>();

    /// <summary>External listing references for variants (stored reference only in Phase A).</summary>
    public DbSet<ChannelListing> ChannelListings => Set<ChannelListing>();

    /// <summary>Typed external references owned by products.</summary>
    public DbSet<ProductExternalRef> ProductExternalRefs => Set<ProductExternalRef>();

    /// <summary>Typed external references owned by variants.</summary>
    public DbSet<VariantExternalRef> VariantExternalRefs => Set<VariantExternalRef>();

    // ---------------------------------------------------------------------
    // Media (A4). Metadata records plus explicit per-owner-type join tables;
    // binary content lives outside the database and web root.
    // ---------------------------------------------------------------------

    /// <summary>Media file metadata records (binary content held by the file store).</summary>
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    /// <summary>Explicit FK join linking media to Catalog products.</summary>
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();

    /// <summary>Explicit FK join linking media to Catalog variants.</summary>
    public DbSet<VariantMedia> VariantMedia => Set<VariantMedia>();

    /// <summary>Explicit FK join linking media to Catalog pieces.</summary>
    public DbSet<PieceMedia> PieceMedia => Set<PieceMedia>();

    // ---------------------------------------------------------------------
    // Cross-cutting foundation (A4): audit trail and idempotency records.
    // ---------------------------------------------------------------------

    /// <summary>Append-only audit trail of business mutations.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Idempotency records keyed by a unique caller-supplied key.</summary>
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    // ---------------------------------------------------------------------
    // Integration foundation (R11) — connectors disabled in Phase A.
    // ---------------------------------------------------------------------

    /// <summary>External platform connection records (disabled by default in Phase A).</summary>
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();

    /// <summary>External-to-internal resource mappings (unique Platform/Account/ExternalId).</summary>
    public DbSet<EntityMapping> EntityMappings => Set<EntityMapping>();

    /// <summary>Durable inbound provider notifications (unique DeliveryKey).</summary>
    public DbSet<IntegrationInbox> IntegrationInbox => Set<IntegrationInbox>();

    /// <summary>Durable outgoing integration jobs (unique OperationKey).</summary>
    public DbSet<IntegrationOutbox> IntegrationOutbox => Set<IntegrationOutbox>();

    /// <summary>Incremental sync cursors per connection + resource.</summary>
    public DbSet<SyncCheckpoint> SyncCheckpoints => Set<SyncCheckpoint>();

    /// <summary>Synchronization run history.</summary>
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    /// <summary>Actionable permanent-error exception queue.</summary>
    public DbSet<SyncException> SyncExceptions => Set<SyncException>();

    /// <summary>Captured external orders (schema + stores; posting logic designed-only).</summary>
    public DbSet<ExternalOrder> ExternalOrders => Set<ExternalOrder>();

    /// <summary>Normalized external-order lines.</summary>
    public DbSet<ExternalOrderLine> ExternalOrderLines => Set<ExternalOrderLine>();

    /// <summary>Stock reservations held against captured external orders.</summary>
    public DbSet<OrderReservation> OrderReservations => Set<OrderReservation>();

    // ---------------------------------------------------------------------
    // Identity / Administration (A2). This context derives from
    // IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, so the ASP.NET Core
    // Identity tables (AspNetUsers/Roles/UserRoles/UserClaims/RoleClaims/
    // UserLogins/UserTokens) are part of this single database and migration
    // chain (task 4.1). Staff identity only; there is no public registration.
    // Below is the non-identity business settings record.
    // ---------------------------------------------------------------------

    /// <summary>The single-row business-wide settings record.</summary>
    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();

    // ---------------------------------------------------------------------
    // Designed-only later-phase schema (task 3.4). These tables are modeled and
    // migrated so later phases extend rather than replace the schema; no services,
    // endpoints, or UI exercise them in Phase A. Mapping lives in the per-module
    // IEntityTypeConfiguration types discovered from this assembly.
    // ---------------------------------------------------------------------

    // Production (R02) — designed-only.

    /// <summary>Ordered production workflow templates (designed-only).</summary>
    public DbSet<ProductionWorkflow> ProductionWorkflows => Set<ProductionWorkflow>();

    /// <summary>Ordered stages within a production workflow (designed-only).</summary>
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();

    /// <summary>Planned production batches (designed-only).</summary>
    public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();

    /// <summary>Tracked lines within a production batch (designed-only).</summary>
    public DbSet<ProductionBatchLine> ProductionBatchLines => Set<ProductionBatchLine>();

    /// <summary>Append-only stage-transition history for batch lines (designed-only).</summary>
    public DbSet<StageHistory> StageHistories => Set<StageHistory>();

    // Inventory (R04) — designed-only.

    /// <summary>Stock-holding locations (designed-only).</summary>
    public DbSet<Location> Locations => Set<Location>();

    /// <summary>Append-only inventory movement headers (designed-only).</summary>
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    /// <summary>Append-only inventory movement lines (designed-only).</summary>
    public DbSet<InventoryMovementLine> InventoryMovementLines => Set<InventoryMovementLine>();

    /// <summary>On-hand balances per variant and location (designed-only).</summary>
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();

    /// <summary>Stock allocations against events (designed-only).</summary>
    public DbSet<InventoryAllocation> InventoryAllocations => Set<InventoryAllocation>();

    // Markets (R03) — designed-only.

    /// <summary>Market/show events (designed-only).</summary>
    public DbSet<Event> Events => Set<Event>();

    /// <summary>Reusable packing templates (designed-only).</summary>
    public DbSet<PackingTemplate> PackingTemplates => Set<PackingTemplate>();

    /// <summary>Lines within a packing template (designed-only).</summary>
    public DbSet<PackingTemplateItem> PackingTemplateItems => Set<PackingTemplateItem>();

    /// <summary>Concrete per-event packing items (designed-only).</summary>
    public DbSet<EventPackingItem> EventPackingItems => Set<EventPackingItem>();

    /// <summary>Reusable event equipment catalog and assignments (designed-only).</summary>
    public DbSet<EventEquipment> EventEquipment => Set<EventEquipment>();

    /// <summary>Explicit FK join linking media to events (designed-only).</summary>
    public DbSet<EventMedia> EventMedia => Set<EventMedia>();

    // Sales & reconciliation (R04, R05) — designed-only.

    /// <summary>Sale headers (designed-only).</summary>
    public DbSet<Sale> Sales => Set<Sale>();

    /// <summary>Sale lines (designed-only).</summary>
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    /// <summary>Sale returns (designed-only).</summary>
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();

    /// <summary>Sale return lines (designed-only).</summary>
    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();

    /// <summary>Expenses recorded against events (designed-only).</summary>
    public DbSet<EventExpense> EventExpenses => Set<EventExpense>();

    /// <summary>End-of-event reconciliations (designed-only).</summary>
    public DbSet<EventReconciliation> EventReconciliations => Set<EventReconciliation>();

    // Costing (R06) — designed-only.

    /// <summary>Versioned cost estimates (designed-only).</summary>
    public DbSet<CostEstimate> CostEstimates => Set<CostEstimate>();

    /// <summary>Cost estimate lines (designed-only).</summary>
    public DbSet<CostEstimateLine> CostEstimateLines => Set<CostEstimateLine>();

    // Marketing & Wholesale (R07, R08) — designed-only.

    /// <summary>Marketing/listing tasks (designed-only).</summary>
    public DbSet<ListingTask> ListingTasks => Set<ListingTask>();

    /// <summary>Wholesale accounts (designed-only).</summary>
    public DbSet<WholesaleAccount> WholesaleAccounts => Set<WholesaleAccount>();

    /// <summary>Reusable wholesale contacts (designed-only).</summary>
    public DbSet<WholesaleContact> WholesaleContacts => Set<WholesaleContact>();

    /// <summary>Recorded interactions with accounts/contacts (designed-only).</summary>
    public DbSet<Interaction> Interactions => Set<Interaction>();

    /// <summary>Follow-up tasks against accounts/contacts (designed-only).</summary>
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();

    // Channel stock policy (R11 later-phase) — designed-only.

    /// <summary>Per-channel stock sharing policies (designed-only).</summary>
    public DbSet<ChannelStockPolicy> ChannelStockPolicies => Set<ChannelStockPolicy>();

    /// <summary>Concrete per-channel stock allocations (designed-only).</summary>
    public DbSet<ChannelAllocation> ChannelAllocations => Set<ChannelAllocation>();

    // Publishing (R07 later-phase) — designed-only.

    /// <summary>Content publishing drafts (designed-only).</summary>
    public DbSet<PublishingDraft> PublishingDrafts => Set<PublishingDraft>();

    /// <summary>Explicit approvals of publishing drafts (designed-only).</summary>
    public DbSet<PublishingApproval> PublishingApprovals => Set<PublishingApproval>();

    /// <summary>Remote results of publishing drafts (designed-only).</summary>
    public DbSet<PublishingResult> PublishingResults => Set<PublishingResult>();

    /// <summary>
    /// Applies model-wide type conventions that do not depend on the concrete CLR type of a
    /// property. These run before <see cref="OnModelCreating"/>.
    /// </summary>
    /// <param name="configurationBuilder">The convention configuration builder.</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Money is the dominant decimal family and the one where a wrong (lossy) default is
        // most damaging, so it is the model-wide default. Rate and dimension columns set
        // their own precision in the entity configurations added by later tasks, using the
        // named DecimalPrecision pairs.
        configurationBuilder
            .Properties<decimal>()
            .HavePrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// Builds the model. Entity configurations are discovered from this assembly; the base
    /// conventions below are applied to every entity type after discovery so no individual
    /// configuration has to restate them.
    /// </summary>
    /// <param name="builder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The base IdentityDbContext mapping (AspNetUsers/Roles/UserRoles/UserClaims/
        // RoleClaims/UserLogins/UserTokens) must run first so our conventions and the
        // ApplyConfigurationsFromAssembly discovery build on the fully-formed Identity model.
        base.OnModelCreating(builder);

        // Entity configurations for tasks 3.2–3.4 live in this assembly and are discovered
        // here. The Catalog (3.2) and the cross-cutting/media/integration/identity (3.3)
        // configurations are already present; task 3.4 adds the designed-only ones.
        builder.ApplyConfigurationsFromAssembly(typeof(PenguinPlankDbContext).Assembly);

        ApplyBaseEntityConvention(builder);
        ApplyNoCascadeDeleteConvention(builder);
    }

    /// <summary>
    /// Applies the shared column shape for every <see cref="Entity"/>-derived type: a GUID
    /// primary key, <c>datetimeoffset</c> audit timestamps, and — for mutable aggregates —
    /// a <c>rowversion</c> concurrency token. Mapping this by convention keeps the base
    /// contract in one place and out of every individual configuration.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ApplyBaseEntityConvention(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            EntityTypeBuilder entity = modelBuilder.Entity(entityType.ClrType);

            entity.HasKey(nameof(Entity.Id));
            entity.Property(nameof(Entity.Id)).HasColumnType("uniqueidentifier");

            entity.Property(nameof(Entity.CreatedAtUtc)).HasColumnType("datetimeoffset");
            entity.Property(nameof(Entity.UpdatedAtUtc)).HasColumnType("datetimeoffset");

            if (typeof(VersionedEntity).IsAssignableFrom(entityType.ClrType))
            {
                entity.Property(nameof(VersionedEntity.RowVersion))
                    .IsRowVersion();
            }
        }
    }

    /// <summary>
    /// Sets <see cref="DeleteBehavior.Restrict"/> as the default for every business foreign key
    /// in the model. The business requires that ledger, sale, costing, and production history
    /// is never destroyed by a cascading delete of a parent (invariant 5 / requirement 5.7);
    /// making restrict the model-wide default means a configuration must <em>opt in</em> to
    /// any cascade rather than silently inherit one.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core Identity's own foreign keys (user → claims/logins/tokens/user-roles,
    /// role → role-claims) are left with the cascade behavior Identity configures in its own
    /// <c>OnModelCreating</c>. Those cascades are standard Identity
    /// housekeeping — deleting a user removes its credential rows — and carry none of the
    /// business-history concern this convention protects, so they are excluded rather than
    /// forced to restrict (which would orphan Identity child rows).
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ApplyNoCascadeDeleteConvention(ModelBuilder modelBuilder)
    {
        IEnumerable<IMutableForeignKey> foreignKeys = modelBuilder.Model
            .GetEntityTypes()
            .Where(entityType => !IsIdentityEntityType(entityType))
            .SelectMany(entityType => entityType.GetForeignKeys());

        foreach (IMutableForeignKey foreignKey in foreignKeys)
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    /// <summary>
    /// Determines whether an entity type belongs to ASP.NET Core Identity (the user, role, and
    /// their claim/login/token/user-role tables) so the no-cascade business convention leaves
    /// Identity's own delete behavior untouched.
    /// </summary>
    /// <param name="entityType">The entity type to classify.</param>
    /// <returns><see langword="true"/> for Identity-owned types; otherwise <see langword="false"/>.</returns>
    private static bool IsIdentityEntityType(IMutableEntityType entityType)
    {
        string? clrNamespace = entityType.ClrType.Namespace;

        return clrNamespace is not null
            && clrNamespace.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal);
    }
}
