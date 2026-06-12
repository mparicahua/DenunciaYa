using DenunciaYA.API.DTOs;
using Npgsql;

namespace DenunciaYA.API.Services;

public class DerivacionService(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("PostgreSQL")!;

    public async Task<CreateDerivacionResponse> CreateAsync(CreateDerivacionRequest req, int usuarioId)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            // 1. Verificar denuncia en estado ADMITIDA o EN_INVESTIGACION
            int estadoActualId = 0;
            int denuncianteId = 0;
            bool esAnonima = false;
            string codigoSeguimiento = string.Empty;
            string estadoNombre = string.Empty;

            await using (var cmd = new NpgsqlCommand(
                @"SELECT d.id, d.estado_id, d.codigo_seguimiento, d.denunciante_id, d.es_anonima, ed.nombre
                  FROM denuncias d INNER JOIN estados_denuncia ed ON ed.id = d.estado_id
                  WHERE d.id = @did", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new KeyNotFoundException("Denuncia no encontrada.");
                estadoActualId = reader.GetInt32(1);
                codigoSeguimiento = reader.GetString(2);
                denuncianteId = reader.GetInt32(3);
                esAnonima = reader.GetBoolean(4);
                estadoNombre = reader.GetString(5);
            }

            if (estadoNombre != "ADMITIDA" && estadoNombre != "EN_INVESTIGACION")
                throw new InvalidOperationException($"La denuncia debe estar en estado ADMITIDA o EN_INVESTIGACION. Estado actual: {estadoNombre}");

            // 2. Verificar fiscal activo y obtener usuario_id
            int usuarioFiscalId = 0;
            await using (var cmd = new NpgsqlCommand(
                @"SELECT f.id, p.usuario_id FROM fiscales f
                  INNER JOIN personas p ON p.id = f.persona_id
                  WHERE f.id = @fid AND f.activo = true", conn, tx))
            {
                cmd.Parameters.AddWithValue("fid", req.FiscalId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new KeyNotFoundException("Fiscal no encontrado o inactivo.");
                usuarioFiscalId = reader.GetInt32(1);
            }

            // 3. Insertar derivación con estado PENDIENTE
            int derivacionId;
            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO derivaciones (denuncia_id, fiscal_id, derivado_por, fecha_derivacion, motivo, estado)
                  VALUES (@did, @fid, @uid, NOW(), @motivo, 'PENDIENTE') RETURNING id", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                cmd.Parameters.AddWithValue("fid", req.FiscalId);
                cmd.Parameters.AddWithValue("uid", usuarioId);
                cmd.Parameters.AddWithValue("motivo", (object?)req.Motivo ?? DBNull.Value);
                derivacionId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 4. Obtener ID del estado DERIVADA
            int estadoDerivadaId;
            await using (var cmd = new NpgsqlCommand("SELECT id FROM estados_denuncia WHERE nombre = 'DERIVADA'", conn, tx))
            {
                estadoDerivadaId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 5. Actualizar estado de la denuncia
            await using (var cmd = new NpgsqlCommand(
                "UPDATE denuncias SET estado_id = @eid, updated_at = NOW() WHERE id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("eid", estadoDerivadaId);
                cmd.Parameters.AddWithValue("id", req.DenunciaId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 6. Historial de estados
            await using (var cmd = new NpgsqlCommand(
                "INSERT INTO historial_estados (denuncia_id, estado_anterior_id, estado_nuevo_id, usuario_id, motivo, created_at) VALUES (@did, @eaid, @enid, @uid, @motivo, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                cmd.Parameters.AddWithValue("eaid", estadoActualId);
                cmd.Parameters.AddWithValue("enid", estadoDerivadaId);
                cmd.Parameters.AddWithValue("uid", usuarioId);
                cmd.Parameters.AddWithValue("motivo", (object?)req.Motivo ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            // 7. Notificar al fiscal
            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO notificaciones (usuario_id, denuncia_id, canal, asunto, mensaje, estado, intentos, created_at)
                  VALUES (@uid, @did, 'SISTEMA', 'Caso derivado a su despacho', @msg, 'PENDIENTE', 0, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("uid", usuarioFiscalId);
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                cmd.Parameters.AddWithValue("msg", $"La denuncia {codigoSeguimiento} fue derivada a su despacho. Motivo: {req.Motivo}");
                await cmd.ExecuteNonQueryAsync();
            }

            // 8. Notificar al denunciante si no es anónima
            if (!esAnonima)
            {
                int usuarioDenuncianteId = 0;
                await using (var cmd = new NpgsqlCommand(
                    "SELECT u.id FROM personas p INNER JOIN usuarios u ON u.id = p.usuario_id WHERE p.id = @pid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("pid", denuncianteId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null) usuarioDenuncianteId = (int)result;
                }

                if (usuarioDenuncianteId > 0)
                {
                    await using var cmd = new NpgsqlCommand(
                        @"INSERT INTO notificaciones (usuario_id, denuncia_id, canal, asunto, mensaje, estado, intentos, created_at)
                          VALUES (@uid, @did, 'EMAIL', @asunto, @msg, 'PENDIENTE', 0, NOW())", conn, tx);
                    cmd.Parameters.AddWithValue("uid", usuarioDenuncianteId);
                    cmd.Parameters.AddWithValue("did", req.DenunciaId);
                    cmd.Parameters.AddWithValue("asunto", $"Su denuncia {codigoSeguimiento} fue derivada a fiscal");
                    cmd.Parameters.AddWithValue("msg", "Su denuncia fue derivada al fiscal competente para continuar la investigación.");
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
            return new CreateDerivacionResponse { Id = derivacionId, Mensaje = "Derivación registrada exitosamente." };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
