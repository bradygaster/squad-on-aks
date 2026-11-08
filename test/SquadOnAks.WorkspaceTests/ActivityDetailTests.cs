using System.Text.Json;
using Squad.Agents.AI;
using SquadOnAks.Web.Security;
using SquadOnAks.Web.Work;

internal static class ActivityDetailTests
{
    public static void Run()
    {
        var evidence = new WorkEvidenceStore();
        var redactor = new SecretRedactor("activity-secret");
        SquadTraceEvidence.Record(evidence, redactor, new()
        {
            Kind = SquadAgentTraceEventKind.SubagentDispatched,
            RawEventType = "Dispatch",
            SdkAgentId = "catalog-id",
            DispatchedPersonaName = "catalog",
            DispatchedPersonaDescription = "Build the catalog API",
            DispatchedPrompt = "Private instructions must not become activity details."
        });
        SquadTraceEvidence.Record(evidence, redactor, new()
        {
            Kind = SquadAgentTraceEventKind.AssistantMessage,
            RawEventType = "Message",
            SdkAgentId = "catalog-id",
            Content = "Private reasoning and activity-secret",
            RequestedToolNames = ["view", "rg", "view", "activity-secret", "ghp_private", "<script>", "bash --token=secret"]
        });
        WorkEvidenceItem progress = evidence.GetSnapshot().Items.Last();
        Require(progress.Actor == "catalog" && progress.Activity?.Task == "Build the catalog API",
            "Subagent progress must retain its recorded persona and task through the SDK identity.");
        Require(progress.Activity!.Tools!.SequenceEqual(["view", "rg"]),
            "Public tools must be bounded identifiers, deduplicated and credential-redacted.");
        string publicDetail = JsonSerializer.Serialize(progress.Activity);
        Require(!publicDetail.Contains("Private", StringComparison.Ordinal) &&
            !publicDetail.Contains("activity-secret", StringComparison.Ordinal),
            "Activity metadata must not promote assistant content, private prompts or credentials.");
        SquadTraceEvidence.Record(evidence, redactor, new()
        {
            Kind = SquadAgentTraceEventKind.SubagentCompleted,
            RawEventType = "Completed",
            SdkAgentId = "catalog-id",
            SubagentName = "general-purpose"
        });
        Require(evidence.GetSnapshot().Items.Last().Actor == "catalog",
            "A catalog agent type must not replace a recorded persona identity.");
        SquadTraceEvidence.Record(evidence, redactor, new()
        {
            Kind = SquadAgentTraceEventKind.AssistantMessage,
            RawEventType = "Message",
            SdkAgentId = "other-id",
            RequestedToolNames = ["bash"]
        });
        Require(evidence.GetSnapshot().Items.Last().Actor is null &&
            evidence.GetSnapshot().Items.Last().Activity?.Task is null,
            "Unattributed activity must not borrow another member's identity or task.");
        foreach (string description in new[]
        {
            "Use activity-secret", "password=private", "<script>private</script>", "two\nlines",
            "Read `private source`", "Visit https://private.example", new string('x', 201)
        })
        {
            SquadTraceEvidence.Record(evidence, redactor, new()
            {
                Kind = SquadAgentTraceEventKind.SubagentDispatched,
                RawEventType = "Dispatch",
                DispatchedPersonaName = "reviewer",
                DispatchedPersonaDescription = description,
                DispatchedPrompt = "Fallback private prompt"
            });
            Require(evidence.GetSnapshot().Items.Last().Activity is null,
                "Unsafe dispatch descriptions must not be shown or replaced with private prompts.");
        }
        var restored = JsonSerializer.Deserialize<WorkEvidenceSnapshot>(
            JsonSerializer.Serialize(evidence.GetSnapshot()))!;
        Require(restored.Items[1].Activity?.Task == "Build the catalog API",
            "Historical evidence must retain structured activity details.");
        evidence.Reset();
        SquadTraceEvidence.Record(evidence, redactor, new()
        {
            Kind = SquadAgentTraceEventKind.AssistantMessage,
            RawEventType = "Message",
            SdkAgentId = "catalog-id",
            RequestedToolNames = ["view"]
        });
        Require(evidence.GetSnapshot().Items.Single().Actor is null &&
            evidence.GetSnapshot().Items.Single().Activity?.Task is null,
            "Resetting a turn must clear prior identity/task associations.");
        Console.WriteLine("PASS public activity metadata, credential exclusion, persona correlation, historical persistence and turn isolation");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
