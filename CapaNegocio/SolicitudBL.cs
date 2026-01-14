using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio
{
    public class SolicitudBL
    {
        private readonly SolicitudAOCRDAO _solicitudDAO;
        private readonly HistorialEstadoDAO _historialDAO;
        private readonly DocumentoDAO _documentoDAO;

        private static readonly HashSet<string> EstadosPermitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PENDIENTE",
            "EN_REVISION",
            "OBSERVADO",
            "APROBADO",
            "RECHAZADO",
            "ANULADO"
        };

        public SolicitudBL()
        {
            _solicitudDAO = new SolicitudAOCRDAO();
            _historialDAO = new HistorialEstadoDAO();
            _documentoDAO = new DocumentoDAO();
        }

        #region Consultas y Listados

        public List<SolicitudAOCR> ObtenerTodasActivas()
            => _solicitudDAO.ListarActivas();

        public List<SolicitudAOCR> ObtenerTodos()
            => _solicitudDAO.ObtenerTodos();

        public List<SolicitudAOCR> ObtenerPorUsuario(int codigoUsuario)
        {
            if (codigoUsuario <= 0) return new List<SolicitudAOCR>();
            return _solicitudDAO.ObtenerPorUsuario(codigoUsuario);
        }

        public SolicitudAOCR ObtenerDetalle(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0) return null;
            return _solicitudDAO.ObtenerPorId(codigoSolicitud);
        }

        #endregion

        #region Historial

        /// <summary>
        /// Firma REAL en tu DAO: RegistrarCambio(int codigoSolicitud, string anterior, string nuevo, int usuario, string obs)
        /// </summary>
        private void RegistrarHistorial(int codigoSolicitud, string estadoAnterior, string estadoNuevo, int codigoUsuario, string observacion)
        {
            _historialDAO.RegistrarCambio(
                codigoSolicitud,                 // ✅ int
                estadoAnterior,
                estadoNuevo,
                codigoUsuario,                   // ✅ int
                observacion ?? ""
            );
        }

        #endregion

        #region Creación / Guardado Integral

        /// <summary>
        /// Compatibilidad con Controller: crea una solicitud básica.
        /// </summary>
        public bool Crear(SolicitudAOCR solicitud, int usuarioId, out string mensaje)
        {
            int id = GuardarSolicitudCompleta(solicitud, null, usuarioId, out mensaje);
            return id > 0;
        }

        /// <summary>
        /// Guarda solicitud + aeronaves + historial dentro de una transacción.
        /// </summary>
        public int GuardarSolicitudCompleta(SolicitudAOCR modelo, List<Aeronave> aeronaves, int usuarioId, out string mensaje)
        {
            mensaje = "";

            if (usuarioId <= 0)
            {
                mensaje = "Usuario inválido.";
                return 0;
            }

            var (okSol, errSol) = ValidarSolicitud(modelo);
            if (!okSol)
            {
                mensaje = errSol;
                return 0;
            }

            var (okAer, errAer) = ValidarAeronaves(aeronaves);
            if (!okAer)
            {
                mensaje = errAer;
                return 0;
            }

            var txOptions = new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromSeconds(60)
            };

            using (var scope = new TransactionScope(TransactionScopeOption.Required, txOptions))
            {
                try
                {
                    // Valores controlados por servidor (seguridad / integridad)
                    modelo.CodigoUsuario = usuarioId;
                    modelo.FechaSolicitud = DateTime.Now;
                    modelo.Estado = "PENDIENTE";

                    if (string.IsNullOrWhiteSpace(modelo.NumeroSolicitud))
                        modelo.NumeroSolicitud = GenerarNumeroSolicitudSeguro(DateTime.Now.Year);

                    // 1) Cabecera
                    int idSolicitud = _solicitudDAO.InsertarConReturn(modelo);
                    if (idSolicitud <= 0)
                    {
                        mensaje = "No se pudo registrar la solicitud.";
                        return 0;
                    }

                    // 2) Detalle aeronaves (DAO estático según tu proyecto)
                    if (aeronaves != null && aeronaves.Any())
                    {
                        foreach (var nave in aeronaves)
                        {
                            if (nave == null) continue;
                            nave.CodigoSolicitud = idSolicitud;
                            AeronaveDAO.Insertar(nave);
                        }
                    }

                    // 3) Historial inicial
                    RegistrarHistorial(idSolicitud, null, "PENDIENTE", usuarioId, "Registro inicial del trámite.");

                    scope.Complete();
                    mensaje = "Trámite registrado con éxito.";
                    return idSolicitud;
                }
                catch (Exception ex)
                {
                    mensaje = "Error al registrar (transacción revertida): " + ex.Message;
                    return 0;
                }
            }
        }

        #endregion

        #region Actualización / Permisos

        public bool Actualizar(SolicitudAOCR modelo, int codigoUsuario, out string mensaje, bool esAdmin = false)
        {
            mensaje = "";

            if (codigoUsuario <= 0) { mensaje = "Usuario inválido."; return false; }
            if (modelo == null || modelo.CodigoSolicitud <= 0) { mensaje = "Datos inválidos."; return false; }

            try
            {
                var actual = _solicitudDAO.ObtenerPorId(modelo.CodigoSolicitud);
                if (actual == null) { mensaje = "Solicitud no encontrada."; return false; }

                // Seguridad: propietario o admin
                if (!esAdmin && actual.CodigoUsuario != codigoUsuario)
                {
                    mensaje = "No tiene permisos para modificar este registro.";
                    return false;
                }

                // Blindaje: no permitir cambio de dueño / número desde UI
                modelo.CodigoUsuario = actual.CodigoUsuario;
                if (!string.IsNullOrWhiteSpace(actual.NumeroSolicitud))
                    modelo.NumeroSolicitud = actual.NumeroSolicitud;

                // Auditoría
                modelo.UpdatedAt = DateTime.Now;
                modelo.UpdatedBy = codigoUsuario.ToString();

                bool ok = _solicitudDAO.ActualizarGeneral(modelo);
                mensaje = ok ? "Datos actualizados correctamente." : "No se pudo actualizar el registro.";
                return ok;
            }
            catch (Exception ex)
            {
                mensaje = "Error al actualizar: " + ex.Message;
                return false;
            }
        }

        #endregion

        #region Cambio de estado

        public bool CambiarEstado(int id, string nuevoEstado, int usuarioId, string observaciones, out string mensaje)
        {
            mensaje = "";

            if (id <= 0) { mensaje = "ID inválido."; return false; }
            if (usuarioId <= 0) { mensaje = "Usuario inválido."; return false; }

            nuevoEstado = (nuevoEstado ?? "").Trim().ToUpperInvariant();
            if (!EstadosPermitidos.Contains(nuevoEstado))
            {
                mensaje = "Estado no permitido.";
                return false;
            }

            try
            {
                // Si tu DAO devuelve bool, está OK
                bool ok = _solicitudDAO.CambiarEstado(id, nuevoEstado, usuarioId, observaciones ?? "");
                if (!ok)
                {
                    mensaje = "No se pudo cambiar el estado.";
                    return false;
                }

                // Historial (si quieres estadoAnterior, puedes consultarlo antes)
                RegistrarHistorial(id, null, nuevoEstado, usuarioId, observaciones);

                mensaje = "Estado actualizado.";
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "Error al cambiar estado: " + ex.Message;
                return false;
            }
        }

        #endregion

        #region Validaciones / Apoyo

        private (bool ok, string error) ValidarSolicitud(SolicitudAOCR modelo)
        {
            if (modelo == null) return (false, "Datos inválidos.");

            if (string.IsNullOrWhiteSpace(modelo.TipoSolicitud))
                return (false, "Debe seleccionar el tipo de solicitud.");

            if (string.IsNullOrWhiteSpace(modelo.TipoOperacion))
                return (false, "Debe seleccionar el tipo de operación.");

            if (!string.IsNullOrWhiteSpace(modelo.Estado) && !EstadosPermitidos.Contains(modelo.Estado))
                return (false, "Estado inválido.");

            return (true, "");
        }

        private (bool ok, string error) ValidarAeronaves(List<Aeronave> aeronaves)
        {
            if (aeronaves == null) return (true, "");

            if (aeronaves.Count > 50)
                return (false, "Demasiadas aeronaves (límite 50).");

            foreach (var a in aeronaves)
            {
                if (a == null) return (false, "Lista de aeronaves inválida.");
                if (string.IsNullOrWhiteSpace(a.Matricula))
                    return (false, "Toda aeronave debe tener matrícula.");
            }

            return (true, "");
        }

        public string GenerarNumeroSolicitudSeguro(int year)
        {
            // Producción real: se recomienda SEQUENCE/tabla correlativos + bloqueo
            var total = _solicitudDAO.ObtenerTodos().Count(s => s.FechaSolicitud.Year == year);
            return $"AOCR-{year}-{(total + 1):D5}";
        }

        #endregion
    }
}
