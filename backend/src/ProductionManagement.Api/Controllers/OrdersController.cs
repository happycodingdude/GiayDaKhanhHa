using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Adjustments;
using ProductionManagement.Application.Features.Orders;
using ProductionManagement.Application.Features.Production;
using ProductionManagement.Application.Features.Statistics;

namespace ProductionManagement.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController(
    OrderService orderService,
    ProductionDayService productionDayService,
    AdjustmentService adjustmentService,
    StatisticsService statisticsService) : ControllerBase
{
    /// <summary>Tạo đơn hàng cùng kế hoạch sản xuất ban đầu trong một transaction.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDetailDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await orderService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { orderId = order.Id }, order);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> GetList(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await orderService.GetListAsync(status, search, page, pageSize, ct));

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(Guid orderId, CancellationToken ct)
        => Ok(await orderService.GetByIdAsync(orderId, ct));

    /// <summary>Bảng sản xuất theo ngày: gộp kế hoạch, thực tế và phần thiếu/chênh lệch suy ra.</summary>
    [HttpGet("{orderId:guid}/production-plans")]
    public async Task<ActionResult<ProductionPlanListDto>> GetProductionPlans(Guid orderId, CancellationToken ct)
        => Ok(await orderService.GetProductionPlansAsync(orderId, ct));

    /// <summary>
    /// Toàn bộ state của một ngày sản xuất: kế hoạch, các lần đã ghi nhận, trần còn được nhập và
    /// trạng thái đóng/mở. Đây là màn hình chính của luồng ghi nhận sản lượng (CR-01 §6.3).
    /// </summary>
    [HttpGet("{orderId:guid}/production-days/{productionDate}")]
    public async Task<ActionResult<ProductionDayDetailDto>> GetProductionDay(
        Guid orderId, DateOnly productionDate, CancellationToken ct)
        => Ok(await productionDayService.GetAsync(orderId, productionDate, ct));

    /// <summary>
    /// Ghi nhận thêm một lần sản lượng trong ngày. Sản lượng là số cộng thêm, không phải giá trị
    /// thay thế (CR-01 OV-2). Trả về state đầy đủ của ngày để frontend không phải refetch.
    /// </summary>
    [HttpPost("{orderId:guid}/production-days/{productionDate}/entries")]
    public async Task<ActionResult<ProductionDayDetailDto>> CreateProductionEntry(
        Guid orderId, DateOnly productionDate, CreateProductionEntryRequest request, CancellationToken ct)
        => Ok(await productionDayService.CreateEntryAsync(orderId, productionDate, request, ct));

    /// <summary>
    /// Xuất hàng — chốt sổ ngày sản xuất. Body rỗng: sản lượng do server tính từ các lần ghi nhận,
    /// client không bao giờ gửi lên con số này (CR-01 §6.6, N-11). Không có endpoint mở lại.
    /// </summary>
    [HttpPost("{orderId:guid}/production-days/{productionDate}/close")]
    public async Task<ActionResult<CloseProductionDayDto>> CloseProductionDay(
        Guid orderId, DateOnly productionDate, CancellationToken ct)
        => Ok(await productionDayService.CloseAsync(orderId, productionDate, ct));

    [HttpGet("{orderId:guid}/plan-adjustments")]
    public async Task<ActionResult<IReadOnlyList<PlanAdjustmentDto>>> GetPlanAdjustments(Guid orderId, CancellationToken ct)
        => Ok(await adjustmentService.GetHistoryAsync(orderId, ct));

    [HttpGet("{orderId:guid}/statistics")]
    public async Task<ActionResult<OrderStatisticsDto>> GetStatistics(Guid orderId, CancellationToken ct)
        => Ok(await statisticsService.GetOrderStatisticsAsync(orderId, ct));
}
