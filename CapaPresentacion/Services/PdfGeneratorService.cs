using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web;
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
                var document = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(document, ms);

                document.AddTitle($"Orden de Recaudacion DGAC-{model.NumeroOrden}");
                document.AddSubject("Orden de Recaudacion - DGAC");
                document.AddCreator("Sistema AOCR - DGAC");
                document.AddAuthor("DGAC");

                writer.PageEvent = new FooterEvent();

                document.Open();

                var fontTitulo = FontFactory.GetFont("Helvetica", 12, iTextSharp.text.Font.BOLD, BaseColor.BLACK);
                var fontSub = FontFactory.GetFont("Helvetica", 10, iTextSharp.text.Font.BOLD, BaseColor.BLACK);
                var fontNormal = FontFactory.GetFont("Helvetica", 9, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);
                var fontBold = FontFactory.GetFont("Helvetica", 9, iTextSharp.text.Font.BOLD, BaseColor.BLACK);
                var fontSmall = FontFactory.GetFont("Helvetica", 8, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);

                AddHeader(document, fontTitulo, fontSub, fontSmall);

                var num = new Paragraph($"No. {model.NumeroOrden}", fontBold)
                {
                    Alignment = Element.ALIGN_RIGHT,
                    SpacingAfter = 6f
                };
                document.Add(num);

                AddInfoTable(document, fontBold, fontNormal, model);
                AddConceptTable(document, fontBold, fontNormal, model);
                AddNotas(document, fontSmall);
                AddFirma(document, fontNormal, fontBold, model);

                document.Close();
                return ms.ToArray();
            }
        }

        private void AddHeader(Document document, iTextSharp.text.Font fontTitulo, iTextSharp.text.Font fontSub, iTextSharp.text.Font fontSmall)
        {
            var header = new PdfPTable(3) { WidthPercentage = 100 };
            header.SetWidths(new float[] { 1.2f, 3.6f, 1.2f });

            var escudo = TryLoadImage("~/Content/imganes/escudo-ecuador.jpg", 60f, 60f)
                         ?? TryLoadImage("~/Content/assets/imganes/escudo-ecuador.jpg", 60f, 60f);

            var logo = TryLoadImage("~/Content/imganes/logodgac.png", 70f, 70f)
                       ?? TryLoadImage("~/Content/assets/imganes/logodgac.png", 70f, 70f);

            header.AddCell(ImageCell(escudo));
            header.AddCell(TitleCell(fontTitulo, fontSmall));
            header.AddCell(ImageCell(logo));

            document.Add(header);

            var line = new Paragraph("ORDEN DE RECAUDACION", fontSub)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 6f,
                SpacingAfter = 6f
            };
            document.Add(line);
        }

        private PdfPCell TitleCell(iTextSharp.text.Font fontTitulo, iTextSharp.text.Font fontSmall)
        {
            var cell = new PdfPCell { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER };
            cell.AddElement(new Paragraph("REPUBLICA DEL ECUADOR", fontSmall) { Alignment = Element.ALIGN_CENTER });
            cell.AddElement(new Paragraph("DIRECCION GENERAL DE AVIACION CIVIL", fontTitulo) { Alignment = Element.ALIGN_CENTER });
            cell.AddElement(new Paragraph("DIRECCION DE CERTIFICACION AERONAUTICA Y VIGILANCIA CONTINUA", fontSmall) { Alignment = Element.ALIGN_CENTER });
            return cell;
        }

        private void AddInfoTable(Document document, iTextSharp.text.Font bold, iTextSharp.text.Font normal, OrdenRecaudacionPdfDto model)
        {
            var info = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 10f };
            info.SetWidths(new float[] { 2, 5 });

            var fecha = FormatFechaLarga(model.FechaEmision);
            var lugar = string.IsNullOrWhiteSpace(model.LugarEmision) ? "" : model.LugarEmision;
            var lugarFecha = string.IsNullOrWhiteSpace(lugar) ? fecha : (lugar + ", " + fecha);

            AddInfoRow(info, "Lugar y Fecha de Emision:", lugarFecha, bold, normal);
            AddInfoRow(info, "Nombres Completos o Nombre de Cia.", model.NombreCompania, bold, normal);
            AddInfoRow(info, "Numero de cedula o RUC", model.Ruc, bold, normal);
            AddInfoRow(info, "Direccion de correo electronico", model.Email, bold, normal);
            AddInfoRow(info, "Numero telefonico de contacto", model.Telefono, bold, normal);

            document.Add(info);
        }

        private void AddConceptTable(Document document, iTextSharp.text.Font bold, iTextSharp.text.Font normal, OrdenRecaudacionPdfDto model)
        {
            model.CalcularTotales();

            var conceptos = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 10f };
            conceptos.SetWidths(new float[] { 4, 1 });

            AddHeader(conceptos, "CONCEPTO");
            AddHeader(conceptos, "VALOR");

            AddSectionHeader(conceptos, "DETALLE DEL SERVICIO OTORGADO (DERECHOS POR):", bold);

            var items = BuildLineItems(model);
            foreach (var it in items)
            {
                if (it.IsSpacer)
                {
                    AddSpacerRow(conceptos, normal);
                    continue;
                }

                if (it.IsHeader)
                {
                    AddSectionHeader(conceptos, it.Text, bold);
                    continue;
                }

                AddLine(conceptos, it.Text, it.Amount, normal, bold, it.IsTotal);
            }

            document.Add(conceptos);
        }

        private void AddNotas(Document document, iTextSharp.text.Font font)
        {
            document.Add(new Paragraph(" ", font));
            document.Add(new Paragraph("(1) Resolucion 066/2010 de 21 julio de 2010 - Registro Oficial Edicion Especial No.61", font));
            document.Add(new Paragraph("(2) Los valores recaudados tienen el caracter de No Reembolsables.", font));
            document.Add(new Paragraph("(3) El pago de este rubro no garantiza que el Proceso sea satisfactorio si la compania no cumple con lo requerido.", font));
        }

        private void AddFirma(Document document, iTextSharp.text.Font normal, iTextSharp.text.Font bold, OrdenRecaudacionPdfDto model)
        {
            document.Add(new Paragraph(" ", normal));
            document.Add(new Paragraph(" ", normal));

            var linea = new Paragraph("_______________________________", normal) { Alignment = Element.ALIGN_LEFT };
            document.Add(linea);

            if (!string.IsNullOrWhiteSpace(model.NombreInspector))
                document.Add(new Paragraph(model.NombreInspector, bold));

            if (!string.IsNullOrWhiteSpace(model.CargoInspector))
                document.Add(new Paragraph(model.CargoInspector, normal));
        }

        private void AddInfoRow(PdfPTable table, string label, string value, iTextSharp.text.Font bold, iTextSharp.text.Font normal)
        {
            var c1 = new PdfPCell(new Phrase(label ?? "", bold)) { Padding = 6f };
            var c2 = new PdfPCell(new Phrase(value ?? "", normal)) { Padding = 6f };
            table.AddCell(c1);
            table.AddCell(c2);
        }

        private void AddHeader(PdfPTable table, string text)
        {
            var f = FontFactory.GetFont("Helvetica", 9, iTextSharp.text.Font.BOLD, BaseColor.BLACK);
            var cell = new PdfPCell(new Phrase(text ?? "", f))
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 6f
            };
            table.AddCell(cell);
        }

        private void AddSectionHeader(PdfPTable table, string text, iTextSharp.text.Font font)
        {
            var cell = new PdfPCell(new Phrase(text ?? "", font))
            {
                Colspan = 2,
                Padding = 6f
            };
            table.AddCell(cell);
        }

        private void AddLine(PdfPTable table, string text, decimal? amount, iTextSharp.text.Font normal, iTextSharp.text.Font bold, bool isTotal)
        {
            var fLeft = isTotal ? bold : normal;
            var fRight = isTotal ? bold : normal;

            var c1 = new PdfPCell(new Phrase(text ?? "", fLeft))
            {
                Padding = 6f,
                HorizontalAlignment = Element.ALIGN_LEFT
            };

            var monto = amount.HasValue ? FormatMonto(amount.Value) : "";
            var c2 = new PdfPCell(new Phrase(monto, fRight))
            {
                Padding = 6f,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };

            table.AddCell(c1);
            table.AddCell(c2);
        }

        private void AddSpacerRow(PdfPTable table, iTextSharp.text.Font font)
        {
            var cell = new PdfPCell(new Phrase(" ", font))
            {
                Colspan = 2,
                Padding = 4f
            };
            table.AddCell(cell);
        }

        private string FormatMonto(decimal value)
        {
            var culture = new CultureInfo("es-EC");
            return "USD$ " + value.ToString("N2", culture);
        }

        private string FormatFechaLarga(DateTime fecha)
        {
            var culture = new CultureInfo("es-EC");
            return fecha.ToString("d 'de' MMMM 'de' yyyy", culture);
        }

        private Image TryLoadImage(string virtualPath, float maxW, float maxH)
        {
            try
            {
                if (HttpContext.Current == null) return null;
                var path = HttpContext.Current.Server.MapPath(virtualPath);
                if (!File.Exists(path)) return null;
                var img = Image.GetInstance(path);
                img.ScaleToFit(maxW, maxH);
                return img;
            }
            catch
            {
                return null;
            }
        }

        private PdfPCell ImageCell(Image img)
        {
            if (img == null)
            {
                return new PdfPCell(new Phrase(""))
                {
                    Border = Rectangle.NO_BORDER
                };
            }

            return new PdfPCell(img)
            {
                Border = Rectangle.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 2f
            };
        }

        private List<PdfLineItem> BuildLineItems(OrdenRecaudacionPdfDto model)
        {
            var list = new List<PdfLineItem>();

            if (model.Detalles == null || model.Detalles.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(model.ConceptoPrincipal))
                {
                    list.Add(new PdfLineItem { Text = model.ConceptoPrincipal, Amount = model.Subtotal });
                }
            }
            else
            {
                foreach (var d in model.Detalles)
                {
                    var code = (d.CodigoConcepto ?? "").Trim().ToUpperInvariant();
                    var baseText = MapConceptoText(code, d.NombreConcepto);
                    var qtyText = d.Cantidad > 1 && d.ValorUnitario > 0 ? $" ({d.Cantidad} x {FormatMonto(d.ValorUnitario)})" : "";

                    list.Add(new PdfLineItem
                    {
                        Text = baseText + qtyText,
                        Amount = d.SubtotalLinea
                    });

                    if (d.AdminLinea > 0)
                    {
                        var pct = PercentText(d.PorcentajeAdmin);
                        list.Add(new PdfLineItem
                        {
                            Text = $"Gastos Administrativos ({pct})",
                            Amount = d.AdminLinea
                        });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Observacion))
            {
                list.Add(new PdfLineItem { IsSpacer = true });
                list.Add(new PdfLineItem { Text = "Refer.: " + model.Observacion, Amount = null });
            }

            list.Add(new PdfLineItem
            {
                Text = "TOTAL: " + model.TotalEnLetras,
                Amount = model.Total,
                IsTotal = true
            });

            return list;
        }

        private string MapConceptoText(string code, string nombre)
        {
            switch (code)
            {
                case "EMI_AOCR":
                    return "Emision de Especificaciones Operacionales ecuatorianas (Reconocimiento AOC)";
                case "REN_AOCR":
                    return "Renovacion de Especificaciones Operacionales ecuatorianas (Reconocimiento AOC)";
                case "MOD_AOCR_INC":
                    return "Modificacion por incremento de aeronave (distinto modelo y tipo)";
                case "MOD_AOCR_SIN_INC":
                    return "Modificacion o enmienda del AOCR sin incremento de equipo de vuelo";
                case "INSPECCION_EXT":
                    return "Inspecciones por Requerimiento del Operador";
                case "VIATICOS_INSPECTOR":
                    return "Pago de viaticos por comision de servicios";
                default:
                    return string.IsNullOrWhiteSpace(nombre) ? "Concepto" : nombre;
            }
        }

        private string PercentText(decimal value)
        {
            if (value <= 0) return "0%";
            var pct = value > 1m ? value : value * 100m;
            return pct.ToString("0.##") + "%";
        }

        private class FooterEvent : PdfPageEventHelper
        {
            public override void OnEndPage(PdfWriter writer, Document document)
            {
                var footer = new PdfPTable(1) { TotalWidth = 500f, LockedWidth = true };

                var f = FontFactory.GetFont("Helvetica", 7, iTextSharp.text.Font.NORMAL, BaseColor.GRAY);
                var p = new Paragraph($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} - Pagina {writer.PageNumber}", f)
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

        private class PdfLineItem
        {
            public string Text { get; set; }
            public decimal? Amount { get; set; }
            public bool IsTotal { get; set; }
            public bool IsHeader { get; set; }
            public bool IsSpacer { get; set; }
        }
    }
}
