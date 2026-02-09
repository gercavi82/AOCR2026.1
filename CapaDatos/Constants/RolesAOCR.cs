using System.Collections.Generic;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Roles del sistema AOCR con estructura jerárquica
    /// Define quién puede hacer qué en cada etapa del workflow
    /// </summary>
    public static class RolesAOCR
    {
        // ==========================================
        // ROLES OPERATIVOS
        // ==========================================
        
        /// <summary>
        /// Administrador del sistema - Acceso total
        /// </summary>
        public const string ADMINISTRADOR = "Administrador";
        
        /// <summary>
        /// Operador - Recepciona y analiza requisitos iniciales
        /// </summary>
        public const string OPERADOR = "Operador";
        
        /// <summary>
        /// Evaluador Técnico - Evalúa aspectos técnicos de solicitudes
        /// </summary>
        public const string EVALUADOR_TECNICO = "EvaluadorTecnico";
        
        /// <summary>
        /// Inspector - Realiza inspecciones físicas en campo
        /// </summary>
        public const string INSPECTOR = "Inspector";
        
        // ==========================================
        // ROLES DE COORDINACIÓN
        // ==========================================
        
        /// <summary>
        /// Coordinador de Inspecciones - Supervisa inspecciones
        /// </summary>
        public const string COORDINADOR_INSPECCIONES = "CoordinadorInspecciones";
        
        /// <summary>
        /// Coordinador Legal - Revisión y aprobación legal
        /// </summary>
        public const string COORDINADOR_LEGAL = "CoordinadorLegal";
        
        /// <summary>
        /// Coordinador Financiero - Revisión y aprobación financiera
        /// </summary>
        public const string COORDINADOR_FINANCIERO = "CoordinadorFinanciero";
        
        // ==========================================
        // ROLES DE DIRECCIÓN (CRÍTICO)
        // ==========================================
        
        /// <summary>
        /// Director Financiero - Aprobación final jerárquica
        /// CRÍTICO: Última autorización antes de emisión AOCR
        /// </summary>
        public const string DIRECTOR_FINANCIERO = "DirectorFinanciero";
        
        /// <summary>
        /// Jefatura Técnica - Supervisión técnica general
        /// </summary>
        public const string JEFATURA_TECNICA = "JefaturaTecnica";
        
        // ==========================================
        // ROLES EXTERNOS
        // ==========================================
        
        /// <summary>
        /// Solicitante - Operador de aeronaves que solicita AOCR
        /// </summary>
        public const string SOLICITANTE = "Solicitante";
        
        /// <summary>
        /// Representante Legal - Representante del solicitante
        /// </summary>
        public const string REPRESENTANTE_LEGAL = "RepresentanteLegal";
        
        // ==========================================
        // GRUPOS DE ROLES PARA AUTORIZACIÓN
        // ==========================================
        
        /// <summary>
        /// Roles con acceso administrativo completo
        /// </summary>
        public static readonly string[] ROLES_ADMIN = new[]
        {
            ADMINISTRADOR
        };
        
        /// <summary>
        /// Roles de coordinación (pueden aprobar y supervisar)
        /// </summary>
        public static readonly string[] ROLES_COORDINADORES = new[]
        {
            COORDINADOR_INSPECCIONES,
            COORDINADOR_LEGAL,
            COORDINADOR_FINANCIERO
        };
        
        /// <summary>
        /// Roles de dirección (aprobación final)
        /// </summary>
        public static readonly string[] ROLES_DIRECTORES = new[]
        {
            DIRECTOR_FINANCIERO,
            JEFATURA_TECNICA
        };
        
        /// <summary>
        /// Roles técnicos (evaluación e inspección)
        /// </summary>
        public static readonly string[] ROLES_TECNICOS = new[]
        {
            EVALUADOR_TECNICO,
            INSPECTOR,
            COORDINADOR_INSPECCIONES
        };
        
        /// <summary>
        /// Roles internos de DGAC (todos menos solicitantes/externos)
        /// </summary>
        public static readonly string[] ROLES_INTERNOS = new[]
        {
            ADMINISTRADOR,
            OPERADOR,
            EVALUADOR_TECNICO,
            INSPECTOR,
            COORDINADOR_INSPECCIONES,
            COORDINADOR_LEGAL,
            COORDINADOR_FINANCIERO,
            DIRECTOR_FINANCIERO,
            JEFATURA_TECNICA
        };
        
        /// <summary>
        /// Roles externos (no son personal DGAC)
        /// </summary>
        public static readonly string[] ROLES_EXTERNOS = new[]
        {
            SOLICITANTE,
            REPRESENTANTE_LEGAL
        };
        
        // ==========================================
        // PERMISOS POR ESTADO
        // ==========================================
        
        /// <summary>
        /// Obtiene roles que pueden editar una solicitud en un estado dado
        /// </summary>
        public static List<string> ObtenerRolesPermitidosParaEstado(string estado)
        {
            var roles = new List<string>();
            
            switch (estado)
            {
                case "RECEPCIONADO":
                case "ANALISIS_REQUISITOS":
                    roles.AddRange(new[] { ADMINISTRADOR, OPERADOR });
                    break;
                
                case "SUBSANACION":
                    roles.AddRange(new[] { ADMINISTRADOR, SOLICITANTE }); // Solo solicitante puede subsanar
                    break;
                
                case "SUBSANADO":
                    roles.AddRange(new[] { ADMINISTRADOR, OPERADOR, EVALUADOR_TECNICO });
                    break;
                
                case "EN_EVALUACION_TECNICA":
                    roles.AddRange(new[] { ADMINISTRADOR, EVALUADOR_TECNICO, INSPECTOR, COORDINADOR_INSPECCIONES });
                    break;
                
                case "EN_EVALUACION_LEGAL":
                    roles.AddRange(new[] { ADMINISTRADOR, COORDINADOR_LEGAL });
                    break;
                
                case "EN_EVALUACION_FINANCIERA":
                    roles.AddRange(new[] { ADMINISTRADOR, COORDINADOR_FINANCIERO });
                    break;
                
                case "EN_APROBACION_COORDINADOR":
                    roles.AddRange(new[] { ADMINISTRADOR, COORDINADOR_LEGAL, COORDINADOR_FINANCIERO });
                    break;
                
                case "EN_APROBACION_DIRECTOR":
                    roles.AddRange(new[] { ADMINISTRADOR, DIRECTOR_FINANCIERO, JEFATURA_TECNICA });
                    break;
                
                case "APROBADO":
                case "AOCR_EMITIDO":
                    roles.AddRange(new[] { ADMINISTRADOR, COORDINADOR_FINANCIERO });
                    break;
                
                default:
                    roles.Add(ADMINISTRADOR); // Solo admin por defecto
                    break;
            }
            
            return roles;
        }
        
        /// <summary>
        /// Verifica si un rol puede cambiar al estado especificado
        /// </summary>
        public static bool PuedeTransicionarAEstado(string rol, string estadoDestino)
        {
            switch (estadoDestino)
            {
                case "SUBSANACION":
                    return EsRolInterno(rol) && !EsExternoSoloLectura(rol);
                
                case "SUBSANADO":
                    return rol == SOLICITANTE || rol == REPRESENTANTE_LEGAL;
                
                case "EN_APROBACION_COORDINADOR":
                    return EsCoordinador(rol) || EsAdmin(rol);
                
                case "EN_APROBACION_DIRECTOR":
                    return EsCoordinador(rol) || EsAdmin(rol);
                
                case "APROBADO":
                    return EsDirector(rol) || EsAdmin(rol);
                
                case "RECHAZADO":
                    return EsRolInterno(rol); // Cualquier interno puede rechazar
                
                case "AOCR_EMITIDO":
                    return EsDirector(rol) || EsCoordinador(rol) || EsAdmin(rol);
                
                case "AOCR_ENTREGADO":
                    return EsCoordinador(rol) || EsAdmin(rol);
                
                default:
                    return EsAdmin(rol);
            }
        }
        
        // ==========================================
        // HELPERS DE VERIFICACIÓN
        // ==========================================
        
        public static bool EsAdmin(string rol)
        {
            return rol == ADMINISTRADOR;
        }
        
        public static bool EsCoordinador(string rol)
        {
            return rol == COORDINADOR_INSPECCIONES ||
                   rol == COORDINADOR_LEGAL ||
                   rol == COORDINADOR_FINANCIERO;
        }
        
        public static bool EsDirector(string rol)
        {
            return rol == DIRECTOR_FINANCIERO ||
                   rol == JEFATURA_TECNICA;
        }
        
        public static bool EsRolInterno(string rol)
        {
            return System.Array.IndexOf(ROLES_INTERNOS, rol) >= 0;
        }
        
        public static bool EsRolExterno(string rol)
        {
            return System.Array.IndexOf(ROLES_EXTERNOS, rol) >= 0;
        }
        
        public static bool EsExternoSoloLectura(string rol)
        {
            return rol == SOLICITANTE || rol == REPRESENTANTE_LEGAL;
        }
        
        public static bool PuedeRevisarDocumentos(string rol)
        {
            return EsAdmin(rol) ||
                   rol == OPERADOR ||
                   rol == EVALUADOR_TECNICO ||
                   EsCoordinador(rol) ||
                   EsDirector(rol);
        }
        
        public static bool PuedeAprobar(string rol)
        {
            return EsAdmin(rol) ||
                   EsCoordinador(rol) ||
                   EsDirector(rol);
        }
        
        public static bool PuedeSolicitarSubsanacion(string rol)
        {
            return EsAdmin(rol) ||
                   rol == OPERADOR ||
                   rol == EVALUADOR_TECNICO ||
                   EsCoordinador(rol) ||
                   EsDirector(rol);
        }
        
        /// <summary>
        /// Obtiene descripción amigable del rol
        /// </summary>
        public static string ObtenerDescripcion(string rol)
        {
            switch (rol)
            {
                case ADMINISTRADOR:
                    return "Administrador del Sistema";
                case OPERADOR:
                    return "Operador - Recepción de Solicitudes";
                case EVALUADOR_TECNICO:
                    return "Evaluador Técnico";
                case INSPECTOR:
                    return "Inspector de Campo";
                case COORDINADOR_INSPECCIONES:
                    return "Coordinador de Inspecciones";
                case COORDINADOR_LEGAL:
                    return "Coordinador Legal";
                case COORDINADOR_FINANCIERO:
                    return "Coordinador Financiero";
                case DIRECTOR_FINANCIERO:
                    return "Director Financiero";
                case JEFATURA_TECNICA:
                    return "Jefe de Área Técnica";
                case SOLICITANTE:
                    return "Solicitante AOCR";
                case REPRESENTANTE_LEGAL:
                    return "Representante Legal";
                default:
                    return rol;
            }
        }
    }
}
