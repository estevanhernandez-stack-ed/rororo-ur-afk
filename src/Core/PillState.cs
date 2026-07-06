namespace Labs626.UrAfk.Core;

public enum PillStateKind { Off, Watching, PreGrab, Grabbing, Kept, Disconnected, ConsentRevoked }

public sealed record PillSnapshot(PillStateKind Kind, string Text);
