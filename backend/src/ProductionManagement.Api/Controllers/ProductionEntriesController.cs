using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Production;

namespace ProductionManagement.Api.Controllers;

/// <summary>
/// Sửa/xoá một lần ghi nhận sản lượng. Chỉ hợp lệ khi ngày còn mở — ngày đã Xuất hàng là bất biến
/// (CR-01 §6.5, N-04). Không bắt buộc nhập lý do, nhưng mọi thay đổi đều vào production_entry_logs.
/// </summary>
[ApiController]
[Route("api/v1/production-entries")]
public sealed class ProductionEntriesController(ProductionDayService productionDayService) : ControllerBase
{
    [HttpPut("{entryId:guid}")]
    public async Task<ActionResult<ProductionDayDetailDto>> Update(
        Guid entryId, UpdateProductionEntryRequest request, CancellationToken ct)
        => Ok(await productionDayService.UpdateEntryAsync(entryId, request, ct));

    /// <summary>Xoá mềm. Không có hard delete: lịch sử ghi nhận phải dựng lại được (CR-01 §6.5).</summary>
    [HttpDelete("{entryId:guid}")]
    public async Task<ActionResult<ProductionDayDetailDto>> Delete(Guid entryId, CancellationToken ct)
        => Ok(await productionDayService.DeleteEntryAsync(entryId, ct));
}
