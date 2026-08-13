using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Adjustments;

namespace ProductionManagement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AdjustmentsController(AdjustmentService adjustmentService) : ControllerBase
{
    /// <summary>Calculates a proposal. Nothing is persisted (Step 4 §8).</summary>
    [HttpPost("production-plans/{productionPlanId:long}/adjustments/preview")]
    public async Task<ActionResult<AdjustmentPreviewDto>> Preview(
        long productionPlanId, PreviewAdjustmentRequest request, CancellationToken ct)
        => Ok(await adjustmentService.PreviewAsync(productionPlanId, request, ct));

    /// <summary>
    /// Applies the manager's proposal after revalidating it against current server state.
    /// A stale proposal is rejected with 409 rather than silently altered (Step 4 §10, §11).
    /// </summary>
    [HttpPost("production-plans/{productionPlanId:long}/adjustments")]
    public async Task<ActionResult<PlanAdjustmentDto>> Apply(
        long productionPlanId, ApplyAdjustmentRequest request, CancellationToken ct)
        => Ok(await adjustmentService.ApplyAsync(productionPlanId, request, ct));

    /// <summary>Applied → Reversed. History is preserved, never edited (Step 4 §13).</summary>
    [HttpPost("plan-adjustments/{adjustmentId:long}/reverse")]
    public async Task<ActionResult<PlanAdjustmentDto>> Reverse(long adjustmentId, CancellationToken ct)
        => Ok(await adjustmentService.ReverseAsync(adjustmentId, ct));
}
