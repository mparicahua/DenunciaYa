using System.Security.Claims;
using DenunciaYA.API.DTOs;
using DenunciaYA.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DenunciaYA.API.Controllers;

[ApiController]
[Route("api/asignaciones")]
public class AsignacionesController(AsignacionService asignacionService) : ControllerBase
{
    private int GetUserId()
    {
        var claim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 1;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAsignacionRequest req)
    {
        try
        {
            var userId = GetUserId();
            var result = await asignacionService.CreateAsync(req, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}
