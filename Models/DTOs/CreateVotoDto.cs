// CreateVotoDto.cs
using System;
using CityCore.Data;

namespace CityCore.Api.Models.DTOs
{
    public class CreateVotoDto
    {
        public TipoVoto Tipo { get; set; }          // IMPORTANTE = 0, URGENTE = 1
        public DateTime Fecha { get; set; }
      
        public int ReporteId { get; set; }
    }
}
