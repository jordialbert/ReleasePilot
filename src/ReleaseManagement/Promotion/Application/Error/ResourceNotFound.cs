namespace ReleaseManagement.Application;

public sealed class ResourceNotFound(string resource)
    : Exception($"The {resource} was not found.")
{
    public string Code { get; } = "resource_not_found";
}
