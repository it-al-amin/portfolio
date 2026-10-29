using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Data;

public class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectImage> ProjectImages => Set<ProjectImage>();
    public DbSet<Education> Education => Set<Education>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<ServiceOffering> Services => Set<ServiceOffering>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Profile>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.FullName).HasMaxLength(120);
            e.Property(p => p.Headline).HasMaxLength(160);
        });
        b.Entity<EmailSettings>().Property(p => p.Id).ValueGeneratedNever();
        b.Entity<EmailSettings>().Property(p => p.SmtpSecurity).HasConversion<string>().HasMaxLength(20);

        b.Entity<SkillCategory>()
            .HasMany(c => c.Skills).WithOne().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Project>(e =>
        {
            e.HasIndex(p => p.Slug).IsUnique();
            e.HasMany(p => p.Images).WithOne().HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ProjectImage>().HasOne<MediaFile>().WithMany().HasForeignKey(i => i.MediaId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Achievement>().Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
        b.Entity<Achievement>().HasOne<Document>().WithMany().HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Document>().Property(d => d.Kind).HasConversion<string>().HasMaxLength(20);

        b.Entity<ContactMessage>(e =>
        {
            e.Property(m => m.NotificationStatus).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(m => m.CreatedAt);
            e.HasIndex(m => m.ContentHash);
        });

        b.Entity<AdminUser>().HasIndex(u => u.Email).IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<OrderedEntity>())
        {
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
