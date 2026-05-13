using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio.Integraciones.As400Sync;
using CapaModelo;

namespace CapaNegocio
{
    /// <summary>
    /// Fachada de infraestructura para desacoplar el controlador AOCR de instanciaciones DAO ad-hoc.
    /// </summary>
    public class SolicitudAocrInfraBL
    {
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly HistorialEstadoDAO _historialEstadoDao = new HistorialEstadoDAO();
        private readonly RevisionDocumentalDAO _revisionDocumentalDao = new RevisionDocumentalDAO();
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao = new UsuarioInternoRTDAO();
        private readonly UsuarioAS400DAO _usuarioAs400Dao = new UsuarioAS400DAO(new SecureConfigurationService());
        private readonly EmpresaAS400DAO _empresaAs400Dao = new EmpresaAS400DAO(new SecureConfigurationService());
        private readonly MirrorReadService _mirrorReadService = new MirrorReadService();
        private readonly TrazabilidadDAO _trazabilidadDao = new TrazabilidadDAO();

        // =========================================================
        // TRAZABILIDAD COMPLETA DEL EXPEDIENTE
        // =========================================================
        public List<EventoTrazabilidad> ObtenerTrazabilidadCompleta(int codigoSolicitud)
        {
            try
            {
                return _trazabilidadDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<EventoTrazabilidad>();
            }
            catch
            {
                return new List<EventoTrazabilidad>();
            }
        }

        public List<DocumentoSubsanacionRegistro> ObtenerDocumentosSubsanacionPorSolicitud(int codigoSolicitud)
        {
            try
            {
                return _trazabilidadDao.ObtenerDocumentosSubsanacionPorSolicitud(codigoSolicitud)
                       ?? new List<DocumentoSubsanacionRegistro>();
            }
            catch
            {
                return new List<DocumentoSubsanacionRegistro>();
            }
        }

        public List<Inspeccion> ListarInspeccionesPorSolicitud(int codigoSolicitud)
        {
            return _inspeccionDao.ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();
        }

        public List<HistorialEstado> ObtenerHistorialEstadosPorSolicitud(int codigoSolicitud)
        {
            return _historialEstadoDao.ObtenerPorSolicitud(codigoSolicitud) ?? new List<HistorialEstado>();
        }

        public Dictionary<int, Tuple<string, string>> ObtenerUltimasRevisionesPorSolicitud(int codigoSolicitud)
        {
            return _revisionDocumentalDao.ObtenerUltimasRevisionesPorSolicitud(codigoSolicitud)
                   ?? new Dictionary<int, Tuple<string, string>>();
        }

        public Dictionary<int, RevisionDocumentalDetalle> ObtenerUltimosDetallesRevisionPorSolicitud(int codigoSolicitud)
        {
            return _revisionDocumentalDao.ObtenerUltimosDetallesPorSolicitud(codigoSolicitud)
                   ?? new Dictionary<int, RevisionDocumentalDetalle>();
        }

        public bool RegistrarRevisionDocumental(int codigoSolicitud, int codigoDocumento, string decision, string observacion, int usuarioId, string usuarioRegistro)
        {
            return _revisionDocumentalDao.RegistrarRevision(codigoSolicitud, codigoDocumento, decision, observacion, usuarioId, usuarioRegistro);
        }

        public bool RegistrarEventoHistorialRevision(int codigoSolicitud, int? codigoDocumento, string tipoEvento, string observacion, int usuarioId, string usuarioRegistro)
        {
            return _revisionDocumentalDao.RegistrarEventoHistorial(codigoSolicitud, codigoDocumento, tipoEvento, observacion, usuarioId, usuarioRegistro);
        }

        public HashSet<int> ObtenerDocumentosConEventoHistorial(int codigoSolicitud, string tipoEvento)
        {
            return _revisionDocumentalDao.ObtenerDocumentosConEventoHistorial(codigoSolicitud, tipoEvento)
                   ?? new HashSet<int>();
        }

        public AsignacionRTRegistro ObtenerAsignacionActiva(int codigoSolicitud)
        {
            return _usuarioInternoRtDao.ObtenerAsignacionActiva(codigoSolicitud);
        }

        public List<AsignacionRTRegistro> ObtenerHistorialAsignacion(int codigoSolicitud)
        {
            return _usuarioInternoRtDao.ObtenerHistorialAsignacion(codigoSolicitud) ?? new List<AsignacionRTRegistro>();
        }

        public string ObtenerCedulaPorCodigoUsuario(string codigoUsuario)
        {
            var clave = (codigoUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clave))
            {
                return null;
            }

            try
            {
                var mirror = _mirrorReadService.ObtenerIdentificacionPorClavesUsuario(new[] { clave });
                if (mirror != null && !string.IsNullOrWhiteSpace(mirror.Cedula))
                {
                    return mirror.Cedula.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerCedulaPorCodigoUsuario mirror error: " + ex.Message);
            }

            return _usuarioAs400Dao.ObtenerCedulaPorCodigoUsuario(clave);
        }

        public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario)
        {
            var clave = (codigoUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clave))
            {
                return null;
            }

            try
            {
                var mirror = _mirrorReadService.ObtenerIdentificacionPorClavesUsuario(new[] { clave });
                if (mirror != null && !string.IsNullOrWhiteSpace(mirror.Ruc))
                {
                    return mirror.Ruc.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerNumeroRucPorCodigoUsuario mirror error: " + ex.Message);
            }

            return _usuarioAs400Dao.ObtenerNumeroRucPorCodigoUsuario(clave);
        }

        public Empresa ObtenerEmpresaPorCodigo(string codigoOaci)
        {
            var codigo = (codigoOaci ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return null;
            }

            try
            {
                var mirror = _mirrorReadService.ObtenerCompaniaPorCodigo(codigo);
                if (mirror != null)
                {
                    return new Empresa
                    {
                        CodigoOaci = mirror.CodigoOaci,
                        CodigoIata = mirror.CodigoIata,
                        CodigoNumeroCia = mirror.CodigoNumeroCia,
                        Nombre = mirror.NombreCompania
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerEmpresaPorCodigo mirror error: " + ex.Message);
            }

            return _empresaAs400Dao.ObtenerEmpresaPorCodigo(codigo);
        }

        public List<Empresa> ObtenerEmpresas()
        {
            try
            {
                var mirror = _mirrorReadService.ListarCompaniasActivas(5000);
                if (mirror != null && mirror.Count > 0)
                {
                    return mirror
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CodigoOaci))
                        .Select(c => new Empresa
                        {
                            CodigoOaci = c.CodigoOaci,
                            CodigoIata = c.CodigoIata,
                            CodigoNumeroCia = c.CodigoNumeroCia,
                            Nombre = c.NombreCompania
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SolicitudAocrInfraBL] ObtenerEmpresas mirror error: " + ex.Message);
            }

            return _empresaAs400Dao.ObtenerEmpresas() ?? new List<Empresa>();
        }
    }
}
