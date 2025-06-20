using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CityCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CityCore.Api.Services
{
    /// <summary>
    /// Servicio que periódicamente cierra las votaciones que llevan más de 1 minuto abiertas.
    /// </summary>
    public class VotacionClosureService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan            _delay = TimeSpan.FromSeconds(30);

        public VotacionClosureService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<CityCoreContext>();

                    // Buscar votaciones abiertas hace >1 minuto y no cerradas
                    var expiradas = await db.Votaciones
                        .Include(v => v.Participaciones)
                        .Include(v => v.Reporte)
                        .Where(v => v.FechaFin == null
                                    && v.FechaInicio.AddMinutes(1) <= DateTime.UtcNow)
                        .ToListAsync(stoppingToken);

                    foreach (var vot in expiradas)
                    {
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
                    }

                    if (expiradas.Any())
                        await db.SaveChangesAsync(stoppingToken);
                }
                catch
                {
                    // aquí podrías loggear el error
                }

                // espera antes de la siguiente pasada
                await Task.Delay(_delay, stoppingToken);
            }
        }
    }
}
