using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaModelo.ReportesFinancieros;

namespace CapaNegocio
{
    public class ReportesFinancierosBL
    {
        private readonly ReportesFinancierosDAO _dao;

        private static readonly Dictionary<int, string> MapaTramites = new Dictionary<int, string>
        {
            { 1, "Emision AOCR" },
            { 2, "Renovacion AOCR" },
            { 3, "Modificacion AOCR" },
            { 4, "Inspeccion / Viaticos" }
        };

        public ReportesFinancierosBL()
        {
            _dao = new ReportesFinancierosDAO();
        }

        public ReportesFinancierosBL(ReportesFinancierosDAO dao)
        {
            _dao = dao ?? new ReportesFinancierosDAO();
        }

        public FiltroReporteDTO NormalizarFiltros(FiltroReporteDTO filtro)
        {
            var normalizado = filtro ?? new FiltroReporteDTO();

            if (normalizado.FechaDesde.HasValue)
            {
                normalizado.FechaDesde = normalizado.FechaDesde.Value.Date;
            }

            if (normalizado.FechaHasta.HasValue)
            {
                normalizado.FechaHasta = normalizado.FechaHasta.Value.Date;
            }

            if (normalizado.FechaDesde.HasValue && normalizado.FechaHasta.HasValue &&
                normalizado.FechaDesde.Value > normalizado.FechaHasta.Value)
            {
                var temp = normalizado.FechaDesde;
                normalizado.FechaDesde = normalizado.FechaHasta;
                normalizado.FechaHasta = temp;
            }

            normalizado.Estado = LimpiarTexto(normalizado.Estado);
            normalizado.RolGestion = LimpiarTexto(normalizado.RolGestion);
            normalizado.Unidad = LimpiarTexto(normalizado.Unidad);

            return normalizado;
        }

        public ReporteResumenDTO ObtenerResumen(FiltroReporteDTO filtro)
        {
            try
            {
                var resumen = _dao.ObtenerResumen(NormalizarFiltros(filtro)) ?? new ReporteResumenDTO();
                AjustarEtiquetasTramite(resumen);
                return resumen;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error obteniendo resumen financiero", ex.ToString(), "ReportesFinancierosBL");
                return new ReporteResumenDTO();
            }
        }

        public IList<ReporteOrdenDTO> ObtenerOrdenes(FiltroReporteDTO filtro)
        {
            try
            {
                var ordenes = _dao.ObtenerOrdenes(NormalizarFiltros(filtro)) ?? new List<ReporteOrdenDTO>();
                foreach (var orden in ordenes)
                {
                    orden.TipoTramite = ResolverNombreTramite(orden.TipoTramiteId, orden.TipoTramite);
                }

                return ordenes;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error obteniendo ordenes para reportes", ex.ToString(), "ReportesFinancierosBL");
                return new List<ReporteOrdenDTO>();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerUsuariosSolicitantes()
        {
            try
            {
                return _dao.ObtenerUsuariosSolicitantes() ?? new List<FiltroOpcionDTO>();
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error obteniendo usuarios para filtro", ex.ToString(), "ReportesFinancierosBL");
                return new List<FiltroOpcionDTO>();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerTiposTramite()
        {
            try
            {
                var tipos = _dao.ObtenerTiposTramite() ?? new List<FiltroOpcionDTO>();
                foreach (var tipo in tipos)
                {
                    if (int.TryParse(tipo.Value, out var id))
                    {
                        tipo.Text = ResolverNombreTramite(id, tipo.Text);
                    }
                }

                return tipos;
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error obteniendo tipos de tramite para filtro", ex.ToString(), "ReportesFinancierosBL");
                return new List<FiltroOpcionDTO>();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerRolesGestion()
        {
            try
            {
                return _dao.ObtenerRolesGestion() ?? new List<FiltroOpcionDTO>();
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error obteniendo roles de gestion para filtro", ex.ToString(), "ReportesFinancierosBL");
                return new List<FiltroOpcionDTO>();
            }
        }

        public IList<FiltroOpcionDTO> ObtenerUnidades()
        {
            try
            {
                return _dao.ObtenerUnidades() ?? new List<FiltroOpcionDTO>();
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error obteniendo unidades para filtro", ex.ToString(), "ReportesFinancierosBL");
                return new List<FiltroOpcionDTO>();
            }
        }

        private static string LimpiarTexto(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return null;
            }

            return valor.Trim();
        }

        private static string ResolverNombreTramite(int? tipoTramiteId, string fallback)
        {
            if (!tipoTramiteId.HasValue)
            {
                return string.IsNullOrWhiteSpace(fallback) ? "N/D" : fallback;
            }

            return MapaTramites.ContainsKey(tipoTramiteId.Value)
                ? MapaTramites[tipoTramiteId.Value]
                : "Tramite " + tipoTramiteId.Value;
        }

        private static void AjustarEtiquetasTramite(ReporteResumenDTO resumen)
        {
            if (resumen == null || resumen.RecaudacionPorTramite == null)
            {
                return;
            }

            foreach (var item in resumen.RecaudacionPorTramite.Where(x => x != null))
            {
                var numero = default(int);
                if (item.Tramite != null && item.Tramite.StartsWith("Tramite ", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(item.Tramite.Replace("Tramite ", string.Empty), out numero))
                {
                    item.Tramite = ResolverNombreTramite(numero, item.Tramite);
                }
            }
        }
    }
}
