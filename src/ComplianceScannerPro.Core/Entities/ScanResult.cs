using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Core.Entities;

public class ScanResult
{
    public int Id { get; set; }
    public string ScanId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public int Score { get; set; } = 0;
    public AccessibilityGrade Grade { get; set; } = AccessibilityGrade.F;
    public int PagesScanned { get; set; } = 0;
    public int TotalIssues { get; set; } = 0;
    public int CriticalIssues { get; set; } = 0;
    public int WarningIssues { get; set; } = 0;
    public int InfoIssues { get; set; } = 0;
    public string? ErrorMessage { get; set; }
    public string? ReportPdfPath { get; set; }
    
    public int WebsiteId { get; set; }
    public Website Website { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    
    public ICollection<AccessibilityIssue> Issues { get; set; } = new List<AccessibilityIssue>();
}