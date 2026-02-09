// ===================================================================
// Subsanacion.cs
// ===================================================================
// Propósito: Modelo de datos para subsanaciones de solicitudes AOCR
// 
// Representa una solicitud de corrección/completar documentación enviada
// por un técnico al operador, y la respuesta del operador.
// 
// Estados:
//   - PENDIENTE: Subsanación solicitada, esperando respuesta del operador
//   - COMPLETADA: Operador completó la solicitud de subsanación
//   - CANCELADA: Subsanación cancelada (no aplicable)
//   - VENCIDA: Expiró el tiempo para responder (opcional)
// 
// Fecha: 2025-01-05
// ===================================================================

using System;

namespace CapaModelo
{
    public class Subsanacion
    {
        /// <summary>
        /// Código único de la subsanación (PK)
        /// </summary>
        public int CodigoSubsanacion { get; set; }

        /// <summary>
        /// Código de la solicitud AOCR relacionada (FK)
        /// </summary>
        public int CodigoSolicitud { get; set; }

        /// <summary>
        /// Fecha cuando se solicitó la subsanación
        /// </summary>
        public DateTime? FechaSolicitud { get; set; }

        /// <summary>
        /// Descripción de documentos/requisitos que deben subsanarse
        /// </summary>
        public string Observaciones { get; set; }

        /// <summary>
        /// Usuario técnico que solicita la subsanación (FK)
        /// </summary>
        public int CodigoUsuarioSolicitante { get; set; }

        /// <summary>
        /// Fecha cuando el operador respondió/completó la subsanación
        /// </summary>
        public DateTime? FechaRespuesta { get; set; }

        /// <summary>
        /// Comentarios del operador al completar la subsanación
        /// </summary>
        public string Respuesta { get; set; }

        /// <summary>
        /// Usuario operador que completó la subsanación (FK)
        /// </summary>
        public int? CodigoUsuarioRespuesta { get; set; }

        /// <summary>
        /// Estado de la subsanación: PENDIENTE, COMPLETADA, CANCELADA, VENCIDA
        /// </summary>
        public string Estado { get; set; }

        // ===============================================================
        // AUDITORÍA
        // ===============================================================

        /// <summary>
        /// Fecha de creación del registro
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Fecha de última actualización
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Usuario que creó el registro
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// Usuario que actualizó el registro
        /// </summary>
        public string UpdatedBy { get; set; }

        // ===============================================================
        // PROPIEDADES CALCULADAS / NAVEGACIÓN (NotMapped)
        // ===============================================================

        /// <summary>
        /// Días transcurridos desde la solicitud
        /// </summary>
        public int DiasPendiente
        {
            get
            {
                if (FechaRespuesta.HasValue)
                    return 0;

                if (!FechaSolicitud.HasValue)
                    return 0;

                return (DateTime.Now - FechaSolicitud.Value).Days;
            }
        }

        /// <summary>
        /// Indica si la subsanación está pendiente de respuesta
        /// </summary>
        public bool EsPendiente => Estado == "PENDIENTE";

        /// <summary>
        /// Indica si la subsanación fue completada
        /// </summary>
        public bool EstaCompletada => Estado == "COMPLETADA";
    }
}
