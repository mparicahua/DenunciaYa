namespace DenunciaYA.API.Models;

public class Funcionario
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public int PersonaId { get; set; }
    public int DistritoFiscalId { get; set; }
    public string Cargo { get; set; } = string.Empty;
    public string CodigoEmpleado { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
