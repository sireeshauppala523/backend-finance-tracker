namespace PersonalFinanceTracker.Api.Entities;

public class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConditionField { get; set; } = string.Empty;
    public string ConditionOperator { get; set; } = string.Empty;
    public string ConditionValue { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ActionValue { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
