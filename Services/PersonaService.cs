using DenunciaYA.API.DTOs;
using Npgsql;

namespace DenunciaYA.API.Services;

public class PersonaService(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("PostgreSQL")!;

    private static PersonaResponse Map(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Nombres = r.GetString(1),
        Apellidos = r.GetString(2),
        Dni = r.IsDBNull(3) ? null : r.GetString(3),
        FechaNacimiento = r.IsDBNull(4) ? null : DateOnly.FromDateTime(r.GetDateTime(4)),
        Genero = r.IsDBNull(5) ? null : r.GetString(5),
        Telefono = r.IsDBNull(6) ? null : r.GetString(6),
        EmailContacto = r.IsDBNull(7) ? null : r.GetString(7),
        UbigeoId = r.IsDBNull(8) ? null : r.GetInt32(8),
        Direccion = r.IsDBNull(9) ? null : r.GetString(9),
        CreatedAt = r.IsDBNull(10) ? null : r.GetDateTime(10)
    };

    private const string SelectColumns =
        "id, nombres, apellidos, dni, fecha_nacimiento, genero, telefono, email_contacto, ubigeo_id, direccion, created_at";

    public async Task<PersonaResponse> CreateAsync(CreatePersonaRequest req)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        try
        {
            await using var cmd = new NpgsqlCommand(
                $@"INSERT INTO personas (nombres, apellidos, dni, fecha_nacimiento, genero, telefono, email_contacto, ubigeo_id, direccion, created_at)
                   VALUES (@nombres, @apellidos, @dni, @fecha_nacimiento, @genero, @telefono, @email_contacto, @ubigeo_id, @direccion, NOW())
                   RETURNING {SelectColumns}", conn);
            AddParams(cmd, req.Nombres, req.Apellidos, req.Dni, req.FechaNacimiento, req.Genero, req.Telefono, req.EmailContacto, req.UbigeoId, req.Direccion);

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return Map(reader);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException("Ya existe una persona registrada con ese DNI.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            throw new InvalidOperationException($"Valor inválido: {ex.MessageText}");
        }
    }

    public async Task<List<PersonaResponse>> GetAllAsync()
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand($"SELECT {SelectColumns} FROM personas ORDER BY id DESC", conn);
        var lista = new List<PersonaResponse>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) lista.Add(Map(reader));
        return lista;
    }

    public async Task<PersonaResponse> GetByIdAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand($"SELECT {SelectColumns} FROM personas WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new KeyNotFoundException("Persona no encontrada.");
        return Map(reader);
    }

    public async Task<PersonaResponse> UpdateAsync(int id, UpdatePersonaRequest req)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        try
        {
            await using var cmd = new NpgsqlCommand(
                $@"UPDATE personas SET nombres = @nombres, apellidos = @apellidos, dni = @dni,
                       fecha_nacimiento = @fecha_nacimiento, genero = @genero, telefono = @telefono,
                       email_contacto = @email_contacto, ubigeo_id = @ubigeo_id, direccion = @direccion
                   WHERE id = @id
                   RETURNING {SelectColumns}", conn);
            cmd.Parameters.AddWithValue("id", id);
            AddParams(cmd, req.Nombres, req.Apellidos, req.Dni, req.FechaNacimiento, req.Genero, req.Telefono, req.EmailContacto, req.UbigeoId, req.Direccion);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new KeyNotFoundException("Persona no encontrada.");
            return Map(reader);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException("Ya existe una persona registrada con ese DNI.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23514")
        {
            throw new InvalidOperationException($"Valor inválido: {ex.MessageText}");
        }
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync();

        try
        {
            await using var cmd = new NpgsqlCommand("DELETE FROM personas WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
                throw new KeyNotFoundException("Persona no encontrada.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            throw new InvalidOperationException("No se puede eliminar: la persona está referenciada por una denuncia, denunciado, testigo, fiscal o funcionario.");
        }
    }

    private static void AddParams(NpgsqlCommand cmd, string nombres, string apellidos, string? dni,
        DateOnly? fechaNacimiento, string? genero, string? telefono, string? emailContacto, int? ubigeoId, string? direccion)
    {
        cmd.Parameters.AddWithValue("nombres", nombres);
        cmd.Parameters.AddWithValue("apellidos", apellidos);
        cmd.Parameters.AddWithValue("dni", (object?)dni ?? DBNull.Value);
        cmd.Parameters.AddWithValue("fecha_nacimiento", (object?)(fechaNacimiento?.ToDateTime(TimeOnly.MinValue)) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("genero", (object?)genero ?? DBNull.Value);
        cmd.Parameters.AddWithValue("telefono", (object?)telefono ?? DBNull.Value);
        cmd.Parameters.AddWithValue("email_contacto", (object?)emailContacto ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ubigeo_id", (object?)ubigeoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("direccion", (object?)direccion ?? DBNull.Value);
    }
}
