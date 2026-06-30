using System.Text.Json;

namespace DenunciaYA.API.DTOs;

public class CreateDenunciaRequest
{
    public int TipoDelitoId { get; set; }
    public int? UbigeoId { get; set; }
    public string? DireccionHecho { get; set; }
    public DateTime? FechaHecho { get; set; }
    public string DescripcionHecho { get; set; } = string.Empty;
    public bool EsAnonima { get; set; }
    public List<DenunciadoRequest> Denunciados { get; set; } = [];
    public List<TestigoRequest> Testigos { get; set; } = [];

    // Datos propios del tipo de delito (ej. monto_estimado, placa_vehiculo, relacion_agresor).
    public JsonElement? DetallesEspecificos { get; set; }
}

public class DenunciadoRequest
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? RelacionConDenunciante { get; set; }
    public string? DescripcionFisica { get; set; }
}

public class TestigoRequest
{
    public string? NombreAnonimo { get; set; }
    public string? Declaracion { get; set; }
}

public class CreateDenunciaResponse
{
    public int Id { get; set; }
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

public class UpdateEstadoRequest
{
    public int NuevoEstadoId { get; set; }
    public string? Motivo { get; set; }
}

public class DenunciaDetalleResponse
{
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public string DescripcionHecho { get; set; } = string.Empty;
    public DateTime? FechaHecho { get; set; }
    public string TipoDelito { get; set; } = string.Empty;
    public string CategoriaDelito { get; set; } = string.Empty;
    public string EstadoActual { get; set; } = string.Empty;
    public string NombreDenunciante { get; set; } = string.Empty;
    public string? DistritoFiscal { get; set; }
    public JsonElement? DetallesEspecificos { get; set; }
}

public class DenunciaPendienteResponse
{
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public DateTime? FechaHecho { get; set; }
    public string TipoDelito { get; set; } = string.Empty;
    public string? DistritoFiscal { get; set; }
}

public class HistorialEstadoResponse
{
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public string UsuarioCambio { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; }
}

public class DenunciaFuncionarioResponse
{
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public DateTime? FechaHecho { get; set; }
    public string TipoDelito { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string NombreDenunciante { get; set; } = string.Empty;
}
