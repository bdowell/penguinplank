IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [Action] nvarchar(128) NOT NULL,
        [EntityType] nvarchar(128) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Timestamp] datetimeoffset NOT NULL,
        [PermittedChangeSummary] nvarchar(2000) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY NONCLUSTERED ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [BusinessSettings] (
        [Id] uniqueidentifier NOT NULL,
        [Timezone] nvarchar(64) NOT NULL DEFAULT N'America/Los_Angeles',
        [Currency] nvarchar(3) NOT NULL DEFAULT N'USD',
        [DefaultLaborRate] decimal(19,6) NOT NULL,
        [DefaultOverheadRate] decimal(19,6) NOT NULL,
        [DefaultDimensionUnit] nvarchar(16) NOT NULL DEFAULT N'in',
        [ImageSizeLimitBytes] bigint NOT NULL DEFAULT CAST(20971520 AS bigint),
        [VideoSizeLimitBytes] bigint NOT NULL DEFAULT CAST(209715200 AS bigint),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BusinessSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [CareProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CareProfiles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ChannelStockPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelId] uniqueidentifier NOT NULL,
        [PoolEligible] bit NOT NULL,
        [BufferQuantity] int NOT NULL,
        [QuotaQuantity] int NULL,
        [ExclusivePieceAssignment] bit NOT NULL,
        [AuthorityPolicy] nvarchar(64) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ChannelStockPolicies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [CostEstimates] (
        [Id] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [VariantId] uniqueidentifier NULL,
        [PieceId] uniqueidentifier NULL,
        [LaborCost] decimal(19,4) NOT NULL,
        [WasteCost] decimal(19,4) NOT NULL,
        [OverheadCost] decimal(19,4) NOT NULL,
        [TotalCost] decimal(19,4) NOT NULL,
        [IsApproved] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CostEstimates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [EventEquipment] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [EventId] uniqueidentifier NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EventEquipment] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [EventExpenses] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [Category] nvarchar(64) NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [ReceiptMediaId] uniqueidentifier NULL,
        [WorkHours] decimal(12,4) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EventExpenses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [EventReconciliations] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [CountedTotal] decimal(19,4) NOT NULL,
        [ExpectedTotal] decimal(19,4) NOT NULL,
        [Variance] decimal(19,4) NOT NULL,
        [ClosureResolution] nvarchar(2000) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EventReconciliations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [Events] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [InternalDescription] nvarchar(4000) NULL,
        [PublicDescription] nvarchar(4000) NULL,
        [Venue] nvarchar(400) NULL,
        [Timezone] nvarchar(64) NULL,
        [StartDate] datetimeoffset NOT NULL,
        [EndDate] datetimeoffset NOT NULL,
        [Status] nvarchar(64) NULL,
        [SetupNotes] nvarchar(4000) NULL,
        [LoadOutNotes] nvarchar(4000) NULL,
        [PublicationState] nvarchar(32) NOT NULL DEFAULT N'Draft',
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Events] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [FollowUps] (
        [Id] uniqueidentifier NOT NULL,
        [WholesaleAccountId] uniqueidentifier NULL,
        [WholesaleContactId] uniqueidentifier NULL,
        [AssigneeId] uniqueidentifier NULL,
        [DueDate] datetimeoffset NULL,
        [Status] nvarchar(32) NULL,
        [Note] nvarchar(2000) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FollowUps] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [IdempotencyRecords] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(200) NOT NULL,
        [CallerId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(128) NOT NULL,
        [PayloadHash] nvarchar(128) NOT NULL,
        [ResultReference] nvarchar(400) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [IntegrationConnections] (
        [Id] uniqueidentifier NOT NULL,
        [Platform] nvarchar(64) NOT NULL,
        [Account] nvarchar(200) NOT NULL,
        [Scopes] nvarchar(2000) NOT NULL,
        [ApiVersion] nvarchar(32) NOT NULL,
        [EnabledCapabilities] nvarchar(1000) NOT NULL,
        [SyncDirection] nvarchar(32) NOT NULL DEFAULT N'None',
        [Health] nvarchar(32) NOT NULL DEFAULT N'Unknown',
        [Enabled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CredentialReference] nvarchar(400) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_IntegrationConnections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [Interactions] (
        [Id] uniqueidentifier NOT NULL,
        [WholesaleAccountId] uniqueidentifier NULL,
        [WholesaleContactId] uniqueidentifier NULL,
        [InteractionDate] datetimeoffset NOT NULL,
        [InteractionType] nvarchar(32) NOT NULL,
        [Summary] nvarchar(4000) NULL,
        [AssigneeId] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Interactions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [InventoryAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [PieceId] uniqueidentifier NULL,
        [AllocatedQuantity] int NOT NULL,
        [State] nvarchar(32) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventoryAllocations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [InventoryMovements] (
        [Id] uniqueidentifier NOT NULL,
        [MovementType] nvarchar(64) NOT NULL,
        [MovementDate] datetimeoffset NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [Reason] nvarchar(1000) NULL,
        [RelatedSaleId] uniqueidentifier NULL,
        [SourceLocationId] uniqueidentifier NULL,
        [DestinationLocationId] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InventoryMovements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ListingTasks] (
        [Id] uniqueidentifier NOT NULL,
        [ReferenceType] nvarchar(32) NOT NULL,
        [ReferenceId] uniqueidentifier NULL,
        [Channel] nvarchar(64) NULL,
        [TaskType] nvarchar(64) NOT NULL,
        [AssigneeId] uniqueidentifier NULL,
        [DueDate] datetimeoffset NULL,
        [State] nvarchar(32) NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ListingTasks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [Locations] (
        [Id] uniqueidentifier NOT NULL,
        [LocationCode] nvarchar(64) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Type] nvarchar(32) NOT NULL,
        [EventId] uniqueidentifier NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Locations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [MediaAssets] (
        [Id] uniqueidentifier NOT NULL,
        [StorageKey] nvarchar(200) NOT NULL,
        [MimeType] nvarchar(128) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Checksum] nvarchar(128) NOT NULL,
        [Caption] nvarchar(1000) NULL,
        [Role] nvarchar(64) NULL,
        [SortOrder] int NOT NULL,
        [Visibility] nvarchar(32) NOT NULL DEFAULT N'Private',
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MediaAssets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [PackingTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PackingTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductionWorkflows] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProductionWorkflows] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(200) NULL,
        [PublicDescription] nvarchar(4000) NULL,
        [InternalNotes] nvarchar(4000) NULL,
        [PublicationState] nvarchar(32) NOT NULL DEFAULT N'Draft',
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [PublishingDrafts] (
        [Id] uniqueidentifier NOT NULL,
        [ReferenceType] nvarchar(32) NOT NULL,
        [ReferenceId] uniqueidentifier NULL,
        [Destination] nvarchar(64) NOT NULL,
        [ContentSnapshot] nvarchar(max) NULL,
        [ApprovedFields] nvarchar(4000) NULL,
        [ApprovedMedia] nvarchar(4000) NULL,
        [ScheduledAtUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PublishingDrafts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [Sales] (
        [Id] uniqueidentifier NOT NULL,
        [Channel] nvarchar(64) NOT NULL,
        [EventId] uniqueidentifier NULL,
        [ExternalReference] nvarchar(200) NULL,
        [ContactId] uniqueidentifier NULL,
        [CustomerId] uniqueidentifier NULL,
        [SaleDate] datetimeoffset NOT NULL,
        [Subtotal] decimal(19,4) NOT NULL,
        [DiscountTotal] decimal(19,4) NOT NULL,
        [TaxTotal] decimal(19,4) NOT NULL,
        [FeeTotal] decimal(19,4) NOT NULL,
        [GrandTotal] decimal(19,4) NOT NULL,
        [CostSnapshot] decimal(19,4) NOT NULL,
        [MovementId] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Sales] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [WholesaleAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyName] nvarchar(200) NOT NULL,
        [Stage] nvarchar(32) NULL,
        [InternalNotes] nvarchar(4000) NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WholesaleAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [WholesaleContacts] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [WholesaleAccountId] uniqueidentifier NULL,
        [Email] nvarchar(256) NULL,
        [Phone] nvarchar(64) NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WholesaleContacts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [WoodSpecies] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WoodSpecies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [CareProfileVersions] (
        [Id] uniqueidentifier NOT NULL,
        [CareProfileId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [Guidance] nvarchar(4000) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CareProfileVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CareProfileVersions_CareProfiles_CareProfileId] FOREIGN KEY ([CareProfileId]) REFERENCES [CareProfiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ChannelAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelStockPolicyId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [PieceId] uniqueidentifier NULL,
        [AllocatedQuantity] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ChannelAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChannelAllocations_ChannelStockPolicies_ChannelStockPolicyId] FOREIGN KEY ([ChannelStockPolicyId]) REFERENCES [ChannelStockPolicies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [CostEstimateLines] (
        [Id] uniqueidentifier NOT NULL,
        [CostEstimateId] uniqueidentifier NOT NULL,
        [Description] nvarchar(400) NOT NULL,
        [Quantity] decimal(12,4) NOT NULL,
        [Rate] decimal(19,6) NOT NULL,
        [LineCost] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CostEstimateLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CostEstimateLines_CostEstimates_CostEstimateId] FOREIGN KEY ([CostEstimateId]) REFERENCES [CostEstimates] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [EventPackingItems] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [ReferenceType] nvarchar(32) NOT NULL,
        [ReferenceId] uniqueidentifier NULL,
        [PlannedQuantity] int NOT NULL,
        [ActualQuantity] int NOT NULL,
        [IsChecked] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EventPackingItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EventPackingItems_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [EntityMappings] (
        [Id] uniqueidentifier NOT NULL,
        [ConnectionId] uniqueidentifier NOT NULL,
        [Platform] nvarchar(64) NOT NULL,
        [Account] nvarchar(200) NOT NULL,
        [ExternalId] nvarchar(200) NOT NULL,
        [OwnerType] nvarchar(64) NOT NULL,
        [InternalId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EntityMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EntityMappings_IntegrationConnections_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [IntegrationConnections] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ExternalOrders] (
        [Id] uniqueidentifier NOT NULL,
        [ConnectionId] uniqueidentifier NOT NULL,
        [Platform] nvarchar(64) NOT NULL,
        [Account] nvarchar(200) NOT NULL,
        [ExternalId] nvarchar(200) NOT NULL,
        [PaymentState] nvarchar(64) NOT NULL,
        [FulfillmentState] nvarchar(64) NOT NULL,
        [PostingStatus] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ExternalOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExternalOrders_IntegrationConnections_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [IntegrationConnections] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [IntegrationInbox] (
        [Id] uniqueidentifier NOT NULL,
        [ConnectionId] uniqueidentifier NOT NULL,
        [DeliveryKey] nvarchar(200) NOT NULL,
        [PayloadReference] nvarchar(400) NOT NULL,
        [SignatureState] nvarchar(64) NOT NULL,
        [Status] nvarchar(32) NOT NULL DEFAULT N'Received',
        [Attempts] int NOT NULL,
        [LeaseOwner] nvarchar(128) NULL,
        [LeaseExpiresAtUtc] datetimeoffset NULL,
        [ReceivedAtUtc] datetimeoffset NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IntegrationInbox] PRIMARY KEY NONCLUSTERED ([Id]),
        CONSTRAINT [FK_IntegrationInbox_IntegrationConnections_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [IntegrationConnections] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [IntegrationOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [ConnectionId] uniqueidentifier NOT NULL,
        [OperationKey] nvarchar(200) NOT NULL,
        [PayloadReference] nvarchar(400) NOT NULL,
        [Status] nvarchar(32) NOT NULL DEFAULT N'Pending',
        [Attempts] int NOT NULL,
        [LeaseOwner] nvarchar(128) NULL,
        [LeaseExpiresAtUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IntegrationOutbox] PRIMARY KEY NONCLUSTERED ([Id]),
        CONSTRAINT [FK_IntegrationOutbox_IntegrationConnections_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [IntegrationConnections] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [SyncCheckpoints] (
        [Id] uniqueidentifier NOT NULL,
        [ConnectionId] uniqueidentifier NOT NULL,
        [Resource] nvarchar(128) NOT NULL,
        [Cursor] nvarchar(400) NULL,
        [LastReconciledAtUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SyncCheckpoints] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SyncCheckpoints_IntegrationConnections_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [IntegrationConnections] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [SyncRuns] (
        [Id] uniqueidentifier NOT NULL,
        [ConnectionId] uniqueidentifier NOT NULL,
        [Resource] nvarchar(128) NOT NULL,
        [ProcessedCount] int NOT NULL,
        [FailedCount] int NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [LastSuccessAtUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SyncRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SyncRuns_IntegrationConnections_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [IntegrationConnections] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [InventoryMovementLines] (
        [Id] uniqueidentifier NOT NULL,
        [InventoryMovementId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [PieceId] uniqueidentifier NULL,
        [Quantity] int NOT NULL,
        [CarriedCost] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InventoryMovementLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryMovementLines_InventoryMovements_InventoryMovementId] FOREIGN KEY ([InventoryMovementId]) REFERENCES [InventoryMovements] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [InventoryBalances] (
        [Id] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [LocationId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [UnitCost] decimal(19,4) NOT NULL,
        [TotalCost] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InventoryBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryBalances_Locations_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Locations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [EventMedia] (
        [EventId] uniqueidentifier NOT NULL,
        [MediaAssetId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_EventMedia] PRIMARY KEY ([EventId], [MediaAssetId]),
        CONSTRAINT [FK_EventMedia_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EventMedia_MediaAssets_MediaAssetId] FOREIGN KEY ([MediaAssetId]) REFERENCES [MediaAssets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [PackingTemplateItems] (
        [Id] uniqueidentifier NOT NULL,
        [PackingTemplateId] uniqueidentifier NOT NULL,
        [ReferenceType] nvarchar(32) NOT NULL,
        [ReferenceId] uniqueidentifier NULL,
        [PlannedQuantity] int NOT NULL,
        [Instructions] nvarchar(2000) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PackingTemplateItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PackingTemplateItems_PackingTemplates_PackingTemplateId] FOREIGN KEY ([PackingTemplateId]) REFERENCES [PackingTemplates] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductionBatches] (
        [Id] uniqueidentifier NOT NULL,
        [ProductionWorkflowId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [PlannedQuantity] int NOT NULL,
        [CompletedQuantity] int NOT NULL,
        [Priority] int NOT NULL,
        [TargetDate] datetimeoffset NULL,
        [Status] nvarchar(64) NULL,
        [WorkflowSnapshot] nvarchar(4000) NULL,
        [ParentBatchId] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProductionBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionBatches_ProductionWorkflows_ProductionWorkflowId] FOREIGN KEY ([ProductionWorkflowId]) REFERENCES [ProductionWorkflows] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [WorkflowStages] (
        [Id] uniqueidentifier NOT NULL,
        [ProductionWorkflowId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [SequenceOrder] int NOT NULL,
        [ExpectedWaitHours] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WorkflowStages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowStages_ProductionWorkflows_ProductionWorkflowId] FOREIGN KEY ([ProductionWorkflowId]) REFERENCES [ProductionWorkflows] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductExternalRefs] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Platform] nvarchar(64) NOT NULL,
        [ExternalId] nvarchar(200) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProductExternalRefs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductExternalRefs_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductMedia] (
        [OwnerId] uniqueidentifier NOT NULL,
        [MediaAssetId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_ProductMedia] PRIMARY KEY ([OwnerId], [MediaAssetId]),
        CONSTRAINT [FK_ProductMedia_MediaAssets_MediaAssetId] FOREIGN KEY ([MediaAssetId]) REFERENCES [MediaAssets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductMedia_Products_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Sku] nvarchar(64) NOT NULL,
        [Barcode] nvarchar(64) NULL,
        [TrackingMode] nvarchar(32) NOT NULL,
        [UnitOfMeasure] nvarchar(32) NOT NULL,
        [Length] decimal(12,4) NULL,
        [Width] decimal(12,4) NULL,
        [Thickness] decimal(12,4) NULL,
        [Diameter] decimal(12,4) NULL,
        [DimensionUnit] nvarchar(16) NULL,
        [Finish] nvarchar(200) NULL,
        [RetailPrice] decimal(19,4) NULL,
        [WholesalePrice] decimal(19,4) NULL,
        [CasePack] int NULL,
        [CareProfileId] uniqueidentifier NULL,
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariants_CareProfiles_CareProfileId] FOREIGN KEY ([CareProfileId]) REFERENCES [CareProfiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [PublishingApprovals] (
        [Id] uniqueidentifier NOT NULL,
        [PublishingDraftId] uniqueidentifier NOT NULL,
        [ApprovedById] uniqueidentifier NULL,
        [ApprovedAtUtc] datetimeoffset NOT NULL,
        [Note] nvarchar(2000) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PublishingApprovals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PublishingApprovals_PublishingDrafts_PublishingDraftId] FOREIGN KEY ([PublishingDraftId]) REFERENCES [PublishingDrafts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [PublishingResults] (
        [Id] uniqueidentifier NOT NULL,
        [PublishingDraftId] uniqueidentifier NOT NULL,
        [RemoteId] nvarchar(200) NULL,
        [Status] nvarchar(32) NULL,
        [RedactedError] nvarchar(2000) NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PublishingResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PublishingResults_PublishingDrafts_PublishingDraftId] FOREIGN KEY ([PublishingDraftId]) REFERENCES [PublishingDrafts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [SaleLines] (
        [Id] uniqueidentifier NOT NULL,
        [SaleId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [PieceId] uniqueidentifier NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(19,4) NOT NULL,
        [LineDiscount] decimal(19,4) NOT NULL,
        [CostSnapshot] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SaleLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaleLines_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [SaleReturns] (
        [Id] uniqueidentifier NOT NULL,
        [SaleId] uniqueidentifier NOT NULL,
        [ReturnDate] datetimeoffset NOT NULL,
        [RefundTotal] decimal(19,4) NOT NULL,
        [CompensatingMovementId] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SaleReturns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaleReturns_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ExternalOrderLines] (
        [Id] uniqueidentifier NOT NULL,
        [ExternalOrderId] uniqueidentifier NOT NULL,
        [ExternalLineId] nvarchar(200) NOT NULL,
        [VariantId] uniqueidentifier NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(19,4) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ExternalOrderLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExternalOrderLines_ExternalOrders_ExternalOrderId] FOREIGN KEY ([ExternalOrderId]) REFERENCES [ExternalOrders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [OrderReservations] (
        [Id] uniqueidentifier NOT NULL,
        [ExternalOrderId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [ReservedQuantity] int NOT NULL,
        [State] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OrderReservations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderReservations_ExternalOrders_ExternalOrderId] FOREIGN KEY ([ExternalOrderId]) REFERENCES [ExternalOrders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [SyncExceptions] (
        [Id] uniqueidentifier NOT NULL,
        [SyncRunId] uniqueidentifier NOT NULL,
        [AffectedResource] nvarchar(200) NOT NULL,
        [RedactedError] nvarchar(2000) NOT NULL,
        [Status] nvarchar(32) NOT NULL DEFAULT N'Open',
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SyncExceptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SyncExceptions_SyncRuns_SyncRunId] FOREIGN KEY ([SyncRunId]) REFERENCES [SyncRuns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductionBatchLines] (
        [Id] uniqueidentifier NOT NULL,
        [ProductionBatchId] uniqueidentifier NOT NULL,
        [PlannedQuantity] int NOT NULL,
        [CompletedQuantity] int NOT NULL,
        [CurrentStageId] uniqueidentifier NULL,
        [Disposition] nvarchar(64) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProductionBatchLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionBatchLines_ProductionBatches_ProductionBatchId] FOREIGN KEY ([ProductionBatchId]) REFERENCES [ProductionBatches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ChannelListings] (
        [Id] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Channel] nvarchar(64) NOT NULL,
        [ExternalId] nvarchar(200) NOT NULL,
        [Url] nvarchar(2000) NULL,
        [Status] nvarchar(64) NOT NULL,
        [ReadinessState] nvarchar(64) NULL,
        [LastVerifiedAtUtc] datetimeoffset NULL,
        [LastVerifiedSource] nvarchar(200) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ChannelListings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChannelListings_ProductVariants_VariantId] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [ProductPieces] (
        [Id] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [PieceCode] nvarchar(64) NULL,
        [Length] decimal(12,4) NULL,
        [Width] decimal(12,4) NULL,
        [Thickness] decimal(12,4) NULL,
        [Diameter] decimal(12,4) NULL,
        [DimensionUnit] nvarchar(16) NULL,
        [Finish] nvarchar(200) NULL,
        [Story] nvarchar(4000) NULL,
        [Status] nvarchar(64) NULL,
        [ProductionDate] datetimeoffset NULL,
        [CareProfileVersionId] uniqueidentifier NULL,
        [ProductionBatchLineId] uniqueidentifier NULL,
        [PublicationState] nvarchar(32) NOT NULL DEFAULT N'Draft',
        [ActiveFlag] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProductPieces] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductPieces_CareProfileVersions_CareProfileVersionId] FOREIGN KEY ([CareProfileVersionId]) REFERENCES [CareProfileVersions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductPieces_ProductVariants_VariantId] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [VariantExternalRefs] (
        [Id] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [Platform] nvarchar(64) NOT NULL,
        [ExternalId] nvarchar(200) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_VariantExternalRefs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VariantExternalRefs_ProductVariants_VariantId] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [VariantMedia] (
        [OwnerId] uniqueidentifier NOT NULL,
        [MediaAssetId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_VariantMedia] PRIMARY KEY ([OwnerId], [MediaAssetId]),
        CONSTRAINT [FK_VariantMedia_MediaAssets_MediaAssetId] FOREIGN KEY ([MediaAssetId]) REFERENCES [MediaAssets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VariantMedia_ProductVariants_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [VariantWoods] (
        [VariantId] uniqueidentifier NOT NULL,
        [WoodSpeciesId] uniqueidentifier NOT NULL,
        [Proportion] decimal(5,2) NULL,
        CONSTRAINT [PK_VariantWoods] PRIMARY KEY ([VariantId], [WoodSpeciesId]),
        CONSTRAINT [FK_VariantWoods_ProductVariants_VariantId] FOREIGN KEY ([VariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VariantWoods_WoodSpecies_WoodSpeciesId] FOREIGN KEY ([WoodSpeciesId]) REFERENCES [WoodSpecies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [SaleReturnLines] (
        [Id] uniqueidentifier NOT NULL,
        [SaleReturnId] uniqueidentifier NOT NULL,
        [OriginalSaleLineId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [RefundAmount] decimal(19,4) NOT NULL,
        [RestockDisposition] nvarchar(32) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SaleReturnLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaleReturnLines_SaleReturns_SaleReturnId] FOREIGN KEY ([SaleReturnId]) REFERENCES [SaleReturns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [StageHistories] (
        [Id] uniqueidentifier NOT NULL,
        [ProductionBatchLineId] uniqueidentifier NOT NULL,
        [WorkflowStageId] uniqueidentifier NOT NULL,
        [EnteredAtUtc] datetimeoffset NOT NULL,
        [ExitedAtUtc] datetimeoffset NULL,
        [Disposition] nvarchar(200) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StageHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StageHistories_ProductionBatchLines_ProductionBatchLineId] FOREIGN KEY ([ProductionBatchLineId]) REFERENCES [ProductionBatchLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StageHistories_WorkflowStages_WorkflowStageId] FOREIGN KEY ([WorkflowStageId]) REFERENCES [WorkflowStages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [PieceMedia] (
        [OwnerId] uniqueidentifier NOT NULL,
        [MediaAssetId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_PieceMedia] PRIMARY KEY ([OwnerId], [MediaAssetId]),
        CONSTRAINT [FK_PieceMedia_MediaAssets_MediaAssetId] FOREIGN KEY ([MediaAssetId]) REFERENCES [MediaAssets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PieceMedia_ProductPieces_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [ProductPieces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE TABLE [PieceWoods] (
        [PieceId] uniqueidentifier NOT NULL,
        [WoodSpeciesId] uniqueidentifier NOT NULL,
        [Proportion] decimal(5,2) NULL,
        CONSTRAINT [PK_PieceWoods] PRIMARY KEY ([PieceId], [WoodSpeciesId]),
        CONSTRAINT [FK_PieceWoods_ProductPieces_PieceId] FOREIGN KEY ([PieceId]) REFERENCES [ProductPieces] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PieceWoods_WoodSpecies_WoodSpeciesId] FOREIGN KEY ([WoodSpeciesId]) REFERENCES [WoodSpecies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE CLUSTERED INDEX [IX_AuditEntries_CreatedAtUtc] ON [AuditEntries] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_EntityType_EntityId_Timestamp] ON [AuditEntries] ([EntityType], [EntityId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CareProfileVersions_CareProfileId_VersionNumber] ON [CareProfileVersions] ([CareProfileId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ChannelAllocations_ChannelStockPolicyId] ON [ChannelAllocations] ([ChannelStockPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ChannelListings_VariantId_Channel] ON [ChannelListings] ([VariantId], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ChannelStockPolicies_ChannelId] ON [ChannelStockPolicies] ([ChannelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_CostEstimateLines_CostEstimateId] ON [CostEstimateLines] ([CostEstimateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EntityMappings_ConnectionId] ON [EntityMappings] ([ConnectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EntityMappings_Platform_Account_ExternalId] ON [EntityMappings] ([Platform], [Account], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EventExpenses_EventId] ON [EventExpenses] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EventMedia_MediaAssetId] ON [EventMedia] ([MediaAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EventPackingItems_EventId] ON [EventPackingItems] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EventReconciliations_EventId] ON [EventReconciliations] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Events_StartDate] ON [Events] ([StartDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ExternalOrderLines_ExternalOrderId] ON [ExternalOrderLines] ([ExternalOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ExternalOrders_ConnectionId] ON [ExternalOrders] ([ConnectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExternalOrders_Platform_Account_ExternalId] ON [ExternalOrders] ([Platform], [Account], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_FollowUps_AssigneeId_Status_DueDate] ON [FollowUps] ([AssigneeId], [Status], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IdempotencyRecords_Key] ON [IdempotencyRecords] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_IntegrationConnections_Platform_Account] ON [IntegrationConnections] ([Platform], [Account]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_IntegrationInbox_ConnectionId_Status_LeaseExpiresAtUtc] ON [IntegrationInbox] ([ConnectionId], [Status], [LeaseExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE CLUSTERED INDEX [IX_IntegrationInbox_CreatedAtUtc] ON [IntegrationInbox] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IntegrationInbox_DeliveryKey] ON [IntegrationInbox] ([DeliveryKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_IntegrationOutbox_ConnectionId_Status_LeaseExpiresAtUtc] ON [IntegrationOutbox] ([ConnectionId], [Status], [LeaseExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE CLUSTERED INDEX [IX_IntegrationOutbox_CreatedAtUtc] ON [IntegrationOutbox] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IntegrationOutbox_OperationKey] ON [IntegrationOutbox] ([OperationKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Interactions_InteractionDate] ON [Interactions] ([InteractionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InventoryAllocations_EventId] ON [InventoryAllocations] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InventoryBalances_LocationId] ON [InventoryBalances] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryBalances_VariantId_LocationId] ON [InventoryBalances] ([VariantId], [LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InventoryMovementLines_InventoryMovementId] ON [InventoryMovementLines] ([InventoryMovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_MovementDate] ON [InventoryMovements] ([MovementDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ListingTasks_DueDate] ON [ListingTasks] ([DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Locations_LocationCode] ON [Locations] ([LocationCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MediaAssets_StorageKey] ON [MediaAssets] ([StorageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_OrderReservations_ExternalOrderId] ON [OrderReservations] ([ExternalOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PackingTemplateItems_PackingTemplateId] ON [PackingTemplateItems] ([PackingTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PieceMedia_MediaAssetId] ON [PieceMedia] ([MediaAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PieceWoods_WoodSpeciesId] ON [PieceWoods] ([WoodSpeciesId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductExternalRefs_Platform_ProductId_ExternalId] ON [ProductExternalRefs] ([Platform], [ProductId], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductExternalRefs_ProductId] ON [ProductExternalRefs] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductionBatches_ProductionWorkflowId] ON [ProductionBatches] ([ProductionWorkflowId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductionBatches_TargetDate] ON [ProductionBatches] ([TargetDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductionBatchLines_ProductionBatchId] ON [ProductionBatchLines] ([ProductionBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductMedia_MediaAssetId] ON [ProductMedia] ([MediaAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductPieces_CareProfileVersionId] ON [ProductPieces] ([CareProfileVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductPieces_PieceCode] ON [ProductPieces] ([PieceCode]) WHERE [PieceCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductPieces_VariantId] ON [ProductPieces] ([VariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductVariants_Barcode] ON [ProductVariants] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_CareProfileId] ON [ProductVariants] ([CareProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_ProductId_ActiveFlag] ON [ProductVariants] ([ProductId], [ActiveFlag]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVariants_Sku] ON [ProductVariants] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PublishingApprovals_PublishingDraftId] ON [PublishingApprovals] ([PublishingDraftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_PublishingResults_PublishingDraftId] ON [PublishingResults] ([PublishingDraftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SaleLines_SaleId] ON [SaleLines] ([SaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SaleReturnLines_SaleReturnId] ON [SaleReturnLines] ([SaleReturnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SaleReturns_SaleId] ON [SaleReturns] ([SaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Sales_SaleDate] ON [Sales] ([SaleDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StageHistories_ProductionBatchLineId] ON [StageHistories] ([ProductionBatchLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_StageHistories_WorkflowStageId] ON [StageHistories] ([WorkflowStageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SyncCheckpoints_ConnectionId_Resource] ON [SyncCheckpoints] ([ConnectionId], [Resource]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SyncExceptions_SyncRunId_Status] ON [SyncExceptions] ([SyncRunId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_SyncRuns_ConnectionId_StartedAtUtc] ON [SyncRuns] ([ConnectionId], [StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VariantExternalRefs_Platform_VariantId_ExternalId] ON [VariantExternalRefs] ([Platform], [VariantId], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VariantExternalRefs_VariantId] ON [VariantExternalRefs] ([VariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VariantMedia_MediaAssetId] ON [VariantMedia] ([MediaAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_VariantWoods_WoodSpeciesId] ON [VariantWoods] ([WoodSpeciesId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WoodSpecies_Name] ON [WoodSpecies] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_WorkflowStages_ProductionWorkflowId_SequenceOrder] ON [WorkflowStages] ([ProductionWorkflowId], [SequenceOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006180839_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006180839_InitialSchema', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006183319_AddIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006183319_AddIdentity', N'10.0.12');
END;

COMMIT;
GO

