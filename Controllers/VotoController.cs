using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CityCore.Api.Models.DTOs;
using CityCore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CityCore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]  // Requiere JWT válido
    public class VotoController : ControllerBase
    {
        private readonly CityCoreContext _db;
        public VotoController(CityCoreContext db) => _db = db;

        /// <summary>
        /// Registra un voto IMPORTANTE o URGENTE para un reporte activo.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateVotoDto dto)
        {
            // Extraer usuarioId del token (claim "sub")
            var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // 1) Verificar que el reporte existe
            var rpt = await _db.Reportes.FindAsync(dto.ReporteId);
            if (rpt == null)
                return NotFound(new { mensaje = "Reporte no existe." });

            // 2) No permitir votar un reporte resuelto
            if (rpt.Estado == EstadoReporte.RESUELTO)
                return BadRequest(new { mensaje = "No se puede votar un reporte resuelto." });

            // 3) Evitar doble voto del mismo usuario sobre el mismo reporte
            bool yaVoto = await _db.Votos
                .AnyAsync(v => v.UsuarioId == usuarioId && v.ReporteId == dto.ReporteId);
            if (yaVoto)
                return Conflict(new { mensaje = "Ya has votado este reporte." });

            // 4) Registrar voto
            var voto = new Voto
            {
                UsuarioId  = usuarioId,
                ReporteId  = dto.ReporteId,
                Tipo       = dto.Tipo,
                Fecha      = DateTime.UtcNow
            };
            _db.Votos.Add(voto);
            await _db.SaveChangesAsync();

            // 5) Devolver 201 Created con el ID del nuevo voto
            return CreatedAtAction(
                actionName: nameof(Create),
                routeValues: new { id = voto.VotoId },
                value:       new { votoId = voto.VotoId }
            );
        }
    }
}
