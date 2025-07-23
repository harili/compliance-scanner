using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Infrastructure.Identity;

namespace ComplianceScannerPro.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Website> Websites { get; set; }
    public DbSet<ScanResult> ScanResults { get; set; }
    public DbSet<AccessibilityIssue> AccessibilityIssues { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configuration PostgreSQL pour UTC
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("timestamp with time zone");
                }
            }
        }

        // ApplicationUser configuration
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Company).HasMaxLength(200);
            entity.Property(u => u.AgencyLogo).HasMaxLength(500);
            
            // Relation optionnelle - un utilisateur peut avoir un abonnement
            entity.HasOne(u => u.Subscription)
                  .WithOne()
                  .HasForeignKey<Subscription>(s => s.UserId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // Subscription configuration
        builder.Entity<Subscription>(entity =>
        {
            entity.Property(s => s.PlanName).HasMaxLength(100).IsRequired();
            entity.Property(s => s.Price).HasPrecision(10, 2);
            entity.Property(s => s.StripeSubscriptionId).HasMaxLength(200);
            entity.Property(s => s.StripePriceId).HasMaxLength(200);
            entity.HasIndex(s => s.StripeSubscriptionId).IsUnique();
        });

        // Website configuration
        builder.Entity<Website>(entity =>
        {
            entity.Property(w => w.Url).HasMaxLength(2000).IsRequired();
            entity.Property(w => w.Name).HasMaxLength(200).IsRequired();
            entity.Property(w => w.Description).HasMaxLength(1000);
            entity.HasIndex(w => new { w.UserId, w.Url }).IsUnique();
            
            entity.HasOne<ApplicationUser>()
                  .WithMany(u => u.Websites)
                  .HasForeignKey(w => w.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ScanResult configuration
        builder.Entity<ScanResult>(entity =>
        {
            entity.Property(s => s.ScanId).HasMaxLength(100).IsRequired();
            entity.Property(s => s.ErrorMessage).HasMaxLength(2000);
            entity.Property(s => s.ReportPdfPath).HasMaxLength(500);
            entity.HasIndex(s => s.ScanId).IsUnique();
            entity.HasIndex(s => s.UserId);
            entity.HasIndex(s => s.WebsiteId);
            
            entity.HasOne(s => s.Website)
                  .WithMany(w => w.ScanResults)
                  .HasForeignKey(s => s.WebsiteId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne<ApplicationUser>()
                  .WithMany(u => u.ScanResults)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AccessibilityIssue configuration
        builder.Entity<AccessibilityIssue>(entity =>
        {
            entity.Property(i => i.RgaaRule).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Title).HasMaxLength(500).IsRequired();
            entity.Property(i => i.Description).HasMaxLength(2000);
            entity.Property(i => i.PageUrl).HasMaxLength(2000).IsRequired();
            entity.Property(i => i.ElementSelector).HasMaxLength(1000);
            entity.Property(i => i.ElementHtml).HasColumnType("text");
            entity.Property(i => i.FixSuggestion).HasColumnType("text");
            entity.Property(i => i.CodeExample).HasColumnType("text");
            entity.HasIndex(i => i.ScanResultId);
            entity.HasIndex(i => new { i.RgaaRule, i.ScanResultId });
            
            entity.HasOne(i => i.ScanResult)
                  .WithMany(s => s.Issues)
                  .HasForeignKey(i => i.ScanResultId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Les plans d'abonnement seront créés dynamiquement via Stripe
    }
}