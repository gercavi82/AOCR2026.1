using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaModelo;
using CapaModelo.Common;
using CapaNegocio.Helpers;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Contadores del sidebar usando las mismas consultas que las bandejas por rol.
    /// </summary>
    public sealed class AocrSidebarCounterService
    {
        private readonly CoordinacionBandejaService _coordinacionBandeja = new CoordinacionBandejaService();
        private readonly InspectorBandejaService _inspectorBandeja = new InspectorBandejaService();
        private readonly DireccionBandejaService _direccionBandeja = new DireccionBandejaService();
        private readonly OrdenRecaudacionDAO _ordenDao = new OrdenRecaudacionDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();

        public AocrSidebarCoordinatorCounters ObtenerContadoresCoordinacion()
        {
            return new AocrSidebarCoordinatorCounters
            {
                PendientesAsignacion = _coordinacionBandeja.ContarPendientesAsignacion(),
                ColaDocumental = _coordinacionBandeja.ContarColaDocumental(),
                RevisionFormalAocr = _coordinacionBandeja.ContarRevisionFormalAocr()
            };
        }

        public InspectorBandejaContadores ObtenerContadoresInspector(AocrBandejaRoleContext context)
        {
            if (context == null)
            {
                return new InspectorBandejaContadores();
            }

            return _inspectorBandeja.ObtenerContadores(context);
        }

        public AocrSidebarDireccionCounters ObtenerContadoresDireccion()
        {
            return new AocrSidebarDireccionCounters
            {
                BandejaEjecutivaAprobacion = _direccionBandeja.ContarPendientesRevisionDcav(),
                FirmasPendientesDirdac = _direccionBandeja.ContarFirmasPendientesDirdac()
            };
        }

        public AocrSidebarRtCounters ObtenerContadoresRt(int userId, bool esAdministrador, IEnumerable<SolicitudAOCR> solicitudesFiltradas)
        {
            var solicitudes = (solicitudesFiltradas ?? Enumerable.Empty<SolicitudAOCR>()).Where(s => s != null).ToList();
            var contador = new AocrSidebarRtCounters
            {
                Activas = solicitudes.Count(s => EsEstadoAbierto(s.Estado)),
                Observadas = solicitudes.Count(s => string.Equals(EstadoSolicitud.Normalizar(s.Estado), EstadoSolicitud.Observada, System.StringComparison.OrdinalIgnoreCase)),
                DocumentosFinales = (new AocrBandejaDAO().ListarGeneradasFirmadas() ?? new List<AocrBandejaDocumentoRow>()).Count
            };

            if (userId > 0)
            {
                contador.PendientesSubsanacion = new SubsanacionDAO().ContarPendientesPorOperador(userId);
                if (contador.PendientesSubsanacion <= 0)
                {
                    contador.PendientesSubsanacion = contador.Observadas;
                }
            }

            return contador;
        }

        public IList<SolicitudAOCR> ObtenerSolicitudesRtBase(int userId, bool esAdministrador)
        {
            if (userId > 0)
            {
                return _solicitudDao.ObtenerPorUsuario(userId) ?? new List<SolicitudAOCR>();
            }

            return esAdministrador ? (_solicitudDao.ObtenerTodos() ?? new List<SolicitudAOCR>()) : new List<SolicitudAOCR>();
        }

        public AocrSidebarFinancieroCounters ObtenerContadoresFinanciero()
        {
            var ordenes = _ordenDao.ObtenerTodasLasOrdenes(null) ?? new List<OrdenRecaudacion>();
            var comprobanteService = new ComprobanteService();
            var contador = new AocrSidebarFinancieroCounters();

            foreach (var orden in ordenes.Where(o => o != null))
            {
                try
                {
                    var pago = _ordenDao.ObtenerUltimoPagoPorOrden(orden.Id);
                    var factura = _ordenDao.ObtenerFacturaPagoPorOrden(orden.Id);
                    var tieneFactura = FinancialOrderStateHelper.TieneFacturaRegistrada(
                        factura != null ? factura.NumeroFactura : null,
                        factura != null ? factura.Fr3Estado : null,
                        factura != null ? factura.Fr3Numero : null);
                    var estadoPago = pago != null ? pago.Estado : null;
                    var tieneComprobanteValido = comprobanteService.ExisteComprobanteValido(orden.Id);

                    if (FinancialOrderStateHelper.DebeOcultarDeBandejaFinanciera(orden.Estado, tieneComprobanteValido))
                    {
                        continue;
                    }

                    if (FinancialOrderStateHelper.EsPendienteGestion(orden.Estado, estadoPago, tieneFactura, tieneComprobanteValido))
                    {
                        contador.PendientesValidacion++;
                    }

                    if (FinancialOrderStateHelper.CoincideFiltro(orden.Estado, estadoPago, tieneFactura, EstadoOrden.EnRevisionFinanciera, tieneComprobanteValido))
                    {
                        contador.PagosCargados++;
                    }

                    if (FinancialOrderStateHelper.EsObservada(orden.Estado, estadoPago))
                    {
                        contador.Observadas++;
                    }

                    if (FinancialOrderStateHelper.EsAprobadaOFacturada(orden.Estado, estadoPago, tieneFactura))
                    {
                        contador.Aprobadas++;
                    }

                    if (tieneFactura)
                    {
                        contador.Facturadas++;
                    }

                    if (FinancialOrderStateHelper.EsHistorialFinanciero(orden.Estado, estadoPago, tieneFactura))
                    {
                        contador.Historial++;
                    }
                }
                catch
                {
                    // Ignorar ordenes con datos incompletos para no bloquear el sidebar.
                }
            }

            return contador;
        }

        public int ContarDocumentosFinalesCoordinacion()
        {
            var filas = new AocrBandejaDAO().ListarGeneradasFirmadas();
            return filas == null ? 0 : filas.Count;
        }

        private static bool EsEstadoAbierto(string estado)
        {
            var normalizado = EstadoSolicitud.Normalizar(estado);
            return !string.Equals(normalizado, EstadoSolicitud.Finalizado, System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizado, EstadoSolicitud.Anulada, System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizado, EstadoSolicitud.AOCR_EmitidoRecibido, System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizado, EstadoSolicitud.AOCR_Legalizado, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class AocrSidebarCoordinatorCounters
    {
        public int PendientesAsignacion { get; set; }
        public int ColaDocumental { get; set; }
        public int RevisionFormalAocr { get; set; }
    }

    public sealed class AocrSidebarDireccionCounters
    {
        public int BandejaEjecutivaAprobacion { get; set; }
        public int FirmasPendientesDirdac { get; set; }
    }

    public sealed class AocrSidebarRtCounters
    {
        public int Activas { get; set; }
        public int Observadas { get; set; }
        public int PendientesSubsanacion { get; set; }
        public int DocumentosFinales { get; set; }
    }

    public sealed class AocrSidebarFinancieroCounters
    {
        public int PendientesValidacion { get; set; }
        public int PagosCargados { get; set; }
        public int Observadas { get; set; }
        public int Aprobadas { get; set; }
        public int Facturadas { get; set; }
        public int Historial { get; set; }
    }
}
