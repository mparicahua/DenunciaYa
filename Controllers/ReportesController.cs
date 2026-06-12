using DenunciaYA.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DenunciaYA.API.Controllers;

[ApiController]
[Route("api/reportes")]
public class ReportesController(ReporteService reporteService) : ControllerBase
{
    [HttpGet("denuncias-por-delito")]
    public async Task<IActionResult> PorDelito(
        [FromQuery] DateTime? fecha_inicio = null,
        [FromQuery] DateTime? fecha_fin = null,
        [FromQuery] int min_denuncias = 0)
    {
        var result = await reporteService.GetPorDelitoAsync(fecha_inicio, fecha_fin, min_denuncias);
        return Ok(result);
    }

    [HttpGet("denuncias-por-delito/exportar")]
    public async Task<IActionResult> ExportarPorDelito(
        [FromQuery] DateTime? fecha_inicio = null,
        [FromQuery] DateTime? fecha_fin = null,
        [FromQuery] int min_denuncias = 0)
    {
        var csv = await reporteService.ExportarPorDelitoCsvAsync(fecha_inicio, fecha_fin, min_denuncias);
        var filename = $"reporte-denuncias-{DateTime.Now:yyyyMMdd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", filename);
    }

    [HttpGet("denuncias-por-mes")]
    public async Task<IActionResult> PorMes([FromQuery] int? anio = null)
    {
        var anioConsulta = anio ?? DateTime.Now.Year;
        var result = await reporteService.GetPorMesAsync(anioConsulta);
        return Ok(result);
    }

    [HttpGet("denuncias-por-zona")]
    public async Task<IActionResult> PorZona(
        [FromQuery] DateTime? fecha_inicio = null,
        [FromQuery] DateTime? fecha_fin = null,
        [FromQuery] int min_denuncias = 1)
    {
        var result = await reporteService.GetPorZonaAsync(fecha_inicio, fecha_fin, min_denuncias);
        return Ok(result);
    }
}
