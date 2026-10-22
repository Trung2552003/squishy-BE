using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

[Route("api/[controller]")]
[ApiController]
public class PreorderController : ControllerBase
{
    private readonly IPreorderService _preorderService;

    public PreorderController(IPreorderService preorderService)
    {
        _preorderService = preorderService;
    }

    // ===== Trang đặt trước công khai cho khách =====

    [HttpGet("campaigns/open")]
    public async Task<IActionResult> GetOpenCampaigns()
    {
        return Ok(await _preorderService.GetOpenCampaignsAsync());
    }

    [HttpGet("campaigns/by-slug/{slug}")]
    public async Task<IActionResult> GetCampaignPage(string slug)
    {
        var result = await _preorderService.GetCampaignPageAsync(slug);
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePreorderRequest request)
    {
        var result = await _preorderService.CreateAsync(request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    // ===== Quản trị trong CMS =====

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status = null, [FromQuery] string? keyword = null, [FromQuery] Guid? campaignId = null)
    {
        return Ok(await _preorderService.GetAllAsync(status, keyword, campaignId));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] Guid? campaignId = null)
    {
        return Ok(await _preorderService.GetSummaryAsync(campaignId));
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns()
    {
        return Ok(await _preorderService.GetCampaignsAsync());
    }

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] SavePreorderCampaignRequest request)
    {
        var result = await _preorderService.SaveCampaignAsync(null, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("campaigns/{id}")]
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] SavePreorderCampaignRequest request)
    {
        var result = await _preorderService.SaveCampaignAsync(id, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("campaigns/{id}")]
    public async Task<IActionResult> DeleteCampaign(Guid id)
    {
        return Ok(await _preorderService.DeleteCampaignAsync(id));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePreorderStatusRequest request)
    {
        var result = await _preorderService.UpdateStatusAsync(id, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("items/all")]
    public async Task<IActionResult> GetAllItems()
    {
        return Ok(await _preorderService.GetAllItemsAsync());
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] SavePreorderItemRequest request)
    {
        var result = await _preorderService.SaveItemAsync(null, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] SavePreorderItemRequest request)
    {
        var result = await _preorderService.SaveItemAsync(id, request);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        return Ok(await _preorderService.DeleteItemAsync(id));
    }
}
