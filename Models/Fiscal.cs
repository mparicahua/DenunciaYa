namespace DenunciaYA.API.Models;

public class Fiscal
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public int PersonaId { get; set; }
    public int DistritoFiscalId { get; set; }
    public string? Especialidad { get; set; }
    public string CodigoFiscal { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
