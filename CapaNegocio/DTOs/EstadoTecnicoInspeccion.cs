using System;

namespace CapaNegocio.DTOs
{
    public class EstadoTecnicoInspeccion
    {
        public int CodigoInspeccion { get; set; }
        public int CodigoSolicitud { get; set; }
        public int? InspectorId { get; set; }
        
        public bool LvExiste { get; set; }
        public bool LvFinalizada { get; set; }
        public bool LvFirmada { get; set; }
        public string RutaLvFirmada { get; set; }
        public bool ArchivoLvFirmadoExiste { get; set; }
        
        public bool InformeExiste { get; set; }
        public string EstadoInforme { get; set; }
        public string RutaInformeFirmado { get; set; }
        
        public string EstadoCentral { get; set; }
        
        public bool PuedeCrearInforme { get; set; }
        public bool PuedeEditarInforme { get; set; }
        public bool PuedeFirmarInforme { get; set; }
        public bool PuedeVerInforme { get; set; }
        
        public string MotivoBloqueo { get; set; }
    }
}
