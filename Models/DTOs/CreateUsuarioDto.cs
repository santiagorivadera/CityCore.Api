
namespace CityCore.Api.Models.DTOs
{
    public class CreateUsuarioDto
    {
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Correo { get; set; }
        public string PassHash { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}
