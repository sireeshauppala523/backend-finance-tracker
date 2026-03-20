namespace PersonalFinanceTracker.Api.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Color { get; set; } = "#7C8EA6";
    public string Icon { get; set; } = "circle";
    public bool IsArchived { get; set; }
}