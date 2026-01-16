using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapaModelo;
using CapaDatos.DAOs;

namespace CapaNegocio
{
    public class DocumentoBL
    {
        private readonly DocumentoDAO _documentoDAO;
        private readonly SolicitudAOCRDAO _solicitudAOCRDAO;

        // Configuraciones de validación
        private readonly string[] _extensionesPermitidas =
            { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".zip", ".rar" };

        private const long TAMANIO_MAXIMO = 10 * 1024 * 1024; // 10MB

        public DocumentoBL()
        {
            _documentoDAO = new DocumentoDAO();
            _solicitudAOCRDAO = new SolicitudAOCRDAO();
        }

        #region CRUD Principal

        // ✅ Si tu DocumentoDAO NO tiene ObtenerTodos(), elimina este método o créalo en DAO.
        // (Te lo dejo pero comentado para que no te rompa compilación si no existe)
        /*
        public List<Documento> ObtenerTodos()
        {
            return _documentoDAO.ObtenerTodos();
        }
        */

        public Documento ObtenerPorId(int id)
        {
            if (id <= 0) throw new ArgumentException("ID de documento inválido");
            return _documentoDAO.ObtenerPorId(id);
        }

        public List<Documento> ObtenerPorSolicitud(int solicitudId)
        {
            if (solicitudId <= 0) throw new ArgumentException("ID de solicitud inválido");
            return _documentoDAO.ObtenerPorSolicitud(solicitudId);
        }

        public bool Crear(Documento documento)
        {
            ValidarDocumento(documento);

            var solicitud = _solicitudAOCRDAO.ObtenerPorId(documento.CodigoSolicitud);
            if (solicitud == null)
                throw new Exception("La solicitud asociada no existe.");

            // Regla de Negocio: Solo permitir adjuntos en ciertos estados
            string[] estadosPermitidos = { "PENDIENTE", "EN_REVISION", "DOCUMENTOS_COMPLETOS", "BORRADOR" };
            if (!estadosPermitidos.Contains(solicitud.Estado))
                throw new Exception($"No se pueden agregar documentos. La solicitud está en estado: {solicitud.Estado}");

            // ✅ Ajuste a tu modelo real
            documento.FechaCarga = DateTime.Now;
            documento.Estado = "PENDIENTE";
            documento.Validado = false;

            return _documentoDAO.Crear(documento) > 0;
        }

        // ✅ Este método depende de que exista MarcarComoEliminado o similar en DAO
        // Si en tu DAO tienes MarcarComoEliminado, úsalo. Si tienes Eliminar(id), ajusta.
        public bool Eliminar(int id, string rutaFisicaOpcional = null, string usuario = "sistema")
        {
            var documento = _documentoDAO.ObtenerPorId(id);
            if (documento == null) throw new Exception("Documento no encontrado");

            // Borrado físico (si tú guardas una ruta física)
            // Importante: en DB tú guardas RutaGuardada (web), NO necesariamente ruta física.
            // Si quieres borrar físico: pásame la ruta física desde el Controller o arma el MapPath.
            var rutaFisica = rutaFisicaOpcional;

            if (!string.IsNullOrWhiteSpace(rutaFisica) && File.Exists(rutaFisica))
            {
                try { File.Delete(rutaFisica); }
                catch { /* Log IO si quieres */ }
            }

            // ✅ Borrado lógico por estado (recomendado)
            return _documentoDAO.MarcarComoEliminado(id, usuario);
        }

        #endregion

        #region Gestión de Flujo de Aprobación

        // ✅ Para aprobar/rechazar necesitas un método Actualizar(doc) en DocumentoDAO.
        // Si no lo tienes, dímelo y te lo implemento.
        public bool Aprobar(int documentoId, string usuario, string observaciones = null)
        {
            var documento = _documentoDAO.ObtenerPorId(documentoId);
            if (documento == null) throw new Exception("Documento no encontrado");

            documento.Estado = "APROBADO";
            documento.Observaciones = observaciones;
            documento.Validado = true;

            bool ok = _documentoDAO.Actualizar(documento);

            if (ok)
            {
                VerificarDocumentosCompletos(documento.CodigoSolicitud, usuario);
            }

            return ok;
        }

        public bool Rechazar(int documentoId, string usuario, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("Debe especificar el motivo del rechazo");

            var documento = _documentoDAO.ObtenerPorId(documentoId);
            if (documento == null) throw new Exception("Documento no encontrado");

            documento.Estado = "RECHAZADO";
            documento.Observaciones = motivo;
            documento.Validado = false;

            return _documentoDAO.Actualizar(documento);
        }

        #endregion

        #region Validaciones y Auxiliares

        private void ValidarDocumento(Documento d)
        {
            if (d == null) throw new Exception("Datos de documento nulos");
            if (d.CodigoSolicitud <= 0) throw new Exception("Código de solicitud no válido");
            if (string.IsNullOrWhiteSpace(d.NombreArchivo)) throw new Exception("El nombre del archivo es obligatorio");

            string ext = Path.GetExtension(d.NombreArchivo).ToLower();
            if (!_extensionesPermitidas.Contains(ext))
                throw new Exception($"Extensión {ext} no permitida. Use: " + string.Join(", ", _extensionesPermitidas));

            // ✅ Ajuste a tu modelo real: TamanoBytes
            if (d.TamanoBytes.HasValue && d.TamanoBytes.Value > TAMANIO_MAXIMO)
                throw new Exception("El archivo excede el límite de 10MB");
        }

        /// <summary>
        /// Cambia la solicitud a DOCUMENTOS_COMPLETOS si todos están aprobados.
        /// </summary>
        private void VerificarDocumentosCompletos(int solicitudId, string usuario)
        {
            var documentos = _documentoDAO.ObtenerPorSolicitud(solicitudId);

            // Regla: Si hay documentos y TODOS están aprobados
            if (documentos.Any() && documentos.All(d => d.Estado == "APROBADO"))
            {
                var solicitud = _solicitudAOCRDAO.ObtenerPorId(solicitudId);

                if (solicitud != null && solicitud.Estado == "EN_REVISION")
                {
                    // convertir usuario (string) a int
                    int idUsuarioReal;
                    if (!int.TryParse(usuario, out idUsuarioReal))
                        idUsuarioReal = 1; // sistema

                    _solicitudAOCRDAO.CambiarEstado(
                        solicitudId,
                        "DOCUMENTOS_COMPLETOS",
                        idUsuarioReal,
                        "Sistema: Todos los documentos aprobados."
                    );
                }
            }
        }
        public List<Documento> ObtenerTodos()
        {
            return _documentoDAO.ObtenerTodos();
        }


        #endregion
    }
}
