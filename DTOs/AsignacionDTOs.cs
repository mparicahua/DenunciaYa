namespace DenunciaYA.API.DTOs;

public class CreateAsignacionRequest
{
    public int DenunciaId { get; set; }
    public int FuncionarioId { get; set; }
    public string? Observaciones { get; set; }
}

public class CreateAsignacionResponse
{
    public int Id { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
