using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Entities;

namespace PersonalFinanceTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<SharedAccountMember> SharedAccountMembers => Set<SharedAccountMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<User>().Property(x => x.PreferredCurrency).HasMaxLength(8);
        modelBuilder.Entity<Budget>().HasIndex(x => new { x.UserId, x.AccountId, x.CategoryId, x.Month, x.Year }).IsUnique();
        modelBuilder.Entity<Transaction>().Property(x => x.Tags).HasColumnType("text[]");
        modelBuilder.Entity<Rule>().HasIndex(x => x.UserId);
        modelBuilder.Entity<SharedAccountMember>().HasIndex(x => new { x.AccountId, x.UserId }).IsUnique();
        modelBuilder.Entity<SharedAccountMember>().Property(x => x.Role).HasMaxLength(16);
    }

    public async Task SeedDefaultsAsync()
    {
        if (await Categories.AnyAsync(x => x.UserId == null))
        {
            return;
        }

        var categories = new[]
        {
            new Category { Name = "Food", Type = "expense", Color = "#D97757", Icon = "utensils" },
            new Category { Name = "Rent", Type = "expense", Color = "#9A6BCE", Icon = "home" },
            new Category { Name = "Utilities", Type = "expense", Color = "#4F7CAC", Icon = "bolt" },
            new Category { Name = "Transport", Type = "expense", Color = "#3F8F7A", Icon = "car" },
            new Category { Name = "Shopping", Type = "expense", Color = "#C46A7B", Icon = "bag" },
            new Category { Name = "Salary", Type = "income", Color = "#2F7A5C", Icon = "briefcase" },
            new Category { Name = "Freelance", Type = "income", Color = "#4C8CFF", Icon = "sparkles" },
            new Category { Name = "Bonus", Type = "income", Color = "#E3A73F", Icon = "gift" }
        };

        await Categories.AddRangeAsync(categories);
        await SaveChangesAsync();
    }
}
