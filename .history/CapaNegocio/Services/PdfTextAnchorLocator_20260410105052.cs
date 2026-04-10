using System;
using System.Collections.Generic;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Busca texto ancla dentro de un PDF y devuelve la página y coordenadas reales
    /// para posicionar la firma digital sobre el bloque correcto.
    /// 
    /// Estrategia v2:  Cada plantilla contiene anclas ocultas (1px, blanco) dentro del
    ///     div.signature-slot.  El localizador busca esas anclas, que marcan la esquina
    ///     superior-izquierda del slot.  El rectángulo de firma se calcula hacia abajo
    ///     desde esa posición.
    /// 
    /// Si las anclas v2 no se encuentran, se intenta con anclas legacy (texto visible)
    /// como fallback.
    /// </summary>
    public static class PdfTextAnchorLocator
    {
        // ── Anclas v2 (ocultas dentro del slot — posición exacta) ──────────
        private static readonly Dictionary<string, string[]> AnclasV2 = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "INFORME_TECNICO_INSPECTOR", new[] { "ANCLA_FIRMA_INSPECTOR_DGAC" } },
            { "INFORME_TECNICO_DIRDAC",    new[] { "ANCLA_FIRMA_DIRDAC_JEFATURA" } },
            { "AOCR_FIRMANTE",             new[] { "ANCLA_FIRMA_AUTORIZACION_AOCR" } },
        };

        // ── Anclas legacy (texto visible — fallback) ────────────────────────
        private static readonly Dictionary<string, string[]> AnclasLegacy = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "INFORME_TECNICO_INSPECTOR", new[] { "FIRMA DEL INSPECTOR DGAC" } },
            { "INFORME_TECNICO_DIRDAC",    new[] { "FIRMA DEL DIRECTOR / JEFATURA" } },
            { "AOCR_FIRMANTE",             new[] { "FIRMA DE AUTORIZACION AOCR", "FIRMA DE AUTORIZACION" } },
        };

        // Dimensiones del rectángulo de firma según tipo de plantilla
        private const float AnchoInforme = 200f;   // ~50% columna A4 con márgenes
        private const float AltoInforme = 96f;
        private const float AnchoAocr = 160f;      // ~34% columna A4
        private const float AltoAocr = 96f;

        /// <summary>
        /// Intenta localizar el recuadro de firma buscando el texto ancla correspondiente al rol.
        /// Devuelve null si no encuentra el ancla.
        /// </summary>
        public static PdfAnchorResult BuscarAnclaPorRol(PdfReader reader, string rolFirmante)
        {
            if (reader == null || string.IsNullOrWhiteSpace(rolFirmante))
            {
                return null;
            }

            var rol = rolFirmante.Trim().ToUpperInvariant();

            // Intentar anclas v2 primero (posición exacta dentro del slot)
            string[] anclasV2;
            if (AnclasV2.TryGetValue(rol, out anclasV2))
            {
                var resultado = BuscarAnclas(reader, rol, anclasV2, esV2: true);
                if (resultado != null)
                {
                    System.Diagnostics.Trace.WriteLine(string.Format(
                        "[AnchorLocator] Ancla v2 encontrada. Rol={0}, Pagina={1}, Ancla=\"{2}\", Y={3:F1}",
                        rol, resultado.Pagina, resultado.AnclaUsada, resultado.PosY));
                    return resultado;
                }
            }

            // Fallback: anclas legacy (texto visible de encabezado)
            string[] anclasLeg;
            if (AnclasLegacy.TryGetValue(rol, out anclasLeg))
            {
                var resultado = BuscarAnclas(reader, rol, anclasLeg, esV2: false);
                if (resultado != null)
                {
                    System.Diagnostics.Trace.WriteLine(string.Format(
                        "[AnchorLocator] Ancla legacy encontrada. Rol={0}, Pagina={1}, Ancla=\"{2}\", Y={3:F1}",
                        rol, resultado.Pagina, resultado.AnclaUsada, resultado.PosY));
                    return resultado;
                }
            }

            System.Diagnostics.Trace.WriteLine(string.Format(
                "[AnchorLocator] Sin ancla. Rol={0}, Paginas={1}. Se usará fallback fijo.",
                rol, reader.NumberOfPages));
            return null;
        }

        private static PdfAnchorResult BuscarAnclas(PdfReader reader, string rol, string[] anclas, bool esV2)
        {
            for (var pagina = reader.NumberOfPages; pagina >= 1; pagina--)
            {
                var resultados = ExtraerPosicionesTexto(reader, pagina);
                if (resultados == null || resultados.Count == 0)
                {
                    continue;
                }

                foreach (var ancla in anclas)
                {
                    var match = resultados.FirstOrDefault(r =>
                        r.Texto != null && r.Texto.IndexOf(ancla, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (match != null)
                    {
                        var pageSize = reader.GetPageSize(pagina);
                        var rect = CalcularRectanguloFirma(match, pageSize, rol, esV2);
                        return new PdfAnchorResult
                        {
                            Pagina = pagina,
                            TextoEncontrado = match.Texto,
                            AnclaUsada = ancla,
                            PosX = match.X,
                            PosY = match.Y,
                            RectanguloFirma = rect,
                            PageSize = pageSize,
                            OrigenAncla = esV2 ? "V2" : "LEGACY"
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Calcula el rectángulo útil para insertar la firma.
        /// 
        /// Ancla v2: el texto oculto está en la esquina superior-izquierda del slot.
        ///     El rectángulo va DESDE esa posición hacia abajo.
        /// 
        /// Ancla legacy: el encabezado visible está ARRIBA del slot.
        ///     El rectángulo se coloca debajo del encabezado con un offset.
        /// </summary>
        private static Rectangle CalcularRectanguloFirma(TextPositionInfo ancla, Rectangle pageSize, string rol, bool esV2)
        {
            var esAocr = rol == "AOCR_FIRMANTE";
            var ancho = esAocr ? AnchoAocr : AnchoInforme;
            var alto = esAocr ? AltoAocr : AltoInforme;

            float left, top;

            if (esV2)
            {
                // Ancla v2: el texto está dentro del slot, marca su esquina superior-izquierda
                left = ancla.X;
                top = ancla.Y + 2f; // small offset up to cover the anchor text
            }
            else
            {
                // Ancla legacy: el encabezado está arriba del slot
                left = ancla.X;
                top = ancla.Y - ancla.FontSize - 14f; // debajo del header + gap "small" text
            }

            var bottom = top - alto;

            // Clamp dentro de la página
            if (left + ancho > pageSize.Width - 8f)
            {
                left = Math.Max(0f, pageSize.Width - ancho - 8f);
            }
            if (left < 0f) left = 0f;

            if (bottom < 0f)
            {
                bottom = 0f;
                top = bottom + alto;
            }

            if (top > pageSize.Height)
            {
                top = pageSize.Height;
                bottom = Math.Max(0f, top - alto);
            }

            return new Rectangle(left, bottom, left + ancho, top);
        }

        private static List<TextPositionInfo> ExtraerPosicionesTexto(PdfReader reader, int pagina)
        {
            try
            {
                var strategy = new TextPositionExtractionStrategy();
                PdfTextExtractor.GetTextFromPage(reader, pagina, strategy);
                return strategy.Resultados;
            }
            catch
            {
                return new List<TextPositionInfo>();
            }
        }

        /// <summary>
        /// Estrategia de extracción que captura texto junto con sus coordenadas.
        /// </summary>
        private sealed class TextPositionExtractionStrategy : ITextExtractionStrategy
        {
            public List<TextPositionInfo> Resultados { get; private set; }
            private readonly List<TextChunkInfo> _chunks;

            public TextPositionExtractionStrategy()
            {
                Resultados = new List<TextPositionInfo>();
                _chunks = new List<TextChunkInfo>();
            }

            public void BeginTextBlock() { }
            public void EndTextBlock()
            {
                AgruparChunks();
            }

            public void RenderText(TextRenderInfo renderInfo)
            {
                var text = renderInfo.GetText();
                if (string.IsNullOrEmpty(text)) return;

                var baseline = renderInfo.GetBaseline();
                var start = baseline.GetStartPoint();
                var end = baseline.GetEndPoint();

                _chunks.Add(new TextChunkInfo
                {
                    Text = text,
                    X = start[Vector.I1],
                    Y = start[Vector.I2],
                    EndX = end[Vector.I1],
                    FontSize = renderInfo.GetAscentLine().GetStartPoint()[Vector.I2] - start[Vector.I2]
                });
            }

            public void RenderImage(ImageRenderInfo renderInfo) { }

            public string GetResultantText()
            {
                AgruparChunks();
                return string.Join("\n", Resultados.Select(r => r.Texto));
            }

            private void AgruparChunks()
            {
                if (_chunks.Count == 0) return;

                var ordered = _chunks.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
                var linea = new List<TextChunkInfo> { ordered[0] };

                for (var i = 1; i < ordered.Count; i++)
                {
                    if (Math.Abs(ordered[i].Y - linea[0].Y) < 2f)
                    {
                        linea.Add(ordered[i]);
                    }
                    else
                    {
                        EmitirLinea(linea);
                        linea = new List<TextChunkInfo> { ordered[i] };
                    }
                }

                EmitirLinea(linea);
                _chunks.Clear();
            }

            private void EmitirLinea(List<TextChunkInfo> chunks)
            {
                if (chunks == null || chunks.Count == 0) return;

                var sorted = chunks.OrderBy(c => c.X).ToList();
                var textoCompleto = string.Join("", sorted.Select(c => c.Text));
                if (string.IsNullOrWhiteSpace(textoCompleto)) return;

                Resultados.Add(new TextPositionInfo
                {
                    Texto = textoCompleto.Trim(),
                    X = sorted[0].X,
                    Y = sorted[0].Y,
                    EndX = sorted[sorted.Count - 1].EndX,
                    FontSize = sorted[0].FontSize
                });
            }

            private sealed class TextChunkInfo
            {
                public string Text { get; set; }
                public float X { get; set; }
                public float Y { get; set; }
                public float EndX { get; set; }
                public float FontSize { get; set; }
            }
        }
    }

    public class TextPositionInfo
    {
        public string Texto { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float EndX { get; set; }
        public float FontSize { get; set; }
    }

    public class PdfAnchorResult
    {
        public int Pagina { get; set; }
        public string TextoEncontrado { get; set; }
        public string AnclaUsada { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public Rectangle RectanguloFirma { get; set; }
        public Rectangle PageSize { get; set; }
        public string OrigenAncla { get; set; }
    }
}
