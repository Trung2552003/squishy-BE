using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

/// <summary>Thu chi khác: dữ liệu tài chính nội bộ nên bắt buộc đăng nhập.</summary>
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class CashTransactionController : ControllerBase
{
    private readonly ICashTransactionService _service;

    public CashTransactionController(ICashTransactionService service)
    {
        _service = service;
    }

    [HttpGet("categories")]
    public IActionResult GetCategories() => Ok(_service.GetCategories());

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? type = null, [FromQuery] string? category = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? keyword = null)
    {
        return Ok(await _service.GetAllAsync(type, category, from, to, keyword));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCashTransactionRequest request)
    {
        var result = await _service.SaveAsync(null, request, User.FindFirst(ClaimTypes.Name)?.Value);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCashTransactionRequest request)
    {
        var result = await _service.SaveAsync(id, request, User.FindFirst(ClaimTypes.Name)?.Value);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }
}
