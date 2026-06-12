using DenunciaYA.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace DenunciaYA.API.Controllers;

[ApiController]
[Route("api/funcionarios")]
public class FuncionariosController(DenunciaService denunciaService) : ControllerBase
{
    [HttpGet("{id:int}/denuncias")]
    public async Task<IActionResult> GetDenuncias(int id)
    {
        var result = await denunciaService.GetByFuncionarioAsync(id);
        return Ok(result);
    }
}
