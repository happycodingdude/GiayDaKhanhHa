using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Statistics;

namespace ProductionManagement.Api.Controllers;

[ApiController]
[Route("api/v1/statistics")]
public sealed class StatisticsController(StatisticsService statisticsService) : ControllerBase
{
    /// <summary>Số liệu dashboard đều là suy ra. Không có entity hay bảng dashboard nào (Step 4 §16).</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatisticsDto>> GetDashboard(CancellationToken ct)
        => Ok(await statisticsService.GetDashboardAsync(ct));
}
