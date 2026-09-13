using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.ProductionLines;

namespace ProductionManagement.Api.Controllers;

/// <summary>
/// Danh mục dây chuyền sản xuất (CR-001 §6.3). Không có endpoint xoá ở Phase này: dây chuyền đã
/// được dùng thì không xoá được, và dây chuyền chưa dùng cũng chỉ cần tắt (QĐ-9, BR-N15).
/// </summary>
[ApiController]
[Route("api/v1/production-lines")]
public sealed class ProductionLinesController(ProductionLineService productionLineService) : ControllerBase
{
    /// <summary>Bỏ trống <c>status</c> để lấy tất cả; màn hình lập tiến độ chỉ lấy <c>Active</c>.</summary>
    [HttpGet]
    public async Task<ActionResult<ProductionLineListDto>> GetList(
        [FromQuery] string? status, CancellationToken ct)
        => Ok(await productionLineService.GetListAsync(status, ct));

    [HttpPost]
    public async Task<ActionResult<ProductionLineDto>> Create(SaveProductionLineRequest request, CancellationToken ct)
        => Ok(await productionLineService.CreateAsync(request, ct));

    [HttpPut("{productionLineId:guid}")]
    public async Task<ActionResult<ProductionLineDto>> Update(
        Guid productionLineId, SaveProductionLineRequest request, CancellationToken ct)
        => Ok(await productionLineService.UpdateAsync(productionLineId, request, ct));

    /// <summary>
    /// Bật/tắt. Chuyển sang Inactive luôn được phép, kể cả khi đang được dùng — chỉ ảnh hưởng tới
    /// lựa chọn mới (BR-N14).
    /// </summary>
    [HttpPatch("{productionLineId:guid}/status")]
    public async Task<ActionResult<ProductionLineDto>> ChangeStatus(
        Guid productionLineId, UpdateProductionLineStatusRequest request, CancellationToken ct)
        => Ok(await productionLineService.ChangeStatusAsync(productionLineId, request, ct));
}
