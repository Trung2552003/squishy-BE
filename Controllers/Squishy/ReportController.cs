using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

/// <summary>Số liệu doanh thu, lợi nhuận là dữ liệu nội bộ: bắt buộc đăng nhập.</summary>
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <param name="from">Từ ngày (tính cả ngày này)</param>
    /// <param name="to">Đến trước ngày này (không tính); bỏ trống from/to = toàn bộ thời gian</param>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        return Ok(await _reportService.GetSummaryAsync(from, to));
    }

    [HttpGet("chart")]
    public async Task<IActionResult> GetChart([FromQuery] int days = 7, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        return Ok(await _reportService.GetChartAsync(days, from, to));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts([FromQuery] int top = 5, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        return Ok(await _reportService.GetTopProductsAsync(top, from, to));
    }
}
