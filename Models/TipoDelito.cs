namespace DenunciaYA.API.Models;

public class TipoDelito
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ArticuloCp { get; set; }
    public string? Descripcion { get; set; }
    public string? Gravedad { get; set; }
    public bool Activo { get; set; }
}
