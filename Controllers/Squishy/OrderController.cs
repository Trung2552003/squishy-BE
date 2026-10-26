using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

[Route("api/[controller]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status = null, [FromQuery] string? keyword = null, [FromQuery] string? orderType = null)
    {
        var result = await _orderService.GetAllAsync(status, keyword, orderType);
        return Ok(result);
    }

    [HttpGet("fulfillment")]
    public async Task<IActionResult> GetFulfillment([FromQuery] string? prepStatus = null)
    {
        var result = await _orderService.GetFulfillmentOrdersAsync(prepStatus);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _orderService.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var result = await _orderService.CreateOrderAsync(request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
    {
        var result = await _orderService.UpdateStatusAsync(id, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Sửa đơn khi nhập sai (người nhận, phí ship, người trả ship, giảm giá, vận chuyển).</summary>
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateInfo(Guid id, [FromBody] UpdateOrderInfoRequest request)
    {
        var result = await _orderService.UpdateInfoAsync(id, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}/fulfillment")]
    public async Task<IActionResult> UpdateFulfillment(Guid id, [FromBody] UpdateFulfillmentRequest request)
    {
        var result = await _orderService.UpdateFulfillmentAsync(id, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id}/payment-proof")]
    public async Task<IActionResult> UploadPaymentProof(Guid id, [FromBody] string imageUrl)
    {
        var result = await _orderService.UploadPaymentProofAsync(id, imageUrl);
        return Ok(result);
    }
}
