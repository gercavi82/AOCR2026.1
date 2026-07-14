using System;

namespace CapaModelo
{
    public class NoConformidad
    {
        public int CodigoNoConformidad { get; set; }
        public int CodigoInspeccion { get; set; }
        public int CodigoInforme { get; set; }
        public int CodigoSolicitud { get; set; }
        public int? CodigoNoConformidadRaiz { get; set; }
        public int? CodigoSolicitudOrigen { get; set; }
        public int? CodigoInspeccionOrigen { get; set; }
        public int? CodigoInformeOrigen { get; set; }
        public int? CodigoSolicitudNueva { get; set; }
        public int? CodigoInspeccionNueva { get; set; }
        public int? CodigoInformeCierre { get; set; }
        public int CicloEvaluacion { get; set; } = 1;
        public DateTime? FechaCierre { get; set; }
        public int? UsuarioCierre { get; set; }
        public string ObservacionCierre { get; set; }
        public string CorrelationId { get; set; }
        
        /// <summary>
        /// CON_INSPECCION o SIN_INSPECCION
        /// </summary>
        public string TipoRuta { get; set; }
        
        /// <summary>
        /// BORRADOR, GENERADA, FIRMADA_INSPECTOR, ENVIADA_COORDINADOR, DEVUELTA_INSPECTOR, CORREGIDA, APROBADA_COORDINADOR, FIRMADA_COORDINADOR, NOTIFICADA_RT, EN_SUBSANACION, SUBSANADA_RT, EN_REVISION_INSPECTOR, CERRADA
        /// </summary>
        public string Estado { get; set; }
        
        public string NumeroNoConformidad { get; set; }
        public string Resumen { get; set; }
        public string Detalle { get; set; }
        public string FundamentoTecnico { get; set; }
        public string AccionesRequeridas { get; set; }
        
        public int? PlazoSubsanacion { get; set; }
        public bool RequiereNuevaInspeccion { get; set; }
        public int Version { get; set; }
        
        public string RutaPdf { get; set; }
        public string RutaPdfFirmadoInspector { get; set; }
        public string RutaPdfFirmadoCoordinador { get; set; }
        public string RutaPdfSubsanacionRt { get; set; }
        public string HashDocumento { get; set; }
        
        public DateTime? FechaGeneracion { get; set; }
        public DateTime? FechaFirmaInspector { get; set; }
        public DateTime? FechaEnvioCoordinador { get; set; }
        public DateTime? FechaDevolucion { get; set; }
        public DateTime? FechaFirmaCoordinador { get; set; }
        public DateTime? FechaNotificacionRt { get; set; }
        public DateTime? FechaSubsanacionRt { get; set; }
        
        public int? UsuarioCreacion { get; set; }
        public int? UsuarioFirmaInspector { get; set; }
        public int? UsuarioFirmaCoordinador { get; set; }
        
        public string ObservacionDevolucion { get; set; }
        
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
