using System.Text.Json;
using DenunciaYA.API.DTOs;
using Npgsql;
using NpgsqlTypes;

namespace DenunciaYA.API.Services;

public class DenunciaService(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("PostgreSQL")!;

    public async Task<CreateDenunciaResponse> CreateAsync(CreateDenunciaRequest req, int usuarioId)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            // 1. Generar código correlativo
            int anio = DateTime.Now.Year;
            int numero;
            await using (var cmd = new NpgsqlCommand(
                "SELECT COALESCE(MAX(CAST(SPLIT_PART(codigo_seguimiento, '-', 3) AS INT)), 0) + 1 FROM denuncias WHERE codigo_seguimiento LIKE 'DEN-' || @anio || '-%'", conn, tx))
            {
                cmd.Parameters.AddWithValue("anio", anio.ToString());
                numero = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            var codigoSeguimiento = $"DEN-{anio}-{numero:D5}";

            // 2. Obtener ID del estado INGRESADA
            int estadoIngresadaId;
            await using (var cmd = new NpgsqlCommand("SELECT id FROM estados_denuncia WHERE nombre = 'INGRESADA'", conn, tx))
            {
                estadoIngresadaId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 3. Obtener persona del denunciante
            int denunciantePersonaId;
            await using (var cmd = new NpgsqlCommand("SELECT id FROM personas WHERE usuario_id = @uid", conn, tx))
            {
                cmd.Parameters.AddWithValue("uid", usuarioId);
                denunciantePersonaId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 4. Insertar denuncia
            int denunciaId;
            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO denuncias (codigo_seguimiento, denunciante_id, tipo_delito_id, estado_id,
                  ubigeo_id, direccion_hecho, fecha_hecho, descripcion_hecho, es_anonima, created_at, updated_at, detalles_especificos)
                  VALUES (@cod, @denunciante_id, @tipo_delito_id, @estado_id,
                  @ubigeo_id, @direccion_hecho, @fecha_hecho, @descripcion_hecho, @es_anonima, NOW(), NOW(), @detalles_especificos)
                  RETURNING id", conn, tx))
            {
                cmd.Parameters.AddWithValue("cod", codigoSeguimiento);
                cmd.Parameters.AddWithValue("denunciante_id", denunciantePersonaId);
                cmd.Parameters.AddWithValue("tipo_delito_id", req.TipoDelitoId);
                cmd.Parameters.AddWithValue("estado_id", estadoIngresadaId);
                cmd.Parameters.AddWithValue("ubigeo_id", (object?)req.UbigeoId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("direccion_hecho", (object?)req.DireccionHecho ?? DBNull.Value);
                cmd.Parameters.AddWithValue("fecha_hecho", (object?)req.FechaHecho ?? DBNull.Value);
                cmd.Parameters.AddWithValue("descripcion_hecho", req.DescripcionHecho);
                cmd.Parameters.AddWithValue("es_anonima", req.EsAnonima);
                cmd.Parameters.Add(new NpgsqlParameter("detalles_especificos", NpgsqlDbType.Jsonb)
                {
                    Value = req.DetallesEspecificos.HasValue ? req.DetallesEspecificos.Value.GetRawText() : DBNull.Value
                });
                denunciaId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 5. Por cada denunciado: crear persona y luego denunciado
            foreach (var d in req.Denunciados)
            {
                int personaId;
                await using (var cmd = new NpgsqlCommand(
                    "INSERT INTO personas (nombres, apellidos, dni, created_at) VALUES (@n, @a, @dni, NOW()) RETURNING id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("n", d.Nombres);
                    cmd.Parameters.AddWithValue("a", d.Apellidos);
                    cmd.Parameters.AddWithValue("dni", (object?)d.Dni ?? DBNull.Value);
                    personaId = (int)(await cmd.ExecuteScalarAsync())!;
                }

                await using (var cmd = new NpgsqlCommand(
                    "INSERT INTO denunciados (denuncia_id, persona_id, relacion_con_denunciante, descripcion_fisica) VALUES (@did, @pid, @rel, @desc)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("did", denunciaId);
                    cmd.Parameters.AddWithValue("pid", personaId);
                    cmd.Parameters.AddWithValue("rel", (object?)d.RelacionConDenunciante ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("desc", (object?)d.DescripcionFisica ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // 6. Por cada testigo
            foreach (var t in req.Testigos)
            {
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO testigos (denuncia_id, persona_id, nombre_anonimo, declaracion, created_at) VALUES (@did, NULL, @na, @decl, NOW())", conn, tx);
                cmd.Parameters.AddWithValue("did", denunciaId);
                cmd.Parameters.AddWithValue("na", (object?)t.NombreAnonimo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("decl", (object?)t.Declaracion ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            // 7. Historial de estados
            await using (var cmd = new NpgsqlCommand(
                "INSERT INTO historial_estados (denuncia_id, estado_anterior_id, estado_nuevo_id, usuario_id, motivo, created_at) VALUES (@did, NULL, @enid, @uid, 'Denuncia registrada por el ciudadano', NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", denunciaId);
                cmd.Parameters.AddWithValue("enid", estadoIngresadaId);
                cmd.Parameters.AddWithValue("uid", usuarioId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 8. Notificación al denunciante
            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO notificaciones (usuario_id, denuncia_id, canal, asunto, mensaje, estado, intentos, created_at)
                  VALUES (@uid, @did, 'EMAIL', @asunto, @mensaje, 'PENDIENTE', 0, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("uid", usuarioId);
                cmd.Parameters.AddWithValue("did", denunciaId);
                cmd.Parameters.AddWithValue("asunto", $"Su denuncia {codigoSeguimiento} ha sido registrada");
                cmd.Parameters.AddWithValue("mensaje", $"Use el código {codigoSeguimiento} para hacer seguimiento.");
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new CreateDenunciaResponse
            {
                Id = denunciaId,
                CodigoSeguimiento = codigoSeguimiento,
                Mensaje = "Denuncia registrada exitosamente"
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateEstadoAsync(int id, UpdateEstadoRequest req, int usuarioId)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            // 1. Verificar denuncia y que no esté en estado final
            int estadoActualId = 0;
            int denuncianteId = 0;
            bool esAnonima = false;
            string codigoSeguimiento = string.Empty;
            bool esFinal = false;
            string estadoNombre = string.Empty;

            await using (var cmd = new NpgsqlCommand(
                @"SELECT d.id, d.estado_id, d.denunciante_id, d.es_anonima, d.codigo_seguimiento, ed.es_final, ed.nombre
                  FROM denuncias d INNER JOIN estados_denuncia ed ON ed.id = d.estado_id WHERE d.id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("id", id);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new KeyNotFoundException("Denuncia no encontrada.");
                estadoActualId = reader.GetInt32(1);
                denuncianteId = reader.GetInt32(2);
                esAnonima = reader.GetBoolean(3);
                codigoSeguimiento = reader.GetString(4);
                esFinal = reader.GetBoolean(5);
                estadoNombre = reader.GetString(6);
            }

            if (esFinal)
                throw new InvalidOperationException("No se puede cambiar estado de denuncia finalizada.");

            // 2. Actualizar estado
            await using (var cmd = new NpgsqlCommand(
                "UPDATE denuncias SET estado_id = @neid, updated_at = NOW() WHERE id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("neid", req.NuevoEstadoId);
                cmd.Parameters.AddWithValue("id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Historial de estados
            await using (var cmd = new NpgsqlCommand(
                "INSERT INTO historial_estados (denuncia_id, estado_anterior_id, estado_nuevo_id, usuario_id, motivo, created_at) VALUES (@did, @eaid, @enid, @uid, @motivo, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", id);
                cmd.Parameters.AddWithValue("eaid", estadoActualId);
                cmd.Parameters.AddWithValue("enid", req.NuevoEstadoId);
                cmd.Parameters.AddWithValue("uid", usuarioId);
                cmd.Parameters.AddWithValue("motivo", (object?)req.Motivo ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            if (!esAnonima)
            {
                // 4. Obtener usuario del denunciante
                int usuarioDenuncianteId = 0;
                await using (var cmd = new NpgsqlCommand(
                    "SELECT u.id FROM personas p INNER JOIN usuarios u ON u.id = p.usuario_id WHERE p.id = @pid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("pid", denuncianteId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null) usuarioDenuncianteId = (int)result;
                }

                // 5. Obtener nombre del nuevo estado
                string nombreNuevoEstado = string.Empty;
                await using (var cmd = new NpgsqlCommand("SELECT nombre FROM estados_denuncia WHERE id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", req.NuevoEstadoId);
                    nombreNuevoEstado = (string)(await cmd.ExecuteScalarAsync())!;
                }

                if (usuarioDenuncianteId > 0)
                {
                    await using var cmd = new NpgsqlCommand(
                        @"INSERT INTO notificaciones (usuario_id, denuncia_id, canal, asunto, mensaje, estado, intentos, created_at)
                          VALUES (@uid, @did, 'EMAIL', @asunto, @mensaje, 'PENDIENTE', 0, NOW())", conn, tx);
                    cmd.Parameters.AddWithValue("uid", usuarioDenuncianteId);
                    cmd.Parameters.AddWithValue("did", id);
                    cmd.Parameters.AddWithValue("asunto", $"Actualización de su denuncia {codigoSeguimiento}");
                    cmd.Parameters.AddWithValue("mensaje", $"El estado de su denuncia cambió a: {nombreNuevoEstado}");
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteEvidenciaAsync(int denunciaId, int evidenciaId)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        // 1. Verificar que la evidencia existe y pertenece a esa denuncia
        await using (var cmd = new NpgsqlCommand(
            "SELECT e.id FROM evidencias e WHERE e.id = @id AND e.denuncia_id = @did", conn))
        {
            cmd.Parameters.AddWithValue("id", evidenciaId);
            cmd.Parameters.AddWithValue("did", denunciaId);
            var result = await cmd.ExecuteScalarAsync();
            if (result == null)
                throw new KeyNotFoundException("Evidencia no encontrada para esta denuncia.");
        }

        // 2. Eliminar
        await using (var cmd = new NpgsqlCommand("DELETE FROM evidencias WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", evidenciaId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<DenunciaDetalleResponse> GetDetalleAsync(string codigo)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT d.codigo_seguimiento, d.descripcion_hecho, d.fecha_hecho,
                     td.nombre AS tipo_delito, cd.nombre AS categoria_delito,
                     ed.nombre AS estado_actual,
                     CASE WHEN d.es_anonima = true THEN 'ANÓNIMO' ELSE p.nombres || ' ' || p.apellidos END AS nombre_denunciante,
                     df.nombre AS distrito_fiscal,
                     d.detalles_especificos
              FROM denuncias d
              INNER JOIN tipos_delito td       ON td.id = d.tipo_delito_id
              INNER JOIN categorias_delito cd  ON cd.id = td.categoria_id
              INNER JOIN estados_denuncia ed   ON ed.id = d.estado_id
              INNER JOIN personas p            ON p.id = d.denunciante_id
              LEFT JOIN  distritos_fiscales df ON df.id = d.distrito_fiscal_id
              WHERE d.codigo_seguimiento = @codigo", conn);
        cmd.Parameters.AddWithValue("codigo", codigo);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new KeyNotFoundException("Denuncia no encontrada.");

        return new DenunciaDetalleResponse
        {
            CodigoSeguimiento = reader.GetString(0),
            DescripcionHecho = reader.GetString(1),
            FechaHecho = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            TipoDelito = reader.GetString(3),
            CategoriaDelito = reader.GetString(4),
            EstadoActual = reader.GetString(5),
            NombreDenunciante = reader.GetString(6),
            DistritoFiscal = reader.IsDBNull(7) ? null : reader.GetString(7),
            DetallesEspecificos = reader.IsDBNull(8) ? null : JsonDocument.Parse(reader.GetString(8)).RootElement
        };
    }

    public async Task<List<HistorialEstadoResponse>> GetHistorialAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT ea.nombre AS estado_anterior, en.nombre AS estado_nuevo,
                     h.motivo, u.email AS usuario_cambio, h.created_at
              FROM historial_estados h
              INNER JOIN estados_denuncia en ON en.id = h.estado_nuevo_id
              LEFT JOIN  estados_denuncia ea ON ea.id = h.estado_anterior_id
              INNER JOIN usuarios u          ON u.id = h.usuario_id
              WHERE h.denuncia_id = @id
              ORDER BY h.created_at DESC", conn);
        cmd.Parameters.AddWithValue("id", id);

        var lista = new List<HistorialEstadoResponse>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new HistorialEstadoResponse
            {
                EstadoAnterior = reader.IsDBNull(0) ? null : reader.GetString(0),
                EstadoNuevo = reader.GetString(1),
                Motivo = reader.IsDBNull(2) ? null : reader.GetString(2),
                UsuarioCambio = reader.GetString(3),
                FechaCambio = reader.GetDateTime(4)
            });
        }
        return lista;
    }

    public async Task<List<DenunciaPendienteResponse>> GetPendientesAsignacionAsync()
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT d.codigo_seguimiento, d.fecha_hecho, td.nombre AS tipo_delito, df.nombre AS distrito_fiscal
              FROM denuncias d
              INNER JOIN tipos_delito td       ON td.id = d.tipo_delito_id
              INNER JOIN estados_denuncia ed   ON ed.id = d.estado_id
              LEFT JOIN  distritos_fiscales df ON df.id = d.distrito_fiscal_id
              WHERE NOT EXISTS (
                  SELECT 1 FROM asignaciones a WHERE a.denuncia_id = d.id AND a.activo = true
              )
              ORDER BY d.fecha_hecho ASC", conn);

        var lista = new List<DenunciaPendienteResponse>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new DenunciaPendienteResponse
            {
                CodigoSeguimiento = reader.GetString(0),
                FechaHecho = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                TipoDelito = reader.GetString(2),
                DistritoFiscal = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        return lista;
    }

    public async Task<List<DenunciaFuncionarioResponse>> GetByFuncionarioAsync(int funcionarioId)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT d.codigo_seguimiento, d.fecha_hecho, td.nombre AS tipo_delito,
                     ed.nombre AS estado,
                     CASE WHEN d.es_anonima = true THEN 'ANÓNIMO' ELSE p.nombres || ' ' || p.apellidos END AS nombre_denunciante
              FROM asignaciones a
              INNER JOIN denuncias d         ON d.id = a.denuncia_id
              INNER JOIN tipos_delito td     ON td.id = d.tipo_delito_id
              INNER JOIN estados_denuncia ed ON ed.id = d.estado_id
              INNER JOIN personas p          ON p.id = d.denunciante_id
              WHERE a.funcionario_id = @id AND a.activo = true
              ORDER BY d.fecha_hecho DESC", conn);
        cmd.Parameters.AddWithValue("id", funcionarioId);

        var lista = new List<DenunciaFuncionarioResponse>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(new DenunciaFuncionarioResponse
            {
                CodigoSeguimiento = reader.GetString(0),
                FechaHecho = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                TipoDelito = reader.GetString(2),
                Estado = reader.GetString(3),
                NombreDenunciante = reader.GetString(4)
            });
        }
        return lista;
    }
}
