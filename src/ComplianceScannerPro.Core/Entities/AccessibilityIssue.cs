using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Core.Entities;

public class AccessibilityIssue
{
    public int Id { get; set; }
    public string RgaaRule { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public string PageUrl { get; set; } = string.Empty;
    public string? ElementSelector { get; set; }
    public string? ElementHtml { get; set; }
    public string? FixSuggestion { get; set; }
    public string? CodeExample { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    
    public int ScanResultId { get; set; }
    public ScanResult ScanResult { get; set; } = null!;
}