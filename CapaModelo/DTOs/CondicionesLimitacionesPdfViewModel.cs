using System;
using System.Collections.Generic;

namespace CapaModelo.DTOs
{
    /// <summary>
    /// AC-10: ViewModel tipado exclusivamente para la generación del documento PDF oficial
    /// de Condiciones y Limitaciones con membrete y estándares institucionales DGAC.
    /// </summary>
    public class CondicionesLimitacionesPdfViewModel
    {
        public int SolicitudId { get; set; }
        public string NumeroAocr { get; set; }
        public int Version { get; set; } = 1;
        public string TipoTramite { get; set; } = "EMISIÓN";
        public DateTime? FechaEmision { get; set; } = DateTime.Now;
        public DateTime? FechaVencimiento { get; set; }

        // Compañía y Operador
        public string Compania { get; set; }
        public string NombreOperador { get; set; }
        public string PaisOperador { get; set; }
        public string NumeroAoc { get; set; }
        public string RepresentanteTecnico { get; set; }
        public string CedulaRt { get; set; }

        // Estaciones Autorizadas (AC-02)
        public List<CondicionEstacionPdfItem> Estaciones { get; set; } = new List<CondicionEstacionPdfItem>();

        // Aeronaves Autorizadas
        public List<CondicionAeronavePdfItem> Aeronaves { get; set; } = new List<CondicionAeronavePdfItem>();

        // Tipo de Operación y Rutas
        public string TipoOperacion { get; set; }
        public string RutasAutorizadas { get; set; }
        public string AlcanceAutorizado { get; set; }

        // Condiciones Aprobadas y Limitaciones
        public string CondicionesAprobadas { get; set; }
        public string Limitaciones { get; set; }
        public string Observaciones { get; set; }

        // Inspector e Informe Técnico
        public string InspectorNombre { get; set; }
        public DateTime? FechaInformeTecnico { get; set; }

        // Autoridad DIRCAV Firmante
        public string NombreDirectorCertificacion { get; set; }
        public string CargoDirectorCertificacion { get; set; } = "Director de Certificación Aeronáutica y Vigilancia Continua";
        public DateTime? FechaFirmaDircav { get; set; }

        // Integridad y Trazabilidad
        public string HashDocumento { get; set; }
        public string CodigoVerificacion { get; set; }
        public bool EsVistaPrevia { get; set; }
        public string EstadoDocumento { get; set; }
    }

    public class CondicionEstacionPdfItem
    {
        public string CodigoOaci { get; set; }
        public string NombreAeropuerto { get; set; }
        public string Ciudad { get; set; }
        public string FechasInspeccion { get; set; }
        public string Estado { get; set; }
    }

    public class CondicionAeronavePdfItem
    {
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public string Serie { get; set; }
        public string Matricula { get; set; }
        public string Configuracion { get; set; }
        public string EstacionesHabilitadas { get; set; }
    }
}
