namespace DenunciaYA.API.DTOs;

public class ReportePorDelitoResponse
{
    public string TipoDelito { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int TotalDenuncias { get; set; }
    public decimal Porcentaje { get; set; }
}

public class ReportePorMesResponse
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int TotalDenuncias { get; set; }
    public int TotalAdmitidas { get; set; }
    public int TotalArchivadas { get; set; }
}

public class ReportePorZonaResponse
{
    public string Departamento { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string Distrito { get; set; } = string.Empty;
    public int TotalDenuncias { get; set; }
}
