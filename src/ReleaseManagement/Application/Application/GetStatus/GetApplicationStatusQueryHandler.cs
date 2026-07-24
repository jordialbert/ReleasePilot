namespace ReleaseManagement.Application;

public sealed class GetApplicationStatusQueryHandler(IApplicationStatusReader status)
{
    public async Task<ApplicationStatusResponse> Handle(
        Guid id,
        CancellationToken cancellationToken)
    {
        var application = await status.Find(id, cancellationToken);
        if (application is null)
        {
            throw new ResourceNotFound("application");
        }

        return application;
    }
}
