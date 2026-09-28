#nullable enable
using System.Collections.Generic;

namespace SSNoir.Session;

public sealed record SessionSetup(string Entry, int Seed, int Growth = 1,
    Dictionary<string, int>? Items = null);

public sealed record SessionOperation(string Id, string Kind, string Label,
    string? Card = null, int? DieValue = null, int? Prepared = null,
    string? Odds = null);

public sealed record SessionClock(string Label, string Note, int Current, int Max, string Style);

public sealed record SessionCard(string Name, string Subtitle, string Kind, bool Disabled,
    string Text, string Skill, IReadOnlyList<string> Tags,
    IReadOnlyList<SessionClock> Clocks);

public sealed record SessionActor(string Id, string Name, int Composure, int MaxComposure,
    IReadOnlyDictionary<string, int> Stats, IReadOnlyList<SessionDie> Dice);

public sealed record SessionDie(int SlotId, int Value);

public sealed record SessionObservation(long Version, string Scene, bool IsInEncounter,
    int WorldDay, string Focus, string Injury, string Scars,
    IReadOnlyList<SessionCard> Cards,
    IReadOnlyList<SessionCard> CarryCards, IReadOnlyList<SessionActor> Actors,
    IReadOnlyDictionary<string, int> Inventory, IReadOnlyList<string> RestBlockers,
    IReadOnlyList<string> Dossier, string? EncounterResult,
    IReadOnlyList<SessionOperation> Operations,
    IReadOnlyList<SessionClock> Clocks);

public sealed record SessionFrameState(string Scene, int WorldDay, string Focus,
    IReadOnlyList<SessionCard> Cards, IReadOnlyList<SessionActor> Actors,
    IReadOnlyDictionary<string, int> Inventory, int InjurySeverity,
    IReadOnlyList<SessionClock> Clocks);

public sealed record SessionEvent(string Phase, string Text, SessionFrameState? State = null);

public sealed record SessionStep(long Version, string OperationId, string? Reason,
    string BeforeHash, string AfterHash, SessionObservation Before,
    SessionObservation After, IReadOnlyList<SessionEvent> Events);

public sealed class SessionTranscript
{
    public SessionSetup Setup { get; set; } = null!;
    public string ContentRevision { get; set; } = string.Empty;
    public string StopReason { get; set; } = "手动保存";
    public List<SessionEvent> OpeningEvents { get; set; } = new();
    public List<SessionStep> Steps { get; set; } = new();
}
