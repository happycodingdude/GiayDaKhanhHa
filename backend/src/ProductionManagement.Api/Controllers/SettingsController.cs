using Microsoft.AspNetCore.Mvc;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Application.Features.Settings;

namespace ProductionManagement.Api.Controllers;

/// <summary>
/// Cấu hình vận hành. Chu kỳ ghi nhận chỉ để nhắc trên màn hình nhập sản lượng — server không dùng
/// nó để từ chối request nào, và cấu hình không hồi tố dữ liệu đã ghi (CR-01 §6.8, N-10).
/// </summary>
[ApiController]
[Route("api/v1/settings")]
public sealed class SettingsController(SettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemSettingsDto>> Get(CancellationToken ct)
        => Ok(await settingsService.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<SystemSettingsDto>> Update(
        UpdateSystemSettingsRequest request, CancellationToken ct)
        => Ok(await settingsService.UpdateAsync(request, ct));
}
