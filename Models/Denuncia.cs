namespace DenunciaYA.API.Models;

public class Denuncia
{
    public int Id { get; set; }
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public int DenuncianteId { get; set; }
    public int TipoDelitoId { get; set; }
    public int EstadoId { get; set; }
    public int? DistritoFiscalId { get; set; }
    public int? UbigeoId { get; set; }
    public string? DireccionHecho { get; set; }
    public DateTime? FechaHecho { get; set; }
    public string DescripcionHecho { get; set; } = string.Empty;
    public bool EsAnonima { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
