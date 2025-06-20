

namespace CityCore.Api.Models.DTOs
{
    public class UsuarioDto
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Correo { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}
