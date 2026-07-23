namespace ReleaseManagement.Application;

public sealed class InvalidInput(string field)
    : Exception($"The {field} value is invalid.")
{
    public string Code { get; } = "malformed_input";
}
