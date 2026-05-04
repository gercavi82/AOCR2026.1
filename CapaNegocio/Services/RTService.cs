using System;
using System.IO;
using System.Web;
using CapaDatos.DAOs;
using CapaModelo.RT;
using CapaModelo.RT.ViewModels;
using CapaNegocio.Helpers;

namespace CapaNegocio.Services
{
    public class RTService
    {
        private readonly RTDao _rtDao = new RTDao();
        private readonly DocumentoRTDao _docDao = new DocumentoRTDao();

        public const string EstadoBorrador = "BORRADOR";
        public const string EstadoEnviadoLegacy = "ENVIADA";
        public const string EstadoEnviado = "ENVIADO";
        public const string EstadoEnRevisionCoordinador = "EN_REVISION_COORDINADOR";
        public const string EstadoDevueltoConObservaciones = "DEVUELTO_CON_OBSERVACIONES";
        public const string EstadoAprobado = "APROBADO";
        public const string EstadoFirmado = "FIRMADO";
        public const string EstadoFinalizado = "FINALIZADO";
        public const string TipoDocumentoDesignacion = "DESIGNACION_RT";

        public string ObtenerTextoDeclaracion()
        {
            return "Yo, ______________________ declaro conocer las políticas y procedimientos técnicos y operativos " +
                   "de la compañía __________ aplicables en las estaciones regulares de Ecuador.\n\n" +
                   "Asumo la responsabilidad como RT de mantener comunicación directa con la DGAC del Ecuador, a fin de gestionar " +
                   "los trámites de emisión, renovación o modificación del AOCR; así como también, de mantener la supervisión de " +
                   "las empresas contratadas para la asistencia técnica en tierra a sus aeronaves en los aeropuertos de Ecuador.";
        }

        public string ObtenerTextoDeclaracionPersonalizado(string nombreCompleto, string compania)
        {
            var nombre = string.IsNullOrWhiteSpace(nombreCompleto) ? "______________________" : nombreCompleto.Trim();
            var empresa = string.IsNullOrWhiteSpace(compania) ? "__________" : compania.Trim();
            return "Yo, " + nombre + " declaro conocer las políticas y procedimientos técnicos y operativos " +
                   "de la compañía " + empresa + " aplicables en las estaciones regulares de Ecuador.\n\n" +
                   "Asumo la responsabilidad como RT de mantener comunicación directa con la DGAC del Ecuador, a fin de gestionar " +
                   "los trámites de emisión, renovación o modificación del AOCR; así como también, de mantener la supervisión de " +
                   "las empresas contratadas para la asistencia técnica en tierra a sus aeronaves en los aeropuertos de Ecuador.";
        }

        public SolicitudRTModel GetSolicitudByUsuario(int usuarioId)
        {
            return _rtDao.GetSolicitudByUsuario(usuarioId);
        }

        public CompaniaModel GetCompaniaById(int companiaId)
        {
            return _rtDao.GetCompaniaById(companiaId);
        }

        public DocumentoModel GetDocumentoDesignacion(int solicitudId)
        {
            return _docDao.GetDocumentoDesignacion(solicitudId);
        }

        public bool EsEstadoEditable(string estado)
        {
            return string.Equals(NormalizarEstado(estado), EstadoBorrador, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizarEstado(estado), EstadoDevueltoConObservaciones, StringComparison.OrdinalIgnoreCase);
        }

        public string NormalizarEstado(string estado)
        {
            var estadoNormalizado = (estado ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(estadoNormalizado))
            {
                return EstadoBorrador;
            }

            if (estadoNormalizado == EstadoEnviadoLegacy)
            {
                return EstadoEnviado;
            }

            return estadoNormalizado;
        }

