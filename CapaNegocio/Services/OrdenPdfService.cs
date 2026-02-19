using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using CapaDatos.DAOs;
using CapaModelo.DTOs;

namespace CapaNegocio.Services
{
    public interface IOrdenPdfService
    {
        byte[] GenerarPdfOrden(int ordenId, int usuarioId);
    }

    public class OrdenPdfService : IOrdenPdfService
    {
        private readonly IOrdenRecaudacionDAO _dao;

        public OrdenPdfService(IOrdenRecaudacionDAO dao)
        {
            _dao = dao ?? throw new ArgumentNullException(nameof(dao));
        }

        public byte[] GenerarPdfOrden(int ordenId, int usuarioId)
        {
            if (ordenId <= 0) throw new ArgumentException("ordenId inválido.");
            if (usuarioId <= 0) throw new ArgumentException("usuarioId inválido.");

            var dto = _dao.ObtenerDatosParaPdf(ordenId, usuarioId);
            if (dto == null) throw new Exception("No se encontró información para generar el PDF.");

            dto.CalcularTotales();

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(doc, ms);
                writer.CloseStream = false;

                doc.AddAuthor("AOCR");
                doc.AddCreator("AOCR - Sistema de recaudación");
                doc.AddTitle("Orden de recaudación");
                doc.Open();

                var title = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                var normal = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var bold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

                doc.Add(new Paragraph("ORDEN DE RECAUDACIÓN - AOCR", title));
                doc.Add(new Paragraph($"Número: {dto.NumeroOrden}", bold));
                doc.Add(new Paragraph($"Fecha: {dto.FechaEmision:dd/MM/yyyy}", normal));
                if (!string.IsNullOrWhiteSpace(dto.LugarEmision))
                    doc.Add(new Paragraph($"Lugar: {dto.LugarEmision}", normal));

                doc.Add(new Paragraph(" ", normal));

                doc.Add(new Paragraph($"Solicitante/Compañía: {dto.NombreCompania}", normal));
                doc.Add(new Paragraph($"RUC/Cédula: {dto.Ruc}", normal));
                doc.Add(new Paragraph($"Email: {dto.Email}", normal));
                if (!string.IsNullOrWhiteSpace(dto.Telefono))
                    doc.Add(new Paragraph($"Teléfono: {dto.Telefono}", normal));

                doc.Add(new Paragraph(" ", normal));

                // Tabla detalle
                var table = new PdfPTable(4) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 15, 55, 10, 20 });

                table.AddCell(Cell("Código", bold));
                table.AddCell(Cell("Concepto", bold));
                table.AddCell(Cell("Cant.", bold));
                table.AddCell(Cell("Total", bold));

                foreach (var d in dto.Detalles)
                {
                    table.AddCell(Cell(d.CodigoConcepto, normal));
                    table.AddCell(Cell(d.NombreConcepto, normal));
                    table.AddCell(Cell(d.Cantidad.ToString(), normal));
                    table.AddCell(Cell(d.ValorTotal.ToString("N2"), normal));
                }

                doc.Add(table);
                doc.Add(new Paragraph(" ", normal));

                doc.Add(new Paragraph($"Subtotal: {dto.Subtotal:N2}", normal));
                doc.Add(new Paragraph($"Gastos Admin: {dto.ValorGastosAdmin:N2}", normal));
                doc.Add(new Paragraph($"TOTAL: {dto.Total:N2}", bold));
                doc.Add(new Paragraph($"TOTAL EN LETRAS: {dto.TotalEnLetras}", normal));

                if (!string.IsNullOrWhiteSpace(dto.Referencia))
                    doc.Add(new Paragraph($"Referencia: {dto.Referencia}", normal));

                if (!string.IsNullOrWhiteSpace(dto.Observacion))
                    doc.Add(new Paragraph($"Observación: {dto.Observacion}", normal));

                doc.Add(new Paragraph(" ", normal));

                if (!string.IsNullOrWhiteSpace(dto.NombreInspector))
                    doc.Add(new Paragraph($"Elaborado por: {dto.NombreInspector}", normal));
                if (!string.IsNullOrWhiteSpace(dto.CargoInspector))
                    doc.Add(new Paragraph($"Cargo: {dto.CargoInspector}", normal));

                doc.Close();
                writer.Close();

                return ms.ToArray();
            }
        }

        private PdfPCell Cell(string text, Font font)
        {
            return new PdfPCell(new Phrase(text ?? "", font))
            {
                Padding = 6,
                BorderColor = BaseColor.LIGHT_GRAY
            };
        }
    }
}
