using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fantasoccer.Application.Api.Hubs;
using Fantasoccer.Application.Api.Mcp;
using Fantasoccer.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Fantasoccer.Application.Api.Controllers;

// HTTP/MCP adapter for the existing auction commands. All business logic remains in AuctionHub.
// Broadcasts still reach the real SignalR clients; caller notifications become the tool response.
[ApiController]
[Authorize(Policy = "authenticated")]
[Route("McpAuction/[action]")]
public sealed class McpAuctionController(AuctionHubService auctions, IHubContext<AuctionHub> liveHub,
    IServiceProvider services, McpOptions options) : ControllerBase
{
    [JsonConverter(typeof(JsonStringEnumConverter<AuctionCommand>))]
    public enum AuctionCommand
    {
        JoinAuction, LeaveAuction, GetConnectedUsers, SetCurrentRole, PlaceBid, GetDiscardedPlayers,
        ShowNextPlayer, CloseCurrentAuction, ChangePlayerStatus, AssignPlayerToTeam, ChangeTimer,
        PauseAuction, ResumeAuction, CloseAuction, FinalizeAuction, ReopenAuction, SendEmojiReaction,
        CompleteReset, TransferAuctioneerRole, TimerReset
    }

    public sealed record AuctionRequest(AuctionCommand Operation, Dictionary<string, JsonElement> Arguments);

    [HttpGet]
    public IActionResult Commands()
        => !options.Enabled ? NotFound() : Ok(Enum.GetValues<AuctionCommand>().Select(command =>
        {
            var method = typeof(AuctionHub).GetMethod(command + "Async")!;
            return new
            {
                operation = command.ToString(),
                arguments = method.GetParameters().Select(parameter => new
                {
                    name = parameter.Name, type = parameter.ParameterType.Name,
                    required = !parameter.HasDefaultValue,
                    defaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : null,
                    enumValues = parameter.ParameterType.IsEnum
                        ? Enum.GetValues(parameter.ParameterType).Cast<object>().Select(value => new { name = value.ToString(), value = Convert.ToInt64(value) }).ToArray()
                        : null
                })
            };
        }));

    [HttpPost]
    public async Task<IActionResult> InvokeAsync([FromBody] AuctionRequest request)
    {
        if (!options.Enabled) return NotFound();
        if (!Enum.IsDefined(request.Operation) || !request.Arguments.TryGetValue("auctionId", out var auctionId) ||
            auctionId.ValueKind != JsonValueKind.String) return BadRequest("A valid operation and auctionId are required.");
        var email = User.FindFirstValue(ClaimTypes.Email);
        var wrapper = await auctions.GetCurrentWrapperAsync(auctionId.GetString()!);
        if (email == null || wrapper?.Group?.Users?.Any(x =>
            string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase) && x.Role != IdentityRole.None) != true) return NotFound();

        var method = typeof(AuctionHub).GetMethod(request.Operation + "Async", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        var parameters = method.GetParameters();
        if (request.Arguments.Keys.Except(parameters.Select(x => x.Name!), StringComparer.Ordinal).Any()) return BadRequest("Unknown command argument.");
        var arguments = new object?[parameters.Length];
        try
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (request.Arguments.TryGetValue(parameter.Name!, out var argument))
                    arguments[i] = argument.Deserialize(parameter.ParameterType);
                else if (parameter.HasDefaultValue) arguments[i] = parameter.DefaultValue;
                else return BadRequest("Missing command argument: " + parameter.Name);
            }
        }
        catch (JsonException) { return BadRequest("Invalid command arguments."); }

        using var hub = ActivatorUtilities.CreateInstance<AuctionHub>(services);
        var notifications = new ConcurrentQueue<object>();
        var caller = new CallerProxy(notifications);
        hub.Context = new CallerContext(HttpContext);
        hub.Clients = new CallerClients(liveHub.Clients, caller);
        hub.Groups = liveHub.Groups;
        var task = (Task)method.Invoke(hub, arguments)!;
        await task;
        var result = method.ReturnType.IsGenericType ? method.ReturnType.GetProperty("Result")!.GetValue(task) : null;
        var response = new { result, notifications = notifications.ToArray() };
        return caller.Failed ? BadRequest(response) : Ok(response);
    }

    private sealed class CallerContext(HttpContext context) : HubCallerContext
    {
        public override string ConnectionId => "mcp-" + context.TraceIdentifier;
        public override string? UserIdentifier => context.User.FindFirstValue(ClaimTypes.Email);
        public override ClaimsPrincipal User => context.User;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features => context.Features;
        public override CancellationToken ConnectionAborted => context.RequestAborted;
        public override void Abort() { }
    }

    private sealed class CallerProxy(ConcurrentQueue<object> notifications) : IClientProxy
    {
        public bool Failed { get; private set; }
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (method == "Error") Failed = true;
            notifications.Enqueue(new { @event = method, arguments = args });
            return Task.CompletedTask;
        }
    }

    private sealed class CallerClients(IHubClients clients, IClientProxy caller) : IHubCallerClients
    {
        public IClientProxy Caller => caller;
        public IClientProxy Others => clients.All;
        public IClientProxy All => clients.All;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => clients.AllExcept(excludedConnectionIds);
        public IClientProxy Client(string connectionId) => clients.Client(connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => clients.Clients(connectionIds);
        public IClientProxy Group(string groupName) => clients.Group(groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => clients.GroupExcept(groupName, excludedConnectionIds);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => clients.Groups(groupNames);
        public IClientProxy OthersInGroup(string groupName) => clients.Group(groupName);
        public IClientProxy User(string userId) => clients.User(userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => clients.Users(userIds);
    }
}
