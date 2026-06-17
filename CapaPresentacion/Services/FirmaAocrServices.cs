using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using Rotativa;

namespace CapaPresentacion.Services
{
    public sealed class FirmaAocrContexto
    {
        public SolicitudAOCR Solicitud { get; set; }
        public Inspeccion Inspeccion { get; set; }
        public InspeccionInformeTecnico Informe { get; set; }
        public Certificado Certificado { get; set; }
        public AocrFirmaDocumento Firma { get; set; }
        public List<AeronaveSolicitud> Aeronaves { get; set; }
        public AocrDocumentoPdfViewModel Documento { get; set; }
        public List<string> CamposFaltantes { get; set; }
        public bool PdfExiste { get; set; }
        public long PdfBytes { get; set; }
        public bool PdfFirmadoExiste { get; set; }
        public long PdfFirmadoBytes { get; set; }
    }

    public sealed class FirmaAocrOperacionResultado
    {
        public bool Ok { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public int SolicitudId { get; set; }
        public string RutaPdf { get; set; }
        public string RutaPdfFirmado { get; set; }
        public string HashPdfFirmado { get; set; }
        public long Bytes { get; set; }
        public string EstadoAocr { get; set; }
        public string EstadoSolicitud { get; set; }
        public string UrlDescarga { get; set; }
        public string RedirectUrl { get; set; }
        public List<string> CamposFaltantes { get; set; }
        public bool PuedeGenerarPdf { get; set; }
        public bool PuedeFirmar { get; set; }
        public int CamposActualizados { get; set; }
    }

    public sealed class FirmaAocrValidationResult
    {
        public bool Ok { get; set; }
        public List<string> CamposFaltantes { get; set; }
        public bool PuedeGenerarPdf { get; set; }
        public bool PuedeFirmar { get; set; }
        public string Mensaje { get; set; }
    }

    public sealed class FirmaAocrValidationService
    {
        public FirmaAocrValidationResult ValidarDatosObligatorios(FirmaAocrContexto contexto)
        {
            var campos = contexto != null && contexto.CamposFaltantes != null
                ? contexto.CamposFaltantes
                : new List<string> { "contexto del documento AOCR" };
            var ok = campos.Count == 0;
            var informeAprobado = contexto != null && FirmaAocrWorkflowService.InformeAprobadoDireccion(contexto.Informe);
            var puedeGenerar = ok && informeAprobado && contexto != null && !contexto.PdfFirmadoExiste;
            var puedeFirmar = puedeGenerar && contexto.PdfExiste;

            Trace.TraceInformation(
                "[FIRMA_AOCR_V2][VALIDATION] SolicitudId=" + (contexto != null && contexto.Solicitud != null ? contexto.Solicitud.CodigoSolicitud : 0) +
                "; CamposFaltantes=" + (campos.Count > 0 ? string.Join("|", campos) : "ninguno") +
                "; PuedeGenerarPdf=" + puedeGenerar +
                "; PuedeFirmar=" + puedeFirmar);

            return new FirmaAocrValidationResult
            {
                Ok = ok,
                CamposFaltantes = campos,
                PuedeGenerarPdf = puedeGenerar,
                PuedeFirmar = puedeFirmar,
                Mensaje = ok ? "Datos AOCR completos." : "El AOCR tiene campos obligatorios incompletos."
            };
        }
    }

    public sealed class FirmaAocrAuthorizationService
    {
        private static readonly string[] RolesFirma =
        {
            "Direccion",
            "DireccionJefaturaTecnica",
            "DIRDAC",
            "JefaturaTecnica"
        };

        public bool UsuarioPuedeEntrar(IPrincipal user)
        {
            return RolesFirma.Any(rol => user != null && user.IsInRole(rol));
        }

        public string ObtenerRolActual(IPrincipal user)
        {
            return RolesFirma.FirstOrDefault(rol => user != null && user.IsInRole(rol)) ?? string.Empty;
        }
    }

