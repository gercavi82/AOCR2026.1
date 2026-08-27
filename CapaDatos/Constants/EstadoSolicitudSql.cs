using System;

using System.Collections.Generic;

using System.Linq;



namespace CapaDatos.Constants

{

    /// <summary>

    /// Normalización SQL única para estados de solicitud AOCR.

    /// Debe mantenerse alineada con <see cref="EstadoSolicitud.Normalizar"/>.

    /// </summary>

    public static class EstadoSolicitudSql

    {

        public const string RawNormalizedExpressionTemplate =

            "REPLACE(TRIM(TRANSLATE(UPPER(COALESCE({0}, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ')";



        public static string RawNormalizedExpression(string columnReference)

        {

            return string.Format(RawNormalizedExpressionTemplate, columnReference);

        }



        public static string CanonicalExpression(string columnReference)

        {

            var raw = RawNormalizedExpression(columnReference);

            return @"

CASE

    WHEN " + raw + @" IN (

        'PENDIENTE ASIGNACION RT', 'PENDIENTE ASIGNACION TECNICA', 'PENDIENTE ASIGNACION',

        'PENDIENTE ASIGNACION COORDINADOR', 'PENDIENTE ASIGNACION INSPECTOR'

    ) THEN 'PENDIENTE ASIGNACION RT'

    WHEN " + raw + @" IN ('PENDIENTE', 'BORRADOR', 'SOLICITUD CREADA') THEN 'PENDIENTE'

    WHEN " + raw + @" IN (

        'EN REVISION DOCUMENTAL', 'PENDIENTE REVISION DOCUMENTAL', 'PENDIENTE CARGA DOCUMENTAL RT',

        'ENVIADO COORDINADOR', 'EN REVISION COORDINADOR', 'EN REVISION COORDINADOR FINAL', 'ENVIADO', 'PREPARANDO',

        'DOCUMENTACION PENDIENTE', 'EN REVISION', 'EN REVISION INSPECTOR', 'EN REVISION AOCR'

    ) THEN 'EN REVISION'

    WHEN " + raw + @" IN ('DOCUMENTACION COMPLETA', 'DOCUMENTOS COMPLETOS') THEN 'DOCUMENTACION COMPLETA'

    WHEN " + raw + @" IN (

        'ACEPTACION DOCUMENTAL', 'ACEPTADO INSPECTOR', 'APROBADO POR INSPECTOR', 'DOCUMENTACION ACEPTADA'

    ) THEN 'ACEPTACION DOCUMENTAL'

    WHEN " + raw + @" IN ('REQUIERE INSPECCION') THEN 'REQUIERE INSPECCION'

    WHEN " + raw + @" IN ('SUBSANADA', 'SUBSANADO') THEN 'SUBSANADA'

    WHEN " + raw + @" IN (

        'INSPECCION ASIGNADA', 'INSPECTOR ASIGNADO', 'ENVIADO A INSPECTOR',

        'EN INSPECCION', 'INSPECCION PROGRAMADA', 'INSPECCION A PROGRAMAR'

    ) THEN 'EN INSPECCION'

    WHEN " + raw + @" IN ('AOCR EN ELABORACION') THEN 'AOCR EN ELABORACION'

    WHEN " + raw + @" IN (

        'AOCR EN REVISION', 'ENVIADO A JEFATURA', 'ENVIADO A LEGALIZACION'

    ) THEN 'AOCR EN REVISION'

    WHEN " + raw + @" IN ('VALIDADO', 'VALIDADO TECNICAMENTE', 'AOCR VALIDADO') THEN 'AOCR VALIDADO'

    WHEN " + raw + @" IN ('AOCR LEGALIZADO', 'LEGALIZADO', 'CERTIFICADO LEGALIZADO') THEN 'AOCR LEGALIZADO'

    WHEN " + raw + @" IN ('AOCR EMITIDO', 'AOCR EMITIDO RECIBIDO', 'AOCR EMITIDO/RECIBIDO', 'CERTIFICADO EMITIDO', 'AOCR ENTREGADO') THEN 'AOCR EMITIDO RECIBIDO'

    WHEN " + raw + @" IN ('FINALIZADO', 'FINALIZADA', 'CERRADA', 'CERRADO') THEN 'FINALIZADO'

    WHEN " + raw + @" IN ('ANULADA', 'ANULADO') THEN 'ANULADA'

    WHEN " + raw + @" IN ('FIRMADO COORDINADOR', 'AUTORIZACION FIRMADA') THEN 'FIRMADO COORDINADOR'

    WHEN " + raw + @" IN (

        'OBSERVADA', 'OBSERVADO', 'DEVUELTO', 'DEVUELTA', 'DEVUELTO CON OBSERVACIONES',

        'DEVUELTO RT', 'OBSERVADO JEFATURA', 'RECHAZADO', 'RECHAZADO POR DIRECCION'

    ) THEN 'OBSERVADA'

    ELSE " + raw + @"

END";

        }



        public static string ToSqlToken(string estado)

