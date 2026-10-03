using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Orders;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.ProductionLines;

/// <summary>
/// Danh mục dây chuyền sản xuất (CR-001 §6.3). Đây là cấu hình, không phải dữ liệu phát sinh:
/// không có thao tác xoá ở Phase này, chỉ bật/tắt trạng thái (QĐ-9, BR-N15).
/// </summary>
public sealed class ProductionLineService(IAppDbContext db, IClock clock)
{
    public async Task<ProductionLineListDto> GetListAsync(string? status, CancellationToken ct = default)
    {
        var query = db.ProductionLines.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<ProductionLineStatus>(status, ignoreCase: true, out var parsed))
            {
                throw new ValidationException(
                    "status", "INVALID_VALUE", "Status must be 'Active', 'Inactive' or 'All'.");
            }

            query = query.Where(l => l.Status == parsed);
        }

        // Cùng thứ tự với thứ tự chia đều ở tầng 1, để bảng cấu hình và ma trận phân bổ đọc khớp nhau.
        var lines = await query
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Code)
            .Select(l => new
            {
                Line = l,
                // Đã được gán cho ít nhất một đơn hàng — frontend dùng để ẩn hành động xoá (BR-N15).
                InUse = db.OrderProductionLines.Any(o => o.ProductionLineId == l.Id),
            })
            .ToListAsync(ct);

        var inProduction = await db.Orders.AsNoTracking()
            .InProductionOn(clock.Today)
            .SelectMany(o => o.ProductionLines)
            .GroupBy(l => l.ProductionLineId)
            .Select(g => new { ProductionLineId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductionLineId, x => x.Count, ct);

        return new ProductionLineListDto(lines
            .Select(x => ToDto(x.Line, x.InUse, inProduction.GetValueOrDefault(x.Line.Id)))
            .ToList());
    }

    public async Task<ProductionLineDto> CreateAsync(SaveProductionLineRequest request, CancellationToken ct = default)
    {
        var line = ProductionLine.Create(request.Code, request.Name, request.SortOrder, request.Note, clock.UtcNow);

        await GuardCodeAvailableAsync(line.Code, null, ct);

        db.ProductionLines.Add(line);
        await SaveAsync(line.Code, ct);

        return ToDto(line, inUse: false, inProductionOrderCount: 0);
    }

    public async Task<ProductionLineDto> UpdateAsync(
        Guid productionLineId, SaveProductionLineRequest request, CancellationToken ct = default)
    {
        var line = await FindAsync(productionLineId, ct);

        line.Update(request.Code, request.Name, request.SortOrder, request.Note, clock.UtcNow);

        await GuardCodeAvailableAsync(line.Code, productionLineId, ct);
        await SaveAsync(line.Code, ct);

        return ToDto(
            line, await IsInUseAsync(productionLineId, ct), (await OrdersInProductionAsync(productionLineId, ct)).Count);
    }

    /// <summary>
    /// Bật/tắt dây chuyền. Ngừng hoạt động bị chặn khi dây chuyền đang được gán cho đơn "Đang sản
    /// xuất" — đơn đó sẽ mất chỗ sản xuất giữa chừng. Ngoài trường hợp đó, ngừng vẫn được phép kể cả
    /// khi dây chuyền đã được dùng: nó chỉ ảnh hưởng tới lựa chọn mới, dữ liệu lịch sử vẫn hiển thị
    /// bình thường (CR-001 §6.3, BR-N14).
    /// </summary>
    public async Task<ProductionLineDto> ChangeStatusAsync(
        Guid productionLineId, UpdateProductionLineStatusRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ProductionLineStatus>(request.Status, ignoreCase: true, out var status))
        {
            throw new ValidationException("status", "INVALID_VALUE", "Status must be 'Active' or 'Inactive'.");
        }

        var line = await FindAsync(productionLineId, ct);
        var inProduction = await OrdersInProductionAsync(productionLineId, ct);

        if (status == ProductionLineStatus.Inactive && inProduction.Count > 0)
        {
            throw new ConflictException(
                ErrorCodes.ProductionLineInProduction,
                $"Production line '{line.Code}' is used by {inProduction.Count} order(s) in production "
                + $"({string.Join(", ", inProduction)}) and cannot be deactivated.");
        }

        line.ChangeStatus(status, clock.UtcNow);
        await db.SaveChangesAsync(ct);

        return ToDto(line, await IsInUseAsync(productionLineId, ct), inProduction.Count);
    }

    private async Task<ProductionLine> FindAsync(Guid productionLineId, CancellationToken ct)
        => await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == productionLineId, ct)
           ?? throw new NotFoundException(ErrorCodes.ProductionLineNotFound, "Production line was not found.");

    private Task<bool> IsInUseAsync(Guid productionLineId, CancellationToken ct)
        => db.OrderProductionLines.AnyAsync(o => o.ProductionLineId == productionLineId, ct);

    /// <summary>Mã giày của các đơn "Đang sản xuất" trên dây chuyền.</summary>
    private Task<List<string>> OrdersInProductionAsync(Guid productionLineId, CancellationToken ct)
        => db.Orders.AsNoTracking()
            .InProductionOn(clock.Today)
            .Where(o => o.ProductionLines.Any(l => l.ProductionLineId == productionLineId))
            .OrderBy(o => o.ShoeCode)
            .Select(o => o.ShoeCode)
            .ToListAsync(ct);

    private async Task GuardCodeAvailableAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        var taken = await db.ProductionLines
            .AnyAsync(l => l.Code == code && (exceptId == null || l.Id != exceptId), ct);

        if (taken)
        {
            throw new ConflictException(
                ErrorCodes.ProductionLineCodeAlreadyExists, $"Production line code '{code}' is already in use.");
        }
    }

    /// <summary>Kiểm tra trước vẫn có thể thua một request song song; unique index là chốt chặn cuối.</summary>
    private async Task SaveAsync(string code, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException?.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505")
        {
            throw new ConflictException(
                ErrorCodes.ProductionLineCodeAlreadyExists, $"Production line code '{code}' is already in use.");
        }
    }

    private static ProductionLineDto ToDto(ProductionLine line, bool inUse, int inProductionOrderCount)
        => new(line.Id, line.Code, line.Name, line.Status.ToString(), line.SortOrder, line.Note, inUse,
            inProductionOrderCount);
}
