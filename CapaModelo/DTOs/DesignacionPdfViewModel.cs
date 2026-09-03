using System;
using System.Collections.Generic;

namespace CapaModelo.DTOs
{
    /// <summary>
    /// AC-06: DTO tipado con los metadatos necesarios para generar el PDF oficial
    /// de designación del Inspector firmado por DIRCAV.
    /// </summary>
    public class DesignacionPdfViewModel
    {
        public int DesignacionId { get; set; }
        public int SolicitudId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NumeroDesignacion { get; set; }
        public int Version { get; set; } = 1;
        public string Estado { get; set; }

        public string Compania { get; set; }
        public string NombreOperador { get; set; }
        public string PaisOperador { get; set; }
        public string NumeroAoc { get; set; }
        public string TipoOperacion { get; set; }
        public string TipoSolicitud { get; set; }

        public string ResponsableTecnico { get; set; }
        public string CedulaRt { get; set; }
        public string EmailRt { get; set; }

        public string InspectorPrincipalNombre { get; set; }
        public string InspectorPrincipalCedula { get; set; }
        public string InspectorPrincipalCargo { get; set; }

        public string InspectorApoyoNombre { get; set; }
        public string InspectorApoyoCedula { get; set; }
        public string InspectorApoyoCargo { get; set; }

        public List<DesignacionEstacionItemDto> Estaciones { get; set; }

        public DateTime FechaEmision { get; set; } = DateTime.Now;
        public DateTime? FechaFirma { get; set; }
        public string AutoridadDircavNombre { get; set; }
        public string AutoridadDircavCargo { get; set; } = "Director de Certificación Aeronáutica (DIRCAV)";

        public bool EsVistaPrevia { get; set; }
        public string HashDocumento { get; set; }
        public string CodigoVerificacion { get; set; }

        public DesignacionPdfViewModel()
        {
            Estaciones = new List<DesignacionEstacionItemDto>();
            NumeroSolicitud = string.Empty;
            NumeroDesignacion = string.Empty;
            Compania = string.Empty;
            NombreOperador = string.Empty;
            PaisOperador = "Ecuador";
            TipoOperacion = "Transporte Aéreo Regular";
            TipoSolicitud = "Emisión";
            ResponsableTecnico = string.Empty;
            InspectorPrincipalNombre = string.Empty;
            InspectorPrincipalCargo = "Inspector de Operaciones / Aeronavegabilidad";
            AutoridadDircavNombre = "Autoridad DIRCAV";
        }
    }

    public class DesignacionEstacionItemDto
    {
        public int EstacionId { get; set; }
        public string CodigoOaci { get; set; }
        public string NombreCiudad { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estado { get; set; }
    }
}
