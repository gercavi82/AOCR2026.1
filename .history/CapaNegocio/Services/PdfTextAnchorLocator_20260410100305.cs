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
    /// </summary>
    public static class PdfTextAnchorLocator
    {
        /// <summary>
        /// Mapas de rol de firmante → textos ancla que identifican el bloque de firma.
        /// El primero que aparezca determina la posición.
        /// </summary>
        private static readonly Dictionary<string, string[]> AnclaPorRol = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "INFORME_TECNICO_INSPECTOR", new[] { "Espacio reservado para firma digital del inspector", "FIRMA DEL INSPECTOR DGAC" } },
            { "INFORME_TECNICO_DIRDAC",    new[] { "Espacio reservado para firma digital de direccion", "FIRMA DEL DIRECTOR / JEFATURA" } },
            { "AOCR_FIRMANTE",             new[] { "Espacio reservado para firma digital", "FIRMA DE AUTORIZACION AOCR", "FIRMA DE AUTORIZACION" } },
        };

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

            string[] anclas;
            if (!AnclaPorRol.TryGetValue(rol, out anclas) || anclas == null || anclas.Length == 0)
            {
                return null;
            }

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
                        var rect = CalcularRectanguloFirma(match, pageSize);
                        return new PdfAnchorResult
                        {
                            Pagina = pagina,
                            TextoEncontrado = match.Texto,
                            AnclaUsada = ancla,
                            PosX = match.X,
                            PosY = match.Y,
                            RectanguloFirma = rect,
                            PageSize = pageSize
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Calcula el rectángulo útil para insertar la firma a partir de la posición del ancla.
        /// El rectángulo se ubica justo debajo del texto ancla, con un tamaño fijo
        /// adecuado para QR + texto de firma.
        /// </summary>
        private static Rectangle CalcularRectanguloFirma(TextPositionInfo ancla, Rectangle pageSize)
        {
            var anchoFirma = 148f;
            var altoFirma = 90f;

            var left = Math.Max(ancla.X, 0f);
            var top = ancla.Y - 2f;
            var bottom = top - altoFirma;

            if (left + anchoFirma > pageSize.Width)
            {
                left = Math.Max(0f, pageSize.Width - anchoFirma - 8f);
            }

            if (bottom < 0f)
            {
                bottom = 0f;
                top = bottom + altoFirma;
            }

            return new Rectangle(left, bottom, left + anchoFirma, top);
        }

        /// <summary>
        /// Extrae las posiciones de texto de una página del PDF usando iTextSharp.
        /// </summary>
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
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

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

            /// <summary>
            /// Agrupa chunks de texto de la misma línea (misma Y ± tolerancia) en
            /// un solo TextPositionInfo con el texto concatenado y la X del primer chunk.
            /// </summary>
            private void AgruparChunks()
            {
                if (_chunks.Count == 0)
                {
                    return;
                }

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
                if (chunks == null || chunks.Count == 0)
                {
                    return;
                }

                var sorted = chunks.OrderBy(c => c.X).ToList();
                var textoCompleto = string.Join("", sorted.Select(c => c.Text));
                if (string.IsNullOrWhiteSpace(textoCompleto))
                {
                    return;
                }

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
    }
}
