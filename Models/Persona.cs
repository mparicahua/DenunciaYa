namespace DenunciaYA.API.Models;

public class Persona
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string? Telefono { get; set; }
    public string? EmailContacto { get; set; }
    public int? UbigeoId { get; set; }
    public string? Direccion { get; set; }
    public DateTime CreatedAt { get; set; }
}
