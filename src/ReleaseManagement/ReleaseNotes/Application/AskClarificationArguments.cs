namespace ReleaseManagement.Application;

public sealed record AskClarificationArguments(
    string WorkItemId,
    string Question);
