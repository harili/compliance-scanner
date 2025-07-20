using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Core.Interfaces;

public interface IAccessibilityAnalyzer
{
    Task<List<AccessibilityIssue>> AnalyzePageAsync(string url, string content);
    Task<int> CalculateScoreAsync(List<AccessibilityIssue> issues, int pagesScanned);
    Task<AccessibilityGrade> GetGradeFromScoreAsync(int score);
}