using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CityCore.Api.Dtos;
using CityCore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityCore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReporteController : ControllerBase
    {
        private readonly CityCoreContext      _context;
        private readonly BlobContainerClient _blobContainer;

        public ReporteController(CityCoreContext context, BlobContainerClient blobContainer)
        {
            _context       = context;
            _blobContainer = blobContainer;
        }

        // POST: api/Reporte
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateReporte([FromForm] CreateReporteDto dto)
        {
            var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!await _context.Usuarios.AnyAsync(u => u.UsuarioId == usuarioId))
                return BadRequest(new { mensaje = "Usuario no existe." });

            // Detección de duplicados borrosa...
            var existing = await _context.Reportes
                .AsNoTracking()
                .Select(r => new { r.Descripcion, r.Latitud, r.Longitud })
                .ToListAsync();

            foreach (var r in existing)
            {
                if (DistanceKm(dto.Latitud, dto.Longitud, r.Latitud, r.Longitud) < 0.05
                    && NormalizedLevenshtein(dto.Descripcion, r.Descripcion) > 0.8)
                {
                    return Conflict(new { mensaje = "Ya existe un reporte muy parecido en esta zona." });
                }
            }

            // Creo el reporte
            var reporte = new Reporte
            {
                Titulo        = dto.Titulo,
                Descripcion   = dto.Descripcion,
                Latitud       = dto.Latitud,
                Longitud      = dto.Longitud,
                FechaCreacion = DateTime.UtcNow,
                Estado        = EstadoReporte.ACTIVO,
                UrlMaps       = dto.UrlMaps,
                UsuarioId     = usuarioId
            };
            _context.Reportes.Add(reporte);
            await _context.SaveChangesAsync();

            // Subo la imagen si viene
            await _blobContainer.CreateIfNotExistsAsync(PublicAccessType.None);
            if (dto.Imagen is { Length: > 0 })
            {
                var ext      = Path.GetExtension(dto.Imagen.FileName);
                var blobName = $"reporte_{reporte.ReporteId}{ext}";
                var blob     = _blobContainer.GetBlobClient(blobName);

                using var stream = dto.Imagen.OpenReadStream();
                await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = dto.Imagen.ContentType });

                var imagen = new ImagenReporte
                {
                    ReporteId = reporte.ReporteId,
                    Url       = blob.Uri.ToString()
                };
                _context.Imagenes.Add(imagen);
                await _context.SaveChangesAsync();

                reporte.ImagenReporte = imagen;
            }

            // Devuelvo el DTO con la URL "cruda" por ahora; será reemplazada por SAS en el GET
            var result = new ReporteDto
            {
                ReporteId     = reporte.ReporteId,
                Titulo        = reporte.Titulo,
                Descripcion   = reporte.Descripcion,
                FechaCreacion = reporte.FechaCreacion,
                Estado        = reporte.Estado,
                UrlMaps       = reporte.UrlMaps,
                UsuarioId     = reporte.UsuarioId,
                UrlImagen     = reporte.ImagenReporte?.Url
            };

            return CreatedAtAction(nameof(GetReporte), new { id = result.ReporteId }, result);
        }

        // GET: api/Reporte/{id}
[HttpGet("{id}")]
[AllowAnonymous]
public async Task<ActionResult<ReporteDto>> GetReporte(int id)
{
    var reporte = await _context.Reportes
        .Include(r => r.ImagenReporte)
        .SingleOrDefaultAsync(r => r.ReporteId == id);

    if (reporte == null) return NotFound();

    // ————————————— Generación de SAS para la imagen —————————————
    string? sasUrl = null;
    if (reporte.ImagenReporte?.Url is string blobUrl)
    {
        var blobName   = Path.GetFileName(new Uri(blobUrl).AbsolutePath);
        var blobClient = _blobContainer.GetBlobClient(blobName);

        if (blobClient.CanGenerateSasUri)
        {
            sasUrl = blobClient
                .GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1))
                .ToString();
        }
        else
        {
            sasUrl = blobUrl;
        }
    }

    // —————— Buscamos la votación de cierre abierta (la última sin FechaFin) ——————
    var votacionAbierta = await _context.Votaciones
        .Where(v => v.ReporteId == id && v.FechaFin == null)
        .OrderByDescending(v => v.FechaInicio)   // o FechaCreacion, según tu modelo
        .FirstOrDefaultAsync();

    // ————————————— Construcción del DTO de respuesta —————————————
    var dto = new ReporteDto
    {
        ReporteId     = reporte.ReporteId,
        Titulo        = reporte.Titulo,
        Descripcion   = reporte.Descripcion,
        FechaCreacion = reporte.FechaCreacion,
        Estado        = reporte.Estado,
        UrlMaps       = reporte.UrlMaps,
        UsuarioId     = reporte.UsuarioId,
        UrlImagen     = sasUrl,
        VotacionId    = votacionAbierta?.VotacionId   // NULL si no hay votación abierta
    };

    return Ok(dto);
}


        // GET: api/Reporte/usuario/{usuarioId}
        [HttpGet("usuario/{usuarioId}")]
        public async Task<ActionResult<IEnumerable<ReporteDto>>> GetPorUsuario(int usuarioId)
        {
            var lista = await _context.Reportes
                .Where(r => r.UsuarioId == usuarioId)
                .Include(r => r.ImagenReporte)
                .ToListAsync();

            var dtos = new List<ReporteDto>(lista.Count);
            foreach (var r in lista)
            {
                string? sasUrl = null;
                if (r.ImagenReporte?.Url is string blobUrl)
                {
                    var blobName = Path.GetFileName(new Uri(blobUrl).AbsolutePath);
                    var blobClient = _blobContainer.GetBlobClient(blobName);
                    if (blobClient.CanGenerateSasUri)
                        sasUrl = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1)).ToString();
                    else
                        sasUrl = blobUrl;
                }

                dtos.Add(new ReporteDto
                {
                    ReporteId     = r.ReporteId,
                    Titulo        = r.Titulo,
                    Descripcion   = r.Descripcion,
                    FechaCreacion = r.FechaCreacion,
                    Estado        = r.Estado,
                    UrlMaps       = r.UrlMaps,
                    UsuarioId     = r.UsuarioId,
                    UrlImagen     = sasUrl
                });
            }

            return Ok(dtos);
        }
         // GET: api/Reporte/estadisticas
        [HttpGet("estadisticas")]
        [AllowAnonymous]
        public async Task<ActionResult<EstadisticaDto>> GetEstadisticas()
        {
            var totalAct = await _context.Reportes.CountAsync(r => r.Estado == EstadoReporte.ACTIVO);
            var totalRes = await _context.Reportes.CountAsync(r => r.Estado == EstadoReporte.RESUELTO);
            return Ok(new EstadisticaDto
            {
                TotalActivos   = totalAct,
                TotalResueltos = totalRes
            });
        }

       // GET: api/Reporte/ordenados
