namespace OkRegis.Core.Orchestration;

public enum RegisState { Idle, Connecting, Listening, Paused }

public record HistoryEntry(DateTime At, string Heard, string Result, bool Success);
