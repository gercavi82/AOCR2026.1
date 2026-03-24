using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Models;
using CapaDatos.Services;
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

        public bool RegistrarRevisionDocumental(int codigoSolicitud, int codigoDocumento, string decision, string observacion, int usuarioId, string usuarioRegistro)
        {
            return _revisionDocumentalDao.RegistrarRevision(codigoSolicitud, codigoDocumento, decision, observacion, usuarioId, usuarioRegistro);
        }

        public bool RegistrarEventoHistorialRevision(int codigoSolicitud, int? codigoDocumento, string tipoEvento, string observacion, int usuarioId, string usuarioRegistro)
        {
            return _revisionDocumentalDao.RegistrarEventoHistorial(codigoSolicitud, codigoDocumento, tipoEvento, observacion, usuarioId, usuarioRegistro);
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
            return _usuarioAs400Dao.ObtenerCedulaPorCodigoUsuario(codigoUsuario);
        }

        public string ObtenerNumeroRucPorCodigoUsuario(string codigoUsuario)
        {
            return _usuarioAs400Dao.ObtenerNumeroRucPorCodigoUsuario(codigoUsuario);
        }

        public Empresa ObtenerEmpresaPorCodigo(string codigoOaci)
        {
            return _empresaAs400Dao.ObtenerEmpresaPorCodigo(codigoOaci);
        }

        public List<Empresa> ObtenerEmpresas()
        {
            return _empresaAs400Dao.ObtenerEmpresas() ?? new List<Empresa>();
        }
    }
}
