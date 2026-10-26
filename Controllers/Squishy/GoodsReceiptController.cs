using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

[Route("api/[controller]")]
[ApiController]
public class GoodsReceiptController : ControllerBase
{
    private readonly IGoodsReceiptService _goodsReceiptService;

    public GoodsReceiptController(IGoodsReceiptService goodsReceiptService)
    {
        _goodsReceiptService = goodsReceiptService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _goodsReceiptService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _goodsReceiptService.GetByIdAsync(id);
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoodsReceiptDto dto)
    {
        var result = await _goodsReceiptService.CreateReceiptAsync(dto);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelGoodsReceiptDto dto)
    {
        var result = await _goodsReceiptService.CancelReceiptAsync(id, dto?.Reason);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers()
    {
        var result = await _goodsReceiptService.GetSuppliersAsync();
        return Ok(result);
    }

    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] SupplierDto dto)
    {
        var result = await _goodsReceiptService.CreateSupplierAsync(dto);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("stock-transactions")]
    public async Task<IActionResult> GetStockTransactions([FromQuery] Guid? productId = null)
    {
        var result = await _goodsReceiptService.GetStockTransactionsAsync(productId);
        return Ok(result);
    }
}
