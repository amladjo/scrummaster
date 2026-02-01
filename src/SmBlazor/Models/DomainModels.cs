namespace SmBlazor.Models;

public sealed record DayRule(
    string MemberId,
    string Type,
    DateTime Start,
    DateTime End,
    bool Approved,
    List<string> Reasons)
{
    public string Reason => string.Join(", ", Reasons);
}

public sealed record Holiday(DateTime Date, string Name, string? Country);

public sealed record TeamMember(
    string MemberId,
    string Name,
    string ShortName,
    string Status,
    int PeekOrder,
    bool DayBackup,
    List<string> BackupMembers,
    int? FixedDay,
    string? Country);

public sealed record WhosOutItem(
    DateTime Start,
    DateTime End,
    string MemberId,
    string Reason,
    bool Approved,
    int Status);

