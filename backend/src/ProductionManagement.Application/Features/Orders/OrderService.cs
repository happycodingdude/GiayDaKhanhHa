using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;

namespace ProductionManagement.Application.Features.Orders;

/// <summary>
/// Nghiệp vụ Nhập hàng: tạo/sửa đơn ở trạng thái <c>Pending</c> và đọc dữ liệu đơn hàng.
/// Ảnh gửi kèm lúc tạo hoặc lúc sửa được lưu cùng lần lưu đơn, nên nằm ở đây; thao tác riêng lẻ trên
/// ảnh nằm ở <see cref="OrderImageService"/>. Lập tiến độ nằm ở <see cref="ProductionScheduleService"/>
/// — thời điểm nghiệp vụ khác thì không dồn vào một service (CR-001 §4.1).
/// </summary>
public sealed class OrderService(IAppDbContext db, IClock clock, IOrderImageStorage imageStorage)
{
    /// <summary>
    /// Nhập hàng. Đơn ra đời ở <c>Pending</c>: chưa có ngày, chưa có dây chuyền, chưa có kế hoạch
    /// (CR-001 §6.4, BR-N04).
    /// </summary>
    public async Task<OrderDetailDto> ReceiveAsync(
        CreateOrderRequest request, ValidatedImage? image, CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        // Toàn bộ việc kiểm tra field nằm trong aggregate root.
        var order = Order.Receive(request.ShoeCode, request.Quantity, now);

        var code = order.ShoeCode;
        if (await db.Orders.AnyAsync(o => o.ShoeCode == code, ct))
        {
            throw new ConflictException(
                ErrorCodes.ShoeCodeAlreadyExists, $"Shoe code '{code}' is already in use.");
        }

        // Ghi file TRƯỚC khi commit: nếu ghi database hỏng thì xoá file lại được, còn nếu commit
        // trước rồi ghi file hỏng thì database sẽ trỏ tới một file không tồn tại — hỏng nặng hơn
        // nhiều so với một file mồ côi (CR-001 §10).
        string? imagePath = null;
        if (image is not null)
        {
            imagePath = await SaveImageAsync(order, image, ct);
        }

        try
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Một request khác đã chèn cùng mã giày vào giữa lúc kiểm tra và lúc insert.
            DeleteImageFile(imagePath);
            throw new ConflictException(
                ErrorCodes.ShoeCodeAlreadyExists, $"Shoe code '{code}' is already in use.");
        }
        catch
        {
            DeleteImageFile(imagePath);
            throw;
        }