    public sealed class FirmaAocrWorkflowService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly CertificadoDAO _certificadoDao = new CertificadoDAO();
        private readonly AeronaveSolicitudDAO _aeronaveDao = new AeronaveSolicitudDAO();
        private readonly AocrFirmaDocumentoDAO _firmaDao = new AocrFirmaDocumentoDAO();
        private readonly FirmaAocrAuthorizationService _authorizationService;
        private readonly FirmaAocrStorageService _storageService;

        public FirmaAocrWorkflowService(FirmaAocrAuthorizationService authorizationService, FirmaAocrStorageService storageService)
        {
            _authorizationService = authorizationService;
            _storageService = storageService;
        }

        public FirmaAocrContexto CargarContexto(int solicitudId)
        {
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return null;
            }

            var inspecciones = _inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>();
            var inspeccion = inspecciones
                .OrderByDescending(i => i != null ? i.FechaProgramada : null)
                .ThenByDescending(i => i != null ? i.CodigoInspeccion : 0)
                .FirstOrDefault();
            var informe = inspeccion != null ? _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion) : null;
            var certificado = _certificadoDao.ObtenerPorSolicitud(solicitudId);
            var firma = _firmaDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
            var aeronaves = _aeronaveDao.ObtenerPorSolicitud(solicitudId) ?? new List<AeronaveSolicitud>();
            var documento = ConstruirDocumentoPdfModel(solicitud, inspeccion, informe, certificado, aeronaves);

            var pdfFisico = certificado != null ? _storageService.ResolverRutaFisica(certificado.RutaDocumento) : null;
            var firmadoFisico = firma != null ? _storageService.ResolverRutaFisica(firma.RutaDocumento) : null;
            var pdfExiste = !string.IsNullOrWhiteSpace(pdfFisico) && File.Exists(pdfFisico);
            var firmadoExiste = !string.IsNullOrWhiteSpace(firmadoFisico) && File.Exists(firmadoFisico);

