using Fantasoccer.Application.Core.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fantasoccer.Application.Api.Controllers;

[ApiController]
[Authorize(Policy = "super_admin")]
[Route("api/operations")]
[ServiceFilter(typeof(WorkspaceFaultFilter))]
public sealed class OperationsController(OperationsApplicationService operations, SqlJobApplicationService jobs) : ControllerBase
{
    [HttpGet("logs")] public Task<OperationalLogDto[]> Logs([FromQuery] int limit = 100, [FromQuery] bool? errorsOnly = null, [FromQuery] string? query = null, CancellationToken cancellationToken = default) => operations.LogsAsync(User, limit, errorsOnly, query, cancellationToken);
    [HttpGet("logs/statistics")] public Task<OperationalLogStatistics> Statistics(CancellationToken cancellationToken) => operations.StatisticsAsync(User, cancellationToken);
    [HttpGet("logs/{id:guid}")] public Task<OperationalLogDto> Log(Guid id, CancellationToken cancellationToken) => operations.LogAsync(User, id, cancellationToken);
    [HttpGet("jobs")] public Task<SqlJobStateDto[]> Jobs(CancellationToken cancellationToken) => jobs.StatesAsync(User, cancellationToken);
    [HttpPost("jobs/run")] public Task<SqlJobResult?> Run(RunSqlJobRequest request, CancellationToken cancellationToken) => jobs.RunAsync(User, request, cancellationToken);
}
