using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Estados formalizados del flujo de inspecciones técnicas AOCR
    /// Nomenclatura: Estados en MAYÚSCULAS con guión bajo
    /// </summary>
    public static class EstadosInspeccion
    {
        // ============================================
        // CONSTANTES DE ESTADOS
        // ============================================
        
        /// <summary>Inspección creada, sin programar</summary>
        public const string CREADA = "CREADA";
        
        /// <summary>Inspección programada con fecha/hora/lugar</summary>
        public const string PROGRAMADA = "PROGRAMADA";
        
        /// <summary>Inspector está ejecutando la inspección en campo</summary>
        public const string EN_CURSO = "EN_CURSO";
        
        /// <summary>Inspector solicitó aplazar la inspección</summary>
        public const string APLAZADA = "APLAZADA";
        
        /// <summary>Inspector completó trabajo y generó informe preliminar</summary>
        public const string FINALIZADA = "FINALIZADA";
        
        /// <summary>Informe revisado y aprobado por Jefatura Técnica</summary>
        public const string APROBADA = "APROBADA";
        
        /// <summary>Informe rechazado, requiere correcciones del inspector</summary>
        public const string RECHAZADA = "RECHAZADA";
        
        /// <summary>Inspección cancelada sin completar</summary>
        public const string CANCELADA = "CANCELADA";
        
        /// <summary>Inspección completamente cerrada, no se pueden realizar más cambios</summary>
        public const string CERRADA = "CERRADA";


        // ============================================
        // TODAS LAS CONSTANTES EN ARRAY (para validación)
        // ============================================
        public static readonly string[] TodosLosEstados = new[]
        {
            CREADA,
            PROGRAMADA,
            EN_CURSO,
            APLAZADA,
            FINALIZADA,
            APROBADA,
            RECHAZADA,
            CANCELADA,
            CERRADA
        };


        // ============================================
        // MATRIZ DE TRANSICIONES PERMITIDAS
        // ============================================
        /// <summary>
        /// Define qué estados de destino son válidos desde cada estado actual.
        /// Clave: Estado actual
        /// Valor: Lista de estados permitidos como siguiente paso
        /// </summary>
        public static readonly Dictionary<string, List<string>> TransicionesPermitidas = new Dictionary<string, List<string>>
        {
            // CREADA → PROGRAMADA, CANCELADA
            { CREADA, new List<string> { PROGRAMADA, CANCELADA } },
            
            // PROGRAMADA → EN_CURSO, APLAZADA, CANCELADA
            { PROGRAMADA, new List<string> { EN_CURSO, APLAZADA, CANCELADA } },
            
            // EN_CURSO → FINALIZADA, APLAZADA, CANCELADA
            { EN_CURSO, new List<string> { FINALIZADA, APLAZADA, CANCELADA } },
            
            // APLAZADA → PROGRAMADA (reprogramar), CANCELADA
            { APLAZADA, new List<string> { PROGRAMADA, CANCELADA } },
            
            // FINALIZADA → APROBADA, RECHAZADA
            { FINALIZADA, new List<string> { APROBADA, RECHAZADA } },
            
            // RECHAZADA → EN_CURSO (para corregir), FINALIZADA (re-entrega)
            { RECHAZADA, new List<string> { EN_CURSO, FINALIZADA } },
            
            // APROBADA → CERRADA
            { APROBADA, new List<string> { CERRADA } },
            
            // CANCELADA → no tiene transiciones (estado terminal)
            { CANCELADA, new List<string>() },
            
            // CERRADA → no tiene transiciones (estado terminal)
            { CERRADA, new List<string>() }
        };


        // ============================================
        // MÉTODOS DE VALIDACIÓN
        // ============================================
        
        /// <summary>
        /// Verifica si un estado es válido según las constantes definidas
        /// </summary>
        public static bool EsEstadoValido(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return false;

            return TodosLosEstados.Contains(estado.ToUpperInvariant());
        }


        /// <summary>
        /// Valida si una transición de estado es permitida
        /// </summary>
        /// <param name="estadoActual">Estado actual de la inspección</param>
        /// <param name="estadoDestino">Estado al que se desea transicionar</param>
        /// <returns>True si la transición es válida</returns>
        public static bool EsTransicionValida(string estadoActual, string estadoDestino)
        {
            if (string.IsNullOrWhiteSpace(estadoActual) || string.IsNullOrWhiteSpace(estadoDestino))
                return false;

            // Normalizar a mayúsculas
            estadoActual = estadoActual.ToUpperInvariant();
            estadoDestino = estadoDestino.ToUpperInvariant();

            // Validar que ambos estados existan
            if (!EsEstadoValido(estadoActual) || !EsEstadoValido(estadoDestino))
                return false;

            // Si no hay reglas definidas para el estado actual, no permite transiciones
            if (!TransicionesPermitidas.ContainsKey(estadoActual))
                return false;

            // Verificar si el estado destino está en la lista de permitidos
            return TransicionesPermitidas[estadoActual].Contains(estadoDestino);
        }


        /// <summary>
        /// Obtiene la lista de estados válidos desde el estado actual
        /// </summary>
        public static List<string> ObtenerEstadosPermitidos(string estadoActual)
        {
            if (string.IsNullOrWhiteSpace(estadoActual))
                return new List<string>();

            estadoActual = estadoActual.ToUpperInvariant();

            if (!TransicionesPermitidas.ContainsKey(estadoActual))
                return new List<string>();

            return new List<string>(TransicionesPermitidas[estadoActual]);
        }


        /// <summary>
        /// Verifica si un estado es terminal (no permite más transiciones)
        /// </summary>
        public static bool EsEstadoFinal(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return false;

            estado = estado.ToUpperInvariant();

            return estado == CERRADA || estado == CANCELADA;
        }


        /// <summary>
        /// Verifica si una inspección en un estado específico puede ser editada
        /// </summary>
        public static bool PermiteEdicion(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return false;

            estado = estado.ToUpperInvariant();

            // Solo se puede editar en estados iniciales
            return estado == CREADA || estado == PROGRAMADA || estado == RECHAZADA;
        }


        /// <summary>
        /// Verifica si en un estado específico se puede subir informe
        /// </summary>
        public static bool PermiteSubirInforme(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return false;

            estado = estado.ToUpperInvariant();

            // Puede subir informe en EN_CURSO, FINALIZADA, RECHAZADA (re-entrega)
            return estado == EN_CURSO || estado == FINALIZADA || estado == RECHAZADA;
        }


        /// <summary>
        /// Obtiene descripción legible del estado
        /// </summary>
        public static string ObtenerDescripcion(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return "Estado desconocido";

            switch (estado.ToUpperInvariant())
            {
                case CREADA:
                    return "Creada - Sin programar";
                case PROGRAMADA:
                    return "Programada - Fecha asignada";
                case EN_CURSO:
                    return "En Curso - Inspector trabajando";
                case APLAZADA:
                    return "Aplazada - Requiere reprogramación";
                case FINALIZADA:
                    return "Finalizada - Informe generado";
                case APROBADA:
                    return "Aprobada - Informe validado";
                case RECHAZADA:
                    return "Rechazada - Requiere correcciones";
                case CANCELADA:
                    return "Cancelada - No se completará";
                case CERRADA:
                    return "Cerrada - Proceso completo";
                default:
                    return "Estado desconocido";
            }
        }


        /// <summary>
        /// Obtiene el color CSS para badge según el estado
        /// </summary>
        public static string ObtenerColorBadge(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return "default";

            switch (estado.ToUpperInvariant())
            {
                case CREADA:
                    return "info";           // Azul claro (nuevo)
                case PROGRAMADA:
                    return "primary";        // Azul (en planificación)
                case EN_CURSO:
                    return "warning";        // Amarillo (en proceso)
                case APLAZADA:
                    return "default";        // Gris (suspendido temporal)
                case FINALIZADA:
                    return "success";        // Verde claro (completada)
                case APROBADA:
                    return "success";        // Verde (validada)
                case RECHAZADA:
                    return "danger";         // Rojo (requiere atención)
                case CANCELADA:
                    return "default";        // Gris (no activa)
                case CERRADA:
                    return "inverse";        // Negro (archivada)
                default:
                    return "default";
            }
        }
    }
}
