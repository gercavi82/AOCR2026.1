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
        public const string EstadoEnviada = "ENVIADA";
        public const string TipoDocumentoDesignacion = "DESIGNACION_RT";

        public string ObtenerTextoDeclaracion()
        {
            return "Yo, ______________________ declaro conocer las políticas y procedimientos técnicos y operativos " +
                   "de la compañía __________ aplicables en las estaciones regulares de Ecuador.\n\n" +
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

            if (!string.Equals(solicitud.Estado, EstadoBorrador, StringComparison.OrdinalIgnoreCase))
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

        public void AceptarDeclaracion(int solicitudId, int usuarioId)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            ValidarPropietario(solicitud, usuarioId);

            if (!string.Equals(solicitud.Estado, EstadoBorrador, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La solicitud no está en estado BORRADOR.");

            _rtDao.UpdateDeclaracionAceptada(solicitudId, true);
        }

        public DocumentoModel SubirDesignacionPdf(int solicitudId, int usuarioId, HttpPostedFileBase pdf)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            ValidarPropietario(solicitud, usuarioId);

            if (!string.Equals(solicitud.Estado, EstadoBorrador, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La solicitud no está en estado BORRADOR.");

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
            return doc;
        }

        public void EnviarSolicitud(int solicitudId, int usuarioId)
        {
            var solicitud = _rtDao.GetSolicitudById(solicitudId);
            ValidarPropietario(solicitud, usuarioId);

            if (!string.Equals(solicitud.Estado, EstadoBorrador, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La solicitud ya fue enviada o no está en estado BORRADOR.");

            if (!solicitud.DeclaracionAceptada)
                throw new InvalidOperationException("Debe aceptar la declaración de responsabilidad antes de enviar.");

            var doc = _docDao.GetDocumentoDesignacion(solicitudId);
            if (doc == null)
                throw new InvalidOperationException("Debe adjuntar la Designación de RT legalizada (PDF)." );

            _rtDao.UpdateEstadoEnviada(solicitudId, DateTime.Now);
            _rtDao.InsertHistorialEstado(solicitudId, EstadoEnviada, usuarioId, null);
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
