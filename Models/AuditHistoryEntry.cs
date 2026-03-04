namespace service.Models;

public record AuditHistoryEntry(
    DateTime  ChangedAt,
    string?   ChangeType,
    string?   PropertyName,
    string?   OldValue,
    string?   NewValue,
    string?   ChangedBy);
