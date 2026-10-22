using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

[Route("api/[controller]")]
[ApiController]
public class ReturnController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status = null, [FromQuery] string? requestType = null, [FromQuery] string? keyword = null)
    {
        var result = await _returnService.GetAllAsync(status, requestType, keyword);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _returnService.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    [HttpGet("order/{orderCode}")]
    public async Task<IActionResult> GetReturnableOrder(string orderCode)
    {
        var result = await _returnService.GetReturnableOrderAsync(orderCode);
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequest request)
    {
        var result = await _returnService.CreateAsync(request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReturnStatusRequest request)
    {
        var result = await _returnService.UpdateStatusAsync(id, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }
}
