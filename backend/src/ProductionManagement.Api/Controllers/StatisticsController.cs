using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Statistics;

namespace ProductionManagement.Api.Controllers;

[ApiController]
[Route("api/v1/statistics")]
public sealed class StatisticsController(StatisticsService statisticsService) : ControllerBase
{
    /// <summary>Derived dashboard figures. There is no dashboard entity or table (Step 4 §16).</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatisticsDto>> GetDashboard(CancellationToken ct)
        => Ok(await statisticsService.GetDashboardAsync(ct));
}
