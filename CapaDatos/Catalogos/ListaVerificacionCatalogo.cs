using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using CapaModelo;
using Newtonsoft.Json;

namespace CapaDatos.Catalogos
{
    /// <summary>
    /// AC-07: Catálogo oficial común de preguntas, referencias y orientaciones normativas RDAC 129
    /// para la Lista de Verificación (LV/EAE).
    /// Centraliza la definición de los 14 ítems normativos para evitar duplicidad y resolver
    /// definitivamente el problema de LVs en blanco sin usar datos quemados.
    /// </summary>
    public class ListaVerificacionCatalogo
    {
        public sealed class OrientacionPlantillaDto
        {
            public string Texto { get; set; }
            public bool EsNotaOrientacion { get; set; }
            public bool EsLiteral { get; set; }
            public bool EsSubnumeral { get; set; }
        }

        public virtual List<ListaVerificacionOperacionalEaeItem> ObtenerCatalogoPreguntas()
        {
            var items = new List<ListaVerificacionOperacionalEaeItem>();
            var orden = 1;

            AgregarGrupo(items, ref orden, 1, "129.010 (a)\n129.100 (b) (1)", "129-1",
                "Ha presentado el explotador extranjero el formulario de solicitud de reconocimiento de su AOC?",
                null,
                CrearOrientacion("1. Verificar que el explotador haya completado los formularios de solicitud de la forma y manera que prescribe la AAC.", esSubnumeral: true),
                CrearOrientacion("Nota: Revisar el formulario y asegurarse que este debidamente completado.", esNotaOrientacion: true),
                CrearOrientacion("2. Verificar que los datos coincidan con los registros presentados.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 2, "129.100 (b) (5) y (7)", "129-2",
                "Ha presentado el explotador extranjero una descripcion de la operacion propuesta?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacion("1. Verificar que dentro de la descripcion se identifique si se trata de:", esSubnumeral: true),
                CrearOrientacion("a. operaciones regulares o no regulares.", esLiteral: true),
                CrearOrientacion("b. transporte de pasajeros/carga/otros.", esLiteral: true),
                CrearOrientacion("2. Verificar que en la descripcion del area de operaciones se identifiquen:", esSubnumeral: true),
                CrearOrientacion("a. los aeropuertos que se pretende utilizar.", esLiteral: true),
                CrearOrientacion("b. las areas especiales en que pretende operar; por ejemplo Cordillera de los Andes.", esLiteral: true),
                CrearOrientacion("c. las aprobaciones especificas requeridas.", esLiteral: true),
                CrearOrientacion("3. Verificar que el explotador haya presentado los tipos y las matriculas de las aeronaves sujetas a la operacion.", esSubnumeral: true),
                CrearOrientacion("Nota 1. Algunos Estados requieren que las aeronaves estén listadas en las OpSpecs. Si no están en las OpSpecs, el explotador debe presentarlas en otro documento, como una parte del manual de operaciones, o copias de los certificados de matrícula que atestigüen que el EAE es el explotador de dichas aeronaves.", esNotaOrientacion: true),
                CrearOrientacion("Nota 2. Verificar la nacionalidad de las aeronaves involucradas. Es posible que el Estado de matricula sea diferente del Estado del explotador. En este caso, identificar los Estados de matricula en la Casilla 14.", esNotaOrientacion: true));

            AgregarGrupo(items, ref orden, 3, "129.200 (a) (3) y (4)", "129-3",
                "Ha presentado el explotador extranjero los contratos de las aeronaves?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacion("1. Verificar que se presenten los contratos de las aeronaves afectadas a intercambio o arrendamiento de aeronave con tripulacion.", esSubnumeral: true),
                CrearOrientacion("2. En caso de arrendamiento de aeronave con tripulacion, verificar aprobacion de la AAC del Estado del explotador.", esSubnumeral: true),
                CrearOrientacion("3. Si existe acuerdo del Articulo 83 bis, verificar resumenes del acuerdo.", esSubnumeral: true),
                CrearOrientacion("4. Verificar certificados de cobertura de seguro para cada aeronave.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 4, "129.100 (b) (8)", "129-4",
                "Ha presentado el explotador extranjero los certificados de ruido de las aeronaves?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacion("1. Verificar que el certificado de ruido y documentos tecnicos de respaldo esten de acuerdo al Anexo 16, Volumen 1.", esSubnumeral: true),
                CrearOrientacion("2. Verificar que la homologacion acustica fue otorgada o convalidada por el Estado de matricula y se encuentre vigente.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 5, "129.100 (b) (2)", "129-5",
                "Ha presentado el explotador extranjero una copia de su AOC y las OpSpecs actualizadas que autorizan las operaciones solicitadas?",
                null,
                CrearOrientacion("1. Verificar que el AOC contiene la informacion requerida y autoriza las operaciones solicitadas.", esSubnumeral: true),
                CrearOrientacion("2. Verificar que las OpSpecs contienen la informacion requerida y autorizan las operaciones solicitadas.", esSubnumeral: true),
                CrearOrientacion("a. modelos de aeronave.", esLiteral: true),
                CrearOrientacion("b. tipo de transporte.", esLiteral: true),
                CrearOrientacion("c. area de operaciones.", esLiteral: true),
                CrearOrientacion("d. aprobaciones especificas.", esLiteral: true),
                CrearOrientacion("e. matriculas y aerodromos, si aplica.", esLiteral: true));

            AgregarGrupo(items, ref orden, 6, "129.100 (b) (3)", "129-6",
                "Ha presentado el explotador extranjero una copia de su manual de operaciones vigente?",
                null,
                CrearOrientacion("1. Verificar que el manual de operaciones este completo con el contenido requerido.", esSubnumeral: true),
                CrearOrientacion("2. Verificar indicaciones de aprobacion/aceptacion.", esSubnumeral: true),
                CrearOrientacion("3. Verificar que rutas y aerodromos contienen informacion de:", esSubnumeral: true),
                CrearOrientacion("a. comunicaciones.", esLiteral: true),
                CrearOrientacion("b. navegacion.", esLiteral: true),
                CrearOrientacion("c. aerodromos.", esLiteral: true),
                CrearOrientacion("d. aproximaciones.", esLiteral: true),
                CrearOrientacion("e. llegadas y salidas por instrumentos.", esLiteral: true),
                CrearOrientacion("4. Verificar procedimientos o entrenamientos para areas o aerodromos especiales.", esSubnumeral: true),
                CrearOrientacion("5. Verificar procedimientos especificos de despacho si el Estado los requiere.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 7, "129.100 (b) (9)", "129-7",
                "Ha presentado el explotador extranjero una copia del plan operacional de vuelo para cada ruta que pretende utilizar?",
                null,
                CrearOrientacion("1. Verificar que los planes operacionales de vuelo contienen modelo, pesos, combustible, rutas y aerodromos de alternativa.", esSubnumeral: true),
                CrearOrientacion("2. Verificar que los planes son adecuados frente a la descripcion de la operacion propuesta y las autorizaciones OpSpecs.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 8, "129.100 (b) (9)", "129-8",
                "Ha presentado el explotador extranjero informacion sobre los servicios de mantenimiento que pretende utilizar?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacion("1. Verificar extension y alcance del mantenimiento que pretende realizar.", esSubnumeral: true),
                CrearOrientacion("2. Verificar si usara estructura propia u OMA contratada.", esSubnumeral: true),
                CrearOrientacion("3. Verificar que la organizacion indicada esta autorizada y capacitada para los servicios pretendidos.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 9, "129.100 (b) (9)", "129-9",
                "Ha presentado el explotador extranjero los contratos o cartas de intencion de los servicios de tierra que pretende utilizar?",
                null,
                CrearOrientacion("1. Verificar que los servicios de tierra incluyan, si aplica:", esSubnumeral: true),
                CrearOrientacion("a. manipulacion de equipaje y carga.", esLiteral: true),
                CrearOrientacion("b. despacho y atencion a pasajeros.", esLiteral: true),
                CrearOrientacion("c. combustible y aceite.", esLiteral: true),
                CrearOrientacion("d. deshielo y antihielo.", esLiteral: true),
                CrearOrientacion("e. limpieza.", esLiteral: true),
                CrearOrientacion("f. aprovisionamiento del servicio de a bordo.", esLiteral: true),
                CrearOrientacion("2. Verificar que las organizaciones de servicios de tierra garantizan capacitacion adecuada, incluyendo mercancias peligrosas si aplica.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 10, "129.010 (b)\n129.100 (b) (9)", "129-10",
                "Ha presentado el explotador extranjero informacion sobre su sistema de gestion de seguridad operacional (SMS), en cumplimiento del Anexo 19?",
                null,
                CrearOrientacion("1. Verificar que el explotador tiene todos los elementos minimos presentes.", esSubnumeral: true),
                CrearOrientacion("2. Verificar que tiene implementado el programa de analisis de datos de vuelo como parte del SMS.", esSubnumeral: true),
                CrearOrientacion("3. De ser requerido por el Estado, verificar que el plan de respuesta ante emergencias es adecuado al pais y aeropuertos que pretende utilizar.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 11, "129.010 (b)\n129.100 (b) (9)", "129-11",
                "Ha presentado el explotador extranjero informacion sobre el cumplimiento del Anexo 1 por su tripulacion?",
                null,
                CrearOrientacion("1. Verificar que las licencias de tripulacion de vuelo son expedidas o convalidadas por el Estado de matricula de las aeronaves.", esSubnumeral: true),
                CrearOrientacion("2. Verificar que la tripulacion puede hablar y comprender el idioma utilizado para comunicaciones radiotelefonicas en el Estado.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 12, "129.010 (b)\n129.100 (b) (9)", "129-12",
                "Ha presentado el explotador extranjero informacion sobre alguna exencion emitida por su AAC que se aplique a las operaciones solicitadas?",
                null,
                CrearOrientacion("1. Verificar si la exencion no interfiere con el cumplimiento de los Anexos mencionados.", esSubnumeral: true),
                CrearOrientacion("2. Verificar anotaciones en certificados de aeronavegabilidad y/o licencias cuando hayan sido objeto de exencion.", esSubnumeral: true),
                CrearOrientacion("3. Verificar si la AAC del explotador solicito la aceptacion de la exencion a la AAC y si fue aceptada.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 13, "129.100 (b) (6)", "129-13",
                "Ha presentado el explotador extranjero una copia de su plan de seguridad de la aviacion?",
                null,
                CrearOrientacion("1. Verificar que el plan de seguridad de la aviacion, por su calidad de restricto, haya sido presentado a las personas autorizadas en la AAC u organismo apropiado del Estado.", esSubnumeral: true));

            AgregarGrupo(items, ref orden, 14, "129.100 (b) (4)", "129-14",
                "Ha presentado el explotador extranjero una copia del documento que autoriza los derechos de transito especificos, expedidos por la autoridad del Estado en que pretende operar?",
                null,
                CrearOrientacion("1. Verificar que existe una autorizacion de derecho de transito expedida por la AAC y otro organismo nacional competente.", esSubnumeral: true),
                CrearOrientacion("Nota. Si la autorizacion se emite despues de la evaluacion tecnica, puede considerarse no aplicable justificando en la Casilla 14.", esNotaOrientacion: true));

            return items;
        }

        public virtual List<ListaVerificacionOperacionalEaeItem> LeerRespuestasFormulario(NameValueCollection form)
        {
            var items = ObtenerCatalogoPreguntas();
            foreach (var item in items.Where(i => !i.EsNotaOrientacion))
            {
                var prefijo = "lvItem_" + item.Codigo + "_";
                item.EstadoCumplimiento = NormalizarCumplimiento(form?[prefijo + "cumplimiento"]);
                item.EstadoImplementacion = NormalizarImplementacion(form?[prefijo + "implementacion"]);
                var comentario = (form?[prefijo + "comentarios"] ?? string.Empty).Trim();
                item.PruebasNotasComentarios = comentario.Substring(0, Math.Min(3000, comentario.Length));
            }
            return items;
        }

        public virtual List<ListaVerificacionOperacionalEaeItem> HidratarRespuestas(string itemsJson, List<ListaVerificacionOperacionalEaeItem> itemsExistentes = null)
        {
            var plantilla = ObtenerCatalogoPreguntas();
            var respuestas = new List<ListaVerificacionOperacionalEaeItem>();

            if (itemsExistentes != null && itemsExistentes.Count > 0)
            {
                respuestas = itemsExistentes.Where(i => i != null && !string.IsNullOrWhiteSpace(i.Codigo)).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(itemsJson) && itemsJson.Trim() != "[]")
            {
                try
                {
                    respuestas = JsonConvert.DeserializeObject<List<ListaVerificacionOperacionalEaeItem>>(itemsJson) ?? new List<ListaVerificacionOperacionalEaeItem>();
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("No se pudieron leer las respuestas guardadas de la LV. No se reemplazarán por un borrador vacío.", ex);
                }
            }

            if (respuestas.Count == 0)
            {
                return plantilla;
            }

            if (respuestas.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Codigo))
                .GroupBy(item => item.Codigo.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                throw new InvalidOperationException("La LV contiene respuestas duplicadas para el mismo elemento.");

            var respuestasPorCodigo = respuestas
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Codigo))
                .GroupBy(item => item.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var preguntas = new HashSet<string>(plantilla.Select(item => item.CodigoPregunta), StringComparer.OrdinalIgnoreCase);
            var codigos = new HashSet<string>(plantilla.Select(item => item.Codigo), StringComparer.OrdinalIgnoreCase);
            if (respuestas.Any(item => item != null
                && (!string.IsNullOrWhiteSpace(item.EstadoCumplimiento) || !string.IsNullOrWhiteSpace(item.EstadoImplementacion)
                    || !string.IsNullOrWhiteSpace(item.PruebasNotasComentarios))
                && !codigos.Contains((item.Codigo ?? string.Empty).Trim())
                && !preguntas.Contains((item.Codigo ?? string.Empty).Trim())))
                throw new InvalidOperationException("La LV contiene respuestas de un catálogo no reconocido. Debe revisarse su correspondencia antes de editarla.");
            var respuestasPorPregunta = respuestas
                // Solo las respuestas históricas a nivel de pregunta se expanden.
                // Una orientación respondida no debe completar sus orientaciones hermanas.
                .Where(item => item != null && preguntas.Contains((item.Codigo ?? string.Empty).Trim()))
                .GroupBy(item => item.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in plantilla)
            {
                ListaVerificacionOperacionalEaeItem r = null;
                if (!respuestasPorCodigo.TryGetValue(item.Codigo, out r) && !string.IsNullOrWhiteSpace(item.CodigoPregunta))
                {
                    respuestasPorPregunta.TryGetValue(item.CodigoPregunta, out r);
                }

                if (r != null)
                {
                    item.EstadoCumplimiento = NormalizarCumplimiento(r.EstadoCumplimiento);
                    // Cumplimiento es por requisito; implementación es por orientación.
                    // Un registro histórico por pregunta no acredita todas sus orientaciones.
                    item.EstadoImplementacion = string.Equals((r.Codigo ?? string.Empty).Trim(), item.Codigo, StringComparison.OrdinalIgnoreCase)
                        ? NormalizarImplementacion(r.EstadoImplementacion) : string.Empty;
                    item.PruebasNotasComentarios = (r.PruebasNotasComentarios ?? string.Empty).Trim();
                }
            }

            return plantilla;
        }

        public static string SerializarRespuestas(IEnumerable<ListaVerificacionOperacionalEaeItem> items)
        {
            // El catálogo se define una sola vez. Cada LV persiste solo sus respuestas
            // por código estable; se siguen admitiendo los JSON históricos completos.
            return JsonConvert.SerializeObject((items ?? Enumerable.Empty<ListaVerificacionOperacionalEaeItem>())
                .Where(item => item != null && (!string.IsNullOrWhiteSpace(item.EstadoCumplimiento)
                    || !string.IsNullOrWhiteSpace(item.EstadoImplementacion)
                    || !string.IsNullOrWhiteSpace(item.PruebasNotasComentarios)))
                .Select(item => new { item.Codigo, item.CodigoPregunta, item.EstadoCumplimiento,
                    item.EstadoImplementacion, item.PruebasNotasComentarios }));
        }

        private static OrientacionPlantillaDto CrearOrientacion(string texto, bool esSubnumeral = false, bool esLiteral = false, bool esNotaOrientacion = false)
        {
            return new OrientacionPlantillaDto
            {
                Texto = texto,
                EsNotaOrientacion = esNotaOrientacion,
                EsLiteral = esLiteral,
                EsSubnumeral = esSubnumeral
            };
        }

        private static void AgregarGrupo(List<ListaVerificacionOperacionalEaeItem> items, ref int ordenGlobal, int grupoRequisitoId, string referencia, string codigoPregunta, string preguntaRequisito, string notaPregunta, params OrientacionPlantillaDto[] orientaciones)
        {
            if (items == null || orientaciones == null || orientaciones.Length == 0) return;

            var codigoPreguntaNormalizado = (codigoPregunta ?? string.Empty).Trim();
            var referenciaNormalizada = (referencia ?? string.Empty).Trim();
            var preguntaNormalizada = (preguntaRequisito ?? string.Empty).Trim();
            var notaNormalizada = (notaPregunta ?? string.Empty).Trim();
            var indiceOrientacion = 1;

            foreach (var orientacion in orientaciones.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Texto)))
            {
                items.Add(new ListaVerificacionOperacionalEaeItem
                {
                    Codigo = codigoPreguntaNormalizado + "-" + indiceOrientacion.ToString("00"),
                    CodigoPregunta = codigoPreguntaNormalizado,
                    Orden = ordenGlobal++,
                    GrupoRequisitoId = grupoRequisitoId,
                    Referencia = referenciaNormalizada,
                    PreguntaRequisito = preguntaNormalizada,
                    NotaPregunta = notaNormalizada,
                    OrientacionEvidencia = orientacion.Texto,
                    EsOrientacionIndependiente = true,
                    EsNotaOrientacion = orientacion.EsNotaOrientacion,
                    EsLiteral = orientacion.EsLiteral,
                    EsSubnumeral = orientacion.EsSubnumeral
                });
                indiceOrientacion++;
            }
        }

        public static string NormalizarCumplimiento(string resultado)
        {
            return (resultado ?? string.Empty).Trim().ToUpperInvariant();
        }

        public static string NormalizarImplementacion(string resultado)
        {
            return (resultado ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
