public class Votacion
{
    public int VotacionId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public byte? Resultado { get; set; }
    public int ReporteId { get; set; }
    public int IniciadorId { get; set; }
    // … navegación a Reporte, Usuario, etc.
}
