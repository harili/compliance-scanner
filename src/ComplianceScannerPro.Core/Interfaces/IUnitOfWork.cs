using ComplianceScannerPro.Core.Entities;

namespace ComplianceScannerPro.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<Website> Websites { get; }
    IRepository<ScanResult> ScanResults { get; }
    IRepository<AccessibilityIssue> AccessibilityIssues { get; }
    IRepository<Subscription> Subscriptions { get; }
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}