using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLErpTool.Domain.Receivable;
using PLErpTool.Domain.Shared;

namespace PLErpTool.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.ShortName).IsRequired();
        b.Property(x => x.FullName).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("TEXT");
        b.Property(x => x.UpdatedAt).HasColumnType("TEXT");

        b.HasMany(x => x.Salesmen)
         .WithOne()
         .HasForeignKey(s => s.CustomerId)
         .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.ShortName).IsUnique().HasDatabaseName("UX_Customers_ShortName");
    }
}

public class SalesmanConfiguration : IEntityTypeConfiguration<Salesman>
{
    public void Configure(EntityTypeBuilder<Salesman> b)
    {
        b.ToTable("Salesmen");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.CustomerId).IsRequired();
        b.Property(x => x.Name).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("TEXT");
        b.Property(x => x.UpdatedAt).HasColumnType("TEXT");

        b.HasIndex(x => new { x.CustomerId, x.Name }).IsUnique().HasDatabaseName("UX_Salesmen_Customer_Name");
    }
}

public class MonthlyDebtConfiguration : IEntityTypeConfiguration<MonthlyDebt>
{
    public void Configure(EntityTypeBuilder<MonthlyDebt> b)
    {
        b.ToTable("MonthlyDebts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.CustomerId).IsRequired();
        b.Property(x => x.SalesmanId).IsRequired(false);
        b.Property(x => x.Month).HasConversion(
            m => new DateTime(m.Year, m.Month, 1),
            v => PLErpTool.Domain.Shared.MonthKey.FromDateTime(v))
         .HasColumnType("TEXT");
        b.Property(x => x.DebtAmount).HasConversion(m => m.Value, v => new Money(v, "CNY")).HasColumnType("REAL");
        b.Property(x => x.CollectedAmount).HasConversion(m => m.Value, v => new Money(v, "CNY")).HasColumnType("REAL");
        b.Property(x => x.CreatedAt).HasColumnType("TEXT");
        b.Property(x => x.UpdatedAt).HasColumnType("TEXT");

        b.Ignore(x => x.Balance);
        b.Ignore(x => x.IsNegativeBalance);

        b.HasMany(x => x.Payments)
         .WithOne()
         .HasForeignKey(p => p.MonthlyDebtId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.CustomerId, x.SalesmanId, x.Month }).IsUnique()
         .HasDatabaseName("UX_MonthlyDebts_Cust_Salesman_Month");
        b.HasIndex(x => x.CustomerId).HasDatabaseName("IX_MonthlyDebts_Customer");
        b.HasIndex(x => x.SalesmanId).HasDatabaseName("IX_MonthlyDebts_Salesman");
        b.HasIndex(x => x.Month).HasDatabaseName("IX_MonthlyDebts_Month");
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.MonthlyDebtId).IsRequired();
        b.Property(x => x.TradeDate).HasColumnType("TEXT");
        b.Property(x => x.Type).HasConversion<string>();
        b.Property(x => x.Amount).HasConversion(m => m.Value, v => new Money(v, "CNY")).HasColumnType("REAL");
        b.Property(x => x.Remark);
        b.Property(x => x.DocumentNo);
        b.Property(x => x.ProductName);
        b.Property(x => x.SpecDetails);
        b.Property(x => x.Quantity).HasColumnType("REAL");
        b.Property(x => x.UnitPrice).HasColumnType("REAL");
        b.Property(x => x.CustomerMaterial);
        b.Property(x => x.DebtType);
        b.Property(x => x.ReconciliationRemark);
        b.Property(x => x.ReceivableRemark);
        b.Property(x => x.IsDeleted).HasColumnType("INTEGER");
        // 查询默认过滤已软删除的明细
        b.HasQueryFilter(x => !x.IsDeleted);
        b.Property(x => x.CreatedAt).HasColumnType("TEXT");
        b.Property(x => x.UpdatedAt).HasColumnType("TEXT");

        b.HasIndex(x => x.MonthlyDebtId).HasDatabaseName("IX_Payments_MonthlyDebt");
        b.HasIndex(x => x.Type).HasDatabaseName("IX_Payments_Type");
    }
}

public class PaymentChangeLogConfiguration : IEntityTypeConfiguration<PaymentChangeLog>
{
    public void Configure(EntityTypeBuilder<PaymentChangeLog> b)
    {
        b.ToTable("PaymentChangeLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.PaymentId).IsRequired();
        b.Property(x => x.MonthlyDebtId).IsRequired();
        b.Property(x => x.CustomerName);
        b.Property(x => x.Month);
        b.Property(x => x.DocumentNo);
        b.Property(x => x.PaymentType);
        b.Property(x => x.Action);
        b.Property(x => x.Field);
        b.Property(x => x.OldValue);
        b.Property(x => x.NewValue);
        b.Property(x => x.OldAmount).HasColumnType("REAL");
        b.Property(x => x.NewAmount).HasColumnType("REAL");
        b.Property(x => x.Operator);
        b.Property(x => x.ChangedAt).HasColumnType("TEXT");

        b.HasIndex(x => x.PaymentId).HasDatabaseName("IX_PaymentChangeLogs_Payment");
        b.HasIndex(x => x.MonthlyDebtId).HasDatabaseName("IX_PaymentChangeLogs_MonthlyDebt");
    }
}