            return new FirmaAocrContexto
            {
                Solicitud = solicitud,
                Inspeccion = inspeccion,
                Informe = informe,
                Certificado = certificado,
                Firma = firma,
                Aeronaves = aeronaves,
                Documento = documento,
                CamposFaltantes = ObtenerCamposObligatoriosFaltantesAocrOficial(documento),
                PdfExiste = pdfExiste,
                PdfBytes = pdfExiste ? new FileInfo(pdfFisico).Length : 0,
                PdfFirmadoExiste = firmadoExiste,
                PdfFirmadoBytes = firmadoExiste ? new FileInfo(firmadoFisico).Length : 0
            };
        }

        public FirmaAocrInstitucionalViewModel ConstruirViewModel(int solicitudId, IPrincipal user, UrlHelper url)
        {
            var contexto = CargarContexto(solicitudId);
            if (contexto == null)
            {
                return new FirmaAocrInstitucionalViewModel
                {
                    SolicitudId = solicitudId,
                    MotivoBloqueo = "La solicitud AOCR indicada no existe.",
                    CamposFaltantes = new List<string>(),
                    UrlVolverBandeja = url.Action("PendientesDireccion", "Inspeccion")
                };
            }

            var autorizado = _authorizationService.UsuarioPuedeEntrar(user);
            var camposFaltantes = contexto.CamposFaltantes ?? new List<string>();
            var documentoCompleto = !camposFaltantes.Any();
            var informeAprobado = InformeAprobadoDireccion(contexto.Informe);
            var puedeFirmar = autorizado && contexto.PdfExiste && !contexto.PdfFirmadoExiste && documentoCompleto && informeAprobado;
            var motivo = ConstruirMotivoBloqueo(autorizado, contexto.PdfExiste, contexto.PdfFirmadoExiste, documentoCompleto, informeAprobado);
            var solicitud = contexto.Solicitud;
            var certificado = contexto.Certificado;
            var firma = contexto.Firma;

            return new FirmaAocrInstitucionalViewModel
            {
                SolicitudId = solicitudId,
                AocrId = certificado != null ? certificado.CodigoCertificado : 0,
                NumeroSolicitud = solicitud.NumeroSolicitud ?? solicitud.CodigoSolicitud.ToString(),
                Operadora = PrimerValorNoVacio(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial),
                CodigoAocr = PrimerValorNoVacio(contexto.Documento.NumeroAocr, certificado != null ? certificado.NumeroCertificado : null),
                EstadoSolicitud = solicitud.Estado,
                EstadoAocr = contexto.PdfFirmadoExiste ? "AOCR_FIRMADO_DIRDAC" : (contexto.PdfExiste ? "PDF_OFICIAL_GENERADO" : "PENDIENTE_GENERAR_PDF"),
                InformeTecnicoEstado = contexto.Informe != null ? contexto.Informe.EstadoInforme : "Sin informe",
                ResultadoTecnico = contexto.Informe != null ? contexto.Informe.Resultado : "Sin resultado",
                ResponsableFirma = "Direccion / DIRDAC",
                UsuarioActual = user != null && user.Identity != null ? user.Identity.Name : string.Empty,
                RolActual = _authorizationService.ObtenerRolActual(user),
                CargoFirmante = contexto.Documento.CargoFirmante,
                FechaGeneracion = certificado != null ? certificado.FechaEmision : null,
                FechaFirma = firma != null && firma.FechaFirma != DateTime.MinValue ? (DateTime?)firma.FechaFirma : null,
                NombreArchivoPdf = contexto.PdfExiste ? Path.GetFileName(_storageService.ResolverRutaFisica(certificado.RutaDocumento)) : null,
                NombreArchivoFirmado = contexto.PdfFirmadoExiste ? Path.GetFileName(_storageService.ResolverRutaFisica(firma.RutaDocumento)) : null,
                PdfExiste = contexto.PdfExiste,
                PdfFirmadoExiste = contexto.PdfFirmadoExiste,
                TamanioPdf = contexto.PdfBytes,
                TamanioPdfFirmado = contexto.PdfFirmadoBytes,
                HashPdfFirmado = firma != null ? firma.HashDocumento : null,
                RutaPdf = certificado != null ? certificado.RutaDocumento : null,
                RutaPdfFirmado = firma != null ? firma.RutaDocumento : null,
                PuedeGenerar = autorizado && documentoCompleto && informeAprobado && !contexto.PdfFirmadoExiste,
                PuedeRegenerar = autorizado && documentoCompleto && informeAprobado && !contexto.PdfFirmadoExiste,
                PuedeFirmar = puedeFirmar,
                InformeAprobado = informeAprobado,
                DocumentoCompleto = documentoCompleto,
                MotivoBloqueo = motivo,
                CamposFaltantes = camposFaltantes,
                EstadoExplotador = contexto.Documento != null ? contexto.Documento.EstadoExplotador : null,
                FechaVencimiento = contexto.Documento != null ? contexto.Documento.FechaVencimiento : null,
                FechaEmisionDocumento = contexto.Documento != null ? (DateTime?)contexto.Documento.FechaEmisionDocumento : null,
                AocOriginalNumero = contexto.Documento != null ? contexto.Documento.AocOriginalNumero : null,
                PermisoOperacionCnac = solicitud.NumeroAOC,
                CondicionBaseOperacion = contexto.Documento != null ? contexto.Documento.CondicionBaseOperacion : null,
                PuedeGuardarDatos = autorizado && !contexto.PdfFirmadoExiste,
                UrlGuardarDatos = url.Action("GuardarDatos", "FirmaAocr", new { solicitudId }),
                UrlGenerar = url.Action("GenerarPdf", "FirmaAocr", new { solicitudId }),
                UrlVerPdf = url.Action("VerPdf", "FirmaAocr", new { solicitudId, firmado = false }),
                UrlDescargarPdf = url.Action("DescargarPdf", "FirmaAocr", new { solicitudId, firmado = false }),
                UrlVerPdfFirmado = url.Action("VerPdf", "FirmaAocr", new { solicitudId, firmado = true }),
                UrlDescargarFirmado = url.Action("DescargarFirmado", "FirmaAocr", new { solicitudId }),
                UrlFirmar = url.Action("Firmar", "FirmaAocr", new { solicitudId }),
                UrlVolverBandeja = url.Action("PendientesDireccion", "Inspeccion"),
                UrlCompletarDatos = url.Action("Index", "FirmaAocr", new { solicitudId })
            };
        }

        public FirmaAocrOperacionResultado GuardarDatosObligatorios(int solicitudId, string estadoExplotador, DateTime? fechaVencimiento, int usuarioId, string usuarioNombre)
        {
            var contexto = CargarContexto(solicitudId);
            if (contexto == null || contexto.Solicitud == null)
            {
                return new FirmaAocrOperacionResultado
                {
                    Ok = false,
                    Code = 404,
                    Message = "La solicitud AOCR indicada no existe.",
                    SolicitudId = solicitudId
                };
            }

            var camposActualizados = 0;
            if (!string.Equals(contexto.Solicitud.Pais ?? string.Empty, estadoExplotador ?? string.Empty, StringComparison.Ordinal))
            {
                contexto.Solicitud.Pais = (estadoExplotador ?? string.Empty).Trim();
                contexto.Solicitud.UpdatedBy = !string.IsNullOrWhiteSpace(usuarioNombre) ? usuarioNombre : "sistema";
                _solicitudDao.Actualizar(contexto.Solicitud);
                camposActualizados++;
            }

            var certificado = contexto.Certificado ?? _certificadoDao.ObtenerPorSolicitud(solicitudId);
            if (certificado == null || certificado.CodigoCertificado <= 0)
            {
                certificado = new Certificado
                {
                    CodigoSolicitud = solicitudId,
                    NumeroCertificado = contexto.Documento != null ? contexto.Documento.NumeroAocr : null,
                    Tipo = "AOCR",
                    Estado = "GENERADO",
                    FechaEmision = contexto.Documento != null && contexto.Documento.FechaEmisionDocumento != default(DateTime) ? (DateTime?)contexto.Documento.FechaEmisionDocumento : DateTime.Now,
                    FechaVencimiento = fechaVencimiento,
                    EmitidoPor = usuarioNombre,
                    CreatedAt = DateTime.Now,
                    CreatedBy = usuarioId,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = usuarioId
                };
                certificado.CodigoCertificado = _certificadoDao.Crear(certificado);
                camposActualizados++;
            }
            else if (certificado.FechaVencimiento != fechaVencimiento)
            {
                certificado.FechaVencimiento = fechaVencimiento;
                certificado.FechaEmision = certificado.FechaEmision ?? DateTime.Now;
                certificado.UpdatedAt = DateTime.Now;
                certificado.UpdatedBy = usuarioId;
                _certificadoDao.Actualizar(certificado);
                camposActualizados++;
            }

            var contextoActualizado = CargarContexto(solicitudId);
            var validacion = new FirmaAocrValidationService().ValidarDatosObligatorios(contextoActualizado);
            Trace.TraceInformation(
                "[FIRMA_AOCR_V2][GUARDAR_DATOS_OK] SolicitudId=" + solicitudId +
                "; CamposActualizados=" + camposActualizados);

            return new FirmaAocrOperacionResultado
            {
                Ok = validacion.Ok,
                Code = validacion.Ok ? 200 : 400,
                Message = validacion.Ok ? "Datos AOCR guardados correctamente." : "El AOCR tiene campos obligatorios incompletos.",
                SolicitudId = solicitudId,
                EstadoAocr = validacion.Ok ? (contextoActualizado != null && contextoActualizado.PdfExiste ? "AOCR_PDF_GENERADO" : "AOCR_PENDIENTE_GENERAR_PDF") : "AOCR_DATOS_INCOMPLETOS",
                CamposFaltantes = validacion.CamposFaltantes,
                PuedeGenerarPdf = validacion.PuedeGenerarPdf,
                PuedeFirmar = validacion.PuedeFirmar,
                CamposActualizados = camposActualizados
            };
        }

        public void SincronizarCertificadoPdfOficial(FirmaAocrContexto contexto, string rutaRelativa, int usuarioId, string usuarioNombre)
        {
            if (contexto == null || contexto.Solicitud == null)
            {
                return;
            }

            var certificado = contexto.Certificado ?? _certificadoDao.ObtenerPorSolicitud(contexto.Solicitud.CodigoSolicitud);
            if (certificado == null || certificado.CodigoCertificado <= 0)
            {
                certificado = new Certificado
                {
                    CodigoSolicitud = contexto.Solicitud.CodigoSolicitud,
                    NumeroCertificado = contexto.Documento != null ? contexto.Documento.NumeroAocr : null,
                    Tipo = "AOCR",
                    Estado = "GENERADO",
                    FechaEmision = DateTime.Now,
                    FechaVencimiento = contexto.Documento != null ? contexto.Documento.FechaVencimiento : null,
                    RutaDocumento = rutaRelativa,
                    EmitidoPor = usuarioNombre,
                    CreatedAt = DateTime.Now,
                    CreatedBy = usuarioId,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = usuarioId
                };
                certificado.CodigoCertificado = _certificadoDao.Crear(certificado);
                contexto.Certificado = certificado;
                return;
            }

            certificado.NumeroCertificado = PrimerValorNoVacio(certificado.NumeroCertificado, contexto.Documento != null ? contexto.Documento.NumeroAocr : null);
            certificado.Estado = "GENERADO";
            certificado.FechaEmision = certificado.FechaEmision ?? DateTime.Now;
            certificado.FechaVencimiento = contexto.Documento != null ? contexto.Documento.FechaVencimiento : certificado.FechaVencimiento;
            certificado.RutaDocumento = rutaRelativa;
            certificado.EmitidoPor = PrimerValorNoVacio(certificado.EmitidoPor, usuarioNombre);
            certificado.UpdatedAt = DateTime.Now;
            certificado.UpdatedBy = usuarioId;
            _certificadoDao.Actualizar(certificado);
            contexto.Certificado = certificado;
        }

        public static bool InformeAprobadoDireccion(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return false;
            }

            var estado = (informe.EstadoInforme ?? string.Empty).Trim().ToUpperInvariant();
            var resultado = (informe.Resultado ?? string.Empty).Trim().ToUpperInvariant();
            return estado.Contains("APROB") || resultado.Contains("APROB") || resultado.Contains("FAVORABLE");
        }

        public static string PrimerValorNoVacio(params string[] valores)
        {
            foreach (var valor in valores)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                {
                    return valor.Trim();
                }
            }

            return null;
        }

        private static string ConstruirMotivoBloqueo(bool autorizado, bool pdfExiste, bool firmado, bool completo, bool informeAprobado)
        {
            if (!autorizado)
            {
                return "Solo Direccion, DireccionJefaturaTecnica, DIRDAC o JefaturaTecnica pueden firmar el AOCR.";
            }

            if (firmado)
            {
                return "El AOCR ya fue firmado oficialmente.";
            }

            if (!informeAprobado)
            {
                return "El informe tecnico no esta aprobado por Direccion.";
            }

            if (!completo)
            {
                return "El AOCR tiene campos obligatorios incompletos.";
            }

            if (!pdfExiste)
            {
                return "Primero genere el PDF oficial AOCR.";
            }

            return null;
        }

        private static AocrDocumentoPdfViewModel ConstruirDocumentoPdfModel(SolicitudAOCR solicitud, Inspeccion inspeccion, InspeccionInformeTecnico informe, Certificado certificado, List<AeronaveSolicitud> aeronaves)
        {
            var operador = solicitud != null ? PrimerValorNoVacio(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial) : null;
            var condicionBase = solicitud != null
                ? string.Join(" / ", new[] { solicitud.TipoOperacion, solicitud.AeropuertosEcuador, solicitud.AeropuertosEcuadorOtros }.Where(x => !string.IsNullOrWhiteSpace(x)))
                : null;

            return new AocrDocumentoPdfViewModel
            {
                Solicitud = solicitud,
                Inspeccion = inspeccion,
                Informe = informe,
                Certificado = certificado,
                Aeronaves = aeronaves ?? new List<AeronaveSolicitud>(),
                NumeroAocr = PrimerValorNoVacio(certificado != null ? certificado.NumeroCertificado : null, solicitud != null ? solicitud.NumeroSolicitud : null, solicitud != null ? "AOCR-" + solicitud.CodigoSolicitud : null),
                FirmanteFinal = PrimerValorNoVacio(certificado != null ? certificado.AprobadoPor : null, certificado != null ? certificado.EmitidoPor : null, informe != null ? informe.UsuarioFirma2 : null),
                CargoFirmante = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.CargoDirector) ? solicitud.CargoDirector : "Direccion General de Aviacion Civil",
                FechaEmisionDocumento = certificado != null && certificado.FechaEmision.HasValue ? certificado.FechaEmision.Value : DateTime.Now,
                FechaExpedicion = certificado != null ? certificado.FechaEmision : null,
                FechaVencimiento = certificado != null ? certificado.FechaVencimiento : (solicitud != null ? solicitud.FechaFinOperacion : null),
                AocOriginalNumero = solicitud != null ? solicitud.NumeroAOC : null,
                EstadoOtorgante = "Estado del Operador",
                NombreExplotador = operador,
                EstadoExplotador = solicitud != null ? solicitud.Pais : null,
                RazonSocial = solicitud != null ? solicitud.RazonSocial : null,
                DireccionExplotador = solicitud != null ? solicitud.Direccion : null,
                TelefonoExplotador = solicitud != null ? solicitud.Telefono : null,
                CorreoExplotador = solicitud != null ? solicitud.Email : null,
                PuntoContactoEcuador = solicitud != null ? solicitud.RepresentanteLegal : null,
                ContactoDireccion = solicitud != null ? solicitud.Direccion : null,
                ContactoTelefono = solicitud != null ? solicitud.Telefono : null,
                ContactoCorreo = solicitud != null ? PrimerValorNoVacio(solicitud.CorreoRepresentanteTecnico, solicitud.Email) : null,
                PuntosContactoOperacionales = ConstruirPuntosContactoOperacionales(solicitud),
                BaseLegalReferencia = solicitud != null ? PrimerValorNoVacio(solicitud.AprobacionesEspecialesOtros, solicitud.AprobacionesEspeciales, solicitud.DescripcionOperacion) : null,
                ObservacionesReconocimiento = certificado != null ? certificado.Observaciones : (informe != null ? PrimerValorNoVacio(informe.Conclusiones, informe.Observaciones) : null),
                RepresentanteTecnico = solicitud != null ? PrimerValorNoVacio(solicitud.TecnicoResponsableNombre, solicitud.RepresentanteLegal) : null,
                CondicionBaseOperacion = condicionBase,
                RestriccionesCondiciones = certificado != null ? certificado.Observaciones : (informe != null ? informe.Recomendaciones : null),
                ElaboradoPor = certificado != null ? certificado.EmitidoPor : null,
                RevisadoPor = informe != null ? informe.UsuarioFirma2 : null,
                AeronavesCondiciones = ConstruirFilasAeronavesCondiciones(aeronaves)
            };
        }

        private static string ConstruirPuntosContactoOperacionales(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return null;
            }

            return string.Join(" / ", new[]
            {
                solicitud.DescripcionOperacion,
                solicitud.ResumenOperacionesEae,
                solicitud.Telefono,
                solicitud.Email
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static List<AocrCondicionAeronaveFilaViewModel> ConstruirFilasAeronavesCondiciones(IEnumerable<AeronaveSolicitud> aeronaves)
        {
            return (aeronaves ?? Enumerable.Empty<AeronaveSolicitud>())
                .Select(a => new AocrCondicionAeronaveFilaViewModel
                {
                    ModeloTipo = PrimerValorNoVacio(a.Modelo, a.Marca),
                    Matricula = a.Matricula,
                    Serie = a.Serie,
                    Uio = "Autorizado",
                    Gye = "Autorizado",
                    Mec = "N/A",
                    Ltx = "N/A"
                })
                .ToList();
        }

        private static List<string> ObtenerCamposObligatoriosFaltantesAocrOficial(AocrDocumentoPdfViewModel model)
        {
            var faltantes = new List<string>();
            if (model == null)
            {
                faltantes.Add("contexto del documento AOCR");
                return faltantes;
            }

            AgregarCampoFaltante(faltantes, model.NumeroAocr, "AOCR #");
            AgregarCampoFaltante(faltantes, model.AocOriginalNumero, "AOC base");
            AgregarCampoFaltante(faltantes, model.EstadoOtorgante, "Estado otorgante");
            AgregarCampoFaltante(faltantes, model.NombreExplotador, "Nombre del explotador");
            AgregarCampoFaltante(faltantes, model.EstadoExplotador, "Estado del explotador");
            AgregarCampoFaltante(faltantes, model.PuntoContactoEcuador, "Punto de contacto Ecuador");
            AgregarCampoFaltante(faltantes, model.PuntosContactoOperacionales, "Puntos de contacto operacionales");
            AgregarCampoFaltante(faltantes, model.RepresentanteTecnico, "Representante tecnico");
            AgregarCampoFaltante(faltantes, model.CondicionBaseOperacion, "Aeropuertos autorizados / condicion base");

            if (model.FechaEmisionDocumento == default(DateTime))
            {
                faltantes.Add("Fecha de emision");
            }

            if (!model.FechaVencimiento.HasValue)
            {
                faltantes.Add("Fecha de vencimiento");
            }

            if (model.AeronavesCondiciones == null || !model.AeronavesCondiciones.Any(fila => fila != null && !string.IsNullOrWhiteSpace(fila.ModeloTipo)))
            {
                faltantes.Add("Tabla de aeronaves autorizadas");
            }

            return faltantes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AgregarCampoFaltante(List<string> faltantes, string valor, string nombreCampo)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                faltantes.Add(nombreCampo);
            }
        }
    }

    public sealed class FirmaAocrPdfService
    {
        public byte[] GenerarPdfOficial(ControllerContext controllerContext, AocrDocumentoPdfViewModel model)
        {
            var pdf = new ViewAsPdf("~/Views/CoordinacionJefatura/AocrReconocimientoPdf.cshtml", (object)model)
            {
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(0, 0, 0, 0),
                CustomSwitches = PdfBrandingHelper.StandardRotativaSwitchesInlineBranding
            };

            return pdf.BuildFile(controllerContext);
        }
    }

    public sealed class FirmaAocrDigitalService
    {
        private readonly FirmaDigitalService _firmaDigitalService = new FirmaDigitalService();

        public InformacionCertificadoDigital LeerCertificado(byte[] certificadoBytes, string password)
        {
            return _firmaDigitalService.LeerCertificado(certificadoBytes, password);
        }

        public ResultadoFirmaDigital Firmar(byte[] pdfBytes, byte[] certificadoBytes, string password, string nombreFirmante, string contenidoQr)
        {
            return _firmaDigitalService.FirmarPdf(
                pdfBytes,
                certificadoBytes,
                password,
                nombreFirmante,
                "Firma institucional AOCR",
                "Sistema AOCR DGAC",
                "AOCR_FIRMANTE",
                contenidoQr,
                ObtenerPosicionInstitucionalFija());
        }

        public PosicionFirmaVisualPdf ObtenerPosicionInstitucionalFija()
        {
            return new PosicionFirmaVisualPdf
            {
                NumeroPagina = 1,
                PosicionXRatio = 0.02f,
                PosicionYRatio = 0.06f,
                AnchoRatio = 0.94f,
                AltoRatio = 0.82f
            };
        }
    }

    public sealed class FirmaAocrStorageService
    {
        private readonly HttpServerUtilityBase _server;

        public FirmaAocrStorageService(HttpServerUtilityBase server)
        {
            _server = server;
        }

        public string GuardarPdfOficial(int solicitudId, byte[] contenido)
        {
            return Guardar("Oficiales", solicitudId, "aocr_oficial", contenido);
        }

        public string GuardarPdfFirmado(int solicitudId, byte[] contenido)
        {
            return Guardar("Firmados", solicitudId, "aocr_firmado", contenido);
        }

        public string ResolverRutaFisica(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return null;
            }

            if (Path.IsPathRooted(ruta))
            {
                return ruta;
            }

            var normalizada = ruta.Trim();
            if (normalizada.StartsWith("~/", StringComparison.Ordinal))
            {
                return _server.MapPath(normalizada);
            }

            if (normalizada.StartsWith("/", StringComparison.Ordinal))
            {
                return _server.MapPath("~" + normalizada);
            }

            return _server.MapPath("~/" + normalizada.TrimStart('~', '/', '\\'));
        }

        public bool Existe(string ruta)
        {
            var fisica = ResolverRutaFisica(ruta);
            return !string.IsNullOrWhiteSpace(fisica) && File.Exists(fisica);
        }

        private string Guardar(string tipoCarpeta, int solicitudId, string prefijo, byte[] contenido)
        {
            var carpetaRelativa = "~/App_Data/Uploads/AOCR/" + tipoCarpeta + "/" + solicitudId;
            var carpetaAbsoluta = _server.MapPath(carpetaRelativa);
            if (!Directory.Exists(carpetaAbsoluta))
            {
                Directory.CreateDirectory(carpetaAbsoluta);
            }

            var nombre = prefijo + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + solicitudId + ".pdf";
            var rutaAbsoluta = Path.Combine(carpetaAbsoluta, nombre);
            File.WriteAllBytes(rutaAbsoluta, contenido ?? new byte[0]);
            return VirtualPathUtility.ToAbsolute(carpetaRelativa.TrimStart('~') + "/" + nombre);
        }
    }

    public sealed class FirmaAocrFinalizacionService
    {
        private readonly AocrFinalizacionService _finalizacionService = new AocrFinalizacionService();

        public AocrFinalizacionResultado LiberarDocumentoFinal(int solicitudId, int usuarioId, Func<string, bool> rutaExiste)
        {
            var resultado = _finalizacionService.IntentarFinalizarEmision(solicitudId, usuarioId, rutaExiste);
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][FINAL_OK] SolicitudId=" + solicitudId + "; EstadoNuevo=" + (resultado != null ? resultado.EstadoNuevo : string.Empty) + "; DocumentoFinalDisponible=" + (resultado != null && resultado.Finalizado));
            Trace.TraceInformation("[FIRMA_AOCR_V2][FINAL_OK] SolicitudId=" + solicitudId + "; EstadoSolicitudNuevo=" + (resultado != null ? resultado.EstadoNuevo : string.Empty) + "; DocumentoFinalDisponible=" + (resultado != null && resultado.Finalizado));
            return resultado;
        }
    }

    public sealed class FirmaAocrHistorialService
    {
        private readonly HistorialEstadoDAO _historialDao = new HistorialEstadoDAO();

        public void Registrar(int solicitudId, string estadoAnterior, string estadoNuevo, int usuarioId, string observacion)
        {
            try
            {
                _historialDao.RegistrarCambio(solicitudId, estadoAnterior, estadoNuevo, usuarioId, observacion);
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][HISTORIAL_OK] SolicitudId=" + solicitudId + "; EstadoAnterior=" + (estadoAnterior ?? string.Empty) + "; EstadoNuevo=" + (estadoNuevo ?? string.Empty));
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FIRMA_AOCR_NUEVA][ERROR] Historial SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
            }
        }
    }

    public sealed class FirmaAocrNotificationService
    {
        public void NotificarLiberacion(int solicitudId, string rutaFirmada)
        {
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][NOTIFICACION_OK] SolicitudId=" + solicitudId + "; RutaFirmada=" + (rutaFirmada ?? string.Empty));
        }
    }
}
