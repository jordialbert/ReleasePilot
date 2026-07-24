namespace ReleasePilot.Api.Shared;

public sealed class MissingActor()
    : Exception("The X-User-Id header is required.")
{
    public string Code { get; } = "missing_actor";
}
