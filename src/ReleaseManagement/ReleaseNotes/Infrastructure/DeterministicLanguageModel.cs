using System.Text.Json;
using ReleaseManagement.Application;

namespace ReleaseManagement.Infrastructure;

public sealed class DeterministicLanguageModel : ILanguageModel
{
    public Task<IReadOnlyList<ModelToolCall>> Complete(
        IReadOnlyList<JsonElement> messages,
        CancellationToken cancellationToken)
    {
        var promotionId = messages[0].GetProperty("promotionId").GetGuid();
        IReadOnlyList<ModelToolCall> calls = messages.Count switch
        {
            1 =>
            [
                new(
                    "get-work-items",
                    "GetWorkItems",
                    JsonSerializer.SerializeToElement(new { promotionId }))
            ],
            3 =>
            [
                new(
                    "ask-clarification",
                    "AskClarification",
                    JsonSerializer.SerializeToElement(new
                    {
                        workItemId = "RP-102",
                        question = "Which endpoint is removed and what replaces it?"
                    }))
            ],
            5 =>
            [
                new(
                    "flag-breaking-change",
                    "FlagBreakingChange",
                    JsonSerializer.SerializeToElement(new
                    {
                        workItemId = "RP-102",
                        reason = "Removes the v1 authentication endpoint."
                    }))
            ],
            _ =>
            [
                new(
                    "submit-release-notes",
                    "SubmitReleaseNotes",
                    JsonSerializer.SerializeToElement(new
                    {
                        draft =
                            """
                            # Release Notes

                            - RP-101 adds deployment health checks.
                            - RP-102 removes the v1 authentication endpoint; clients must use v2.
                            """
                    }))
            ]
        };
        return Task.FromResult(calls);
    }
}
