using ReleaseManagement.Application;

namespace ReleasePilot.Api.Shared;

public static class RequiredActor
{
    public static Guid Parse(string? header)
    {
        if (header is null)
        {
            throw new MissingActor();
        }

        if (!Guid.TryParse(header, out var actorId))
        {
            throw new InvalidInput("X-User-Id");
        }

        return actorId;
    }
}
