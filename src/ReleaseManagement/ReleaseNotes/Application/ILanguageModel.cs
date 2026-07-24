using System.Text.Json;

namespace ReleaseManagement.Application;

public interface ILanguageModel
{
    Task<IReadOnlyList<ModelToolCall>> Complete(
        IReadOnlyList<JsonElement> messages,
        CancellationToken cancellationToken);
}
