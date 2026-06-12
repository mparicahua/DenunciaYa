namespace DenunciaYA.API.Models;

public class EstadoDenuncia
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsFinal { get; set; }
    public int Orden { get; set; }
}
