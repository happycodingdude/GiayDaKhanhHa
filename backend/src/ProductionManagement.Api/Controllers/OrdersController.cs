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
    OrderImageService orderImageService,
    ProductionScheduleService productionScheduleService,
    ProductionDayService productionDayService,
    AdjustmentService adjustmentService,
    StatisticsService statisticsService) : ControllerBase
{
    /// <summary>
    /// Nhập hàng. <c>multipart/form-data</c> vì ảnh mẫu đi kèm ngay trong bước tạo (CR-001 §6.4).
    /// Đơn ra đời ở trạng thái <c>Pending</c>: chưa có ngày, chưa có dây chuyền, chưa có kế hoạch.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(OrderImageRules.MaxSizeBytes + 512 * 1024)]
    public async Task<ActionResult<OrderDetailDto>> Create(
        [FromForm] string? shoeCode,
        [FromForm] int quantity,
        IFormFile? image,
        CancellationToken ct)
    {
        var validated = await ValidateImageAsync(image, ct);
        var order = await orderService.ReceiveAsync(new CreateOrderRequest(shoeCode, quantity), validated, ct);

        return CreatedAtAction(nameof(GetById), new { orderId = order.Id }, order);
    }

    /// <summary>
    /// Sửa thông tin nhập hàng, kèm thay (<c>image</c>) hoặc gỡ (<c>removeImage=true</c>) ảnh mẫu,
    /// tất cả trong một lần lưu. <c>multipart/form-data</c> vì ảnh đi kèm request, giống bước tạo.
    /// </summary>
    [HttpPut("{orderId:guid}")]
    [RequestSizeLimit(OrderImageRules.MaxSizeBytes + 512 * 1024)]
    public async Task<ActionResult<OrderDetailDto>> Update(
        Guid orderId,
        [FromForm] string? shoeCode,
        [FromForm] int quantity,
        [FromForm] bool removeImage,
        IFormFile? image,
        CancellationToken ct)
    {
        var validated = await ValidateImageAsync(image, ct);
        var request = new UpdateOrderRequest(shoeCode, quantity, removeImage);

        return Ok(await orderService.UpdateAsync(orderId, request, validated, ct));
    }

    /// <summary>Xoá một đơn nhập hàng. Chỉ đơn chưa lập tiến độ mới xoá được.</summary>
    [HttpDelete("{orderId:guid}")]
    public async Task<IActionResult> Delete(Guid orderId, CancellationToken ct)
    {
        await orderService.DeleteAsync(orderId, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> GetList(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] Guid? productionLineId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await orderService.GetListAsync(status, search, productionLineId, page, pageSize, ct));

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(Guid orderId, CancellationToken ct)
        => Ok(await orderService.GetByIdAsync(orderId, ct));

    // --- Ảnh mẫu (CR-001 §6.4) --------------------------------------------------------------

    /// <summary>Thay ảnh mẫu. Mỗi đơn tối đa 1 ảnh nên đây luôn là thay thế, không phải thêm mới.</summary>
    [HttpPut("{orderId:guid}/image")]
    [RequestSizeLimit(OrderImageRules.MaxSizeBytes + 512 * 1024)]
    public async Task<ActionResult<OrderDetailDto>> ReplaceImage(
        Guid orderId, IFormFile? image, CancellationToken ct)
    {
        var validated = await ValidateImageAsync(image, ct)
            ?? throw new Domain.ValidationException("image", "REQUIRED", "An image file is required.");

        return Ok(await orderImageService.ReplaceAsync(orderId, validated, ct));
    }

    [HttpDelete("{orderId:guid}/image")]
    public async Task<ActionResult<OrderDetailDto>> DeleteImage(Guid orderId, CancellationToken ct)
        => Ok(await orderImageService.DeleteAsync(orderId, ct));

    /// <summary>
    /// Nội dung ảnh. Endpoint này yêu cầu xác thực như mọi endpoint khác, nên đường dẫn vật lý trên
    /// disk không bao giờ bị lộ ra ngoài (CR-001 §2 QĐ-6).
    /// </summary>
    [HttpGet("{orderId:guid}/image/content")]
    public async Task<IActionResult> GetImageContent(Guid orderId, CancellationToken ct)
    {
        var image = await orderImageService.GetContentAsync(orderId, ct);
        return File(image.Content, image.ContentType, image.FileName);
    }

    // --- Tiến độ ------------------------------------------------------------------------------

    /// <summary>
    /// Lập tiến độ: dây chuyền + khoảng ngày + phân bổ 2 tầng, trong một transaction. Thao tác một
    /// lần cho mỗi đơn (CR-001 §6.6, BR-N05).
    /// </summary>
    [HttpPost("{orderId:guid}/production-schedule")]
    public async Task<ActionResult<OrderDetailDto>> CreateProductionSchedule(
        Guid orderId, CreateProductionScheduleRequest request, CancellationToken ct)
        => Ok(await productionScheduleService.CreateAsync(orderId, request, ct));

    /// <summary>Ma trận ngày × dây chuyền: kế hoạch, thực tế và phần thiếu/chênh lệch suy ra.</summary>
    [HttpGet("{orderId:guid}/production-plans")]
    public async Task<ActionResult<ProductionMatrixDto>> GetProductionMatrix(Guid orderId, CancellationToken ct)
        => Ok(await orderService.GetProductionMatrixAsync(orderId, ct));

    // --- Sản lượng theo ô ---------------------------------------------------------------------

    /// <summary>
    /// Toàn bộ state của một ô sản xuất: kế hoạch, các lần đã ghi nhận, trần còn được nhập và trạng
    /// thái đóng/mở. Đây là màn hình chính của luồng ghi nhận sản lượng (CR-01 §6.3).
    /// </summary>
    [HttpGet("{orderId:guid}/production-days/{productionDate}/lines/{productionLineId:guid}")]
    public async Task<ActionResult<ProductionCellDetailDto>> GetProductionCell(
        Guid orderId, DateOnly productionDate, Guid productionLineId, CancellationToken ct)
        => Ok(await productionDayService.GetAsync(orderId, productionDate, productionLineId, ct));

    /// <summary>
    /// Ghi nhận thêm một lần sản lượng trong ô. Sản lượng là số cộng thêm, không phải giá trị thay
    /// thế (CR-01 OV-2). Trả về state đầy đủ của ô để frontend không phải refetch.
    /// </summary>
    [HttpPost("{orderId:guid}/production-days/{productionDate}/lines/{productionLineId:guid}/entries")]
    public async Task<ActionResult<ProductionCellDetailDto>> CreateProductionEntry(
        Guid orderId, DateOnly productionDate, Guid productionLineId,
        CreateProductionEntryRequest request, CancellationToken ct)
        => Ok(await productionDayService.CreateEntryAsync(orderId, productionDate, productionLineId, request, ct));

    /// <summary>
    /// Xuất hàng — chốt sổ cả một ngày: mọi dây chuyền có kế hoạch trong ngày đóng cùng lúc. Body
    /// rỗng: sản lượng do server tính từ các lần ghi nhận, client không bao giờ gửi lên con số này
    /// (CR-01 §6.6, N-11). Không có endpoint mở lại, và không có endpoint chốt riêng một dây chuyền.
    /// </summary>
    [HttpPost("{orderId:guid}/production-days/{productionDate}/close")]
    public async Task<ActionResult<CloseProductionDayDto>> CloseProductionDay(
        Guid orderId, DateOnly productionDate, CancellationToken ct)
        => Ok(await productionDayService.CloseDayAsync(orderId, productionDate, ct));

    [HttpGet("{orderId:guid}/plan-adjustments")]
    public async Task<ActionResult<IReadOnlyList<PlanAdjustmentDto>>> GetPlanAdjustments(Guid orderId, CancellationToken ct)
        => Ok(await adjustmentService.GetHistoryAsync(orderId, ct));

    [HttpGet("{orderId:guid}/statistics")]
    public async Task<ActionResult<OrderStatisticsDto>> GetStatistics(Guid orderId, CancellationToken ct)
        => Ok(await statisticsService.GetOrderStatisticsAsync(orderId, ct));

    /// <summary>
    /// Đổi <c>IFormFile</c> thành ảnh đã kiểm hợp lệ. Đây là chỗ DUY NHẤT trong hệ thống chạm vào
    /// kiểu của ASP.NET; từ đây trở vào chỉ còn <see cref="ImageUpload"/>.
    /// </summary>
    private static async Task<ValidatedImage?> ValidateImageAsync(IFormFile? image, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
        {
            return null;
        }

        await using var content = image.OpenReadStream();
        return await OrderImageRules.ValidateAsync(
            new ImageUpload(image.FileName, image.ContentType, image.Length, content), ct);
    }
}
