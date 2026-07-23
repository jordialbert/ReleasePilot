namespace ReleaseManagement.Application;

public sealed class UnknownActor()
    : Exception("The acting user is unknown.")
{
    public string Code { get; } = "unknown_actor";
}
