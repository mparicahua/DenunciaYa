using DenunciaYA.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DenunciaYA.API.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController(MongoLogService mongoLogService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 20)
    {
        try
        {
            var total = await mongoLogService.CountAsync();
            var logs = await mongoLogService.GetRecentAsync(limit);
            return Ok(new { total, logs });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
