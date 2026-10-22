using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // Cài đặt tài khoản nhận tiền chỉ cho người đã đăng nhập CMS
    // (nếu để ngỏ, người lạ có thể đổi số tài khoản để nhận tiền của khách)
    [Authorize]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        return Ok(await _paymentService.GetSettingAsync());
    }

    [Authorize]
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdatePaymentSettingDto dto)
    {
        var result = await _paymentService.UpdateSettingAsync(dto);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("webhook-logs")]
    public async Task<IActionResult> GetWebhookLogs([FromQuery] int take = 20)
    {
        return Ok(await _paymentService.GetRecentWebhookLogsAsync(take));
    }

    [HttpGet("generate-qr")]
    public async Task<IActionResult> GenerateQr([FromQuery] string orderCode, [FromQuery] decimal amount, [FromQuery] string? transferContent = null)
    {
        var url = await _paymentService.GenerateQrUrlAsync(orderCode, amount, transferContent);
        return Ok(new { QrUrl = url });
    }

    /// <summary>
    /// SePay gọi vào đây khi tài khoản có biến động số dư.
    /// Đọc nguyên văn body để kiểm tra chữ ký HMAC-SHA256 (SePay ký trên chuỗi "{timestamp}.{body}" gốc).
    /// </summary>
    [HttpPost("sepay-webhook")]
    public async Task<IActionResult> SePayWebhook()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        var result = await _paymentService.ProcessSePayWebhookAsync(rawBody, Request.Headers);
        if (!result.Authorized) return Unauthorized(new { success = false, message = result.Message });

        // SePay coi HTTP 200 + success=true là đã nhận; trả lỗi sẽ khiến SePay gửi lại
        return Ok(new { success = true, status = result.Status, message = result.Message });
    }
}
