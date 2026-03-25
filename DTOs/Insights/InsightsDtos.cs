namespace PersonalFinanceTracker.Api.DTOs.Insights;

public record HealthFactorResponse(string Key, string Label, int Score, string Note);

public record FinancialHealthScoreResponse(
    int Score,
    string Band,
    string Summary,
    IReadOnlyList<string> Suggestions,
    IReadOnlyList<HealthFactorResponse> Factors);
