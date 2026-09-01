using System.Text.Json;
using System.Text.Json.Serialization;
using Swarnakshi.Automation.Workflows;

namespace Swarnakshi.Automation;

/// <summary>
/// A cue: one narration line and the window it is on screen for.
///
/// <paramref name="EndMs"/> is the moment the NEXT line replaced it, not a fixed duration — the
/// caption stays up while its step runs, so a step that takes eight seconds gets an eight-second
/// cue. That is what makes these usable as subtitles directly, without guessing at durations.
/// </summary>
public sealed record NarrationCue(
    int Index,
    long StartMs,
    long EndMs,
    string Text,
    [property: JsonPropertyName("isTitle")] bool IsTitle);

/// <summary>What one journey said, and when. Written next to the run for anything that edits video.</summary>
public sealed record NarrationTranscript(
    string Key,
    string DisplayName,
    string Module,
    string BusinessPurpose,
    string Viewport,
    string RunMode,
    DateTimeOffset RecordedAt,
    long DurationMs,
    bool Succeeded,
    IReadOnlyList<NarrationCue> Cues);

/// <summary>
/// Writes the narration of a run to JSON, one file per journey and viewport.
///
/// The captions only ever existed on screen, and xUnit surfaces the step list only when a test
/// fails — so a green run left nothing to caption a recording with. This is that transcript: the
/// same strings the scenario narrates and the failure message names, with the timing needed to lay
/// them over a video.
/// </summary>
public static class NarrationSidecar
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Best effort by design: a transcript that cannot be written is a missing convenience, never a
    /// failed scenario. The run's verdict must not depend on a file system.
    /// </summary>
    public static string? TryWrite(IWorkflow workflow, WorkflowRunResult result, AutomationOptions options,
        IReadOnlyList<NarrationBeat> beats, TimeSpan narrationElapsed)
    {
        try
        {
            Directory.CreateDirectory(RepoPaths.NarrationDir);

            var transcript = new NarrationTranscript(
                workflow.Key,
                workflow.DisplayName,
                workflow.Module,
                workflow.BusinessPurpose,
                options.Viewport.ToString(),
                options.RunMode.ToString(),
                DateTimeOffset.Now,
                (long)result.Duration.TotalMilliseconds,
                result.Succeeded,
                BuildCues(beats, narrationElapsed));

            var path = Path.Combine(RepoPaths.NarrationDir, $"{workflow.Key}-{options.Viewport}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(transcript, Json));
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static List<NarrationCue> BuildCues(IReadOnlyList<NarrationBeat> beats, TimeSpan end)
    {
        var cues = new List<NarrationCue>(beats.Count);

        for (var i = 0; i < beats.Count; i++)
        {
            // A cue runs until the next one starts; the last runs to the end of the journey.
            var stop = i + 1 < beats.Count ? beats[i + 1].At : end;

            cues.Add(new NarrationCue(
                Index: i + 1,
                StartMs: (long)beats[i].At.TotalMilliseconds,
                EndMs: (long)Math.Max(stop.TotalMilliseconds, beats[i].At.TotalMilliseconds),
                Text: beats[i].Text,
                IsTitle: beats[i].IsTitle));
        }

        return cues;
    }
}
