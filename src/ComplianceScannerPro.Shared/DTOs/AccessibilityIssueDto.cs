using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Shared.DTOs;

public class AccessibilityIssueDto
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
    public DateTime DetectedAt { get; set; }
    public int ScanResultId { get; set; }
}

public class IssuesSummaryDto
{
    public int TotalIssues { get; set; }
    public int CriticalIssues { get; set; }
    public int WarningIssues { get; set; }
    public int InfoIssues { get; set; }
    public List<RgaaRuleStatsDto> RuleStats { get; set; } = new();
}

public class RgaaRuleStatsDto
{
    public string RgaaRule { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public int IssueCount { get; set; }
    public IssueSeverity Severity { get; set; }
}