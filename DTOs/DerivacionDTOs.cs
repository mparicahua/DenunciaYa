namespace DenunciaYA.API.DTOs;

public class CreateDerivacionRequest
{
    public int DenunciaId { get; set; }
    public int FiscalId { get; set; }
    public string? Motivo { get; set; }
}

public class CreateDerivacionResponse
{
    public int Id { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
