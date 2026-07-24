namespace ReleaseManagement.Application;

public sealed record FlagBreakingChangeArguments(
    string WorkItemId,
    string Reason);
