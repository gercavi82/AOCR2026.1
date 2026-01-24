using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Repositories;
using CapaModelo;

namespace CapaNegocio.Services
{
    public class TramiteService
    {
        private readonly ISolicitudRepository _solicitudRepository;
        private readonly IDocumentoRepository _documentoRepository;

        public TramiteService()
        {
            _solicitudRepository = new SolicitudRepository();
            _documentoRepository = new DocumentoRepository();
        }

        // 1. Agregar documento
        public ResultadoOperacion AgregarDocumento(int codigoSolicitud, Documento documento, string usuario)
        {
            try
            {
                if (documento == null)
                    return ResultadoOperacion.Error("El documento no puede ser nulo.");

                var solicitud = _solicitudRepository.ObtenerPorId(codigoSolicitud);
                if (solicitud == null)
                    return ResultadoOperacion.Error("Solicitud no encontrada.");

                documento.CodigoSolicitud = codigoSolicitud;
                documento.FechaCarga = DateTime.Now;
                documento.Estado = "PENDIENTE";
                documento.Validado = false;
                documento.UsuarioRegistro = usuario;
                documento.Version = 1;

                int codigoDocumento = _documentoRepository.Crear(documento);

                if (codigoDocumento > 0)
                {
                    return ResultadoOperacion.Ok(new { Codigo = codigoDocumento }, "Documento cargado exitosamente.");
                }

                return ResultadoOperacion.Error("Error al persistir el documento en la base de datos.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al agregar documento: " + ex.Message);
            }
        }

        // 2. Subsanar documentos (Flujo de Corrección DGAC)
        public ResultadoOperacion SubsanarDocumentos(int codigoSolicitud, List<Documento> documentos, string usuario, string observaciones)
        {
            try
            {
                var solicitud = _solicitudRepository.ObtenerPorId(codigoSolicitud);
                if (solicitud == null) return ResultadoOperacion.Error("Solicitud no encontrada.");

                if (solicitud.Estado != "SUBSANACION")
                    return ResultadoOperacion.Error("La solicitud no se encuentra en fase de subsanación.");

                if (documentos == null || !documentos.Any())
                    return ResultadoOperacion.Error("Debe cargar al menos un archivo para subsanar.");

                foreach (var doc in documentos)
                {
                    doc.CodigoSolicitud = codigoSolicitud;
                    doc.FechaCarga = DateTime.Now;
                    doc.Estado = "SUBSANACION";
                    doc.UsuarioRegistro = usuario;
                    _documentoRepository.Crear(doc);
                }

                solicitud.Estado = "EN_REVISION_DOCUMENTAL";
                solicitud.FechaSubsanacion = DateTime.Now;
                _solicitudRepository.Actualizar(solicitud);

                return ResultadoOperacion.Ok(null, "Documentos de subsanación enviados a revisión.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error en proceso de subsanación: " + ex.Message);
            }
        }

        // 3. Validar un documento individualmente
        public ResultadoOperacion ValidarDocumento(int codigoDocumento, bool esValido, string obs, string usuario)
        {
            try
            {
                bool resultado = _documentoRepository.ValidarDocumento(codigoDocumento, esValido, obs, usuario);

                if (resultado)
                {
                    string msg = esValido ? "Documento aprobado." : "Documento rechazado para subsanación.";
                    return ResultadoOperacion.Ok(null, msg);
                }
                return ResultadoOperacion.Error("No se pudo actualizar el estado de validación.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al validar: " + ex.Message);
            }
        }

        // 4. Consultas de documentos
        public ResultadoOperacion ObtenerDocumentosPorSolicitud(int codigoSolicitud)
        {
            try
            {
                var lista = _documentoRepository.ObtenerPorSolicitud(codigoSolicitud);
                return ResultadoOperacion.Ok(lista, "Lista de documentos obtenida.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al consultar documentos: " + ex.Message);
            }
        }

        // 5. Verificación de flujo completo
        public ResultadoOperacion VerificarCierreRevision(int codigoSolicitud)
        {
            try
            {
                var documentos = _documentoRepository.ObtenerPorSolicitud(codigoSolicitud);

                if (documentos == null || documentos.Count == 0)
                    return ResultadoOperacion.Ok(new { Listo = false }, "No hay documentos cargados.");

                // ✅ bool? -> bool (null se considera false)
                bool todosValidados = documentos.All(d => d.Validado == true);

                // ✅ defensivo: evitar null en Estado
                bool tieneRechazados = documentos.Any(d => string.Equals(d.Estado, "RECHAZADO", StringComparison.OrdinalIgnoreCase));

                return ResultadoOperacion.Ok(new
                {
                    Finalizado = todosValidados,
                    RequiereSubsanacion = tieneRechazados
                }, "Análisis de estado completado.");
            }
            catch (Exception ex)
            {
                return ResultadoOperacion.Error("Error al verificar estado: " + ex.Message);
            }
        }

    }
}
