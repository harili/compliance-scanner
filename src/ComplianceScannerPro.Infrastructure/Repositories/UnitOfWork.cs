using Microsoft.EntityFrameworkCore.Storage;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Data;

namespace ComplianceScannerPro.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;
    
    private IRepository<Website>? _websites;
    private IRepository<ScanResult>? _scanResults;
    private IRepository<AccessibilityIssue>? _accessibilityIssues;
    private IRepository<Subscription>? _subscriptions;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<Website> Websites => 
        _websites ??= new Repository<Website>(_context);

    public IRepository<ScanResult> ScanResults => 
        _scanResults ??= new Repository<ScanResult>(_context);

    public IRepository<AccessibilityIssue> AccessibilityIssues => 
        _accessibilityIssues ??= new Repository<AccessibilityIssue>(_context);

    public IRepository<Subscription> Subscriptions => 
        _subscriptions ??= new Repository<Subscription>(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}