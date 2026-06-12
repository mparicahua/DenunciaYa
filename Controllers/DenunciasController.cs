using System.Security.Claims;
using DenunciaYA.API.DTOs;
using DenunciaYA.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DenunciaYA.API.Controllers;

[ApiController]
[Route("api/denuncias")]
public class DenunciasController(DenunciaService denunciaService) : ControllerBase
{
    private int GetUserId()
    {
        var claim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? int.Parse(claim) : 1;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDenunciaRequest req)
    {
        try
        {
            var userId = GetUserId();
            var result = await denunciaService.CreateAsync(req, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> UpdateEstado(int id, [FromBody] UpdateEstadoRequest req)
    {
        try
        {
            var userId = GetUserId();
            await denunciaService.UpdateEstadoAsync(id, req, userId);
            return Ok(new { mensaje = "Estado actualizado correctamente." });
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

    [HttpDelete("{denuncia_id:int}/evidencias/{id:int}")]
    public async Task<IActionResult> DeleteEvidencia(int denuncia_id, int id)
    {
        try
        {
            await denunciaService.DeleteEvidenciaAsync(denuncia_id, id);
            return Ok(new { mensaje = "Evidencia eliminada correctamente" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpGet("{codigo}/detalle")]
    public async Task<IActionResult> GetDetalle(string codigo)
    {
        try
        {
            var result = await denunciaService.GetDetalleAsync(codigo);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpGet("{id:int}/historial")]
    public async Task<IActionResult> GetHistorial(int id)
    {
        var result = await denunciaService.GetHistorialAsync(id);
        return Ok(result);
    }

    [HttpGet("pendientes-asignacion")]
    public async Task<IActionResult> GetPendientesAsignacion()
    {
        var result = await denunciaService.GetPendientesAsignacionAsync();
        return Ok(result);
    }
}