        public int GuardarBorrador(RegistroRTVM vm, int usuarioId)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));

            var solicitud = _rtDao.GetSolicitudByUsuario(usuarioId);
            var compania = solicitud != null ? _rtDao.GetCompaniaById(solicitud.CompaniaId) : null;

            if (_rtDao.ExisteRuc(vm.Ruc, compania?.Id))
                throw new InvalidOperationException("El RUC ya está registrado.");

            if (_rtDao.ExisteEmail(vm.Email, compania?.Id))
                throw new InvalidOperationException("El email ya está registrado.");

            if (solicitud == null)
            {
                var solicitudId = _rtDao.CreateCompaniaYSolicitudBorrador(
                    usuarioId,
                    new CompaniaModel
                    {
                        RazonSocial = vm.RazonSocial?.Trim(),
                        Ruc = vm.Ruc?.Trim(),
                        Telefono = vm.Telefono?.Trim(),
                        EmailContacto = vm.Email?.Trim(),
                        AreaContableJson = vm.AreaContableJson
                    },
                    ObtenerTextoDeclaracion());
                _rtDao.InsertHistorialEstado(solicitudId, EstadoBorrador, usuarioId, null);
                return solicitudId;
            }

            if (!EsEstadoEditable(solicitud.Estado))
                throw new InvalidOperationException("No se puede modificar una solicitud enviada.");

            _rtDao.UpdateCompania(solicitud.CompaniaId, new CompaniaModel
            {
                RazonSocial = vm.RazonSocial?.Trim(),
                Ruc = vm.Ruc?.Trim(),
                Telefono = vm.Telefono?.Trim(),
                EmailContacto = vm.Email?.Trim(),
                AreaContableJson = vm.AreaContableJson
            });

            return solicitud.Id;
        }

        public void AceptarDeclaracion(int solicitudId, int usuarioId, string textoDeclaracion = null)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            ValidarPropietario(solicitud, usuarioId);

            if (!EsEstadoEditable(solicitud.Estado))
                throw new InvalidOperationException("La solicitud no está en un estado editable.");

            _rtDao.UpdateDeclaracionAceptada(solicitudId, true, textoDeclaracion);
        }

        public DocumentoModel SubirDesignacionPdf(int solicitudId, int usuarioId, HttpPostedFileBase pdf)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            ValidarPropietario(solicitud, usuarioId);

            if (!EsEstadoEditable(solicitud.Estado))
                throw new InvalidOperationException("La solicitud no está en un estado editable.");

            if (!FileStorageHelper.ValidatePdf(pdf, out var error))
                throw new InvalidOperationException(error);

            var folder = $"RT/Designaciones/{solicitudId}";
            var ruta = FileStorageHelper.SavePdf(pdf, folder);
            var fullPath = HttpContext.Current.Server.MapPath(ruta);
            var hash = FileStorageHelper.ComputeSha256(fullPath);

            var doc = new DocumentoModel
            {
                SolicitudRtId = solicitudId,
                Tipo = TipoDocumentoDesignacion,
                NombreArchivo = Path.GetFileName(pdf.FileName),
                RutaStorage = ruta,
                TamanoBytes = pdf.ContentLength,
                HashSha256 = hash,
                CreatedBy = usuarioId.ToString(),
                CreatedAt = DateTime.Now
            };

            _docDao.UpsertDocumentoDesignacion(solicitudId, doc);
            UsuarioDAO.ActualizarDesignacionRT(usuarioId, ruta);
            return doc;
        }

        public DocumentoModel RegistrarDesignacionExistente(int solicitudId, int usuarioId, string rutaStorage, string nombreArchivo)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            ValidarPropietario(solicitud, usuarioId);

            if (!EsEstadoEditable(solicitud.Estado)
                && !string.Equals(NormalizarEstado(solicitud.Estado), EstadoEnviado, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(NormalizarEstado(solicitud.Estado), EstadoEnRevisionCoordinador, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La solicitud no admite actualización del documento en el estado actual.");
            }

            if (string.IsNullOrWhiteSpace(rutaStorage))
            {
                throw new InvalidOperationException("La ruta del documento RT es obligatoria.");
            }

            var rutaFisica = HttpContext.Current != null
                ? HttpContext.Current.Server.MapPath(rutaStorage)
                : null;

            if (string.IsNullOrWhiteSpace(rutaFisica) || !File.Exists(rutaFisica))
            {
                throw new InvalidOperationException("No se encontró el documento RT en el almacenamiento configurado.");
            }

            var info = new FileInfo(rutaFisica);
            var doc = new DocumentoModel
            {
                SolicitudRtId = solicitudId,
                Tipo = TipoDocumentoDesignacion,
                NombreArchivo = string.IsNullOrWhiteSpace(nombreArchivo) ? info.Name : nombreArchivo.Trim(),
                RutaStorage = rutaStorage,
                TamanoBytes = info.Length,
                HashSha256 = FileStorageHelper.ComputeSha256(rutaFisica),
                CreatedBy = usuarioId.ToString(),
                CreatedAt = DateTime.Now
            };

            _docDao.UpsertDocumentoDesignacion(solicitudId, doc);
            return doc;
        }

        public void EnviarSolicitud(int solicitudId, int usuarioId)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            ValidarPropietario(solicitud, usuarioId);

            if (!EsEstadoEditable(solicitud.Estado))
                throw new InvalidOperationException("La solicitud ya fue enviada o no está en un estado editable.");

            if (!solicitud.DeclaracionAceptada)
                throw new InvalidOperationException("Debe aceptar la declaración de responsabilidad antes de enviar.");

            var doc = _docDao.GetDocumentoDesignacion(solicitudId);
            if (doc == null)
                throw new InvalidOperationException("Debe adjuntar la Designación de RT legalizada (PDF)." );

            var fechaEnvio = DateTime.Now;
            _rtDao.UpdateEstado(solicitudId, EstadoEnRevisionCoordinador, null, fechaEnvio);
            _rtDao.InsertHistorialEstado(solicitudId, EstadoEnviado, usuarioId, null);
            _rtDao.InsertHistorialEstado(solicitudId, EstadoEnRevisionCoordinador, usuarioId, null);
        }

        public void DevolverConObservaciones(int solicitudId, int usuarioId, string observacion)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            if (solicitud == null)
                throw new InvalidOperationException("Solicitud no encontrada.");

            var observacionNormalizada = (observacion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(observacionNormalizada))
                throw new InvalidOperationException("Debe registrar una observación para devolver la solicitud RT.");

            _rtDao.UpdateEstado(solicitudId, EstadoDevueltoConObservaciones, observacionNormalizada);
            _rtDao.InsertHistorialEstado(solicitudId, EstadoDevueltoConObservaciones, usuarioId, observacionNormalizada);
        }

        public void RegistrarAprobacionFinal(int solicitudId, int usuarioId, string observacion = null)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            if (solicitud == null)
                throw new InvalidOperationException("Solicitud no encontrada.");

            var observacionNormalizada = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim();

            _rtDao.UpdateEstado(solicitudId, EstadoAprobado, observacionNormalizada);
            _rtDao.InsertHistorialEstado(solicitudId, EstadoAprobado, usuarioId, observacionNormalizada);

            _rtDao.UpdateEstado(solicitudId, EstadoFirmado, observacionNormalizada);
            _rtDao.InsertHistorialEstado(solicitudId, EstadoFirmado, usuarioId, observacionNormalizada);

            _rtDao.UpdateEstado(solicitudId, EstadoFinalizado, observacionNormalizada);
            _rtDao.InsertHistorialEstado(solicitudId, EstadoFinalizado, usuarioId, observacionNormalizada);
        }

        private static void ValidarPropietario(SolicitudRTModel solicitud, int usuarioId)
        {
            if (solicitud == null)
                throw new InvalidOperationException("Solicitud no encontrada.");

            if (solicitud.UsuarioRtId != usuarioId)
                throw new UnauthorizedAccessException("No tiene acceso a esta solicitud.");
        }
    }
}
