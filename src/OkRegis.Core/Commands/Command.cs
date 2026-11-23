using OkRegis.Core.Settings;

namespace OkRegis.Core.Commands;

/// <summary>Un ordre compris. Target = le VRAI nom dans OBS (null pour enregistrement/stream).</summary>
public record Command(CommandKind Kind, string? Target);

public enum ParseStatus { Ok, NoWakeWord, UnknownVerb, UnknownTarget, Disabled }

public record ParseResult(ParseStatus Status, Command? Command);
