using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using CapaDatos;
using CapaDatos.DAOs;
using CapaModelo;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class InspectorIdentityInfo
    {
        public HashSet<int> Ids { get; set; } = new HashSet<int>();
        public HashSet<string> Identificadores { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string Login { get; set; }
        public string CodigoUsuarioSesion { get; set; }
        public string IdentificadoresLog => string.Join(",", Identificadores.OrderBy(x => x));
    }

    public sealed class InspectorAsignadoInfo
    {
        public HashSet<int> Ids { get; set; } = new HashSet<int>();
        public HashSet<string> Identificadores { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string InspectorAsignadoRaw { get; set; }
        public string InspectorAsignadoUsuarioId { get; set; }
        public string IdentificadoresLog => string.Join(",", Identificadores.OrderBy(x => x));
    }

    public sealed class InspectorAsignacionEvaluacion
    {
        public bool EsInspectorAsignado { get; set; }
        public string Motivo { get; set; }
        public InspectorAsignadoInfo Asignado { get; set; } = new InspectorAsignadoInfo();
    }

    public sealed class InspectorIdentityService
    {
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao = new UsuarioInternoRTDAO();
        private readonly string _connectionString;

        public InspectorIdentityService()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString)
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public InspectorIdentityInfo ObtenerIdentidadInspector(int usuarioId, string login, string codigoUsuarioSesion)
        {
            var identidad = new InspectorIdentityInfo
            {
                Login = (login ?? string.Empty).Trim(),
                CodigoUsuarioSesion = (codigoUsuarioSesion ?? string.Empty).Trim()
            };

            AgregarNumero(identidad.Ids, usuarioId);
            AgregarIdentificador(identidad.Identificadores, usuarioId.ToString());
            AgregarIdentificador(identidad.Identificadores, identidad.Login);
            AgregarIdentificador(identidad.Identificadores, identidad.CodigoUsuarioSesion);
            AgregarNumeroParseado(identidad.Ids, identidad.Login);
            AgregarNumeroParseado(identidad.Ids, identidad.CodigoUsuarioSesion);

            try
            {
                var usuario = usuarioId > 0 ? UsuarioDAO.ObtenerPorId(usuarioId) : null;
                if (usuario == null && !string.IsNullOrWhiteSpace(identidad.Login))
                {
                    usuario = UsuarioDAO.ObtenerPorNombreUsuario(identidad.Login);
                }

                if (usuario != null)
                {
                    AgregarNumero(identidad.Ids, usuario.Id);
                    AgregarIdentificador(identidad.Identificadores, usuario.Id.ToString());
                    AgregarIdentificador(identidad.Identificadores, usuario.NombreUsuario);
                    AgregarIdentificador(identidad.Identificadores, usuario.CodigoUsuario);
                    AgregarNumeroParseado(identidad.Ids, usuario.NombreUsuario);
                    AgregarNumeroParseado(identidad.Ids, usuario.CodigoUsuario);
                }
            }
            catch
            {
                // No bloquear flujo por errores de catálogo de usuarios.
            }

            try
            {
                var inspectorActual = usuarioId > 0
                    ? _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(usuarioId)
                    : null;

                if (inspectorActual == null)
                {
                    var claveBusqueda = !string.IsNullOrWhiteSpace(identidad.CodigoUsuarioSesion)
                        ? identidad.CodigoUsuarioSesion
                        : identidad.Login;
                    if (!string.IsNullOrWhiteSpace(claveBusqueda))
                    {
                        inspectorActual = _usuarioInternoRtDao.ObtenerActivoPorCodigoUsuario(claveBusqueda)
                            ?? _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(claveBusqueda);
                    }
                }

                if (inspectorActual != null)
                {
                    AgregarNumero(identidad.Ids, inspectorActual.UsuarioId);
                    AgregarNumero(identidad.Ids, inspectorActual.TecnicoId);

                    AgregarIdentificador(identidad.Identificadores, inspectorActual.UsuarioId.HasValue ? inspectorActual.UsuarioId.Value.ToString() : string.Empty);
                    AgregarIdentificador(identidad.Identificadores, inspectorActual.TecnicoId.HasValue ? inspectorActual.TecnicoId.Value.ToString() : string.Empty);
                    AgregarIdentificador(identidad.Identificadores, inspectorActual.CodigoUsuario);
                    AgregarIdentificador(identidad.Identificadores, inspectorActual.Identificacion);
                    AgregarIdentificador(identidad.Identificadores, inspectorActual.UsuarioLogin);
                }
            }
            catch
            {
                // No bloquear flujo por errores de resolución de inspector.
            }

            return identidad;
        }

        public InspectorAsignacionEvaluacion EvaluarInspectorAsignado(
            int solicitudId,
            SolicitudAOCR solicitud,
            IEnumerable<Inspeccion> inspecciones,
            InspectorIdentityInfo identidad)
        {
            var evaluacion = new InspectorAsignacionEvaluacion();
            var asignado = ObtenerInspectorAsignado(solicitudId, solicitud, inspecciones);
            evaluacion.Asignado = asignado;

            if (identidad == null || (identidad.Ids.Count == 0 && identidad.Identificadores.Count == 0))
            {
                evaluacion.EsInspectorAsignado = false;
                evaluacion.Motivo = "No se pudo resolver la identidad institucional del inspector actual.";
                return evaluacion;
            }

            if (asignado == null || (asignado.Ids.Count == 0 && asignado.Identificadores.Count == 0))
            {
                evaluacion.EsInspectorAsignado = false;
                evaluacion.Motivo = "La solicitud no tiene un inspector asignado en los campos institucionales.";
                return evaluacion;
            }

            if (identidad.Ids.Intersect(asignado.Ids).Any())
            {
                evaluacion.EsInspectorAsignado = true;
                evaluacion.Motivo = "Coincidencia por identificador numérico.";
                return evaluacion;
            }

            if (identidad.Identificadores.Intersect(asignado.Identificadores, StringComparer.OrdinalIgnoreCase).Any())
            {
                evaluacion.EsInspectorAsignado = true;
                evaluacion.Motivo = "Coincidencia por login/cédula/código institucional.";
                return evaluacion;
            }

            evaluacion.EsInspectorAsignado = false;
            evaluacion.Motivo = "El inspector autenticado no coincide con los identificadores asignados a la solicitud.";
            return evaluacion;
        }

        public InspectorAsignadoInfo ObtenerInspectorAsignado(
            int solicitudId,
            SolicitudAOCR solicitud,
            IEnumerable<Inspeccion> inspecciones)
        {
            var info = new InspectorAsignadoInfo();
            var valoresRaw = new List<string>();
            var valoresId = new List<string>();

            if (solicitud != null)
            {
                AgregarNumero(info.Ids, solicitud.CodigoTecnico);
                AgregarIdentificador(info.Identificadores, solicitud.CodigoTecnico.HasValue ? solicitud.CodigoTecnico.Value.ToString() : string.Empty);
                AgregarIdentificador(info.Identificadores, solicitud.TecnicoResponsableCedula);
                AgregarIdentificador(info.Identificadores, solicitud.InspectorApoyoCedula);

                valoresRaw.Add(solicitud.TecnicoResponsableCedula);
                valoresRaw.Add(solicitud.InspectorApoyoCedula);
                valoresId.Add(solicitud.CodigoTecnico.HasValue ? solicitud.CodigoTecnico.Value.ToString() : string.Empty);
            }

            foreach (var inspeccion in inspecciones ?? Enumerable.Empty<Inspeccion>())
            {
                if (inspeccion == null)
                {
                    continue;
                }

                AgregarNumero(info.Ids, inspeccion.CodigoInspector);
                AgregarIdentificador(info.Identificadores, inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : string.Empty);
                AgregarIdentificador(info.Identificadores, inspeccion.InspectorPrincipalCedula);
                AgregarIdentificador(info.Identificadores, inspeccion.InspectorApoyoCedula);

                valoresRaw.Add(inspeccion.InspectorPrincipalCedula);
                valoresRaw.Add(inspeccion.InspectorApoyoCedula);
                valoresId.Add(inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : string.Empty);
            }

            foreach (var valor in LeerAsignacionesPersistidas(solicitudId))
            {
                AgregarIdentificador(info.Identificadores, valor.Value);
                AgregarNumeroParseado(info.Ids, valor.Value);

                if (valor.Key.EndsWith("_id", StringComparison.OrdinalIgnoreCase)
                    || valor.Key.EndsWith("_usuario_id", StringComparison.OrdinalIgnoreCase)
                    || valor.Key.IndexOf("codigo_inspector", StringComparison.OrdinalIgnoreCase) >= 0
                    || valor.Key.IndexOf("codigo_tecnico", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    valoresId.Add(valor.Value);
                }

                if (valor.Key.IndexOf("inspector", StringComparison.OrdinalIgnoreCase) >= 0
                    || valor.Key.IndexOf("tecnico", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    valoresRaw.Add(valor.Value);
                }
            }

            info.InspectorAsignadoRaw = valoresRaw
                .FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor))
                ?? string.Empty;
            info.InspectorAsignadoUsuarioId = valoresId
                .FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor))
                ?? string.Empty;

            return info;
        }

        public static string NormalizarCodigoInspector(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            var texto = valor
                .Trim()
                .ToUpperInvariant()
                .Replace('Á', 'A')
                .Replace('É', 'E')
                .Replace('Í', 'I')
                .Replace('Ó', 'O')
                .Replace('Ú', 'U')
                .Replace('Ü', 'U')
                .Replace('Ñ', 'N');

            var buffer = new List<char>(texto.Length);
            foreach (var caracter in texto)
            {
                if (char.IsLetterOrDigit(caracter))
                {
                    buffer.Add(caracter);
                }
            }

            return new string(buffer.ToArray());
        }

        private IDictionary<string, string> LeerAsignacionesPersistidas(int solicitudId)
        {
            var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (solicitudId <= 0 || string.IsNullOrWhiteSpace(_connectionString))
            {
                return resultado;
            }

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                    var columnasInspeccion = ObtenerColumnasTabla(cn, "aocr_tbinspeccion");
                    var columnasSolicitud = ObtenerColumnasTabla(cn, "aocr_tbsolicitud");

                    AgregarValoresAsignacion(
                        cn,
                        resultado,
                        "aocr_tbinspeccion",
                        "codigo_solicitud",
                        solicitudId,
                        new[]
                        {
                            "codigo_inspector",
                            "inspector_usuario_id",
                            "inspector_principal_id",
                            "inspector_principal_codigo",
                            "inspector_principal_cedula",
                            "inspector_apoyo_id",
                            "inspector_apoyo_codigo",
                            "inspector_apoyo_cedula"
                        },
                        columnasInspeccion,
                        "codigo_inspeccion");

                    AgregarValoresAsignacion(
                        cn,
                        resultado,
                        "aocr_tbsolicitud",
                        "codigo_solicitud",
                        solicitudId,
                        new[]
                        {
                            "codigo_tecnico",
                            "tecnico_responsable_cedula",
                            "inspector_apoyo_cedula",
                            "inspector_principal_id",
                            "inspector_principal_codigo",
                            "inspector_principal_cedula"
                        },
                        columnasSolicitud,
                        null);
                }
            }
            catch
            {
                // No bloquear autorización por disponibilidad/estructura de BD.
            }

            return resultado;
        }

        private static void AgregarValoresAsignacion(
            NpgsqlConnection cn,
            IDictionary<string, string> destino,
            string tabla,
            string columnaSolicitud,
            int solicitudId,
            IEnumerable<string> columnasObjetivo,
            HashSet<string> columnasDisponibles,
            string columnaOrdenDesc)
        {
            var columnas = (columnasObjetivo ?? Enumerable.Empty<string>())
                .Where(columna => columnasDisponibles.Contains(columna))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (columnas.Count == 0)
            {
                return;
            }

            var selectList = string.Join(", ", columnas.Select(columna => "COALESCE(" + columna + "::text, '') AS " + columna));
            var sql = "SELECT " + selectList + " FROM public." + tabla
                + " WHERE " + columnaSolicitud + " = @solicitudId";
            if (!string.IsNullOrWhiteSpace(columnaOrdenDesc) && columnasDisponibles.Contains(columnaOrdenDesc))
            {
                sql += " ORDER BY " + columnaOrdenDesc + " DESC LIMIT 1";
            }
            else
            {
                sql += " LIMIT 1";
            }

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@solicitudId", solicitudId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                    {
                        return;
                    }

                    foreach (var columna in columnas)
                    {
                        var valor = rd[columna] == DBNull.Value ? string.Empty : (rd[columna] ?? string.Empty).ToString();
                        if (!string.IsNullOrWhiteSpace(valor))
                        {
                            destino[columna] = valor.Trim();
                        }
                    }
                }
            }
        }

        private static HashSet<string> ObtenerColumnasTabla(NpgsqlConnection cn, string tableName)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @table_name;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@table_name", tableName);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (!rd.IsDBNull(0))
                        {
                            columnas.Add(rd.GetString(0));
                        }
                    }
                }
            }

            return columnas;
        }

        private static void AgregarIdentificador(HashSet<string> identificadores, string valor)
        {
            var normalizado = NormalizarCodigoInspector(valor);
            if (identificadores == null || string.IsNullOrWhiteSpace(normalizado))
            {
                return;
            }

            identificadores.Add(normalizado);
        }

        private static void AgregarNumero(HashSet<int> ids, int? valor)
        {
            if (ids == null || !valor.HasValue || valor.Value <= 0)
            {
                return;
            }

            ids.Add(valor.Value);
        }

        private static void AgregarNumero(HashSet<int> ids, int valor)
        {
            if (ids == null || valor <= 0)
            {
                return;
            }

            ids.Add(valor);
        }

        private static void AgregarNumeroParseado(HashSet<int> ids, string valor)
        {
            if (ids == null || string.IsNullOrWhiteSpace(valor))
            {
                return;
            }

            int numero;
            if (int.TryParse(valor.Trim(), out numero) && numero > 0)
            {
                ids.Add(numero);
            }
        }
    }
}
