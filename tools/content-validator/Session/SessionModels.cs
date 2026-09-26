#nullable enable
using System.Collections.Generic;

namespace SSNoir.Session;

public sealed record SessionSetup(string Entry, int Seed, int Growth = 1,
    Dictionary<string, int>? Items = null);

public sealed record SessionOperation(string Id, string Kind, string Label);

public sealed record SessionClock(string Label, string Note, int Current, int Max, string Style);

public sealed record SessionCard(string Name, string Subtitle, string Kind, bool Disabled,
    string Text, string Skill, IReadOnlyList<SessionClock> Clocks);

public sealed record SessionActor(string Id, string Name, int Composure, int MaxComposure,
    IReadOnlyDictionary<string, int> Stats, IReadOnlyList<SessionDie> Dice);

public sealed record SessionDie(int SlotId, int Value);

public sealed record SessionObservation(long Version, string Scene, bool IsInEncounter,
    int WorldDay, string Focus, IReadOnlyList<SessionCard> Cards,
    IReadOnlyList<SessionCard> CarryCards, IReadOnlyList<SessionActor> Actors,
    IReadOnlyDictionary<string, int> Inventory, IReadOnlyList<string> RestBlockers,
    IReadOnlyList<string> Dossier, IReadOnlyList<SessionOperation> Operations);

public sealed record SessionEvent(string Phase, string Text);

public sealed record SessionStep(long Version, string OperationId, string? Reason,
    string BeforeHash, string AfterHash, IReadOnlyList<SessionEvent> Events);

public sealed class SessionTranscript
{
    public SessionSetup Setup { get; set; } = null!;
    public string ContentRevision { get; set; } = string.Empty;
    public List<SessionStep> Steps { get; set; } = new();
}
