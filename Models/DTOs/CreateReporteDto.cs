namespace CityCore.Api.Dtos
{
    public class CreateReporteDto
    {
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string UrlMaps { get; set; }
        public IFormFile? Imagen { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
    
}



}
