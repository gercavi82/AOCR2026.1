using System.Collections.Generic;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Estados del workflow de Solicitudes AOCR según diagrama oficial
    /// </summary>
    public static class EstadosSolicitudAOCR
    {
        // ==========================================
        // ESTADOS INICIALES
        // ==========================================
        
        /// <summary>
        /// Solicitud recibida formalmente en el sistema
        /// </summary>
        public const string RECEPCIONADO = "RECEPCIONADO";
        
        /// <summary>
        /// En revisión de documentos y requisitos
        /// </summary>
        public const string ANALISIS_REQUISITOS = "ANALISIS_REQUISITOS";
        
        // ==========================================
        // ESTADOS DE SUBSANACIÓN (CRÍTICO)
        // ==========================================
        
        /// <summary>
        /// Solicitud requiere correcciones de documentos
        /// El solicitante debe cargar documentos corregidos
        /// </summary>
        public const string SUBSANACION = "SUBSANACION";
        
        /// <summary>
        /// Documentos corregidos cargados, pendiente de revisión
        /// </summary>
        public const string SUBSANADO = "SUBSANADO";
        
        // ==========================================
        // ESTADOS DE EVALUACIÓN
        // ==========================================
        
        /// <summary>
        /// En evaluación técnica por inspector
        /// </summary>
        public const string EN_EVALUACION_TECNICA = "EN_EVALUACION_TECNICA";
        
        /// <summary>
        /// En evaluación legal por coordinador legal
        /// </summary>
        public const string EN_EVALUACION_LEGAL = "EN_EVALUACION_LEGAL";
        
        /// <summary>
        /// En evaluación financiera por coordinador financiero
        /// </summary>
        public const string EN_EVALUACION_FINANCIERA = "EN_EVALUACION_FINANCIERA";
        
        // ==========================================
        // ESTADOS DE APROBACIÓN (CRÍTICO)
        // ==========================================
        
        /// <summary>
        /// En aprobación de coordinadores (legal o financiero)
        /// </summary>
        public const string EN_APROBACION_COORDINADOR = "EN_APROBACION_COORDINADOR";
        
        /// <summary>
        /// En aprobación final del Director Financiero
        /// CRÍTICO - Última autorización jerárquica
        /// </summary>
        public const string EN_APROBACION_DIRECTOR = "EN_APROBACION_DIRECTOR";
        
        // ==========================================
        // ESTADOS FINALES
        // ==========================================
        
        /// <summary>
        /// Solicitud aprobada por Director
        /// </summary>
        public const string APROBADO = "APROBADO";
        
        /// <summary>
        /// Solicitud rechazada (estado final negativo)
        /// </summary>
        public const string RECHAZADO = "RECHAZADO";
        
        /// <summary>
        /// Certificado AOCR emitido y generado (PDF)
        /// CRÍTICO - Registro de emisión del certificado
        /// </summary>
        public const string AOCR_EMITIDO = "AOCR_EMITIDO";
        
        /// <summary>
        /// Certificado AOCR entregado al solicitante
        /// Estado final exitoso del proceso
        /// </summary>
        public const string AOCR_ENTREGADO = "AOCR_ENTREGADO";
        
        // ==========================================
        // TRANSICIONES PERMITIDAS
        // ==========================================
        
        /// <summary>
        /// Define qué estados pueden cambiar a qué otros estados
        /// </summary>
        public static readonly Dictionary<string, List<string>> TransicionesPermitidas = new Dictionary<string, List<string>>
        {
            [RECEPCIONADO] = new List<string> { ANALISIS_REQUISITOS },
            
            [ANALISIS_REQUISITOS] = new List<string> 
            { 
                SUBSANACION,                    // Si hay observaciones
                EN_EVALUACION_TECNICA,          // Si todo está OK
                RECHAZADO                       // Si no cumple requisitos básicos
            },
            
            [SUBSANACION] = new List<string> 
            { 
                SUBSANADO,                      // Cuando solicitante carga correcciones
                RECHAZADO                       // Si no subsana en plazo
            },
            
            [SUBSANADO] = new List<string> 
            { 
                ANALISIS_REQUISITOS             // Revaluar documentos corregidos
            },
            
            [EN_EVALUACION_TECNICA] = new List<string> 
            { 
                EN_EVALUACION_LEGAL,
                SUBSANACION,                    // Si se encuentran problemas
                RECHAZADO
            },
            
            [EN_EVALUACION_LEGAL] = new List<string> 
            { 
                EN_EVALUACION_FINANCIERA,
                SUBSANACION,
                RECHAZADO
            },
            
            [EN_EVALUACION_FINANCIERA] = new List<string> 
            { 
                EN_APROBACION_COORDINADOR,
                SUBSANACION,
                RECHAZADO
            },
            
            [EN_APROBACION_COORDINADOR] = new List<string> 
            { 
                EN_APROBACION_DIRECTOR,         // Aprobado por coordinadores
                SUBSANACION,                    // Si coordinador solicita correcciones
                RECHAZADO
            },
            
            [EN_APROBACION_DIRECTOR] = new List<string> 
            { 
                APROBADO,                       // Aprobado por Director
                SUBSANACION,                    // Director solicita correcciones
                RECHAZADO                       // Director rechaza
            },
            
            [APROBADO] = new List<string> 
            { 
                AOCR_EMITIDO                    // Generar certificado
            },
            
            [AOCR_EMITIDO] = new List<string> 
            { 
                AOCR_ENTREGADO                  // Entregar físicamente
            },
            
            // Estados finales sin transiciones
            [RECHAZADO] = new List<string>(),
            [AOCR_ENTREGADO] = new List<string>()
        };
        
        // ==========================================
        // MÉTODOS AUXILIARES
        // ==========================================
        
        /// <summary>
        /// Verifica si una transición de estado es válida
        /// </summary>
        public static bool EsTransicionValida(string estadoActual, string estadoNuevo)
        {
            if (string.IsNullOrEmpty(estadoActual) || string.IsNullOrEmpty(estadoNuevo))
                return false;
            
            if (!TransicionesPermitidas.ContainsKey(estadoActual))
                return false;
            
            return TransicionesPermitidas[estadoActual].Contains(estadoNuevo);
        }
        
        /// <summary>
        /// Obtiene los estados permitidos desde un estado actual
        /// </summary>
        public static List<string> ObtenerEstadosPermitidos(string estadoActual)
        {
            if (string.IsNullOrEmpty(estadoActual))
                return new List<string> { RECEPCIONADO };
            
            if (!TransicionesPermitidas.ContainsKey(estadoActual))
                return new List<string>();
            
            return TransicionesPermitidas[estadoActual];
        }
        
        /// <summary>
        /// Verifica si un estado es final (sin más transiciones)
        /// </summary>
        public static bool EsEstadoFinal(string estado)
        {
            return estado == RECHAZADO || estado == AOCR_ENTREGADO;
        }
        
        /// <summary>
        /// Obtiene descripción amigable del estado
        /// </summary>
        public static string ObtenerDescripcion(string estado)
        {
            switch (estado)
            {
                case RECEPCIONADO:
                    return "Recepcionado - Solicitud ingresada al sistema";
                case ANALISIS_REQUISITOS:
                    return "Análisis de Requisitos - Revisión de documentación";
                case SUBSANACION:
                    return "En Subsanación - Requiere correcciones del solicitante";
                case SUBSANADO:
                    return "Subsanado - Documentos corregidos cargados";
                case EN_EVALUACION_TECNICA:
                    return "Evaluación Técnica - Revisión por inspector";
                case EN_EVALUACION_LEGAL:
                    return "Evaluación Legal - Revisión por coordinador legal";
                case EN_EVALUACION_FINANCIERA:
                    return "Evaluación Financiera - Revisión por coordinador financiero";
                case EN_APROBACION_COORDINADOR:
                    return "Aprobación Coordinador - En revisión de coordinadores";
                case EN_APROBACION_DIRECTOR:
                    return "Aprobación Director - Autorización final";
                case APROBADO:
                    return "Aprobado - Solicitud autorizada";
                case AOCR_EMITIDO:
                    return "AOCR Emitido - Certificado generado";
                case AOCR_ENTREGADO:
                    return "AOCR Entregado - Proceso completado";
                case RECHAZADO:
                    return "Rechazado - Solicitud denegada";
                default:
                    return estado;
            }
        }
    }
}