[HttpGet("ordenados")]
[AllowAnonymous]
public async Task<ActionResult<IEnumerable<ReporteConConteosDto>>> GetPorUrgencia()
{
    var estadosPermitidos = new[] {
        EstadoReporte.ACTIVO,
        EstadoReporte.EN_VOTACION
    };

    var lista = await _context.Reportes
        .Where(r => estadosPermitidos.Contains(r.Estado))
        .Select(r => new ReporteConConteosDto
        {
            ReporteId       = r.ReporteId,
            Titulo          = r.Titulo,
            FechaCreacion   = r.FechaCreacion,
            Estado          = (int)r.Estado,
            VotosUrgente    = r.Votos.Count(v => v.Tipo == TipoVoto.URGENTE),
            VotosImportante = r.Votos.Count(v => v.Tipo == TipoVoto.IMPORTANTE)
        })
        .OrderByDescending(x => x.VotosUrgente)
        .ToListAsync();

    return Ok(lista);
}




// GET: api/Reporte/resueltos-y-activos
[HttpGet("resueltos-y-activos")]
[AllowAnonymous]
public async Task<ActionResult<IEnumerable<ReporteDto>>> GetResueltosYActivos()
{
    var estadosFiltrados = new[] { EstadoReporte.RESUELTO, EstadoReporte.ACTIVO };

    var lista = await _context.Reportes
        .Where(r => estadosFiltrados.Contains(r.Estado))
        .Include(r => r.ImagenReporte)
        .ToListAsync();

    // Ordena: primero RESUELTO (2), luego ACTIVO (0)
    var ordenados = lista
        .OrderByDescending(r => r.Estado) // 2 primero, luego 0
        .ToList();

    var dtos = new List<ReporteDto>(ordenados.Count);
    foreach (var r in ordenados)
    {
        string? sasUrl = null;
        if (r.ImagenReporte?.Url is string blobUrl)
        {
            var blobName = Path.GetFileName(new Uri(blobUrl).AbsolutePath);
            var blobClient = _blobContainer.GetBlobClient(blobName);
            if (blobClient.CanGenerateSasUri)
                sasUrl = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1)).ToString();
            else
                sasUrl = blobUrl;
        }

        dtos.Add(new ReporteDto
        {
            ReporteId     = r.ReporteId,
            Titulo        = r.Titulo,
            Descripcion   = r.Descripcion,
            FechaCreacion = r.FechaCreacion,
            Estado        = r.Estado,
            UrlMaps       = r.UrlMaps,
            UsuarioId     = r.UsuarioId,
            UrlImagen     = sasUrl
        });
    }

    return Ok(dtos);
}


        // DELETE: api/Reporte/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReporte(int id)
        {
            var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reporte   = await _context.Reportes.FindAsync(id);
            if (reporte == null)
                return NotFound(new { mensaje = "Reporte no existe." });

            if (reporte.UsuarioId != usuarioId)
                return Forbid();

            if (reporte.Estado == EstadoReporte.EN_VOTACION)
                return BadRequest(new { mensaje = "No se puede eliminar: reporte en votación." });

            if (reporte.Estado == EstadoReporte.RESUELTO)
                return BadRequest(new { mensaje = "No se puede eliminar: reporte resuelto." });

            _context.Reportes.Remove(reporte);
            await _context.SaveChangesAsync();
            return NoContent();
        }

     // —————————————————— Helpers ——————————————————
        private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat/2) * Math.Sin(dLat/2)
                     + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                     * Math.Sin(dLon/2) * Math.Sin(dLon/2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
            return R * c;
        }
        private static double ToRad(double deg) => deg * Math.PI/180.0;

        private static double NormalizedLevenshtein(string s, string t)
        {
            s = s.ToLowerInvariant();
            t = t.ToLowerInvariant();
            int d = Levenshtein(s, t);
            int max = Math.Max(s.Length, t.Length);
            return max == 0 ? 1.0 : 1.0 - (double)d / max;
        }
        private static int Levenshtein(string s, string t)
        {
            int n = s.Length, m = t.Length;
            var dp = new int[n+1, m+1];
            for (int i = 0; i <= n; i++) dp[i,0] = i;
            for (int j = 0; j <= m; j++) dp[0,j] = j;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i-1] == t[j-1] ? 0 : 1;
                    dp[i,j] = Math.Min(
                        Math.Min(dp[i-1,j] + 1, dp[i,j-1] + 1),
                        dp[i-1,j-1] + cost
                    );
                }
            return dp[n,m];
        }
    }
}