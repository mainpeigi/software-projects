using Microsoft.EntityFrameworkCore;
using Datemulte_2.Models;
using Datemulte_2.Models.DataManagement;
using System.Text.Json;

namespace Datemulte_2.Data;

/// <summary>
/// Entity Framework Core DbContext for data management and user profiles
/// Manages database entities for:
/// - Source files (uploaded CSV/Excel files)
/// - Data sessions (user work sessions with versioning)
/// - Data snapshots (compressed state backups)
/// - Change events (incremental modifications)
/// - UI state (chart settings and filters)
/// - User profiles (authentication and preferences)
/// Uses JSON serialization for complex properties and PostgreSQL-specific types (bytea, text)
/// </summary>
public class DataManagementDbContext : DbContext
{
    public DataManagementDbContext(DbContextOptions<DataManagementDbContext> options)
        : base(options)
    {
    }

    // === Entity Sets (Database Tables) ===
    // Uploaded source files
    public DbSet<SourceFile> SourceFiles => Set<SourceFile>();
    // User data sessions with edit history
    public DbSet<DataSession> DataSessions => Set<DataSession>();
    // Compressed snapshots for fast state recovery
    public DbSet<DataSnapshot> DataSnapshots => Set<DataSnapshot>();
    // Incremental change events for versioning
    public DbSet<DataChangeEvent> DataChangeEvents => Set<DataChangeEvent>();
    // UI state persistence (chart settings, filters)
    public DbSet<SessionUIState> SessionUIStates => Set<SessionUIState>();
    // User profile and preferences
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<SessionShare> SessionShares => Set<SessionShare>();
    public DbSet<ThemeConfiguration> ThemeConfigurations => Set<ThemeConfiguration>();
    public DbSet<SessionComment> SessionComments => Set<SessionComment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
    public DbSet<SessionInvitation> SessionInvitations => Set<SessionInvitation>();
    
