namespace CityCore.Api.Dtos
{
    public class ReporteConConteosDto
    {
        public int    ReporteId     { get; set; }
        public string Titulo        { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int    Estado        { get; set; }
        public int    VotosUrgente  { get; set; }
        public int    VotosImportante { get; set; }
    }
}