        {

            if (string.IsNullOrWhiteSpace(estado))

            {

                return "PENDIENTE";

            }



            var canonico = EstadoSolicitud.Normalizar(estado);

            return canonico

                .Trim()

                .ToUpperInvariant()

                .Replace("Á", "A")

                .Replace("É", "E")

                .Replace("Í", "I")

                .Replace("Ó", "O")

                .Replace("Ú", "U")

                .Replace("_", " ");

        }



        /// <summary>

        /// Estados canónicos SQL visibles en la bandeja de Coordinación pendiente de asignar inspector.

        /// </summary>

        public static IReadOnlyList<string> EstadosCoordinacionPendienteAsignacionSql { get; } =

            new[]

            {

                "EN REVISION",

                "PENDIENTE ASIGNACION RT",

                "DOCUMENTACION COMPLETA",

                "ACEPTACION DOCUMENTAL",

                "REQUIERE INSPECCION",

                "PENDIENTE",

                "DOCUMENTACION PENDIENTE"

            };



        /// <summary>

        /// Estados excluidos de la bandeja de asignación (cerrados, legalizados, ya en inspección activa).

        /// </summary>

        public static IReadOnlyList<string> EstadosExcluidosBandejaCoordinacionSql { get; } =

            new[]

            {

                "AOCR LEGALIZADO",

                "FINALIZADO",

                "ANULADA",

                "EN INSPECCION",

                "AOCR EN REVISION",

                "AOCR VALIDADO",

                "FIRMADO COORDINADOR"

            };



        public static IReadOnlyList<string> EstadosAsignacionInicialCanonicosSql { get; } =

            EstadosCoordinacionPendienteAsignacionSql.ToList();



        public static IReadOnlyList<string> ExpandirVariantesSql(IEnumerable<string> estadosCanonicos)

        {

            return (estadosCanonicos ?? Enumerable.Empty<string>())

                .Where(e => !string.IsNullOrWhiteSpace(e))

                .SelectMany(e =>

                {

                    var token = ToSqlToken(e);

                    return ExpandirAliasCoordinacion(token);

                })

                .Distinct(StringComparer.OrdinalIgnoreCase)

                .ToList();

        }



        private static IEnumerable<string> ExpandirAliasCoordinacion(string token)

        {

            if (string.IsNullOrWhiteSpace(token))

            {

                yield return "PENDIENTE";

                yield break;

            }



            yield return token;



            if (string.Equals(token, "EN REVISION", StringComparison.OrdinalIgnoreCase))

            {

                yield return "EN REVISION COORDINADOR";

                yield return "ENVIADO COORDINADOR";

            }

        }



        /// <summary>
        /// Expresión SQL: la solicitud ya tiene inspector principal asignado de forma efectiva.
        /// Solo referencia columnas presentes en el esquema (compatibilidad entre entornos).
        /// </summary>
        public static string ExpresionTieneInspectorEfectivo(
            string solicitudAlias,
            IReadOnlyCollection<string> columnasSolicitud,
            IReadOnlyCollection<string> columnasInspeccion)
        {
            var colsS = new HashSet<string>(columnasSolicitud ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var colsI = new HashSet<string>(columnasInspeccion ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var condiciones = new List<string>();

            if (colsS.Contains("codigo_tecnico"))
            {
                condiciones.Add("COALESCE(" + solicitudAlias + ".codigo_tecnico, 0) > 0");
            }

            if (colsS.Contains("tecnico_responsable_cedula"))
            {
                condiciones.Add("NULLIF(TRIM(COALESCE(" + solicitudAlias + ".tecnico_responsable_cedula, '')), '') IS NOT NULL");
            }

            if (colsS.Contains("tecnico_responsable_nombre"))
            {
                condiciones.Add("NULLIF(TRIM(COALESCE(" + solicitudAlias + ".tecnico_responsable_nombre, '')), '') IS NOT NULL");
            }

            var subCondiciones = new List<string>();
            if (colsI.Contains("codigo_inspector"))
            {
                subCondiciones.Add("COALESCE(i_asg.codigo_inspector, 0) > 0");
            }

            if (colsI.Contains("inspector_principal_cedula"))
            {
                subCondiciones.Add("NULLIF(TRIM(COALESCE(i_asg.inspector_principal_cedula, '')), '') IS NOT NULL");
            }

            if (colsI.Contains("inspector_principal_nombre"))
            {
                subCondiciones.Add("NULLIF(TRIM(COALESCE(i_asg.inspector_principal_nombre, '')), '') IS NOT NULL");
            }

            if (subCondiciones.Count > 0)
            {
                condiciones.Add(@"
    EXISTS (
        SELECT 1
        FROM aocr_tbinspeccion i_asg
        WHERE i_asg.codigo_solicitud = " + solicitudAlias + @".codigo_solicitud
          AND (" + string.Join(" OR ", subCondiciones) + @")
    )");
            }

            if (condiciones.Count == 0)
            {
                return "FALSE";
            }

            return "(\n    " + string.Join("\n    OR ", condiciones) + "\n)";
        }



        public static bool EstadoPermiteAsignacionInicial(string estado)

        {

            var token = ToSqlToken(estado);

            return EstadosCoordinacionPendienteAsignacionSql.Contains(token, StringComparer.OrdinalIgnoreCase);

        }

    }

}
