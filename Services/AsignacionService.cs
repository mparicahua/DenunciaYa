using DenunciaYA.API.DTOs;
using Npgsql;

namespace DenunciaYA.API.Services;

public class AsignacionService(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("PostgreSQL")!;

    public async Task<CreateAsignacionResponse> CreateAsync(CreateAsignacionRequest req, int usuarioId)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            // 1. Verificar denuncia en estado ADMITIDA
            string codigoSeguimiento = string.Empty;
            string estadoNombre = string.Empty;

            await using (var cmd = new NpgsqlCommand(
                @"SELECT d.id, d.codigo_seguimiento, ed.nombre AS estado
                  FROM denuncias d INNER JOIN estados_denuncia ed ON ed.id = d.estado_id
                  WHERE d.id = @did", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new KeyNotFoundException("Denuncia no encontrada.");
                codigoSeguimiento = reader.GetString(1);
                estadoNombre = reader.GetString(2);
            }

            if (estadoNombre != "ADMITIDA")
                throw new InvalidOperationException($"La denuncia debe estar en estado ADMITIDA. Estado actual: {estadoNombre}");

            // 2. Verificar funcionario activo y obtener usuario_id
            int usuarioFuncionarioId = 0;
            await using (var cmd = new NpgsqlCommand(
                @"SELECT f.id, p.usuario_id FROM funcionarios f
                  INNER JOIN personas p ON p.id = f.persona_id
                  WHERE f.id = @fid AND f.activo = true", conn, tx))
            {
                cmd.Parameters.AddWithValue("fid", req.FuncionarioId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new KeyNotFoundException("Funcionario no encontrado o inactivo.");
                usuarioFuncionarioId = reader.GetInt32(1);
            }

            // 3. Desactivar asignación anterior
            await using (var cmd = new NpgsqlCommand(
                "UPDATE asignaciones SET activo = false WHERE denuncia_id = @did AND activo = true", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 4. Insertar nueva asignación
            int asignacionId;
            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO asignaciones (denuncia_id, funcionario_id, asignado_por, fecha_asignacion, activo, observaciones)
                  VALUES (@did, @fid, @uid, NOW(), true, @obs) RETURNING id", conn, tx))
            {
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                cmd.Parameters.AddWithValue("fid", req.FuncionarioId);
                cmd.Parameters.AddWithValue("uid", usuarioId);
                cmd.Parameters.AddWithValue("obs", (object?)req.Observaciones ?? DBNull.Value);
                asignacionId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 5. Actualizar timestamp denuncia
            await using (var cmd = new NpgsqlCommand("UPDATE denuncias SET updated_at = NOW() WHERE id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("id", req.DenunciaId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 6. Notificar al funcionario
            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO notificaciones (usuario_id, denuncia_id, canal, asunto, mensaje, estado, intentos, created_at)
                  VALUES (@uid, @did, 'SISTEMA', 'Nueva denuncia asignada', @msg, 'PENDIENTE', 0, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("uid", usuarioFuncionarioId);
                cmd.Parameters.AddWithValue("did", req.DenunciaId);
                cmd.Parameters.AddWithValue("msg", $"Se le ha asignado la denuncia {codigoSeguimiento}. Revise el sistema.");
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new CreateAsignacionResponse { Id = asignacionId, Mensaje = "Asignación registrada exitosamente." };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
