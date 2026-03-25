namespace PersonalFinanceTracker.Api.Entities;

public class SharedAccountMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid AddedByUserId { get; set; }
    public string Role { get; set; } = "viewer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
