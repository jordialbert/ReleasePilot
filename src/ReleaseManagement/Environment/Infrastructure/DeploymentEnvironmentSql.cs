using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

internal static class DeploymentEnvironmentSql
{
    /// <summary>
    /// A SQL row list of every Environment with its pipeline position,
    /// e.g. <c>('dev', 1), ('staging', 2), ('production', 3)</c>.
    /// The pipeline order is the <see cref="DeploymentEnvironment"/> declaration order.
    /// </summary>
    public static readonly string PipelineValues = string.Join(
        ", ",
        Enum.GetValues<DeploymentEnvironment>()
            .Select((environment, index) => $"('{Name(environment)}', {index + 1})"));

    public static string Name(DeploymentEnvironment environment) =>
        environment.ToString().ToLowerInvariant();

    public static DeploymentEnvironment Parse(string name) => name switch
    {
        "dev" => DeploymentEnvironment.Dev,
        "staging" => DeploymentEnvironment.Staging,
        "production" => DeploymentEnvironment.Production,
        _ => throw new InvalidOperationException("Unsupported persisted Environment.")
    };
}
