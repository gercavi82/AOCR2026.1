using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
namespace CapaModelo
{
    public class SolicitudAOCR
    {
        public int CodigoSolicitud { get; set; }
        public string NumeroSolicitud { get; set; }
        public DateTime? FechaSolicitud { get; set; }
        public int? TipoSolicitud { get; set; }
        public string Estado { get; set; }

        public string NombreOperador { get; set; }
        public string Ruc { get; set; }
        public string RazonSocial { get; set; }

        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Direccion { get; set; }
        public string Ciudad { get; set; }
        public string Provincia { get; set; }
        public string Pais { get; set; }

        public string RepresentanteLegal { get; set; }
        public string CedulaRepresentante { get; set; }

        public string TipoOperacion { get; set; }
        public string DescripcionOperacion { get; set; }
        public string Observaciones { get; set; }

        public int CodigoUsuario { get; set; }
        public int? CodigoTecnico { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }

        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public DateTime? FechaInicioOperacion { get; set; }
        public DateTime? FechaFinOperacion { get; set; }
        public string ObservacionesGenerales { get; set; }



        public int Id { get; set; }
        public int UsuarioId { get; set; }

        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaEnvioRevision { get; set; }
        public DateTime? FechaRevision { get; set; }
        
        /// <summary>
        /// Fecha en que se recepcionó formalmente la solicitud (estado RECEPCIONADO)
        /// </summary>
        public DateTime? FechaRecepcion { get; set; }
        
        /// <summary>
        /// Fecha en que se solicitó subsanación de documentos (estado SUBSANACION)
        /// </summary>
        public DateTime? FechaSolicitudSubsanacion { get; set; }
        
        /// <summary>
        /// Fecha en que el operador completó la subsanación (estado SUBSANADO)
        /// </summary>
        public DateTime? FechaSubsanacion { get; set; }
        
        /// <summary>
        /// Fecha de aprobación por el Coordinador
        /// </summary>
        public DateTime? FechaAprobacionCoordinador { get; set; }
        
        /// <summary>
        /// Fecha de aprobación final por el Director
        /// </summary>
        public DateTime? FechaAprobacion { get; set; }
        
        /// <summary>
        /// Fecha en que se emitió el certificado AOCR (estado AOCR_EMITIDO)
        /// </summary>
        public DateTime? FechaEmisionAOCR { get; set; }
        
        /// <summary>
        /// Fecha en que se entregó físicamente el certificado AOCR (estado AOCR_ENTREGADO)
        /// </summary>
        public DateTime? FechaEntregaAOCR { get; set; }
        
        /// <summary>
        /// Número único del certificado AOCR emitido (ej: AOCR-2024-001)
        /// </summary>
        public string NumeroAOCR { get; set; }
        
        /// <summary>
        /// Ruta del archivo PDF del certificado AOCR generado
        /// </summary>
        public string RutaArchivoPDFAOCR { get; set; }
        
        public int? UsuarioRevisionId { get; set; }
        public int? UsuarioAprobacionId { get; set; }
        
        /// <summary>
        /// Usuario Coordinador que aprobó la solicitud
        /// </summary>
        public int? UsuarioAprobacionCoordinadorId { get; set; }
        
        /// <summary>
        /// Usuario Director que aprobó finalmente
        /// </summary>
        public int? UsuarioAprobacionDirectorId { get; set; }
        
        public List<Documento> Documentos { get; set; } = new List<Documento>();
        public List<Pago> Pagos { get; set; } = new List<Pago>();
        public List<Inspeccion> Inspecciones { get; set; } = new List<Inspeccion>();
        public List<Observacion> ObservacionesLista { get; set; } = new List<Observacion>();
        public List<HistorialEstado> HistorialEstados { get; set; } = new List<HistorialEstado>();
        // =========================================================
        // ✅ ALIAS / COMPATIBILIDAD (para Services antiguos)
        // =========================================================

        /// <summary>
        /// Alias: algunos servicios usan FechaActualizacion, pero en el modelo real es UpdatedAt.
        /// </summary>
        [NotMapped]
        public DateTime? FechaActualizacion
        {
            get => UpdatedAt;
            set => UpdatedAt = value;
        }

        /// <summary>
        /// Alias: nombre del director/autoridad que firma.
        /// Si no existe en BD, se maneja como dato "de presentación".
        /// (NoMapped para no afectar EF / migraciones).
        /// </summary>
        [NotMapped]
        public string Director { get; set; }

        /// <summary>
        /// Alias: cargo del director/autoridad que firma.
        /// (NoMapped para no afectar BD).
        /// </summary>
        [NotMapped]
        public string CargoDirector { get; set; }

        // ✅ Para auditoría en Postgres (aocr_tbsolicitud.created_by / updated_by)
        public string UsuarioRegistro { get; set; }
        public string UsuarioActualiza { get; set; }

    }



}
