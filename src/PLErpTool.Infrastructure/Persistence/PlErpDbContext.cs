using Microsoft.EntityFrameworkCore;
using PLErpTool.Domain.Receivable;

namespace PLErpTool.Infrastructure.Persistence;

public class PlErpDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Salesman> Salesmen => Set<Salesman>();
    public DbSet<MonthlyDebt> MonthlyDebts => Set<MonthlyDebt>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentChangeLog> PaymentChangeLogs => Set<PaymentChangeLog>();

    public PlErpDbContext(DbContextOptions<PlErpDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlErpDbContext).Assembly);
        modelBuilder.Entity<MonthlyDebt>().Property(d => d.SalesmanId).IsRequired(false);
    }
}