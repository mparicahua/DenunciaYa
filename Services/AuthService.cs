using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DenunciaYA.API.DTOs;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace DenunciaYA.API.Services;

public class AuthService(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("PostgreSQL")!;
    private readonly string _jwtSecret = config["Jwt:Secret"]!;

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest req)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        // Verificar email no existe
        await using (var cmd = new NpgsqlCommand("SELECT id FROM usuarios WHERE email = @email", conn))
        {
            cmd.Parameters.AddWithValue("email", req.Email);
            var exists = await cmd.ExecuteScalarAsync();
            if (exists != null)
                throw new InvalidOperationException("El email ya está registrado.");
        }

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            // Insertar usuario
            int usuarioId;
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            await using (var cmd = new NpgsqlCommand(
                "INSERT INTO usuarios (email, password_hash, activo, created_at, updated_at) VALUES (@email, @ph, true, NOW(), NOW()) RETURNING id", conn, tx))
            {
                cmd.Parameters.AddWithValue("email", req.Email);
                cmd.Parameters.AddWithValue("ph", passwordHash);
                usuarioId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // Insertar persona
            await using (var cmd = new NpgsqlCommand(
                "INSERT INTO personas (usuario_id, nombres, apellidos, dni, telefono, created_at) VALUES (@uid, @nombres, @apellidos, @dni, @tel, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("uid", usuarioId);
                cmd.Parameters.AddWithValue("nombres", req.Nombres);
                cmd.Parameters.AddWithValue("apellidos", req.Apellidos);
                cmd.Parameters.AddWithValue("dni", (object?)req.Dni ?? DBNull.Value);
                cmd.Parameters.AddWithValue("tel", (object?)req.Telefono ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            // Obtener ID del rol CIUDADANO
            int rolId;
            await using (var cmd = new NpgsqlCommand("SELECT id FROM roles WHERE nombre = 'CIUDADANO'", conn, tx))
            {
                rolId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // Asignar rol
            await using (var cmd = new NpgsqlCommand(
                "INSERT INTO usuario_roles (usuario_id, rol_id, created_at) VALUES (@uid, @rid, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("uid", usuarioId);
                cmd.Parameters.AddWithValue("rid", rolId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new RegisterResponse { Id = usuarioId, Email = req.Email, Mensaje = "Usuario registrado exitosamente." };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        string? storedHash = null;
        int userId = 0;
        string email = string.Empty;
        string rol = string.Empty;
        bool activo = false;

        await using (var cmd = new NpgsqlCommand(
            @"SELECT u.id, u.email, u.password_hash, u.activo, r.nombre AS rol
              FROM usuarios u
              INNER JOIN usuario_roles ur ON ur.usuario_id = u.id
              INNER JOIN roles r ON r.id = ur.rol_id
              WHERE u.email = @email", conn))
        {
            cmd.Parameters.AddWithValue("email", req.Email);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new UnauthorizedAccessException("Credenciales inválidas.");

            userId = reader.GetInt32(0);
            email = reader.GetString(1);
            storedHash = reader.GetString(2);
            activo = reader.GetBoolean(3);
            rol = reader.GetString(4);
        }

        if (!activo)
            throw new UnauthorizedAccessException("Usuario inactivo.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, storedHash))
            throw new UnauthorizedAccessException("Credenciales inválidas.");

        // Actualizar ultimo_acceso
        await using (var cmd = new NpgsqlCommand("UPDATE usuarios SET ultimo_acceso = NOW() WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        var token = GenerateJwt(userId, email, rol);
        return new LoginResponse { Token = token, Email = email, Rol = rol };
    }

    private string GenerateJwt(int userId, string email, string rol)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, rol),
            new Claim("userId", userId.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
