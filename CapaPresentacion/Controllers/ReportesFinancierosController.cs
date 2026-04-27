using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaModelo.ReportesFinancieros;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaPresentacion.Models.ViewModels;
using ClosedXML.Excel;
using Rotativa;
using Rotativa.Options;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Financiero,Direccion,JefaturaTecnica,Administrador")]
    public class ReportesFinancierosController : Controller
    {
        private readonly ReportesFinancierosBL _bl;

        public ReportesFinancierosController()
        {
            _bl = new ReportesFinancierosBL();
        }

        public ActionResult Index(FiltroReporteDTO filtros)
        {
            var vm = ConstruirViewModel(filtros, incluirOrdenes: true);
            return View(vm);
        }

        [HttpGet]
        public ActionResult ExportarExcel(FiltroReporteDTO filtros)
        {
            var vm = ConstruirViewModel(filtros, incluirOrdenes: true);
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Reporte");
                var row = 1;

                ws.Cell(row, 1).Value = "Reporte Financiero AOCR";
                ws.Range(row, 1, row, 16).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 14;
                row += 2;

                ws.Cell(row, 1).Value = "Orden";
                ws.Cell(row, 2).Value = "Fecha Creacion";
                ws.Cell(row, 3).Value = "Fecha Pago";
                ws.Cell(row, 4).Value = "Estado";
                ws.Cell(row, 5).Value = "Usuario";
                ws.Cell(row, 6).Value = "Compania";
                ws.Cell(row, 7).Value = "RUC/Cedula";
                ws.Cell(row, 8).Value = "Tramite";
                ws.Cell(row, 9).Value = "Unidad";
                ws.Cell(row, 10).Value = "Rol Gestion";
                ws.Cell(row, 11).Value = "Subtotal";
                ws.Cell(row, 12).Value = "Administracion";
                ws.Cell(row, 13).Value = "Total";
                ws.Cell(row, 14).Value = "Monto Pagado";
                ws.Cell(row, 15).Value = "Saldo Pendiente";
                ws.Cell(row, 16).Value = "Observacion";

                ws.Range(row, 1, row, 16).Style.Font.Bold = true;
                ws.Range(row, 1, row, 16).Style.Fill.BackgroundColor = XLColor.LightGray;
                row++;

                foreach (var orden in vm.Ordenes ?? Enumerable.Empty<ReporteOrdenDTO>())
                {
                    ws.Cell(row, 1).Value = orden.NumeroOrden;
                    ws.Cell(row, 2).Value = orden.FechaCreacion;
                    ws.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy";
                    ws.Cell(row, 3).Value = orden.FechaPago;
                    ws.Cell(row, 3).Style.DateFormat.Format = "dd/MM/yyyy";
                    ws.Cell(row, 4).Value = orden.Estado;
                    ws.Cell(row, 5).Value = orden.UsuarioSolicitante;
                    ws.Cell(row, 6).Value = orden.Compania;
                    ws.Cell(row, 7).Value = orden.RucCedula;
                    ws.Cell(row, 8).Value = orden.TipoTramite;
                    ws.Cell(row, 9).Value = orden.Unidad;
                    ws.Cell(row, 10).Value = orden.RolGestion;
                    ws.Cell(row, 11).Value = orden.Subtotal;
                    ws.Cell(row, 12).Value = orden.Administracion;
                    ws.Cell(row, 13).Value = orden.Total;
                    ws.Cell(row, 14).Value = orden.MontoPagado;
                    ws.Cell(row, 15).Value = orden.SaldoPendiente;
                    ws.Cell(row, 16).Value = orden.Observacion;
                    row++;
                }

                if (row > 3)
                {
                    ws.Range(3, 11, row - 1, 15).Style.NumberFormat.Format = "$ #,##0.00";
                }

                ws.Columns(1, 16).AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var fileName = "ReporteFinanciero_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }

        [HttpGet]
        public ActionResult ExportarPdf(FiltroReporteDTO filtros, bool vistaPrevia = false)
        {
            var vm = ConstruirViewModel(filtros, incluirOrdenes: true);
            var pdf = new ViewAsPdf("ExportPdf", vm)
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Landscape,
                PageMargins = new Margins(0, 0, 0, 0),
                CustomSwitches = PdfBrandingHelper.StandardRotativaSwitches
            };

            if (!vistaPrevia)
            {
                pdf.FileName = "ReporteFinanciero_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf";
            }

            return pdf;
        }

        [HttpGet]
        public ActionResult Imprimir(FiltroReporteDTO filtros)
        {
            var vm = ConstruirViewModel(filtros, incluirOrdenes: true);
            return View("ExportPdf", vm);
        }

        private ReportesFinancierosViewModel ConstruirViewModel(FiltroReporteDTO filtros, bool incluirOrdenes)
        {
            var filtroNormalizado = _bl.NormalizarFiltros(filtros);
            var resumen = _bl.ObtenerResumen(filtroNormalizado);
            var ordenes = incluirOrdenes ? _bl.ObtenerOrdenes(filtroNormalizado) : new List<ReporteOrdenDTO>();

            return new ReportesFinancierosViewModel
            {
                Filtros = filtroNormalizado,
                Resumen = resumen,
                Ordenes = ordenes,
                EstadosDisponibles = CrearEstados(filtroNormalizado.EstadoNormalizado),
                UsuariosDisponibles = ConvertirASelectList(_bl.ObtenerUsuariosSolicitantes(), filtroNormalizado.UsuarioSolicitanteId?.ToString()),
                TramitesDisponibles = ConvertirASelectList(_bl.ObtenerTiposTramite(), filtroNormalizado.TipoTramiteId?.ToString()),
                RolesGestionDisponibles = ConvertirASelectList(_bl.ObtenerRolesGestion(), filtroNormalizado.RolGestion),
                UnidadesDisponibles = ConvertirASelectList(_bl.ObtenerUnidades(), filtroNormalizado.Unidad)
            };
        }

        private static IList<SelectListItem> CrearEstados(string estadoSeleccionado)
        {
            var estados = new[]
            {
                new SelectListItem { Value = "", Text = "Todos" },
                new SelectListItem { Value = "PENDIENTE", Text = "Pendiente" },
                new SelectListItem { Value = "PROCESADA", Text = "Procesada" },
                new SelectListItem { Value = "FACTURADA", Text = "Facturada" },
                new SelectListItem { Value = "COMPLETADA", Text = "Completada" },
                new SelectListItem { Value = "ANULADA", Text = "Anulada" },
                new SelectListItem { Value = "RECHAZADA", Text = "Rechazada" }
            }.ToList();

            foreach (var estado in estados)
            {
                estado.Selected = string.Equals(estado.Value, estadoSeleccionado, StringComparison.OrdinalIgnoreCase);
            }

            return estados;
        }

        private static IList<SelectListItem> ConvertirASelectList(IEnumerable<FiltroOpcionDTO> items, string valorSeleccionado)
        {
            var lista = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Todos" } };
            if (items == null)
            {
                return lista;
            }

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Value))
                {
                    continue;
                }

                lista.Add(new SelectListItem
                {
                    Value = item.Value,
                    Text = string.IsNullOrWhiteSpace(item.Text) ? item.Value : item.Text,
                    Selected = string.Equals(item.Value, valorSeleccionado, StringComparison.OrdinalIgnoreCase)
                });
            }

            return lista;
        }

    }
}
