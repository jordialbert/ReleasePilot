using System.Collections.Concurrent;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class InMemoryDeploymentPort : IDeploymentPort
{
    private readonly ConcurrentDictionary<PromotionId, byte> requests = new();

    public bool Unavailable { get; set; }
    public ICollection<PromotionId> Requests => requests.Keys;

    public Task Start(PromotionId promotionId, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new Exception("Simulated Deployment system failure.");
        }

        requests.TryAdd(promotionId, 0);
        return Task.CompletedTask;
    }
}
