using System;

namespace CapaModelo
{
    /// <summary>
    /// AC-05: Entidad que representa la designación formal de inspectores (principal y apoyo)
    /// realizada por la Autoridad DIRCAV con versionado y trazabilidad histórica.
    /// </summary>
    public class AocrDesignacionInspector
    {
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int? InspeccionId { get; set; }
        public int? EstacionId { get; set; }

        public int InspectorId { get; set; }
        public string InspectorCedula { get; set; }
        public string InspectorNombre { get; set; }

        public string InspectorApoyoCedula { get; set; }
        public string InspectorApoyoNombre { get; set; }

        public int DircavUsuarioId { get; set; }
        public string DircavUsuarioNombre { get; set; }

        public string Estado { get; set; } = "DESIGNACION_PENDIENTE_FIRMA_DIRCAV";
        public string Motivo { get; set; }
        public int Version { get; set; } = 1;
        public bool Vigente { get; set; } = true;

        public DateTime FechaDesignacion { get; set; } = DateTime.Now;
        public DateTime? FechaFirma { get; set; }

        // AC-06: Campos para PDF y firma institucional DIRCAV
        public string RutaPdf { get; set; }
        public string RutaDocumentoFirmado { get; set; }
        public string HashDocumento { get; set; }
        public bool Firmado { get; set; }
        public string UsuarioFirma { get; set; }
        public long? TamanioBytes { get; set; }
        public string MimeType { get; set; } = "application/pdf";

        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public string CreadoPor { get; set; }
        public DateTime? ActualizadoEn { get; set; }
        public string ActualizadoPor { get; set; }
    }

    /// <summary>
    /// Petición tipada para designar o reasignar inspectores desde DIRCAV.
    /// </summary>
    public class DircavDesignacionRequest
    {
        public int SolicitudId { get; set; }
        public int? EstacionId { get; set; }
        public string InspectorPrincipalCedula { get; set; }
        public string InspectorApoyoCedula { get; set; }
        public string Motivo { get; set; }
        public int DircavUsuarioId { get; set; }
        public string DircavUsuarioNombre { get; set; }
        public string RolSolicitante { get; set; }
    }

    /// <summary>
    /// Resultado de operación de designación DIRCAV.
    /// </summary>
    public class DircavDesignacionResult
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public string NuevoEstado { get; set; }
        public int DesignacionId { get; set; }
        public int Version { get; set; }
        public int HttpStatusCode { get; set; } = 200;
    }
}
