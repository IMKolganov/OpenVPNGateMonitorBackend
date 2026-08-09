using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.Performance;
using DataGateMonitor.SharedModels.DataGateMonitor.Performance;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

[ApiController]
[Route("api/performance")]
[Authorize(Roles = "Admin")]
public sealed class PerformanceController(IPerformanceSampleStore sampleStore) : BaseController
{
    [HttpGet("http-requests")]
    [ProducesResponseType(typeof(ApiResponse<PerformanceHttpRequestsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PerformanceHttpRequestsResponse>>> GetHttpRequests(
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        var items = await sampleStore.GetHttpLatestAsync(limit, ct);
        var response = new PerformanceHttpRequestsResponse
        {
            Items = items.Select(MapHttp).ToList()
        };
        return Ok(ApiResponse<PerformanceHttpRequestsResponse>.SuccessResponse(response));
    }

    [HttpGet("db-queries")]
    [ProducesResponseType(typeof(ApiResponse<PerformanceDbQueriesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PerformanceDbQueriesResponse>>> GetDbQueries(
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        var items = await sampleStore.GetDbLatestAsync(limit, ct);
        var response = new PerformanceDbQueriesResponse
        {
            Items = items.Select(MapDb).ToList()
        };
        return Ok(ApiResponse<PerformanceDbQueriesResponse>.SuccessResponse(response));
    }

    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> ClearAll(CancellationToken ct = default)
    {
        await sampleStore.ClearAllAsync(ct);
        return Ok(ApiResponse<string>.SuccessResponse("Performance samples cleared."));
    }

    [HttpDelete("http-requests")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> ClearHttp(CancellationToken ct = default)
    {
        await sampleStore.ClearHttpAsync(ct);
        return Ok(ApiResponse<string>.SuccessResponse("HTTP performance samples cleared."));
    }

    [HttpDelete("db-queries")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> ClearDb(CancellationToken ct = default)
    {
        await sampleStore.ClearDbAsync(ct);
        return Ok(ApiResponse<string>.SuccessResponse("DB performance samples cleared."));
    }

    private static PerformanceHttpRequestEntryDto MapHttp(PerformanceHttpSample s) =>
        new()
        {
            TimestampUtc = s.TimestampUtc,
            RequestId = s.RequestId,
            Method = s.Method,
            Path = s.Path,
            StatusCode = s.StatusCode,
            DurationMs = s.DurationMs,
            UserName = s.UserName,
            TraceId = s.TraceId
        };

    private static PerformanceDbQueryEntryDto MapDb(PerformanceDbSample s) =>
        new()
        {
            TimestampUtc = s.TimestampUtc,
            RequestId = s.RequestId,
            DurationMs = s.DurationMs,
            CommandType = s.CommandType,
            Sql = s.Sql,
            Succeeded = s.Succeeded,
            ExceptionMessage = s.ExceptionMessage
        };
}
