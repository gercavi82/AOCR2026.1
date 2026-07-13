using System;

namespace CapaDatos.Models
{
    public sealed class EnvioDocumentosDcavSnapshot
    {
        public int SolicitudId { get; set; }
        public bool SolicitudActiva { get; set; }
        public string EstadoSolicitud { get; set; }
        public string CodigoCompania { get; set; }
        public int InspeccionId { get; set; }
        public int InspectorId { get; set; }
        public string EstadoCentral { get; set; }
        public long VersionExpediente { get; set; }
        public int InformeId { get; set; }
        public string EstadoInforme { get; set; }
        public string ResultadoInforme { get; set; }
        public bool InformeFinalizado { get; set; }
        public bool InformeFirmado { get; set; }
        public bool ListaFirmada { get; set; }
        public int AocrId { get; set; }
        public int VersionAocr { get; set; }
        public string EstadoAocr { get; set; }
        public string CompaniaAocr { get; set; }
        public int InspectorAocr { get; set; }
        public bool AocrVigente { get; set; }
        public bool AocrEliminado { get; set; }
        public bool AocrFirmado { get; set; }
        public int CondicionesId { get; set; }
        public int VersionCondiciones { get; set; }
        public string EstadoCondiciones { get; set; }
        public string CompaniaCondiciones { get; set; }
        public int InspectorCondiciones { get; set; }
        public bool CondicionesVigente { get; set; }
        public bool CondicionesEliminado { get; set; }
        public bool CondicionesFirmadas { get; set; }
        public string NumeroAoc { get; set; }
        public string Pais { get; set; }
        public string Operador { get; set; }
        public string PuntoContacto { get; set; }
        public string RepresentanteTecnico { get; set; }
        public string Aeropuertos { get; set; }
        public string Condiciones { get; set; }
        public string Limitaciones { get; set; }
        public int AeronavesCompletas { get; set; }
        public DateTime? FechaVencimiento { get; set; }
    }

    public sealed class EnvioDocumentosDcavIdempotencia
    {
        public string Clave { get; set; }
        public int SolicitudId { get; set; }
        public int AocrId { get; set; }
        public int CondicionesId { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public string Resultado { get; set; }
        public DateTime Fecha { get; set; }
    }
}
