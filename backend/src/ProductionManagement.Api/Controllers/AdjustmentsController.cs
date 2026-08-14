using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Adjustments;

namespace ProductionManagement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class AdjustmentsController(AdjustmentService adjustmentService) : ControllerBase
{
    /// <summary>Tính ra đề xuất. Không lưu gì xuống database (Step 4 §8).</summary>
    [HttpPost("production-plans/{productionPlanId:guid}/adjustments/preview")]
    public async Task<ActionResult<AdjustmentPreviewDto>> Preview(
        Guid productionPlanId, PreviewAdjustmentRequest request, CancellationToken ct)
        => Ok(await adjustmentService.PreviewAsync(productionPlanId, request, ct));

    /// <summary>
    /// Áp dụng đề xuất của quản lý sau khi kiểm tra lại với trạng thái hiện tại của server.
    /// Đề xuất đã cũ bị từ chối bằng 409 chứ không bị sửa ngầm (Step 4 §10, §11).
    /// </summary>
    [HttpPost("production-plans/{productionPlanId:guid}/adjustments")]
    public async Task<ActionResult<PlanAdjustmentDto>> Apply(
        Guid productionPlanId, ApplyAdjustmentRequest request, CancellationToken ct)
        => Ok(await adjustmentService.ApplyAsync(productionPlanId, request, ct));

    /// <summary>Applied → Reversed. Lịch sử được giữ nguyên, không bao giờ bị sửa (Step 4 §13).</summary>
    [HttpPost("plan-adjustments/{adjustmentId:guid}/reverse")]
    public async Task<ActionResult<PlanAdjustmentDto>> Reverse(Guid adjustmentId, CancellationToken ct)
        => Ok(await adjustmentService.ReverseAsync(adjustmentId, ct));
}
