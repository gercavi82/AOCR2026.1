using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using CapaDatos.Models; // <-- AJUSTA si tu Orden está en CapaModelo

namespace CapaDatos.Services
{
    public class PdfOrdenRecaudacionService
    {
        public byte[] GenerarPdf(OrdenRecaudacionModel orden, string logoPathFisico, string usuarioGenera)
        {
            if (orden == null) throw new ArgumentNullException(nameof(orden));

            using (var ms = new MemoryStream())
            using (var doc = new Document(PageSize.A4, 40, 40, 40, 40))
            {
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var fontSub = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11);
                var fontBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
                var font = FontFactory.GetFont(FontFactory.HELVETICA, 9);

                // Logo
                if (!string.IsNullOrWhiteSpace(logoPathFisico) && File.Exists(logoPathFisico))
                {
                    var img = Image.GetInstance(logoPathFisico);
                    img.ScaleToFit(120, 60);
                    img.Alignment = Element.ALIGN_LEFT;
                    doc.Add(img);
                }

                doc.Add(new Paragraph("ORDEN DE RECAUDACIÓN", fontTitle) { Alignment = Element.ALIGN_CENTER });
                doc.Add(new Paragraph("Dirección General de Aviación Civil - Ecuador", fontSub));
                doc.Add(new Paragraph("Sistema AOCR", font));
                doc.Add(new Paragraph(" "));

                // Cabecera
                var t = new PdfPTable(2) { WidthPercentage = 100 };
                t.SetWidths(new float[] { 1, 2 });

                t.AddCell(Cell("Número de Orden:", fontBold));
                t.AddCell(Cell(orden.NumeroOrden ?? "-", font));

                t.AddCell(Cell("Fecha:", fontBold));
                t.AddCell(Cell(orden.FechaCreacion.ToString("dd/MM/yyyy HH:mm"), font));

                t.AddCell(Cell("Estado:", fontBold));
                t.AddCell(Cell((orden.Estado ?? "-").ToUpperInvariant(), font));

                t.AddCell(Cell("Compañía:", fontBold));
                t.AddCell(Cell(orden.Compania ?? "-", font));

                t.AddCell(Cell("RUC/Cédula:", fontBold));
                t.AddCell(Cell(orden.RucCedula ?? "-", font));

                t.AddCell(Cell("Correo:", fontBold));
                t.AddCell(Cell(orden.Correo ?? "-", font));

                t.AddCell(Cell("Teléfono:", fontBold));
                t.AddCell(Cell(orden.Telefono ?? "-", font));

                doc.Add(t);
                doc.Add(new Paragraph(" "));

                // Detalle
                doc.Add(new Paragraph("DETALLE", fontSub));
                doc.Add(new Paragraph(" "));

                var dt = new PdfPTable(6) { WidthPercentage = 100 };
                dt.SetWidths(new float[] { 3, 1, 1, 1, 1, 1 });

                dt.AddCell(Header("Concepto", fontBold));
                dt.AddCell(Header("Cant.", fontBold));
                dt.AddCell(Header("V.Unit", fontBold));
                dt.AddCell(Header("Subt.", fontBold));
                dt.AddCell(Header("Admin", fontBold));
                dt.AddCell(Header("Total", fontBold));

                var detalles = orden.Detalles ?? new System.Collections.Generic.List<OrdenDetalleModel>();
                foreach (var d in detalles)
                {
                    var concepto = (d.ConceptoNombre ?? "") + (string.IsNullOrWhiteSpace(d.Descripcion) ? "" : "\n" + d.Descripcion);

                    dt.AddCell(Cell(concepto, font));
                    dt.AddCell(CellR(d.Cantidad.ToString("0.##"), font));
                    dt.AddCell(CellR(d.ValorUnitario.ToString("$ #,##0.00"), font));
                    dt.AddCell(CellR(d.Subtotal.ToString("$ #,##0.00"), font));
                    dt.AddCell(CellR(d.Admin.ToString("$ #,##0.00"), font));
                    dt.AddCell(CellR(d.TotalLinea.ToString("$ #,##0.00"), fontBold));
                }

                doc.Add(dt);
                doc.Add(new Paragraph(" "));

                // Totales
                var tot = new PdfPTable(2)
                {
                    WidthPercentage = 45,
                    HorizontalAlignment = Element.ALIGN_RIGHT
                };

                tot.AddCell(CellR("SUBTOTAL:", fontBold));
                tot.AddCell(CellR(orden.Subtotal.ToString("$ #,##0.00"), font));

                tot.AddCell(CellR("ADMIN:", fontBold));
                tot.AddCell(CellR(orden.Admin.ToString("$ #,##0.00"), font));

                tot.AddCell(CellR("TOTAL:", fontBold));
                tot.AddCell(CellR(orden.Total.ToString("$ #,##0.00"), fontBold));

                doc.Add(tot);

                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph($"Generado por: {usuarioGenera} - {DateTime.Now:dd/MM/yyyy HH:mm}", font));

                doc.Close();
                return ms.ToArray();
            }
        }

        private static PdfPCell Cell(string text, Font f)
        {
            var c = new PdfPCell(new Phrase(text ?? "", f))
            {
                Padding = 5,
                BorderWidth = 0.5f
            };
            return c;
        }

        private static PdfPCell Header(string text, Font f)
        {
            var c = Cell(text, f);
            c.BackgroundColor = BaseColor.LIGHT_GRAY;
            return c;
        }

        private static PdfPCell CellR(string text, Font f)
        {
            var c = Cell(text, f);
            c.HorizontalAlignment = Element.ALIGN_RIGHT;
            return c;
        }
    }
}
