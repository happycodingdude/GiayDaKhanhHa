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
    ProductionRecordService productionRecordService,
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

    [HttpPost("{orderId:guid}/production-records")]
    public async Task<ActionResult<ProductionRecordDto>> CreateProductionRecord(
        Guid orderId, CreateProductionRecordRequest request, CancellationToken ct)
        => Ok(await productionRecordService.CreateAsync(orderId, request, ct));

    /// <summary>Thay thế thực tế đã ghi. Thực tế là một giá trị, không bao giờ là số cộng thêm.</summary>
    [HttpPut("{orderId:guid}/production-records/{productionRecordId:guid}")]
    public async Task<ActionResult<ProductionRecordDto>> UpdateProductionRecord(
        Guid orderId, Guid productionRecordId, UpdateProductionRecordRequest request, CancellationToken ct)
        => Ok(await productionRecordService.UpdateAsync(orderId, productionRecordId, request, ct));

    [HttpGet("{orderId:guid}/plan-adjustments")]
    public async Task<ActionResult<IReadOnlyList<PlanAdjustmentDto>>> GetPlanAdjustments(Guid orderId, CancellationToken ct)
        => Ok(await adjustmentService.GetHistoryAsync(orderId, ct));

    [HttpGet("{orderId:guid}/statistics")]
    public async Task<ActionResult<OrderStatisticsDto>> GetStatistics(Guid orderId, CancellationToken ct)
        => Ok(await statisticsService.GetOrderStatisticsAsync(orderId, ct));
}
