using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE.Controllers.Squishy;

[Route("api/[controller]")]
[ApiController]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public MediaController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? entityType = null, [FromQuery] string? mediaType = null)
    {
        var result = await _mediaService.GetAllMediaAsync(entityType, mediaType);
        return Ok(result);
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file, [FromQuery] string entityType = "PRODUCT", [FromQuery] Guid? entityId = null, [FromQuery] bool isPrimary = false)
    {
        var result = await _mediaService.UploadImageAsync(file, entityType, entityId, isPrimary);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("upload-video")]
    public async Task<IActionResult> UploadVideo([FromForm] IFormFile file, [FromQuery] string entityType = "PRODUCT", [FromQuery] Guid? entityId = null)
    {
        var result = await _mediaService.UploadVideoAsync(file, entityType, entityId);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("by-entity/{entityId}")]
    public async Task<IActionResult> GetByEntity(Guid entityId, [FromQuery] string entityType = "PRODUCT")
    {
        var result = await _mediaService.GetMediaByEntityAsync(entityId, entityType);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool isVideo = false)
    {
        var result = await _mediaService.DeleteMediaAsync(id, isVideo);
        return Ok(result);
    }

    [HttpPost("attach")]
    public async Task<IActionResult> AttachMedia([FromBody] AttachMediaRequest request)
    {
        var result = await _mediaService.AttachMediaAsync(request);
        return Ok(result);
    }
}
