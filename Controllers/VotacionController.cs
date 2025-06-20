using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CityCore.Api.Dtos;
using CityCore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CityCore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VotacionController : ControllerBase
    {
        private readonly CityCoreContext _db;
        public VotacionController(CityCoreContext db) => _db = db;

        /// <summary>
        /// 1) Inicia una votación de 1 minuto. Solo si el reporte está ACTIVO (0).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Start([FromBody] CreateVotacionDto dto)
        {
            var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var rpt = await _db.Reportes.FindAsync(dto.ReporteId);
            if (rpt == null)
                return NotFound(new { mensaje = "Reporte no existe." });

            if (rpt.Estado != EstadoReporte.ACTIVO)
                return Conflict(new { mensaje = "Solo se puede iniciar votación en un reporte ACTIVO." });

            var vot = new VotacionCierre
            {
                ReporteId   = dto.ReporteId,
                IniciadorId = usuarioId,
                FechaInicio = DateTime.UtcNow
            };
            rpt.Estado = EstadoReporte.EN_VOTACION;

            _db.Votaciones.Add(vot);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = vot.VotacionId }, new
            {
                votacionId     = vot.VotacionId,
                estadoAnterior = EstadoReporte.ACTIVO
            });
        }

        /// <summary>
        /// 2) Obtiene datos de la votación.
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(int id)
        {
            var vot = await _db.Votaciones
                .Include(v => v.Participaciones)
                .SingleOrDefaultAsync(v => v.VotacionId == id);
            if (vot == null)
                return NotFound(new { mensaje = "Votación no existe." });

            return Ok(new
            {
                vot.VotacionId,
                vot.ReporteId,
                vot.IniciadorId,
                vot.FechaInicio,
                vot.FechaFin,
                vot.Resultado,
                total     = vot.Participaciones.Count,
                positivos = vot.Participaciones.Count(p => p.Voto)
            });
        }

        /// <summary>
        /// 3) Participa en la votación (1 minuto).
        /// </summary>
        [HttpPost("{id}/participar")]
        public async Task<IActionResult> Participar(int id, [FromBody] CreateParticipacionDto dto)
        {
            var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var vot = await _db.Votaciones.FindAsync(id);
            if (vot == null)
                return NotFound(new { mensaje = "Votación no existe." });

            if (vot.FechaInicio.AddMinutes(1) <= DateTime.UtcNow)
                return BadRequest(new { mensaje = "Votación cerrada." });

            bool ya = await _db.Participaciones
                .AnyAsync(p => p.VotacionId == id && p.UsuarioId == usuarioId);
            if (ya)
                return Conflict(new { mensaje = "Ya participaste." });

            _db.Participaciones.Add(new ParticipacionCierre
            {
                VotacionId = id,
                UsuarioId  = usuarioId,
                Voto       = dto.Voto
            });
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Participar), new { id, usuario = usuarioId }, null);
        }

        /// <summary>
        /// 4) Cierre manual (fallback).
        /// </summary>
        [HttpPost("{id}/cerrar")]
        public async Task<IActionResult> Close(int id)
        {
            var vot = await _db.Votaciones
                .Include(v => v.Participaciones)
                .Include(v => v.Reporte)
                .SingleOrDefaultAsync(v => v.VotacionId == id);
            if (vot == null)
                return NotFound(new { mensaje = "Votación no existe." });

            if (vot.FechaInicio.AddMinutes(1) > DateTime.UtcNow)
                return BadRequest(new { mensaje = "Aún no ha pasado 1 minuto." });

            int total = vot.Participaciones.Count;
            int pos   = vot.Participaciones.Count(p => p.Voto);
            double ratio = total == 0 ? 0 : pos / (double)total;

            if (ratio >= 0.8)
            {
                vot.Reporte.Estado = EstadoReporte.RESUELTO;
                vot.Resultado      = ResultadoCierre.EXITOSO;
            }
            else
            {
                vot.Reporte.Estado = EstadoReporte.ACTIVO;
                vot.Resultado      = ResultadoCierre.FALLIDO;
            }

            vot.FechaFin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { resultado = vot.Resultado, ratio });
        }
    }
}
