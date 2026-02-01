using System.Net.Http.Json;
using System.Text.Json;
using SmBlazor.Models;
using SmBlazor.Utils;

namespace SmBlazor.Services;

public sealed class SmService
{
    private const string CacheKey = "scrumMasterCachedData";
    private const string CacheExpiredKey = "scrumMasterCachedDataExpired";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly LocalStorageService _localStorage;

    public SmService(HttpClient http, LocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
    }

    // Same as JS _fetchUrl
    public string FetchUrl { get; set; } = "https://script.google.com/macros/s/AKfycbzOA3oRs56H9BH9cBDTm0d9_EAvhqMdfZKBQca9LOV3WU4zB4sQjLCdClZDw3Ia7ZLmHg/exec";

    public bool IsLoading { get; private set; }
    public bool HasCachedData { get; private set; }
    public bool CacheNotAllowed { get; private set; }

    // Debug info (shown in UI)
    public string? LastFetchStatus { get; private set; }
    public string? LastFetchError { get; private set; }

    public DateTime Today { get; set; } = DateTime.Today;

    public SmData Data { get; private set; } = new();

    // Ensure spinner is visible for at least this long (ms)
    public int MinLoadingDurationMs { get; set; } = 700;

    public async Task InitializeAsync()
    {
        await TryReadCacheAsync();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        LastFetchStatus = null;
        LastFetchError = null;

        var startedAt = DateTime.UtcNow;

        try
        {
            // WASM issue diagnosis: sometimes GetFromJsonAsync hides useful status details.
            // So we fetch manually, then deserialize.
            using var resp = await _http.GetAsync(FetchUrl, cancellationToken);
            LastFetchStatus = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";

            var content = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                LastFetchError = $"Non-success response. Body: {Truncate(content, 300)}";
                return;
            }

            var data = JsonSerializer.Deserialize<SmData>(content, JsonOptions);
            if (data is not null)
            {
                Data = data;
                await TryWriteCacheAsync(data);
            }
            else
            {
                LastFetchError = $"Deserialize returned null. Body: {Truncate(content, 300)}";
            }
        }
        catch (Exception ex)
        {
            LastFetchError = ex.ToString();
        }
        finally
        {
            // Keep spinner visible for a minimum time
            var elapsedMs = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;
            var remaining = MinLoadingDurationMs - elapsedMs;
            if (remaining > 0)
            {
                try { await Task.Delay(remaining, cancellationToken); } catch { /* ignore */ }
            }

            IsLoading = false;
        }
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "...");

    private async Task TryReadCacheAsync()
    {
        try
        {
            var expiredIso = await _localStorage.GetItemAsync(CacheExpiredKey);
            if (!string.IsNullOrWhiteSpace(expiredIso) && DateTime.TryParse(expiredIso, out var expiredAt))
            {
                // JS logic: invalid if expiredAt < now - 1 day
                if (expiredAt < DateTime.Now.AddDays(-1))
                {
                    HasCachedData = false;
                    return;
                }
            }

            var cached = await _localStorage.GetJsonAsync<SmData>(CacheKey);
            if (cached is not null)
            {
                Data = cached;
                HasCachedData = true;
            }
        }
        catch
        {
            CacheNotAllowed = true;
        }
    }

    private async Task TryWriteCacheAsync(SmData data)
    {
        try
        {
            if (CacheNotAllowed) return;
            await _localStorage.SetJsonAsync(CacheKey, data);
            await _localStorage.SetItemAsync(CacheExpiredKey, DateTime.Now.ToString("O"));
            HasCachedData = true;
        }
        catch
        {
            CacheNotAllowed = true;
        }
    }

    public IReadOnlyList<TeamMember> TeamMembers => (Data.TeamMembers ?? [])
        .OrderBy(m => m.PeekOrder)
        .Select(m => new TeamMember(
            m.MemberId,
            m.Name,
            string.IsNullOrWhiteSpace(m.ShortName) ? m.MemberId : m.ShortName!,
            m.Status,
            m.PeekOrder,
            m.DayBackup,
            string.IsNullOrWhiteSpace(m.BackupMembers)
                ? []
                : m.BackupMembers.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList(),
            m.FixedDay,
            m.Country))
        .ToList();

    public IReadOnlyList<TeamMember> ActiveTeamMembers => TeamMembers.Where(m => m.Status == "Active").ToList();

    public IReadOnlyList<Holiday> Holidays => (Data.Holidays ?? [])
        .Select(h => new Holiday(h.Date.JustDate(), h.Name, h.Country))
        .ToList();

    public IReadOnlyList<DayRule> DayRules
    {
        get
        {
            var raw = (Data.DayRules ?? [])
                .Select(item => new DayRule(
                    item.MemberId,
                    item.Type,
                    item.Start.JustDate(),
                    item.End.JustDate(),
                    item.Approved,
                    string.IsNullOrWhiteSpace(item.Reason) ? [] : [item.Reason!]))
                .OrderBy(x => x.Start)
                .ToList();

            // Holidays -> Vacation rules by country membership (CSV in holiday.country)
            var holidayVacations = new List<DayRule>();
            foreach (var holiday in Holidays)
            {
                var countries = (holiday.Country ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim().ToLowerInvariant())
                    .ToHashSet();

                if (countries.Count == 0) continue;

                var membersInCountry = TeamMembers
                    .Where(m => !string.IsNullOrWhiteSpace(m.Country)
                                && countries.Contains(m.Country!.Trim().ToLowerInvariant()))
                    .ToList();

                foreach (var member in membersInCountry)
                {
                    holidayVacations.Add(new DayRule(
                        member.MemberId,
                        "Vacation",
                        holiday.Date,
                        holiday.Date,
                        true,
                        [$"{holiday.Name} (holiday)"]));
                }
            }

            raw.AddRange(holidayVacations);

            // Merge per member+type, skipping weekends between end and next start (via GetNextWorkDay)
            var grouped = raw.GroupBy(r => r.MemberId + r.Type);
            var merged = new List<DayRule>();

            foreach (var g in grouped)
            {
                var records = g.OrderBy(r => r.Start).ToList();
                if (records.Count == 0) continue;

                var current = records[0];
                var start = current.Start;
                var end = current.End;
                var reasons = new List<string>(current.Reasons.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());

                for (int i = 1; i < records.Count; i++)
                {
                    var r = records[i];
                    var rReasons = r.Reasons.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

                    if (r.Start.IsSameDate(end.GetNextWorkDay()))
                    {
                        // adjacent work days (can cross weeks because GetNextWorkDay skips weekend)
                        end = r.End;
                        foreach (var rr in rReasons)
                            if (!reasons.Contains(rr)) reasons.Add(rr);
                    }
                    else if (r.Start.IsBetweenInclusive(start, end))
                    {
                        if (r.End > end) end = r.End;
                        foreach (var rr in rReasons)
                            if (!reasons.Contains(rr)) reasons.Add(rr);
                    }
                    else
                    {
                        merged.Add(new DayRule(current.MemberId, current.Type, start, end, current.Approved, reasons));

                        current = r;
                        start = r.Start;
                        end = r.End;
                        reasons = new List<string>();
                        foreach (var rr in rReasons)
                            if (!reasons.Contains(rr)) reasons.Add(rr);
                    }
                }

                merged.Add(new DayRule(current.MemberId, current.Type, start, end, current.Approved, reasons));
            }

            // JS sorts descending by start.ToDateString(). Keeping descending by Start.
            return merged.OrderByDescending(r => r.Start).ToList();
        }
    }

    public IReadOnlyList<DayRule> Vacations => DayRules.Where(r => r.Type == "Vacation").ToList();
    public IReadOnlyList<DayRule> Replacement => DayRules.Where(r => r.Type == "Replacement").ToList();

    public IReadOnlyList<WhosOutItem> WhosOut
    {
        get
        {
            bool IsFinished(DateTime end, DateTime today) => end.Date < today.Date;

            return Vacations
                .Where(v => v.End >= Today.AddDays(-10) && v.Start <= Today.AddDays(60))
                .Select(v =>
                {
                    var status = Today.IsBetweenInclusive(v.Start, v.End)
                        ? 0
                        : IsFinished(v.End, Today) ? -1 : 1;

                    return new WhosOutItem(v.Start, v.End, v.MemberId, v.Reason, v.Approved, status);
                })
                .OrderBy(x => x.Status)
                .ThenBy(x => x.Start)
                .ToList();
        }
    }

    public TeamMember? GetTeamMember(string memberId)
        => TeamMembers.FirstOrDefault(m => m.MemberId == memberId);

    public bool IsOnVacation(DateTime date, string memberId)
        => Vacations.Any(v => v.MemberId == memberId && date.Date >= v.Start.Date && date.Date < v.End.Date.AddDays(1));

    public bool IsHolidayForAllTeamMembers(DateTime date)
    {
        var activeIds = ActiveTeamMembers.Select(m => m.MemberId).ToList();
        var rulesForDate = DayRules.Where(r => r.Start.Date <= date.Date && r.End.Date >= date.Date).ToList();

        return activeIds.All(id => rulesForDate.Any(r => r.MemberId == id));
    }

    public string GetAllHolidayName(DateTime date)
    {
        var activeIds = ActiveTeamMembers.Select(m => m.MemberId).ToList();
        var rulesForDate = DayRules
            .Where(r => r.Start.Date <= date.Date && r.End.Date >= date.Date)
            .ToList();

        var reasonCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in activeIds)
        {
            var memberReasons = rulesForDate
                .Where(r => r.MemberId == id)
                .SelectMany(r => r.Reasons)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var reason in memberReasons)
                reasonCount[reason] = (reasonCount.TryGetValue(reason, out var c) ? c : 0) + 1;
        }

        if (reasonCount.Count == 0) return string.Empty;

        var sorted = reasonCount
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .ToList();

        // If top reason applies to all, return only that.
        if (sorted[0].Value == activeIds.Count) return sorted[0].Key;

        return string.Join(", ", sorted.Select(x => x.Key));
    }

    public IReadOnlyList<string> FirstWeekTeamMembers
    {
        get
        {
            var members = new List<string>();
            var teamMembers = ActiveTeamMembers.ToList();
            var tempTeamMembers = ActiveTeamMembers.ToList();

            int memberIndex = 0;
            if (tempTeamMembers.Count == 0) return members;

            while (members.Count < 5)
            {
                var fixedMember = teamMembers.FirstOrDefault(m => m.FixedDay == members.Count + 1);
                if (fixedMember is not null)
                {
                    members.Add(fixedMember.MemberId);

                    // remove from temp so it's not reused
                    var idx = tempTeamMembers.FindIndex(m => m.MemberId == fixedMember.MemberId);
                    if (idx >= 0) tempTeamMembers.RemoveAt(idx);
                    if (memberIndex >= tempTeamMembers.Count) memberIndex = 0;
                    continue;
                }

                if (tempTeamMembers.Count == 0)
                {
                    members.Add(string.Empty);
                    continue;
                }

                var member = tempTeamMembers[memberIndex];
                members.Add(member.MemberId);

                if (member.DayBackup)
                {
                    memberIndex++;
                }
                else
                {
                    tempTeamMembers.RemoveAt(memberIndex);
                }

                if (tempTeamMembers.Count > 0 && memberIndex >= tempTeamMembers.Count) memberIndex = 0;
            }

            return members;
        }
    }

    public IReadOnlyList<string> SecondWeekTeamMembers
    {
        get
        {
            var members = new List<string>();
            var teamMembers = ActiveTeamMembers.ToList();
            if (teamMembers.Count == 0) return members;

            int memberIndex = 0;
            var firstWeek = FirstWeekTeamMembers;

            var restOfTeamMembers = teamMembers
                .Where(m => !firstWeek.Contains(m.MemberId))
                .Concat(teamMembers.Where(m => firstWeek.Contains(m.MemberId)).Where(m => m.DayBackup))
                .ToList();

            var tempTeamMembers = restOfTeamMembers.ToList();

            while (members.Count < 5)
            {
                var fixedMember = teamMembers.FirstOrDefault(m => m.FixedDay == members.Count + 1);
                if (fixedMember is not null)
                {
                    members.Add(fixedMember.MemberId);
                    var idx = tempTeamMembers.FindIndex(m => m.MemberId == fixedMember.MemberId);
                    if (idx >= 0) tempTeamMembers.RemoveAt(idx);
                    if (memberIndex >= tempTeamMembers.Count) memberIndex = 0;
                    continue;
                }

                if (tempTeamMembers.Count == 0)
                {
                    members.Add(string.Empty);
                    continue;
                }

                var member = tempTeamMembers[memberIndex];
                members.Add(member.MemberId);

                if (member.DayBackup)
                {
                    memberIndex++;
                }
                else
                {
                    tempTeamMembers.RemoveAt(memberIndex);
                }

                if (tempTeamMembers.Count > 0 && memberIndex >= tempTeamMembers.Count) memberIndex = 0;
            }

            return members;
        }
    }

    public IReadOnlyList<string> TwoWeekTeamMembers => FirstWeekTeamMembers.Concat(SecondWeekTeamMembers).ToList();

    public TeamMember? GetScrumMaster(DateTime currentDate, int memberIndex)
    {
        var twoWeek = TwoWeekTeamMembers;
        var scrumMasterId = memberIndex >= 0 && memberIndex < twoWeek.Count ? twoWeek[memberIndex] : string.Empty;

        var replacement = Replacement
            .FirstOrDefault(r => currentDate.Date >= r.Start.Date && currentDate.Date < r.End.Date.AddDays(1));

        if (replacement is not null)
            scrumMasterId = replacement.MemberId;

        if (string.IsNullOrWhiteSpace(scrumMasterId) || IsOnVacation(currentDate, scrumMasterId))
            scrumMasterId = GetFirstFreeBackup(currentDate, scrumMasterId, twoWeek);

        return string.IsNullOrWhiteSpace(scrumMasterId) ? null : GetTeamMember(scrumMasterId);
    }

    public string GetScrumMasterName(DateTime currentDate, int memberIndex)
    {
        var sm = GetScrumMaster(currentDate, memberIndex);
        return sm?.Name ?? "Unknown";
    }

    public string? GetFirstFreeBackup(DateTime date, string memberId, IReadOnlyList<string> twoWeekTeamMembers)
    {
        var member = GetTeamMember(memberId);
        if (member is not null)
        {
            foreach (var backupMemberId in member.BackupMembers)
            {
                if (!IsOnVacation(date, backupMemberId)) return backupMemberId;
            }
        }

        static int GetDateSeed(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;

        int seed = GetDateSeed(date) * 13;

        var reduced = Shuffle(ActiveTeamMembers.Where(m => !twoWeekTeamMembers.Contains(m.MemberId)).ToList(), seed);
        foreach (var m in reduced)
            if (!IsOnVacation(date, m.MemberId)) return m.MemberId;

        var all = Shuffle(ActiveTeamMembers.ToList(), seed);
        foreach (var m in all)
            if (!IsOnVacation(date, m.MemberId)) return m.MemberId;

        return null;
    }

    private static List<T> Shuffle<T>(List<T> list, int seed)
    {
        // Matches JS intent: take a seeded RNG and do a variant of Fisher-Yates.
        // We'll implement a deterministic shuffle with System.Random.
        var rng = new Random(seed);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