        return ToDetailDto(order, [], EmptyDerived(order));
    }

    /// <summary>
    /// Sửa thông tin nhập hàng, kèm thay hoặc gỡ ảnh mẫu, trong MỘT lần lưu: hoặc mọi thay đổi được
    /// lưu, hoặc không thay đổi nào cả. Số lượng chỉ đổi được khi đơn chưa lập tiến độ.
    ///
    /// Thứ tự giống hệt lúc nhập hàng: kiểm tra xong hết mới ghi file mới → commit database → mới xoá
    /// file cũ. Commit hỏng thì dọn file mới; file cũ vẫn nguyên vì database vẫn trỏ vào nó.
    /// </summary>
    public async Task<OrderDetailDto> UpdateAsync(
        Guid orderId, UpdateOrderRequest request, ValidatedImage? image, CancellationToken ct = default)
    {
        // Hai ý định trái ngược nhau: client lỗi, không đoán hộ nó muốn cái nào.
        if (image is not null && request.RemoveImage)
        {
            throw new ValidationException(
                "removeImage", "CONFLICTS_WITH_IMAGE", "A new image cannot be uploaded and removed in the same request.");
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        OrderMutationGuard.EnsureEditable(order, clock.Today);

        order.UpdateReceipt(request.ShoeCode, request.Quantity, clock.UtcNow);

        var code = order.ShoeCode;
        if (await db.Orders.AnyAsync(o => o.ShoeCode == code && o.Id != orderId, ct))
        {
            throw new ConflictException(
                ErrorCodes.ShoeCodeAlreadyExists, $"Shoe code '{code}' is already in use.");
        }

        string? newImagePath = null;
        string? previousImagePath = null;

        if (image is not null)
        {
            previousImagePath = order.ImagePath;
            newImagePath = await SaveImageAsync(order, image, ct);
        }
        else if (request.RemoveImage && order.HasImage)
        {
            // Gỡ ảnh khi đơn không có ảnh là no-op chứ không phải 404: PUT phải gửi lại được mà
            // không lỗi, kể cả khi lần gửi trước đã thành công.
            previousImagePath = order.RemoveImage(clock.UtcNow);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            DeleteImageFile(newImagePath);
            throw new ConflictException(
                ErrorCodes.ShoeCodeAlreadyExists, $"Shoe code '{code}' is already in use.");
        }
        catch
        {
            DeleteImageFile(newImagePath);
            throw;
        }

        DeleteImageFile(previousImagePath);

        return await GetByIdAsync(orderId, ct);
    }

    /// <summary>
    /// Xoá cứng một đơn chưa lập tiến độ, kèm file ảnh của nó.
    ///
    /// Khoá dòng đơn trước khi kiểm trạng thái: lập tiến độ cũng khoá đúng dòng này, nên một request
    /// lập tiến độ chạy song song không thể chèn kế hoạch vào một đơn vừa bị xoá, và ngược lại.
    /// </summary>
    public async Task DeleteAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);

        if (!await db.LockOrderAsync(orderId, ct))
        {
            throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");
        }

        var order = await db.Orders.FirstAsync(o => o.Id == orderId, ct);
        order.EnsureDeletable();

        var imagePath = order.ImagePath;

        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // File chỉ bị xoá SAU khi commit: rollback thì dòng database vẫn trỏ tới một file còn nguyên.
        DeleteImageFile(imagePath);
    }

    public async Task<PagedResult<OrderListItemDto>> GetListAsync(
        string? status, string? search, Guid? productionLineId, int page, int pageSize,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var query = db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            // "Scheduled" gom Incomplete + Completed: đó chính là tập hợp mà màn hình Tiến độ hiển
            // thị, và nó không diễn đạt được bằng một giá trị OrderStatus đơn lẻ (CR-001 §7.7).
            if (string.Equals(status, "Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status != OrderStatus.Pending);
            }
            else if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(o => o.Status == parsedStatus);
            }
            else
            {
                throw new ValidationException(
                    "status", "INVALID_VALUE",
                    "Status must be 'Pending', 'Incomplete', 'Completed', 'Scheduled' or 'All'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Tìm theo mã giày không phân biệt hoa thường, viết theo cách không phụ thuộc provider.
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(o => o.ShoeCode.ToLower().Contains(term));
        }

        if (productionLineId is { } lineId)
        {
            query = query.Where(o => o.ProductionLines.Any(l => l.ProductionLineId == lineId));
        }

        var totalCount = await query.CountAsync(ct);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.Id).ToList();

        var cellsByOrder = await OrderQueries.PlanCellsAsync(db, orderIds, ct);
        var linesByOrder = await OrderQueries.ProductionLinesAsync(db, orderIds, ct);
        var snapshots = await db.SnapshotsForOrdersAsync(orderIds, ct);

        var today = clock.Today;
        var snapshotsByOrder = snapshots.ToLookup(d => d.OrderId);

        var items = orders.Select(order =>
        {
            var cells = cellsByOrder[order.Id].ToList();
            var orderSnapshots = snapshotsByOrder[order.Id].ToList();
            var actualByCell = orderSnapshots.ToDictionary(d => d.Key);

            var derived = OrderDerivedCalculator.Compute(
                order.Quantity, order.Status, order.DueDate,
                cells, orderSnapshots.Select(d => d.ToActualCell()).ToList(), today);

            // Vị thế hôm nay gộp mọi dây chuyền: danh sách chỉ cần một con số để liếc qua.
            var todayCells = cells.Where(c => c.ProductionDate == today && c.PlannedQuantity > 0).ToList();
            var todayPlanned = todayCells.Count == 0 ? (int?)null : todayCells.Sum(c => c.PlannedQuantity);
            var todayActual = todayCells.Count == 0
                ? (int?)null
                : todayCells.Sum(c => actualByCell.TryGetValue(
                    new CellKey(c.ProductionDate, c.ProductionLineId), out var d) ? d.ActualQuantity : 0);

            // Chỉ đơn chưa hoàn thành mới cần cảnh báo ô treo: đơn đã xong thì phần thiếu còn lại
            // không cần xử lý nữa (CR-01 §14.6).
            var hasUnclosedPastCell = order.Status == OrderStatus.Incomplete
                && cells.Any(c =>
                    c.PlannedQuantity > 0
                    && c.ProductionDate < today
                    && !(actualByCell.TryGetValue(new CellKey(c.ProductionDate, c.ProductionLineId), out var d)
                         && d.IsClosed));

            return new OrderListItemDto(
                order.Id,
                order.ShoeCode,
                order.Quantity,
                order.StartDate,
                order.DueDate,
                order.Status.ToString(),
                order.HasImage,
                OrderQueries.ImageUrlFor(order),
                linesByOrder[order.Id].ToList(),
                derived.TotalActual,
                derived.Remaining,
                derived.TotalPlan,
                derived.ProgressPercentage,
                derived.ScheduleStatus,
                derived.BehindQuantity,
                derived.DaysRemaining,
                derived.IsOverdue,
                todayPlanned,
                todayActual,
                hasUnclosedPastCell);
        }).ToList();

        return new PagedResult<OrderListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<OrderDetailDto> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        var derived = await ComputeDerivedAsync(order, ct);
        var lines = (await OrderQueries.ProductionLinesAsync(db, [orderId], ct))[orderId].ToList();

        return ToDetailDto(order, lines, derived);
    }

    /// <summary>
    /// Ma trận ngày × dây chuyền: kế hoạch, thực tế và phần thiếu/chênh lệch suy ra, trả phẳng theo
    /// ô để frontend dựng ma trận mà không phải tự join (CR-001 §6.7).
    /// </summary>
    public async Task<ProductionMatrixDto> GetProductionMatrixAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException(ErrorCodes.OrderNotFound, "Order was not found.");

        var plans = await db.ProductionPlans.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.ProductionDate)
            .ToListAsync(ct);

        var snapshots = await db.SnapshotsForOrderAsync(orderId, ct);
        var snapshotsByCell = snapshots.ToDictionary(d => d.Key);

        var userNames = await OrderQueries.UserDisplayNamesAsync(
            db, snapshots.Where(d => d.LastRecordedBy.HasValue).Select(d => d.LastRecordedBy!.Value), ct);

        // Một kế hoạch nguồn tại một thời điểm chỉ có tối đa một điều chỉnh Applied (Step 4 §12).
        var planIds = plans.Select(p => p.Id).ToList();
        var activeBySourcePlan = await db.PlanAdjustments.AsNoTracking()
            .Where(a => planIds.Contains(a.SourceProductionPlanId) && a.Status == AdjustmentStatus.Applied)
            .ToDictionaryAsync(a => a.SourceProductionPlanId, a => a.Id, ct);

        var today = clock.Today;

        var items = plans.Select(plan =>
        {
            snapshotsByCell.TryGetValue(new CellKey(plan.ProductionDate, plan.ProductionLineId), out var day);

            // Chưa ghi nhận lần nào thì để null, không bao giờ là 0. Ô còn mở có sản lượng tạm tính
            // nhưng KHÔNG có phần thiếu và không có chênh lệch (CR-01 OV-5, N-07).
            return new ProductionCellDto(
                Id: plan.Id,
                ProductionDate: plan.ProductionDate,
                ProductionLineId: plan.ProductionLineId,
                InitialPlannedQuantity: plan.InitialPlannedQuantity,
                AddOnQuantity: plan.PlannedQuantity - plan.InitialPlannedQuantity,
                PlannedQuantity: plan.PlannedQuantity,
                DayStatus: ProductionDayQueries.DisplayStatusOf(
                    plan.PlannedQuantity, plan.ProductionDate, day?.IsClosed == true, today),
                ActualQuantity: day?.ActualQuantity,
                IsProvisional: day is not null && !day.IsClosed,
                ProductionDayId: day?.Id,
                ShortageQuantity: ProductionCalculations.Shortage(plan.PlannedQuantity, day?.ClosedActualQuantity),
                Difference: ProductionCalculations.Difference(plan.PlannedQuantity, day?.ClosedActualQuantity),
                ClosedAt: day?.ClosedAt,
                HasActiveAdjustment: activeBySourcePlan.ContainsKey(plan.Id),
                ActiveAdjustmentId: activeBySourcePlan.TryGetValue(plan.Id, out var adjustmentId) ? adjustmentId : null,
                LastRecordedBy: day?.LastRecordedBy is null ? null : userNames.GetValueOrDefault(day.LastRecordedBy.Value),
                LastRecordedAt: day?.LastRecordedAt);
        }).ToList();

        var lines = (await OrderQueries.ProductionLinesAsync(db, [orderId], ct))[orderId]
            .Select(line => new ProductionMatrixLineDto(
                line.Id,
                line.Code,
                line.Name,
                line.Status,
                line.SortOrder,
                line.AllocatedQuantity,
                items.Where(i => i.ProductionLineId == line.Id).Sum(i => i.PlannedQuantity),
                snapshots.Where(d => d.ProductionLineId == line.Id).Sum(d => d.ActualQuantity)))
            .ToList();

        return new ProductionMatrixDto(orderId, order.StartDate, order.DueDate, lines, items);
    }

    internal async Task<OrderDerivedValues> ComputeDerivedAsync(Order order, CancellationToken ct)
    {
        var cells = (await OrderQueries.PlanCellsAsync(db, [order.Id], ct))[order.Id].ToList();
        var snapshots = await db.SnapshotsForOrderAsync(order.Id, ct);

        return OrderDerivedCalculator.Compute(
            order.Quantity, order.Status, order.DueDate,
            cells, snapshots.Select(d => d.ToActualCell()).ToList(), clock.Today);
    }

    internal static OrderDetailDto ToDetailDto(
        Order order, IReadOnlyList<OrderProductionLineDto> lines, OrderDerivedValues derived)
        => new(
            order.Id,
            order.ShoeCode,
            order.Quantity,
            order.StartDate,
            order.DueDate,
            order.Status.ToString(),
            order.HasImage,
            OrderQueries.ImageUrlFor(order),
            order.ImageFileName,
            lines,
            derived.TotalActual,
            derived.Remaining,
            derived.TotalPlan,
            derived.TotalInitialPlan,
            derived.ProgressPercentage,
            derived.ScheduleStatus,
            derived.BehindQuantity,
            derived.DaysRemaining,
            derived.IsOverdue,
            derived.IsPastDueDate,
            order.CreatedAt,
            order.UpdatedAt);

    /// <summary>Đơn vừa nhập hàng chưa có ô nào, nên mọi giá trị suy ra tính từ tập rỗng.</summary>
    private OrderDerivedValues EmptyDerived(Order order)
        => OrderDerivedCalculator.Compute(order.Quantity, order.Status, order.DueDate, [], [], clock.Today);

    private async Task<string> SaveImageAsync(Order order, ValidatedImage image, CancellationToken ct)
    {
        using var content = new MemoryStream(image.Content, writable: false);
        var path = await imageStorage.SaveAsync(order.Id, image.Extension, content, ct);

        order.AttachImage(
            new OrderImage(path, image.FileName, image.ContentType, image.Content.Length), clock.UtcNow);

        return path;
    }

    private void DeleteImageFile(string? path)
    {
        if (path is not null)
        {
            imageStorage.Delete(path);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";
}
