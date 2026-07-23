namespace ReleaseManagement.Application;

public sealed class DeploymentUnavailable()
    : Exception("The Deployment system is unavailable.")
{
    public string Code => "deployment_unavailable";
}
