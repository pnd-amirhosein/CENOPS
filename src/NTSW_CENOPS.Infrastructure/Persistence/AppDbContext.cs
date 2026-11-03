using NTSW_CENOPS.Domain.Monitoring;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Domain.Chat;
using NTSW_CENOPS.Infrastructure.Authorization;
using NTSW_CENOPS.Infrastructure.Identity;
using NTSW_CENOPS.Infrastructure.Knowledge;
using NTSW_CENOPS.Infrastructure.Notes;
using NTSW_CENOPS.Infrastructure.Tasks;
using NTSW_CENOPS.Infrastructure.Announcements;
using NTSW_CENOPS.Infrastructure.Jobs;
using NTSW_CENOPS.Infrastructure.Reports;
using NTSW_CENOPS.Infrastructure.Dashboards;
using NTSW_CENOPS.Infrastructure.Events;
using NTSW_CENOPS.Infrastructure.Stories;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NTSW_CENOPS.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Access> Accesses => Set<Access>();
    public DbSet<RoleAccess> RoleAccesses => Set<RoleAccess>();

    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMember> ChatRoomMembers => Set<ChatRoomMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatMessageAttachment> ChatMessageAttachments => Set<ChatMessageAttachment>();

    public DbSet<Curl> Curls => Set<Curl>();
    public DbSet<SqlQuery> Queries => Set<SqlQuery>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteBlock> NoteBlocks => Set<NoteBlock>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AutomatedJob> AutomatedJobs => Set<AutomatedJob>();
    public DbSet<AutomatedJobRun> AutomatedJobRuns => Set<AutomatedJobRun>();
    public DbSet<AutomatedJobData> AutomatedJobData => Set<AutomatedJobData>();
    public DbSet<MonitoringReport> MonitoringReports => Set<MonitoringReport>();
    public DbSet<GlobalDashboardConfiguration> GlobalDashboardConfigurations => Set<GlobalDashboardConfiguration>();
    public DbSet<UserDashboardConfiguration> UserDashboardConfigurations => Set<UserDashboardConfiguration>();
    public DbSet<DashboardViewerPreference> DashboardViewerPreferences => Set<DashboardViewerPreference>();

    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();
    public DbSet<ReleaseStory> ReleaseStories => Set<ReleaseStory>();
    public DbSet<ReleaseStoryView> ReleaseStoryViews => Set<ReleaseStoryView>();

    public DbSet<HealthApi> HealthApis => Set<HealthApi>();
    public DbSet<HealthCheckResult> HealthCheckResults => Set<HealthCheckResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReleaseStory>(entity =>
        {
            entity.ToTable("ReleaseStories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceFile).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Version).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Markdown).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(x => x.SourceFile).IsUnique();
        });
        modelBuilder.Entity<ReleaseStoryView>(entity =>
        {
            entity.ToTable("ReleaseStoryViews");
            entity.HasKey(x => new { x.StoryId, x.UserId });
            entity.HasIndex(x => x.UserId);
            entity.HasOne<ReleaseStory>().WithMany().HasForeignKey(x => x.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.BirthDate).HasColumnType("date");

            entity.Property(x => x.DisplayName)
                .HasMaxLength(150)
                .IsRequired();

            entity.HasIndex(x => x.DisplayName)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasIndex(x => x.NormalizedUserName)
                .IsUnique()
                .HasDatabaseName("UserNameIndex")
                .HasFilter("[NormalizedUserName] IS NOT NULL AND [IsDeleted] = 0");

            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.HasIndex(x => x.NormalizedName)
                .IsUnique()
                .HasDatabaseName("RoleNameIndex")
                .HasFilter("[NormalizedName] IS NOT NULL AND [IsDeleted] = 0");

            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<Access>(entity =>
        {
            entity.ToTable("Accesses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<RoleAccess>(entity =>
        {
            entity.ToTable("RoleAccesses");
            entity.HasKey(x => new { x.RoleId, x.AccessId });
            entity.HasIndex(x => x.AccessId);
            entity.HasOne<ApplicationRole>().WithMany()
                .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Access>().WithMany()
                .HasForeignKey(x => x.AccessId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessageAttachment>(entity =>
        {
            entity.ToTable("ChatMessageAttachments");
            entity.HasKey(x => new { x.MessageId, x.FileId });
            entity.HasIndex(x => x.FileId);
            entity.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("ChatRooms", table => table.HasCheckConstraint("CK_ChatRooms_Pair",
                "([Kind] = 1 AND [DirectLowUserId] IS NOT NULL AND [DirectHighUserId] IS NOT NULL AND [DirectLowUserId] < [DirectHighUserId]) OR ([Kind] = 2 AND [DirectLowUserId] IS NULL AND [DirectHighUserId] IS NULL)"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Kind).HasConversion<byte>().IsRequired();
            entity.HasIndex(x => new { x.DirectLowUserId, x.DirectHighUserId }).IsUnique()
                .HasDatabaseName("UX_ChatRooms_DirectPair")
                .HasFilter("[Kind] = 1 AND [DirectLowUserId] IS NOT NULL AND [DirectHighUserId] IS NOT NULL");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ChatRoomMember>(entity =>
        {
            entity.ToTable("ChatRoomMembers");
            entity.HasKey(x => new { x.RoomId, x.UserId });
            entity.HasIndex(x => x.UserId);
            entity.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EncryptedContent).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.ForwardedFromDisplayName).HasMaxLength(180);
            entity.Property(x => x.ForwardedFromUsername).HasMaxLength(256);
            entity.HasIndex(x => new { x.RoomId, x.Id });
            entity.HasIndex(x => x.SenderUserId);
            entity.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CalendarEventEntity>(entity =>
        {
            entity.ToTable("CalendarEvents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(3000).IsRequired();
            entity.Property(x => x.Repeat).HasConversion<byte>();
            entity.Property(x => x.CalendarBasis).HasConversion<byte>();
            entity.Property(x => x.MonthDays).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Months).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.IsDeleted, x.StartsAt });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        ConfigureKnowledge<Curl>(modelBuilder, "Curls");
        ConfigureKnowledge<SqlQuery>(modelBuilder, "Queries");
        modelBuilder.Entity<Todo>(entity =>
        {
            entity.ToTable("Todos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Excerpt).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Visibility).HasConversion<byte>().HasDefaultValue(KnowledgeVisibility.Public);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.UpdatedAt);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TodoId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.ToTable("TodoItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).HasMaxLength(12000).IsRequired();
            entity.HasIndex(x => new { x.TodoId, x.Position });
        });
        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Visibility).HasConversion<byte>().HasDefaultValue(KnowledgeVisibility.Public);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.UpdatedAt);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
        modelBuilder.Entity<Note>(entity =>
        {
            entity.ToTable("Notes");
            entity.Property(x => x.Visibility).HasConversion<byte>().HasDefaultValue(KnowledgeVisibility.Public);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Excerpt).HasMaxLength(300).IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.UpdatedAt);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasMany(x => x.Blocks).WithOne().HasForeignKey(x => x.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<NoteBlock>(entity =>
        {
            entity.ToTable("NoteBlocks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasConversion<byte>().IsRequired();
            entity.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Language).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RowGroup).IsRequired(false);
            entity.HasIndex(x => new { x.NoteId, x.Position });
        });

        modelBuilder.Entity<AutomatedJob>(entity =>
        {
            entity.ToTable("AutomatedJobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.SourceKey).HasMaxLength(32).IsRequired();
            entity.Property(x => x.SqlValue).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.LastResult).HasMaxLength(250).IsRequired();
            entity.HasIndex(x => new { x.IsEnabled, x.IsDeleted });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
        modelBuilder.Entity<AutomatedJobRun>(entity =>
        {
            entity.ToTable("AutomatedJobRuns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Trigger).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(24).IsRequired();
            entity.Property(x => x.ResultPreview).HasMaxLength(2500);
            entity.Property(x => x.Error).HasMaxLength(1200);
            entity.HasIndex(x => new { x.JobId, x.StartedAtUtc });
            entity.HasIndex(x => new { x.JobId, x.ScheduledAtUtc }).IsUnique()
                .HasFilter("[ScheduledAtUtc] IS NOT NULL");
            entity.HasOne<AutomatedJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AutomatedJobData>(entity =>
        {
            entity.ToTable("AutomatedJobData");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Date).HasColumnType("date").IsRequired();
            entity.Property(x => x.Value).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.LastCalledAtUtc).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(x => new { x.AutomatedJobId, x.Date }).IsUnique();
            entity.HasOne<AutomatedJob>().WithMany()
                .HasForeignKey(x => x.AutomatedJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonitoringReport>(entity =>
        {
            entity.ToTable("MonitoringReports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.ComparisonField).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ChartType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.MeasureFieldsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(x => x.AutomatedJobId);
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasOne<AutomatedJob>().WithMany().HasForeignKey(x => x.AutomatedJobId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GlobalDashboardConfiguration>(entity =>
        {
            entity.ToTable("GlobalDashboardConfigurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<UserDashboardConfiguration>(entity =>
        {
            entity.ToTable("UserDashboardConfigurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.LayoutJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.IsDefault }).IsUnique()
                .HasDatabaseName("UX_UserDashboards_Default")
                .HasFilter("[IsDefault] = 1");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DashboardViewerPreference>(entity =>
        {
            entity.ToTable("DashboardViewerPreferences");
            entity.HasKey(x => x.UserId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserDashboardConfiguration>().WithMany().HasForeignKey(x => x.DashboardId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HealthApi>()
            .HasIndex(x => x.IsActive);

        modelBuilder.Entity<HealthCheckResult>()
            .HasIndex(x => new { x.HealthApiId, x.CheckedAt });
    }

    private static void ConfigureKnowledge<T>(ModelBuilder modelBuilder, string table)
        where T : KnowledgeEntryBase
    {
        modelBuilder.Entity<T>(entity =>
        {
            entity.ToTable(table);
            entity.Property(x => x.Visibility).HasConversion<byte>().HasDefaultValue(KnowledgeVisibility.Public);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Value).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