    /// <summary>
    /// Configures entity relationships, indexes, and complex property serialization
    /// Uses Fluent API for detailed entity configuration
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure each entity with separate methods for clarity
        ConfigureSourceFile(modelBuilder);
        ConfigureDataSession(modelBuilder);
        ConfigureDataSnapshot(modelBuilder);
        ConfigureDataChangeEvent(modelBuilder);
        ConfigureSessionUIState(modelBuilder);
        ConfigureSessionShare(modelBuilder);
        ConfigureThemeConfiguration(modelBuilder);
        ConfigureSessionComment(modelBuilder);
        ConfigureNotification(modelBuilder);
        ConfigureNotificationPreference(modelBuilder);
        ConfigureApiKey(modelBuilder);
        ConfigureWebhook(modelBuilder);
        ConfigureWebhookDelivery(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureScheduledReport(modelBuilder);

        // Configure UserProfile entity
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();  // Ensure one profile per user
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.AvatarUrl).HasColumnType("text");  // TEXT for base64 data URLs
            entity.Property(e => e.TwoFactorSecret).HasMaxLength(200);
        });
    }

    /// <summary>
    /// Configures SourceFile entity with string length constraints and indexes
    /// Indexes on FileHash for deduplication and UploadedAt for chronological queries
    /// </summary>
    private void ConfigureSourceFile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SourceFile>(entity =>
        {
            entity.HasKey(e => e.FileId);

            // String length constraints
            entity.Property(e => e.FileName).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StorageProvider).HasMaxLength(50).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.FileHash).HasMaxLength(64).IsRequired();  // SHA256 hex string
            entity.Property(e => e.SheetName).HasMaxLength(200);
            entity.Property(e => e.ProcessingStatus).HasMaxLength(50).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.FileHash);     // For file deduplication
            entity.HasIndex(e => e.UploadedAt);   // For chronological sorting
        });
    }

    /// <summary>
    /// Configures DataSession entity with JSON serialization for complex properties
    /// Converts List and Dictionary properties to JSON for PostgreSQL text columns
    /// Sets up relationships with SourceFile, Snapshots, and ChangeEvents
    /// </summary>
    private void ConfigureDataSession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataSession>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            // String length constraints
            entity.Property(e => e.SessionName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SourceFileName).HasMaxLength(500);
            entity.Property(e => e.SourceFileHash).HasMaxLength(64);
            entity.Property(e => e.TimeColumn).HasMaxLength(200);

            // === JSON Serialization for Complex Properties ===
            // Serialize List<string> and Dictionary properties to JSON text columns
            entity.Property(e => e.Columns)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("ColumnsJson")
                .HasColumnType("text");

            entity.Property(e => e.NumericColumns)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("NumericColumnsJson")
                .HasColumnType("text");

            entity.Property(e => e.ColumnUnits)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("ColumnUnitsJson")
                .HasColumnType("text");

            entity.Property(e => e.ColumnToGroupMapping)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("ColumnGroupsJson")
                .HasColumnType("text");

            entity.Property(e => e.SensorGroups)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("SensorGroupsJson")
                .HasColumnType("text");

            // === Relationships ===
            // Optional reference to source file (SetNull if file deleted)
            entity.HasOne(e => e.SourceFile)
                .WithMany()
                .HasForeignKey(e => e.SourceFileId)
                .OnDelete(DeleteBehavior.SetNull);

            // One-to-many: Session has many snapshots (cascade delete)
            entity.HasMany(e => e.Snapshots)
                .WithOne(s => s.Session)
                .HasForeignKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many: Session has many change events (cascade delete)
            entity.HasMany(e => e.ChangeEvents)
                .WithOne(c => c.Session)
                .HasForeignKey(c => c.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // === Indexes ===
            // Composite index for user's recent sessions
            entity.HasIndex(e => new { e.UserId, e.LastModifiedAt });
            // Index for chronological sorting
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    /// <summary>
    /// Configures DataSnapshot entity with binary data columns and unique versioning constraint
    /// Uses PostgreSQL bytea type for compressed data and checksums
    /// Enforces unique (SessionId, VersionNumber) to prevent duplicate snapshots
    /// </summary>
    private void ConfigureDataSnapshot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataSnapshot>(entity =>
        {
            entity.HasKey(e => e.SnapshotId);

            // Unique constraint: only one snapshot per session version
            entity.HasIndex(e => new { e.SessionId, e.VersionNumber }).IsUnique();

            // PostgreSQL binary types for compressed data
            entity.Property(e => e.CompressedData).HasColumnType("BLOB");  // Gzip compressed JSON
            entity.Property(e => e.Checksum).HasColumnType("BLOB");        // SHA256 hash

            // Many-to-one relationship with DataSession (cascade delete)
            entity.HasOne(e => e.Session)
                .WithMany(s => s.Snapshots)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Configures DataChangeEvent entity for incremental edit tracking
    /// Converts EventType enum to string and stores detailed change information
    /// Unique constraint on (SessionId, FromVersion, EventSequence) for proper ordering
    /// </summary>
    private void ConfigureDataChangeEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataChangeEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);

            // Unique constraint: ensures proper event ordering within a version
            entity.HasIndex(e => new { e.SessionId, e.FromVersion, e.EventSequence }).IsUnique();

            // Convert enum to string for database storage
            entity.Property(e => e.EventType)
                .HasConversion<string>()
                .HasMaxLength(20);  // "CellEdit", "RowAdd", "RowDelete", etc.

            // String constraints for change metadata
            entity.Property(e => e.ColumnName).HasMaxLength(200);
            entity.Property(e => e.OldValue).HasColumnType("text");  // Previous value
            entity.Property(e => e.NewValue).HasColumnType("text");  // New value
            entity.Property(e => e.JsonPatch).HasColumnType("text"); // JSON Patch format changes

            // Many-to-one relationship with DataSession (cascade delete)
            entity.HasOne(e => e.Session)
                .WithMany(s => s.ChangeEvents)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Configures SessionUIState entity with extensive JSON serialization
    /// Stores chart settings, filters, colors, and calculated columns as JSON
    /// One-to-one relationship with DataSession (shares same SessionId as primary key)
    /// </summary>
    private void ConfigureSessionUIState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionUIState>(entity =>
        {
            entity.HasKey(e => e.SessionId);

            // === JSON Serialization for Complex UI State ===
            // Convert HashSet, List, and Dictionary properties to JSON text columns
            entity.Property(e => e.SelectedColumnsY1)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<HashSet<string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("SelectedColumnsY1Json")
                .HasColumnType("text");

            entity.Property(e => e.SelectedColumnsY2)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<HashSet<string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("SelectedColumnsY2Json")
                .HasColumnType("text");

            entity.Property(e => e.ChartAssignments)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("ChartAssignmentsJson")
                .HasColumnType("text");

            // Data filter configurations (complex filter objects)
            entity.Property(e => e.ActiveFilters)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<DataFilterState>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("ActiveFiltersJson")
                .HasColumnType("text");

            // Custom color palette for charts
            entity.Property(e => e.CustomColors)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("CustomColorsJson")
                .HasColumnType("text");

            // List of calculated column names
            entity.Property(e => e.CalculatedColumns)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("CalculatedColumnsJson")
                .HasColumnType("text");

            // Formula definitions for calculated columns
            entity.Property(e => e.CalculationFormulas)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, CalculationFormula>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .HasColumnName("CalculationFormulasJson")
                .HasColumnType("text");

            // One-to-one relationship with DataSession (cascade delete)
            entity.HasOne<DataSession>()
                .WithOne()
                .HasForeignKey<SessionUIState>(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
        private void ConfigureSessionShare(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionShare>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Foreign key to session
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Foreign key to user who received the share
            // SharedWithUserId references UserProfile.UserId (not Id)
            entity.HasOne(e => e.SharedWithUser)
                .WithMany(u => u.SharedSessions)
                .HasForeignKey(e => e.SharedWithUserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Foreign key to user who created the share
            // SharedByUserId references UserProfile.UserId (not Id)
            entity.HasOne(e => e.SharedByUser)
                .WithMany(u => u.SessionsSharedByMe)
                .HasForeignKey(e => e.SharedByUserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Indexes
            entity.HasIndex(e => new { e.SessionId, e.SharedWithUserId }).IsUnique();
            entity.HasIndex(e => e.SharedAt);
        });
    }

    private void ConfigureThemeConfiguration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ThemeConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithOne(u => u.Theme)
                .HasForeignKey<ThemeConfiguration>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.ThemeName).HasMaxLength(100);
            entity.Property(e => e.PrimaryColor).HasMaxLength(20);
            entity.Property(e => e.SecondaryColor).HasMaxLength(20);
            entity.Property(e => e.FontFamily).HasMaxLength(200);
        });
    }

    private void ConfigureSessionComment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(e => e.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.Property(e => e.CommentText).HasColumnType("text");
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    private void ConfigureNotification(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Message).HasColumnType("text");
            entity.Property(e => e.ContextJson).HasColumnType("text");
            
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt });
        });
    }

    private void ConfigureNotificationPreference(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.NotificationPreferences)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Unique constraint: one preference per user per notification type
            entity.HasIndex(e => new { e.UserId, e.Type }).IsUnique();
        });
    }

    private void ConfigureApiKey(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.ApiKeys)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.KeyHash).HasMaxLength(64);
            entity.Property(e => e.KeyPrefix).HasMaxLength(20);
            entity.Property(e => e.Scopes).HasMaxLength(500);
            
            entity.HasIndex(e => e.KeyHash);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
        });
    }

    private void ConfigureWebhook(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Webhook>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Webhooks)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.Url).HasMaxLength(2000);
            entity.Property(e => e.Secret).HasMaxLength(200);
            entity.Property(e => e.EventsJson).HasColumnType("text");
            
            entity.HasIndex(e => new { e.UserId, e.IsActive });
        });
    }

    private void ConfigureWebhookDelivery(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Webhook)
                .WithMany(w => w.Deliveries)
                .HasForeignKey(e => e.WebhookId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.PayloadJson).HasColumnType("text");
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.Property(e => e.ResponseBody).HasColumnType("text");
            
            entity.HasIndex(e => new { e.WebhookId, e.IsDelivered, e.CreatedAt });
        });
    }

    private void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.DetailsJson).HasColumnType("text");
            
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.Action, e.CreatedAt });
        });
    }

    private void ConfigureScheduledReport(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduledReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.ScheduledReports)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.ReportName).HasMaxLength(200);
            entity.Property(e => e.Schedule).HasMaxLength(100);
            entity.Property(e => e.EmailRecipients).HasMaxLength(1000);
            
            entity.HasIndex(e => new { e.UserId, e.IsActive });
        });
    }
}
