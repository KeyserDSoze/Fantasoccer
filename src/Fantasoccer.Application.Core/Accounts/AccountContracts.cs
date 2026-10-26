using System.Text.Json.Serialization;

namespace Fantasoccer.Application.Core.Accounts;

public sealed record IdentityGroupDto([property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("role")] int Role);
public sealed record AccountStateDto([property: JsonPropertyName("groupId")] Guid? GroupId = null,
    [property: JsonPropertyName("leagueId")] Guid? LeagueId = null, [property: JsonPropertyName("year")] int? Year = null,
    [property: JsonPropertyName("meOrLeaderEmail")] string? MeOrLeaderEmail = null);
public sealed record AccountIdentityDto(Guid Id, string Username, string Email, AccountStateDto State, IdentityGroupDto[] Groups, bool IsGlobalAdministrator = false);
public sealed record UpdateAccountProfileRequest(string Username);
public sealed record UserSettingsDto(string? AiPrompt, bool AutoOptimalFormation, bool LiveWidgetEnabled);
public sealed record UpdateUserSettingsRequest(bool AutoOptimalFormation, bool LiveWidgetEnabled);
public sealed record ProvisionAccountRequest(string Email, string Username, IdentityGroupDto[] Groups);
public sealed class GlobalAdministratorOptions { public string[] Emails { get; set; } = []; }
