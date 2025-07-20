using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Shared.DTOs;

public class ScanResultDto
{
    public int Id { get; set; }
    public string ScanId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ScanStatus Status { get; set; }
    public int Score { get; set; }
    public AccessibilityGrade Grade { get; set; }
    public int PagesScanned { get; set; }
    public int TotalIssues { get; set; }
    public int CriticalIssues { get; set; }
    public int WarningIssues { get; set; }
    public int InfoIssues { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ReportPdfPath { get; set; }
    public int WebsiteId { get; set; }
    public string WebsiteName { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

public class StartScanDto
{
    public int WebsiteId { get; set; }
}

public class ScanProgressDto
{
    public string ScanId { get; set; } = string.Empty;
    public ScanStatus Status { get; set; }
    public int PagesScanned { get; set; }
    public int CurrentPage { get; set; }
    public string? CurrentUrl { get; set; }
    public int ProgressPercentage { get; set; }
    public string? ErrorMessage { get; set; }
}