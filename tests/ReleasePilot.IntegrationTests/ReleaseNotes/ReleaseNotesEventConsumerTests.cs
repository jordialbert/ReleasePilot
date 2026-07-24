using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;

namespace ReleasePilot.IntegrationTests;

public sealed class ReleaseNotesEventConsumerTests
{
    [Fact]
    public async Task GeneratesGroundedDraftWithClarificationAndBreakingChange()
    {
        var promotionId = new PromotionId(Guid.CreateVersion7());
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            promotionId,
            EventDelivery.PromotionApprovedEventType,
            EventDelivery.ReleaseNotesConsumer,
            1,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery);
        var issueTracker = new RecordingIssueTracker();
        var drafts = new RecordingDraftRepository();
        var model = new ScriptedModel((turn, messages) => turn switch
        {
            0 =>
            [
                new(
                    "1",
                    "GetWorkItems",
                    JsonSerializer.SerializeToElement(
                        new { promotionId = promotionId.Value }))
            ],
            1 =>
            [
                new(
                    "2",
                    "AskClarification",
                    JsonSerializer.SerializeToElement(new
                    {
                        workItemId = "RP-102",
                        question = "What should clients use?"
                    }))
            ],
            2 =>
            [
                new(
                    "3",
                    "FlagBreakingChange",
                    JsonSerializer.SerializeToElement(new
                    {
                        workItemId = "RP-102",
                        reason = "The v1 endpoint is removed."
                    }))
            ],
            _ =>
            [
                new(
                    "4",
                    "SubmitReleaseNotes",
                    JsonSerializer.SerializeToElement(
                        new { draft = "# Release Notes\n\n- RP-102 removes v1." }))
            ]
        });
        var consumer = new ReleaseNotesEventConsumer(
            queue,
            new ReleaseNotesAgent(model, issueTracker, drafts),
            NullLogger<ReleaseNotesEventConsumer>.Instance);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        Assert.True(queue.Completed);
        Assert.False(queue.Failed);
        Assert.Equal("What should clients use?", issueTracker.Question);
        var draft = Assert.Single(drafts.Drafts);
        Assert.Equal("# Release Notes\n\n- RP-102 removes v1.", draft.Content);
        Assert.Equal(
            new BreakingChange("RP-102", "The v1 endpoint is removed."),
            Assert.Single(draft.BreakingChanges));
        Assert.Equal(4, model.Turn);
        Assert.Contains(
            model.Messages,
            message => message.TryGetProperty("toolCallId", out _));
    }

    [Fact]
    public async Task FailsUnknownToolThroughDeliveryRetry()
    {
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            new PromotionId(Guid.CreateVersion7()),
            EventDelivery.PromotionApprovedEventType,
            EventDelivery.ReleaseNotesConsumer,
            1,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery);
        var model = new ScriptedModel(
            static (turn, messages) =>
            [
                new(
                    "1",
                    "InventReleaseNotes",
                    JsonSerializer.SerializeToElement(new { }))
            ]);
        var consumer = new ReleaseNotesEventConsumer(
            queue,
            new ReleaseNotesAgent(
                model,
                new RecordingIssueTracker(),
                new RecordingDraftRepository()),
            NullLogger<ReleaseNotesEventConsumer>.Instance);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        Assert.False(queue.Completed);
        Assert.True(queue.Failed);
        Assert.Equal(
            "Unknown release-notes tool: InventReleaseNotes.",
            queue.FailureError);
    }

    [Fact]
    public async Task FailsInvalidToolArgumentsThroughDeliveryRetry()
    {
        var promotionId = new PromotionId(Guid.CreateVersion7());
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            promotionId,
            EventDelivery.PromotionApprovedEventType,
            EventDelivery.ReleaseNotesConsumer,
            1,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery);
        var model = new ScriptedModel((turn, messages) => turn switch
        {
            0 =>
            [
                new(
                    "1",
                    "GetWorkItems",
                    JsonSerializer.SerializeToElement(
                        new { promotionId = promotionId.Value }))
            ],
            _ =>
            [
                new(
                    "2",
                    "AskClarification",
                    JsonSerializer.SerializeToElement(
                        new { workItemId = "RP-102" }))
            ]
        });
        var consumer = new ReleaseNotesEventConsumer(
            queue,
            new ReleaseNotesAgent(
                model,
                new RecordingIssueTracker(),
                new RecordingDraftRepository()),
            NullLogger<ReleaseNotesEventConsumer>.Instance);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        Assert.False(queue.Completed);
        Assert.True(queue.Failed);
        Assert.Equal(
            "AskClarification requires a question.",
            queue.FailureError);
    }

    [Fact]
    public async Task FailsModelCompletionWithoutSubmission()
    {
        var promotionId = new PromotionId(Guid.CreateVersion7());
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            promotionId,
            EventDelivery.PromotionApprovedEventType,
            EventDelivery.ReleaseNotesConsumer,
            1,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery);
        var model = new ScriptedModel((turn, messages) => turn switch
        {
            0 =>
            [
                new(
                    "1",
                    "GetWorkItems",
                    JsonSerializer.SerializeToElement(
                        new { promotionId = promotionId.Value }))
            ],
            _ => []
        });
        var consumer = new ReleaseNotesEventConsumer(
            queue,
            new ReleaseNotesAgent(
                model,
                new RecordingIssueTracker(),
                new RecordingDraftRepository()),
            NullLogger<ReleaseNotesEventConsumer>.Instance);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        Assert.True(queue.Failed);
        Assert.Equal(
            "The model completed without submitting release notes.",
            queue.FailureError);
    }

    [Fact]
    public async Task RejectsBatchedSubmissionBeforeExecutingTools()
    {
        var promotionId = new PromotionId(Guid.CreateVersion7());
        var drafts = new RecordingDraftRepository();
        var model = new ScriptedModel(
            (turn, messages) =>
            [
                new(
                    "1",
                    "SubmitReleaseNotes",
                    JsonSerializer.SerializeToElement(
                        new { draft = "# Release Notes" })),
                new(
                    "2",
                    "GetWorkItems",
                    JsonSerializer.SerializeToElement(
                        new { promotionId = promotionId.Value }))
            ]);
        var agent = new ReleaseNotesAgent(
            model,
            new RecordingIssueTracker(),
            drafts);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.Generate(
                promotionId,
                new DomainEventId(Guid.CreateVersion7()),
                CancellationToken.None));

        Assert.Equal(
            "SubmitReleaseNotes cannot be batched with other tool calls.",
            exception.Message);
        Assert.Empty(drafts.Drafts);
    }

    [Fact]
    public async Task FailsAfterTenModelTurns()
    {
        var promotionId = new PromotionId(Guid.CreateVersion7());
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            promotionId,
            EventDelivery.PromotionApprovedEventType,
            EventDelivery.ReleaseNotesConsumer,
            1,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery);
        var model = new ScriptedModel(
            (turn, messages) =>
            [
                new(
                    turn.ToString(),
                    "GetWorkItems",
                    JsonSerializer.SerializeToElement(
                        new { promotionId = promotionId.Value }))
            ]);
        var consumer = new ReleaseNotesEventConsumer(
            queue,
            new ReleaseNotesAgent(
                model,
                new RecordingIssueTracker(),
                new RecordingDraftRepository()),
            NullLogger<ReleaseNotesEventConsumer>.Instance);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        Assert.True(queue.Failed);
        Assert.Equal(10, model.Turn);
        Assert.Equal(
            "Release-notes generation exceeded 10 model turns.",
            queue.FailureError);
    }

    private sealed class ScriptedModel(
        Func<int, IReadOnlyList<JsonElement>, IReadOnlyList<ModelToolCall>> complete)
        : ILanguageModel
    {
        public int Turn { get; private set; }
        public IReadOnlyList<JsonElement> Messages { get; private set; } = [];

        public Task<IReadOnlyList<ModelToolCall>> Complete(
            IReadOnlyList<JsonElement> messages,
            CancellationToken cancellationToken)
        {
            Messages = messages;
            var calls = complete(Turn, messages);
            Turn++;
            return Task.FromResult(calls);
        }
    }

    private sealed class RecordingIssueTracker : IIssueTrackerPort
    {
        public string? Question { get; private set; }

        public Task<IReadOnlyList<WorkItem>> GetWorkItems(
            PromotionId promotionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkItem>>(
            [
                new WorkItem("RP-102", "Remove v1", "Remove the v1 endpoint.")
            ]);

        public Task<string> AskClarification(
            string workItemId,
            string question,
            CancellationToken cancellationToken)
        {
            Question = question;
            return Task.FromResult("Clients should use v2.");
        }
    }

    private sealed class RecordingDraftRepository : IReleaseNotesDraftRepository
    {
        public List<RecordedDraft> Drafts { get; } = [];

        public Task Add(
            PromotionId promotionId,
            DomainEventId triggeringEventId,
            string content,
            IReadOnlyList<BreakingChange> breakingChanges,
            CancellationToken cancellationToken)
        {
            Drafts.Add(new RecordedDraft(content, breakingChanges));
            return Task.CompletedTask;
        }
    }

    private sealed record RecordedDraft(
        string Content,
        IReadOnlyList<BreakingChange> BreakingChanges);

    private sealed class RecordingQueue(EventDelivery delivery)
        : IEventDeliveryQueue
    {
        public bool Completed { get; private set; }
        public bool Failed { get; private set; }
        public string? FailureError { get; private set; }

        public Task<EventDelivery?> Claim(
            string consumer,
            CancellationToken cancellationToken) =>
            Task.FromResult<EventDelivery?>(delivery);

        public Task<bool> Complete(
            EventDelivery claimedDelivery,
            CancellationToken cancellationToken)
        {
            Completed = true;
            return Task.FromResult(true);
        }

        public Task<bool> Fail(
            EventDelivery claimedDelivery,
            string error,
            CancellationToken cancellationToken)
        {
            Failed = true;
            FailureError = error;
            return Task.FromResult(true);
        }
    }
}
