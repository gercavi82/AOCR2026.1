using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio empresarial para gestión robusta de estados de Órdenes de Recaudación
    /// Implementa State Machine Pattern + Business Rules
    /// </summary>
    public class EstadoOrdenService
    {
        #region Definición de Estados

        public static class Estados
        {
            public const string BORRADOR = "BORRADOR";
            public const string GENERADA = "GENERADA";  
            public const string ENVIADA = "ENVIADA";
            public const string PAGADA = "PAGADA";
            public const string ANULADA = "ANULADA";
            public const string RECHAZADA = "RECHAZADA";
            public const string APROBADA = "APROBADA";
            public const string FACTURADA = "FACTURADA";
            
            public static readonly List<string> TodosLosEstados = new List<string>
            {
                BORRADOR, GENERADA, ENVIADA, PAGADA, ANULADA, RECHAZADA, APROBADA, FACTURADA
            };
        }

        public static class Roles
        {
            public const string SOLICITANTE = "Solicitante";
            public const string FINANCIERO = "Financiero";
            public const string ADMINISTRADOR = "Administrador";
        }

        #endregion

        #region Matriz de Transiciones Permitidas

        private static readonly Dictionary<string, List<string>> TransicionesPermitidas = new Dictionary<string, List<string>>
        {
            // BORRADOR → Solo se puede GENERAR
            [Estados.BORRADOR] = new List<string> { Estados.GENERADA, Estados.ANULADA },
            
            // GENERADA → Se puede ENVIAR o ANULAR
            [Estados.GENERADA] = new List<string> { Estados.ENVIADA, Estados.ANULADA },
            
            // ENVIADA → Se puede APROBAR, RECHAZAR o registrar PAGO
            [Estados.ENVIADA] = new List<string> { Estados.APROBADA, Estados.RECHAZADA, Estados.PAGADA },
            
            // APROBADA → Se puede registrar PAGO
            [Estados.APROBADA] = new List<string> { Estados.PAGADA },
            
            // PAGADA → Se puede FACTURAR
            [Estados.PAGADA] = new List<string> { Estados.FACTURADA },
            
            // Estados finales (no permiten más transiciones)
            [Estados.FACTURADA] = new List<string>(),
            [Estados.ANULADA] = new List<string>(),
            [Estados.RECHAZADA] = new List<string>()
        };

        #endregion

        #region Permisos por Rol y Estado

        private static readonly Dictionary<string, List<string>> PermisosEdicion = new Dictionary<string, List<string>>
        {
            // Solo BORRADOR se puede editar
            [Estados.BORRADOR] = new List<string> { Roles.SOLICITANTE, Roles.ADMINISTRADOR },
            
            // Otros estados NO se pueden editar (solo cambiar estado)
            [Estados.GENERADA] = new List<string>(),
            [Estados.ENVIADA] = new List<string>(),
            [Estados.APROBADA] = new List<string>(),
            [Estados.PAGADA] = new List<string>(),
            [Estados.FACTURADA] = new List<string>(),
            [Estados.ANULADA] = new List<string>(),
            [Estados.RECHAZADA] = new List<string>()
        };

        private static readonly Dictionary<string, Dictionary<string, List<string>>> PermisosTransicion = 
            new Dictionary<string, Dictionary<string, List<string>>>
        {
            [Estados.BORRADOR] = new Dictionary<string, List<string>>
            {
                [Estados.GENERADA] = new List<string> { Roles.SOLICITANTE, Roles.ADMINISTRADOR },
                [Estados.ANULADA] = new List<string> { Roles.SOLICITANTE, Roles.ADMINISTRADOR }
            },
            [Estados.GENERADA] = new Dictionary<string, List<string>>
            {
                [Estados.ENVIADA] = new List<string> { Roles.SOLICITANTE, Roles.ADMINISTRADOR },
                [Estados.ANULADA] = new List<string> { Roles.SOLICITANTE, Roles.ADMINISTRADOR }
            },
            [Estados.ENVIADA] = new Dictionary<string, List<string>>
            {
                [Estados.APROBADA] = new List<string> { Roles.FINANCIERO, Roles.ADMINISTRADOR },
                [Estados.RECHAZADA] = new List<string> { Roles.FINANCIERO, Roles.ADMINISTRADOR },
                [Estados.PAGADA] = new List<string> { Roles.FINANCIERO, Roles.ADMINISTRADOR }
            },
            [Estados.APROBADA] = new Dictionary<string, List<string>>
            {
                [Estados.PAGADA] = new List<string> { Roles.FINANCIERO, Roles.ADMINISTRADOR }
            },
            [Estados.PAGADA] = new Dictionary<string, List<string>>
            {
                [Estados.FACTURADA] = new List<string> { Roles.FINANCIERO, Roles.ADMINISTRADOR }
            }
        };

        #endregion

        #region Métodos de Validación

        /// <summary>
        /// Valida si una transición de estado es permitida
        /// </summary>
        public static bool EsTransicionValida(string estadoActual, string estadoDestino)
        {
            if (string.IsNullOrWhiteSpace(estadoActual) || string.IsNullOrWhiteSpace(estadoDestino))
                return false;

            var estadoActualNorm = estadoActual.Trim().ToUpperInvariant();
            var estadoDestinoNorm = estadoDestino.Trim().ToUpperInvariant();

            if (!TransicionesPermitidas.ContainsKey(estadoActualNorm))
                return false;

            return TransicionesPermitidas[estadoActualNorm].Contains(estadoDestinoNorm);
        }

        /// <summary>
        /// Valida si un usuario tiene permisos para realizar una transición específica
        /// </summary>
        public static bool TienePermisosParaTransicion(string estadoActual, string estadoDestino, List<string> rolesUsuario)
        {
            if (rolesUsuario == null || !rolesUsuario.Any())
                return false;

            if (!EsTransicionValida(estadoActual, estadoDestino))
                return false;

            var estadoActualNorm = estadoActual.Trim().ToUpperInvariant();
            var estadoDestinoNorm = estadoDestino.Trim().ToUpperInvariant();

            if (!PermisosTransicion.ContainsKey(estadoActualNorm))
                return false;

            if (!PermisosTransicion[estadoActualNorm].ContainsKey(estadoDestinoNorm))
                return false;

            var rolesRequeridos = PermisosTransicion[estadoActualNorm][estadoDestinoNorm];
            return rolesUsuario.Any(rol => rolesRequeridos.Contains(rol));
        }

        /// <summary>
        /// Valida si un usuario puede editar una orden en su estado actual
        /// </summary>
        public static bool PuedeEditar(string estadoActual, List<string> rolesUsuario)
        {
            if (rolesUsuario == null || !rolesUsuario.Any())
                return false;

            var estadoNorm = estadoActual?.Trim().ToUpperInvariant() ?? "";

            if (!PermisosEdicion.ContainsKey(estadoNorm))
                return false;

            var rolesPermitidos = PermisosEdicion[estadoNorm];
            return rolesUsuario.Any(rol => rolesPermitidos.Contains(rol));
        }

        /// <summary>
        /// Obtiene las transiciones válidas desde un estado dado
        /// </summary>
        public static List<string> ObtenerTransicionesPermitidas(string estadoActual)
        {
            var estadoNorm = estadoActual?.Trim().ToUpperInvariant() ?? "";
            
            if (!TransicionesPermitidas.ContainsKey(estadoNorm))
                return new List<string>();

            return new List<string>(TransicionesPermitidas[estadoNorm]);
        }

        /// <summary>
        /// Obtiene las transiciones que un usuario específico puede ejecutar
        /// </summary>
        public static List<string> ObtenerTransicionesPermitidas(string estadoActual, List<string> rolesUsuario)
        {
            var todasLasTransiciones = ObtenerTransicionesPermitidas(estadoActual);
            var transicionesPermitidas = new List<string>();

            foreach (var transicion in todasLasTransiciones)
            {
                if (TienePermisosParaTransicion(estadoActual, transicion, rolesUsuario))
                {
                    transicionesPermitidas.Add(transicion);
                }
            }

            return transicionesPermitidas;
        }

        #endregion

        #region Validaciones de Negocio

        /// <summary>
        /// Valida las reglas de negocio antes de realizar una transición
        /// </summary>
        public static bool ValidarReglasNegocio(string estadoActual, string estadoDestino, 
            decimal totalOrden, bool tieneDetalles, out string mensaje)
        {
            mensaje = "";

            // BORRADOR → GENERADA: Debe tener detalle y total > 0
            if (estadoActual == Estados.BORRADOR && estadoDestino == Estados.GENERADA)
            {
                if (!tieneDetalles)
                {
                    mensaje = "No se puede generar una orden sin conceptos/detalles.";
                    return false;
                }

                if (totalOrden <= 0)
                {
                    mensaje = "No se puede generar una orden con total menor o igual a cero.";
                    return false;
                }
            }

            // ENVIADA → RECHAZADA: Requiere observación (se valida en el controller)
            if (estadoActual == Estados.ENVIADA && estadoDestino == Estados.RECHAZADA)
            {
                // La validación de observación se hace en el controller
            }

            return true;
        }

        /// <summary>
        /// Valida si una orden puede ser anulada
        /// </summary>
        public static bool PuedeSerAnulada(string estadoActual, out string mensaje)
        {
            mensaje = "";

            // No se puede anular si ya está pagada o facturada
            if (estadoActual == Estados.PAGADA)
            {
                mensaje = "No se puede anular una orden que ya fue pagada.";
                return false;
            }

            if (estadoActual == Estados.FACTURADA)
            {
                mensaje = "No se puede anular una orden que ya fue facturada.";
                return false;
            }

            if (estadoActual == Estados.ANULADA)
            {
                mensaje = "La orden ya está anulada.";
                return false;
            }

            return true;
        }

        #endregion

        #region Métodos de Apoyo

        /// <summary>
        /// Normaliza un estado para comparaciones
        /// </summary>
        public static string NormalizarEstado(string estado)
        {
            return estado?.Trim().ToUpperInvariant() ?? "";
        }

        /// <summary>
        /// Verifica si un estado es válido
        /// </summary>
        public static bool EsEstadoValido(string estado)
        {
            var estadoNorm = NormalizarEstado(estado);
            return Estados.TodosLosEstados.Contains(estadoNorm);
        }

        /// <summary>
        /// Obtiene el color CSS para un estado (para UI)
        /// </summary>
        public static string ObtenerColorEstado(string estado)
        {
            switch (NormalizarEstado(estado))
            {
                case Estados.BORRADOR: return "secondary";
                case Estados.GENERADA: return "info";
                case Estados.ENVIADA: return "warning";
                case Estados.APROBADA: return "success";
                case Estados.PAGADA: return "primary";
                case Estados.FACTURADA: return "success";
                case Estados.ANULADA: return "danger";
                case Estados.RECHAZADA: return "danger";
                default: return "light";
            }
        }

        #endregion
    }
}