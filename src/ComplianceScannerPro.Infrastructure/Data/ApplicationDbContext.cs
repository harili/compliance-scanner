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

        // ApplicationUser configuration
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Company).HasMaxLength(200);
            entity.Property(u => u.AgencyLogo).HasMaxLength(500);
            
            entity.HasOne(u => u.Subscription)
                  .WithOne()
                  .HasForeignKey<Subscription>(s => s.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
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

        // Seed default subscription plans
        SeedSubscriptionPlans(builder);
    }

    private void SeedSubscriptionPlans(ModelBuilder builder)
    {
        builder.Entity<Subscription>().HasData(
            new Subscription
            {
                Id = 1,
                PlanName = "Gratuit",
                Price = 0,
                MaxWebsites = 1,
                MaxScansPerMonth = 5,
                ApiAccess = false,
                BrandedReports = false,
                PrioritySupport = false,
                UserId = "system",
                StripePriceId = "free_plan"
            },
            new Subscription
            {
                Id = 2,
                PlanName = "Freelance",
                Price = 99,
                MaxWebsites = 10,
                MaxScansPerMonth = 100,
                ApiAccess = true,
                BrandedReports = false,
                PrioritySupport = false,
                UserId = "system",
                StripePriceId = "price_freelance"
            },
            new Subscription
            {
                Id = 3,
                PlanName = "Agence",
                Price = 299,
                MaxWebsites = 50,
                MaxScansPerMonth = 500,
                ApiAccess = true,
                BrandedReports = true,
                PrioritySupport = true,
                UserId = "system",
                StripePriceId = "price_agency"
            }
        );
    }
}