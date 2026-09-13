namespace ProductionManagement.Application.Contracts;

/// <summary>
/// Một dây chuyền trong danh mục cấu hình. <see cref="InUse"/> = đã được gán cho ít nhất một đơn
/// hàng; frontend dùng nó để ẩn hành động xoá (CR-001 §6.3, BR-N15).
/// </summary>
public sealed record ProductionLineDto(
    Guid Id,
    string Code,
    string Name,
    string Status,
    int SortOrder,
    string? Note,
    bool InUse);

public sealed record ProductionLineListDto(IReadOnlyList<ProductionLineDto> Items);

public sealed record SaveProductionLineRequest(string? Code, string? Name, int SortOrder, string? Note);

public sealed record UpdateProductionLineStatusRequest(string? Status);
