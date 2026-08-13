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
    /// <summary>Creates the order together with its initial production plans, in one transaction.</summary>
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

    [HttpGet("{orderId:long}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(long orderId, CancellationToken ct)
        => Ok(await orderService.GetByIdAsync(orderId, ct));

    /// <summary>The daily production view: plan, actual and derived shortage/difference combined.</summary>
    [HttpGet("{orderId:long}/production-plans")]
    public async Task<ActionResult<ProductionPlanListDto>> GetProductionPlans(long orderId, CancellationToken ct)
        => Ok(await orderService.GetProductionPlansAsync(orderId, ct));

    [HttpPost("{orderId:long}/production-records")]
    public async Task<ActionResult<ProductionRecordDto>> CreateProductionRecord(
        long orderId, CreateProductionRecordRequest request, CancellationToken ct)
        => Ok(await productionRecordService.CreateAsync(orderId, request, ct));

    /// <summary>Replaces the recorded actual. Actual is a value, never an increment.</summary>
    [HttpPut("{orderId:long}/production-records/{productionRecordId:long}")]
    public async Task<ActionResult<ProductionRecordDto>> UpdateProductionRecord(
        long orderId, long productionRecordId, UpdateProductionRecordRequest request, CancellationToken ct)
        => Ok(await productionRecordService.UpdateAsync(orderId, productionRecordId, request, ct));

    [HttpGet("{orderId:long}/plan-adjustments")]
    public async Task<ActionResult<IReadOnlyList<PlanAdjustmentDto>>> GetPlanAdjustments(long orderId, CancellationToken ct)
        => Ok(await adjustmentService.GetHistoryAsync(orderId, ct));

    [HttpGet("{orderId:long}/statistics")]
    public async Task<ActionResult<OrderStatisticsDto>> GetStatistics(long orderId, CancellationToken ct)
        => Ok(await statisticsService.GetOrderStatisticsAsync(orderId, ct));
}
