using System.Text;
using DenunciaYA.API.DTOs;
using Npgsql;

namespace DenunciaYA.API.Services;

public class ReporteService(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("PostgreSQL")!;

    public async Task<List<ReportePorDelitoResponse>> GetPorDelitoAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int minDenuncias)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT td.nombre AS tipo_delito, cd.nombre AS categoria,
                     COUNT(d.id) AS total_denuncias,
                     ROUND((COUNT(d.id) * 100.0) / (SELECT COUNT(*) FROM denuncias), 2) AS porcentaje
              FROM denuncias d
              INNER JOIN tipos_delito td      ON d.tipo_delito_id = td.id
              INNER JOIN categorias_delito cd ON td.categoria_id = cd.id
              WHERE (@fecha_inicio::timestamptz IS NULL OR d.fecha_hecho >= @fecha_inicio)
                AND (@fecha_fin::timestamptz    IS NULL OR d.fecha_hecho <= @fecha_fin)
              GROUP BY td.id, td.nombre, cd.nombre
              HAVING COUNT(d.id) > @min_denuncias
              ORDER BY total_denuncias DESC", conn);

        cmd.Parameters.AddWithValue("fecha_inicio", (object?)fechaInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_fin", (object?)fechaFin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("min_denuncias", minDenuncias);

        var lista = new List<ReportePorDelitoResponse>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new ReportePorDelitoResponse
            {
                TipoDelito = reader.GetString(0),
                Categoria = reader.GetString(1),
                TotalDenuncias = Convert.ToInt32(reader.GetValue(2)),
                Porcentaje = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
            });
        }
        return lista;
    }

    public async Task<string> ExportarPorDelitoCsvAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int minDenuncias)
    {
        var datos = await GetPorDelitoAsync(fechaInicio, fechaFin, minDenuncias);
        var sb = new StringBuilder();
        sb.AppendLine("tipo_delito,categoria,total_denuncias,porcentaje");
        foreach (var r in datos)
            sb.AppendLine($"\"{r.TipoDelito}\",\"{r.Categoria}\",{r.TotalDenuncias},{r.Porcentaje}");
        return sb.ToString();
    }

    public async Task<List<ReportePorMesResponse>> GetPorMesAsync(int anio)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT EXTRACT(YEAR FROM d.created_at)::int AS anio,
                     EXTRACT(MONTH FROM d.created_at)::int AS mes,
                     COUNT(d.id) AS total_denuncias,
                     COUNT(CASE WHEN ed.nombre = 'ADMITIDA'  THEN 1 END) AS total_admitidas,
                     COUNT(CASE WHEN ed.nombre = 'ARCHIVADA' THEN 1 END) AS total_archivadas
              FROM denuncias d
              INNER JOIN estados_denuncia ed ON ed.id = d.estado_id
              WHERE EXTRACT(YEAR FROM d.created_at) = @anio
              GROUP BY EXTRACT(YEAR FROM d.created_at), EXTRACT(MONTH FROM d.created_at)
              ORDER BY anio, mes", conn);
        cmd.Parameters.AddWithValue("anio", anio);

        var lista = new List<ReportePorMesResponse>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new ReportePorMesResponse
            {
                Anio = reader.GetInt32(0),
                Mes = reader.GetInt32(1),
                TotalDenuncias = Convert.ToInt32(reader.GetValue(2)),
                TotalAdmitidas = Convert.ToInt32(reader.GetValue(3)),
                TotalArchivadas = Convert.ToInt32(reader.GetValue(4))
            });
        }
        return lista;
    }

    public async Task<List<ReportePorZonaResponse>> GetPorZonaAsync(
        DateTime? fechaInicio, DateTime? fechaFin, int minDenuncias)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT u.departamento, u.provincia, u.distrito, COUNT(d.id) AS total_denuncias
              FROM denuncias d
              INNER JOIN ubigeo u ON u.id = d.ubigeo_id
              WHERE (@fecha_inicio::timestamptz IS NULL OR d.fecha_hecho >= @fecha_inicio)
                AND (@fecha_fin::timestamptz    IS NULL OR d.fecha_hecho <= @fecha_fin)
              GROUP BY u.id, u.departamento, u.provincia, u.distrito
              HAVING COUNT(d.id) >= @min_denuncias
              ORDER BY total_denuncias DESC", conn);

        cmd.Parameters.AddWithValue("fecha_inicio", (object?)fechaInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_fin", (object?)fechaFin ?? DBNull.Value);
        cmd.Parameters.AddWithValue("min_denuncias", minDenuncias);

        var lista = new List<ReportePorZonaResponse>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new ReportePorZonaResponse
            {
                Departamento = reader.GetString(0),
                Provincia = reader.GetString(1),
                Distrito = reader.GetString(2),
                TotalDenuncias = Convert.ToInt32(reader.GetValue(3))
            });
        }
        return lista;
    }
}
