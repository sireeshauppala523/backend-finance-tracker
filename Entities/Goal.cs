namespace PersonalFinanceTracker.Api.Entities;

public class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateOnly? TargetDate { get; set; }
    public Guid? LinkedAccountId { get; set; }
    public string Icon { get; set; } = "target";
    public string Color { get; set; } = "#2F7A5C";
    public string Status { get; set; } = "active";
}