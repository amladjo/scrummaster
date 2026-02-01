using System.Text.Json.Serialization;
using SmBlazor.Utils;

namespace SmBlazor.Models;

public sealed class SmData
{
    public List<DayRuleDto> DayRules { get; set; } = [];
    public List<HolidayDto> Holidays { get; set; } = [];
    public List<TeamMemberDto> TeamMembers { get; set; } = [];
}

public sealed class DayRuleDto
{
    public string MemberId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Approved { get; set; }
    public string? Reason { get; set; }
}

public sealed class HolidayDto
{
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
}

public sealed class TeamMemberDto
{
    public string MemberId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string Status { get; set; } = string.Empty;

    [JsonConverter(typeof(IntFromStringConverter))]
    public int PeekOrder { get; set; }

    public bool DayBackup { get; set; }
    public string? BackupMembers { get; set; }

    [JsonConverter(typeof(NullableIntFromStringConverter))]
    public int? FixedDay { get; set; }

    public string? Country { get; set; }
}
