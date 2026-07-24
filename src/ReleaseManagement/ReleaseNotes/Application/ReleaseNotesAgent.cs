using System.Text.Json;
using System.Text.Json.Serialization;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class ReleaseNotesAgent(
    ILanguageModel model,
    IIssueTrackerPort issueTracker,
    IReleaseNotesDraftRepository drafts)
{
    private const int MaxTurns = 10;
    private static readonly JsonSerializerOptions ToolJson =
        new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

    public async Task Generate(
        PromotionId promotionId,
        DomainEventId triggeringEventId,
        CancellationToken cancellationToken)
    {
        var messages = new List<JsonElement>
        {
            JsonSerializer.SerializeToElement(
                new { promotionId = promotionId.Value },
                ToolJson)
        };
        var workItems = new Dictionary<string, WorkItem>();
        var breakingChanges = new Dictionary<string, BreakingChange>();

        for (var turn = 0; turn < MaxTurns; turn++)
        {
            var calls = await model.Complete(messages, cancellationToken);
            if (calls.Count == 0)
            {
                throw new InvalidOperationException(
                    "The model completed without submitting release notes.");
            }
            if (calls.Count > 1)
            {
                foreach (var call in calls)
                {
                    if (call.Name == "SubmitReleaseNotes")
                    {
                        throw new InvalidOperationException(
                            "SubmitReleaseNotes cannot be batched with other tool calls.");
                    }
                }
            }

            messages.Add(JsonSerializer.SerializeToElement(
                new { toolCalls = calls },
                ToolJson));
            foreach (var call in calls)
            {
                object result;
                switch (call.Name)
                {
                    case "GetWorkItems":
                    {
                        var arguments =
                            call.Arguments.Deserialize<GetWorkItemsArguments>(ToolJson)!;
                        if (arguments.PromotionId != promotionId.Value)
                        {
                            throw new InvalidOperationException(
                                "GetWorkItems requires the triggering Promotion ID.");
                        }

                        var linkedWorkItems = await issueTracker.GetWorkItems(
                            promotionId,
                            cancellationToken);
                        workItems = linkedWorkItems.ToDictionary(workItem => workItem.Id);
                        result = linkedWorkItems;
                        break;
                    }
                    case "AskClarification":
                    {
                        var arguments =
                            call.Arguments.Deserialize<AskClarificationArguments>(ToolJson)!;
                        if (string.IsNullOrWhiteSpace(arguments.WorkItemId))
                        {
                            throw new InvalidOperationException(
                                "AskClarification requires a Work Item ID.");
                        }
                        if (string.IsNullOrWhiteSpace(arguments.Question))
                        {
                            throw new InvalidOperationException(
                                "AskClarification requires a question.");
                        }
                        if (!workItems.ContainsKey(arguments.WorkItemId))
                        {
                            throw new InvalidOperationException(
                                "AskClarification requires a linked Work Item.");
                        }

                        result = await issueTracker.AskClarification(
                            arguments.WorkItemId,
                            arguments.Question,
                            cancellationToken);
                        break;
                    }
                    case "FlagBreakingChange":
                    {
                        var arguments =
                            call.Arguments.Deserialize<FlagBreakingChangeArguments>(ToolJson)!;
                        if (string.IsNullOrWhiteSpace(arguments.WorkItemId))
                        {
                            throw new InvalidOperationException(
                                "FlagBreakingChange requires a Work Item ID.");
                        }
                        if (string.IsNullOrWhiteSpace(arguments.Reason))
                        {
                            throw new InvalidOperationException(
                                "FlagBreakingChange requires a reason.");
                        }
                        if (!workItems.ContainsKey(arguments.WorkItemId))
                        {
                            throw new InvalidOperationException(
                                "FlagBreakingChange requires a linked Work Item.");
                        }

                        var breakingChange = new BreakingChange(
                            arguments.WorkItemId,
                            arguments.Reason);
                        breakingChanges[arguments.WorkItemId] = breakingChange;
                        result = breakingChange;
                        break;
                    }
                    case "SubmitReleaseNotes":
                    {
                        var arguments =
                            call.Arguments.Deserialize<SubmitReleaseNotesArguments>(ToolJson)!;
                        if (string.IsNullOrWhiteSpace(arguments.Draft))
                        {
                            throw new InvalidOperationException(
                                "SubmitReleaseNotes requires a Markdown draft.");
                        }
                        if (workItems.Count == 0)
                        {
                            throw new InvalidOperationException(
                                "SubmitReleaseNotes requires linked Work Items.");
                        }

                        await drafts.Add(
                            promotionId,
                            triggeringEventId,
                            arguments.Draft,
                            breakingChanges.Values.ToArray(),
                            cancellationToken);
                        return;
                    }
                    default:
                        throw new InvalidOperationException(
                            $"Unknown release-notes tool: {call.Name}.");
                }

                messages.Add(JsonSerializer.SerializeToElement(
                    new { toolCallId = call.Id, result },
                    ToolJson));
            }
        }

        throw new InvalidOperationException(
            $"Release-notes generation exceeded {MaxTurns} model turns.");
    }
}
