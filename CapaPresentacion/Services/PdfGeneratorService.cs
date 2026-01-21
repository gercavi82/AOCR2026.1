using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using CapaModelo.DTOs;

namespace CapaPresentacion.Services
{
    public class PdfGeneratorService
    {
        public byte[] GenerarOrdenRecaudacionPDF(OrdenRecaudacionPdfDto model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.LETTER, 50, 50, 70, 70);
                var writer = PdfWriter.GetInstance(document, ms);

                document.AddTitle($"Orden de Recaudación DGAC-{model.NumeroOrden}");
                document.AddSubject("Orden de Recaudación - DGAC");
                document.AddCreator("Sistema AOCR - DGAC");
                document.AddAuthor("DGAC");

                writer.PageEvent = new FooterEvent();

                document.Open();

                var fontTitulo = FontFactory.GetFont("Helvetica", 16, iTextSharp.text.Font.BOLD, BaseColor.BLACK);
                var fontSub = FontFactory.GetFont("Helvetica", 12, iTextSharp.text.Font.BOLD, BaseColor.DARK_GRAY);
                var fontNormal = FontFactory.GetFont("Helvetica", 10, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);
                var fontBold = FontFactory.GetFont("Helvetica", 10, iTextSharp.text.Font.BOLD, BaseColor.BLACK);

                // Header
                var titulo = new Paragraph("DIRECCIÓN GENERAL DE AVIACIÓN CIVIL", fontTitulo) { Alignment = Element.ALIGN_CENTER };
                document.Add(titulo);

                var sub = new Paragraph("ORDEN DE RECAUDACIÓN", fontSub) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 10f };
                document.Add(sub);

                var num = new Paragraph($"No. {model.NumeroOrden}", fontBold) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 15f };
                document.Add(num);

                // Info table
                var info = new PdfPTable(2) { WidthPercentage = 100 };
                info.SetWidths(new float[] { 1, 2 });

                AddRow(info, "Lugar y Fecha de Emisión:", $"{model.LugarEmision}, {model.FechaEmision:dd/MM/yyyy}", fontBold, fontNormal);
                AddRow(info, "Compañía:", model.NombreCompania, fontBold, fontNormal);
                AddRow(info, "RUC/Cédula:", model.Ruc, fontBold, fontNormal);
                AddRow(info, "Email:", model.Email, fontBold, fontNormal);
                AddRow(info, "Teléfono:", model.Telefono, fontBold, fontNormal);

                if (!string.IsNullOrWhiteSpace(model.Referencia))
                    AddRow(info, "Referencia:", model.Referencia, fontBold, fontNormal);

                info.SpacingAfter = 10f;
                document.Add(info);

                // Concepts
                var t = new Paragraph("DETALLE DEL SERVICIO OTORGADO", fontBold) { SpacingAfter = 8f };
                document.Add(t);

                var conceptos = new PdfPTable(2) { WidthPercentage = 100 };
                conceptos.SetWidths(new float[] { 3, 1 });

                AddHeader(conceptos, "CONCEPTO");
                AddHeader(conceptos, "VALOR");

                AddCell(conceptos, model.ConceptoPrincipal, fontNormal, Element.ALIGN_LEFT);
                AddCell(conceptos, $"USD$ {model.ValorBase:###,##0.00}", fontNormal, Element.ALIGN_RIGHT);

                if (model.Estaciones > 0)
                {
                    AddCell(conceptos, $"Inspecciones ({model.Estaciones} x $500.00)", fontNormal, Element.ALIGN_LEFT);
                    AddCell(conceptos, $"USD$ {model.ValorInspecciones:###,##0.00}", fontNormal, Element.ALIGN_RIGHT);
                }

                if (model.Dias > 0)
                {
                    AddCell(conceptos, $"Viáticos ({model.Dias} x $80.00)", fontNormal, Element.ALIGN_LEFT);
                    AddCell(conceptos, $"USD$ {model.ValorViaticos:###,##0.00}", fontNormal, Element.ALIGN_RIGHT);

                    AddCell(conceptos, "Gastos Administrativos (8% sobre viáticos)", fontNormal, Element.ALIGN_LEFT);
                    AddCell(conceptos, $"USD$ {model.ValorGastosAdmin:###,##0.00}", fontNormal, Element.ALIGN_RIGHT);
                }

                // total
                AddCell(conceptos, $"TOTAL: {model.TotalEnLetras}", fontBold, Element.ALIGN_LEFT);
                AddCell(conceptos, $"USD$ {model.Total:###,##0.00}", fontBold, Element.ALIGN_RIGHT);

                document.Add(conceptos);

                document.Add(new Paragraph("\n\n"));
                document.Add(new Paragraph("Los valores recaudados tienen el carácter de No Reembolsables.", fontNormal));

                document.Close();
                return ms.ToArray();
            }
        }

        private void AddRow(PdfPTable table, string label, string value, iTextSharp.text.Font bold, iTextSharp.text.Font normal)
        {
            var c1 = new PdfPCell(new Phrase(label, bold)) { Border = Rectangle.NO_BORDER, Padding = 5f };
            var c2 = new PdfPCell(new Phrase(value ?? "", normal)) { Border = Rectangle.NO_BORDER, Padding = 5f };
            table.AddCell(c1);
            table.AddCell(c2);
        }

        private void AddHeader(PdfPTable table, string text)
        {
            var f = FontFactory.GetFont("Helvetica", 10, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
            var cell = new PdfPCell(new Phrase(text, f))
            {
                BackgroundColor = new BaseColor(12, 124, 134),
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 8f
            };
            table.AddCell(cell);
        }

        private void AddCell(PdfPTable table, string text, iTextSharp.text.Font font, int align)
        {
            var cell = new PdfPCell(new Phrase(text ?? "", font))
            {
                HorizontalAlignment = align,
                Padding = 6f
            };
            table.AddCell(cell);
        }

        private class FooterEvent : PdfPageEventHelper
        {
            public override void OnEndPage(PdfWriter writer, Document document)
            {
                var footer = new PdfPTable(1) { TotalWidth = 500f, LockedWidth = true };

                var f = FontFactory.GetFont("Helvetica", 7, iTextSharp.text.Font.NORMAL, BaseColor.GRAY);
                var p = new Paragraph($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} - Página {writer.PageNumber}", f)
                {
                    Alignment = Element.ALIGN_CENTER
                };

                var cell = new PdfPCell(p)
                {
                    Border = Rectangle.TOP_BORDER,
                    BorderColorTop = BaseColor.LIGHT_GRAY,
                    BorderWidthTop = 0.5f,
                    PaddingTop = 8f,
                    HorizontalAlignment = Element.ALIGN_CENTER
                };

                footer.AddCell(cell);
                footer.WriteSelectedRows(0, -1, document.Left, document.Bottom + 30, writer.DirectContent);
            }
        }
    }
}
