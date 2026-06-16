using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Contexto central de compañía activa RT: pertenencia, coincidencia y filtrado.
    /// </summary>
    public sealed class AocrCompaniaContextService
    {
        private readonly UsuarioCompaniaRTDAO _usuarioCompaniaDao = new UsuarioCompaniaRTDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();

        public bool ValidarCompaniaPerteneceAlRt(int usuarioId, string companiaCodigo)
        {
            if (usuarioId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                return false;
            }

            return _usuarioCompaniaDao.UsuarioTieneCompaniaAsignada(usuarioId, companiaCodigo.Trim());
        }

        public CompaniaActivaInfo ObtenerCompaniaActiva(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            var codigo = (companiaCodigo ?? string.Empty).Trim();
            if (usuarioId <= 0 || string.IsNullOrWhiteSpace(codigo))
            {
                return new CompaniaActivaInfo { EsValida = false };
            }

            var asignacion = ObtenerAsignacion(usuarioId, codigo);
            if (asignacion == null)
            {
                return new CompaniaActivaInfo { Codigo = codigo, EsValida = false };
            }

            var nombre = !string.IsNullOrWhiteSpace(companiaNombre)
                ? companiaNombre.Trim()
                : (!string.IsNullOrWhiteSpace(asignacion.CompaniaNombre)
                    ? asignacion.CompaniaNombre.Trim()
                    : codigo);

            return new CompaniaActivaInfo
            {
                Codigo = codigo,
                Nombre = nombre,
                Ruc = (asignacion.Usuoid ?? string.Empty).Trim(),
                EsValida = true
            };
        }

        public CompaniaActivaInfo ObtenerCompaniaActivaObligatoria(int usuarioId, string companiaCodigo, string companiaNombre = null)
        {
            var info = ObtenerCompaniaActiva(usuarioId, companiaCodigo, companiaNombre);
            return info != null && info.EsValida ? info : new CompaniaActivaInfo { EsValida = false };
        }

        public bool ValidarSolicitudPerteneceACompaniaActiva(int usuarioId, int solicitudId, string companiaCodigo, string companiaNombre = null)
        {
            if (solicitudId <= 0 || usuarioId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                return false;
            }

            if (!ValidarCompaniaPerteneceAlRt(usuarioId, companiaCodigo))
            {
                return false;
            }

            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            return solicitud != null && SolicitudPerteneceACompania(solicitud, companiaCodigo, companiaNombre);
        }

        public bool ValidarOrdenPerteneceACompaniaActiva(int usuarioId, int ordenId, string companiaCodigo, string companiaNombre = null)
        {
            if (ordenId <= 0 || usuarioId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                return false;
            }

            if (!ValidarCompaniaPerteneceAlRt(usuarioId, companiaCodigo))
            {
                return false;
            }

            var ordenDao = new OrdenRecaudacionDAO();
            var orden = ordenDao.ObtenerOrdenPorId(ordenId);
            return orden != null && OrdenPerteneceACompania(orden, companiaCodigo, companiaNombre, usuarioId);
        }

        public string ResolverRucCompaniaAsignada(int usuarioId, string companiaCodigo)
        {
            var asignacion = ObtenerAsignacion(usuarioId, companiaCodigo);
            return asignacion != null ? (asignacion.Usuoid ?? string.Empty).Trim() : string.Empty;
        }

        public string ObtenerMensajeCompaniaInconsistente()
        {
            return "La compañía enviada no coincide con la compañía activa seleccionada. Recargue la pantalla y vuelva a intentarlo.";
        }

        public string ObtenerMensajeAccesoDenegadoCompania()
        {
            return "No tiene autorización para operar sobre esta compañía.";
        }

        public bool CoincideCompania(
            string companiaActivaCodigo,
            string companiasSolicitud,
            string codigoOaci = null,
            string razonSocial = null,
            string nombreOperador = null,
            string companiaActivaNombre = null)
        {
            var codigo = (companiaActivaCodigo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return true;
            }

            if (ContieneValorLista(companiasSolicitud, codigo))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(codigoOaci)
                && string.Equals(codigoOaci.Trim(), codigo, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(companiaActivaNombre))
            {
                return CoincideTextoEmpresa(razonSocial, companiaActivaNombre)
                    || CoincideTextoEmpresa(nombreOperador, companiaActivaNombre);
            }

            return false;
        }

        public bool SolicitudPerteneceACompania(SolicitudAOCR solicitud, string companiaActivaCodigo, string companiaActivaNombre = null)
        {
            if (solicitud == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                return true;
            }

            if (CoincideCompania(
                companiaActivaCodigo,
                solicitud.CompaniasSeleccionadas,
                solicitud.CodigoOaci,
                solicitud.RazonSocial,
                solicitud.NombreOperador,
                companiaActivaNombre))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(companiaActivaNombre))
            {
                return CoincideTextoEmpresa(solicitud.RazonSocial, companiaActivaNombre)
                    || CoincideTextoEmpresa(solicitud.NombreOperador, companiaActivaNombre);
            }

            return false;
        }

        public bool OrdenPerteneceACompania(
            OrdenRecaudacion orden,
            string companiaActivaCodigo,
            string companiaActivaNombre,
            int usuarioId)
        {
            if (orden == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                var codigoOrden = ResolverCodigoCompaniaDesdeOrden(orden);
                if (!string.IsNullOrWhiteSpace(codigoOrden)
                    && string.Equals(
                        codigoOrden.Trim(),
                        companiaActivaCodigo.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(orden.Compania)
                    && orden.Compania.IndexOf(companiaActivaCodigo.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(companiaActivaCodigo) && string.IsNullOrWhiteSpace(companiaActivaNombre))
            {
                return true;
            }

            if (orden.CodigoSolicitud.HasValue && orden.CodigoSolicitud.Value > 0)
            {
                var solicitud = _solicitudDao.ObtenerPorId(orden.CodigoSolicitud.Value);
                if (solicitud != null
                    && SolicitudPerteneceACompania(solicitud, companiaActivaCodigo, companiaActivaNombre))
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(companiaActivaNombre)
                && CoincideTextoEmpresa(orden.Compania, companiaActivaNombre))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                var nombreAsignado = ResolverNombreCompaniaAsignada(usuarioId, companiaActivaCodigo);
                if (!string.IsNullOrWhiteSpace(nombreAsignado)
                    && CoincideTextoEmpresa(orden.Compania, nombreAsignado))
                {
                    return true;
                }
            }

            if (usuarioId > 0 && !string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                var asignacion = (_usuarioCompaniaDao.ObtenerCompaniasAsignadas(usuarioId, true) ?? new List<UsuarioCompaniaRT>())
                    .FirstOrDefault(c => string.Equals(
                        (c.CompaniaCodigo ?? string.Empty).Trim(),
                        companiaActivaCodigo.Trim(),
                        StringComparison.OrdinalIgnoreCase));

                if (asignacion != null
                    && !string.IsNullOrWhiteSpace(asignacion.Usuoid)
                    && !string.IsNullOrWhiteSpace(orden.RucCedula)
                    && string.Equals(asignacion.Usuoid.Trim(), orden.RucCedula.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public IList<SolicitudAOCR> FiltrarSolicitudesPorCompania(
            IEnumerable<SolicitudAOCR> solicitudes,
            string companiaActivaCodigo,
            string companiaActivaNombre = null)
        {
            var lista = (solicitudes ?? Enumerable.Empty<SolicitudAOCR>()).Where(s => s != null).ToList();
            if (string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                return lista;
            }

            return lista
                .Where(s => SolicitudPerteneceACompania(s, companiaActivaCodigo, companiaActivaNombre))
                .ToList();
        }

        public IList<OrdenRecaudacion> FiltrarOrdenesPorCompania(
            IEnumerable<OrdenRecaudacion> ordenes,
            string companiaActivaCodigo,
            string companiaActivaNombre,
            int usuarioId)
        {
            var lista = (ordenes ?? Enumerable.Empty<OrdenRecaudacion>()).Where(o => o != null).ToList();
            if (string.IsNullOrWhiteSpace(companiaActivaCodigo) && string.IsNullOrWhiteSpace(companiaActivaNombre))
            {
                return lista;
            }

            return lista
                .Where(o => OrdenPerteneceACompania(o, companiaActivaCodigo, companiaActivaNombre, usuarioId))
                .ToList();
        }

        private UsuarioCompaniaRT ObtenerAsignacion(int usuarioId, string companiaCodigo)
        {
            if (usuarioId <= 0 || string.IsNullOrWhiteSpace(companiaCodigo))
            {
                return null;
            }

            return (_usuarioCompaniaDao.ObtenerCompaniasAsignadas(usuarioId, true) ?? new List<UsuarioCompaniaRT>())
                .FirstOrDefault(c => string.Equals(
                    (c.CompaniaCodigo ?? string.Empty).Trim(),
                    companiaCodigo.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        public string ResolverNombreCompaniaAsignada(int usuarioId, string companiaCodigo)
        {
            var asignacion = ObtenerAsignacion(usuarioId, companiaCodigo);
            return asignacion != null ? (asignacion.CompaniaNombre ?? string.Empty).Trim() : null;
        }

        private static bool ContieneValorLista(string valores, string buscado)
        {
            if (string.IsNullOrWhiteSpace(valores) || string.IsNullOrWhiteSpace(buscado))
            {
                return false;
            }

            return valores
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim())
                .Any(x => x.Equals(buscado.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public string FormatearTextoCompaniaOrden(string codigo, string nombre)
        {
            var codigoNormalizado = (codigo ?? string.Empty).Trim();
            var nombreNormalizado = (nombre ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(codigoNormalizado))
            {
                return nombreNormalizado;
            }

            if (string.IsNullOrWhiteSpace(nombreNormalizado))
            {
                return codigoNormalizado;
            }

            if (nombreNormalizado.IndexOf(codigoNormalizado, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return nombreNormalizado;
            }

            return codigoNormalizado + " " + nombreNormalizado;
        }

        public string ResolverCodigoCompaniaDesdeOrden(OrdenRecaudacion orden)
        {
            if (orden == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(orden.CompaniaCodigo))
            {
                return orden.CompaniaCodigo.Trim();
            }

            if (orden.CodigoSolicitud.HasValue && orden.CodigoSolicitud.Value > 0)
            {
                var solicitud = _solicitudDao.ObtenerPorId(orden.CodigoSolicitud.Value);
                if (solicitud != null)
                {
                    var token = ObtenerPrimerToken(solicitud.CompaniasSeleccionadas);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        return token;
                    }

                    if (!string.IsNullOrWhiteSpace(solicitud.CodigoOaci))
                    {
                        return solicitud.CodigoOaci.Trim();
                    }
                }
            }

            return ExtraerCodigoCompaniaDesdeTexto(orden.Compania);
        }

        private static string ObtenerPrimerToken(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            return valor
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => (v ?? string.Empty).Trim())
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                ?? string.Empty;
        }

        private static string ExtraerCodigoCompaniaDesdeTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var normalizado = texto.Trim();
            if (normalizado.StartsWith("[", StringComparison.Ordinal))
            {
                var fin = normalizado.IndexOf(']');
                if (fin > 1)
                {
                    return normalizado.Substring(1, fin - 1).Trim();
                }
            }

            var open = normalizado.LastIndexOf('(');
            var close = normalizado.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                var oaci = normalizado.Substring(open + 1, close - open - 1).Trim();
                if (!string.IsNullOrWhiteSpace(oaci) && oaci.Length <= 10)
                {
                    return oaci;
                }
            }

            var partes = normalizado.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length > 0 && partes[0].All(char.IsDigit))
            {
                return partes[0];
            }

            return string.Empty;
        }

        private static bool CoincideTextoEmpresa(string valor, string referencia)
        {
            if (string.IsNullOrWhiteSpace(valor) || string.IsNullOrWhiteSpace(referencia))
            {
                return false;
            }

            var a = valor.Trim();
            var b = referencia.Trim();
            return a.Equals(b, StringComparison.OrdinalIgnoreCase)
                || a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0
                || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
