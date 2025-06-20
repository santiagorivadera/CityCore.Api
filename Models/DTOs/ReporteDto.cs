using System;
using Microsoft.AspNetCore.Http;
using CityCore.Data; // para EstadoReporte

namespace CityCore.Api.Dtos
{
   // CityCore.Api.Dtos/ReporteDto.cs
public class ReporteDto
{
    public int ReporteId { get; set; }
    public string Titulo { get; set; }
    public string Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; }
    public EstadoReporte Estado { get; set; }
    public string UrlMaps { get; set; }
    public int UsuarioId { get; set; }
    public string? UrlImagen { get; set; }

    /// <summary>
    /// Si está en votación de cierre, acá va el ID de la votación activa.
    /// </summary>
    public int? VotacionId { get; set; }
}

}
