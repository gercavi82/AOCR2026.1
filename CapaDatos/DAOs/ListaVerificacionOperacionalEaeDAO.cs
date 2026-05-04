using System;
using System.Configuration;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    public class ListaVerificacionOperacionalEaeDAO
    {
        private readonly string _cs;
        private static readonly object SyncLock = new object();
        private static bool _schemaReady;

        public ListaVerificacionOperacionalEaeDAO()
        {
            var settings = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
                ? settings.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public ListaVerificacionOperacionalEae ObtenerUltimaPorInspeccion(int codigoInspeccion)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);
                return ObtenerUltimaPorInspeccionInterno(cn, codigoInspeccion);
            }
        }

        public ListaVerificacionOperacionalEae ObtenerPorId(int codigoListaVerificacion)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);
                return ObtenerPorIdInterno(cn, codigoListaVerificacion);
            }
        }

        public ListaVerificacionOperacionalEae GuardarBorrador(ListaVerificacionOperacionalEae lista, int usuarioId)
        {
            if (lista == null)
            {
                throw new ArgumentNullException(nameof(lista));
            }

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                var ultima = ObtenerUltimaPorInspeccionInterno(cn, lista.CodigoInspeccion);
                if (ultima != null && !ultima.Finalizado)
                {
                    const string updateSql = @"
                        UPDATE public.aocr_tblv_operacional_eae
                           SET estado_lista = @estado_lista,
                               nombre_eae = @nombre_eae,
                               numero_aoc_fecha_validez = @numero_aoc_fecha_validez,
                               direccion_estado_explotador = @direccion_estado_explotador,
                               direccion_estado_reconocimiento = @direccion_estado_reconocimiento,
                               tipos_aeronaves = @tipos_aeronaves,
                               tipo_operacion = @tipo_operacion,
                               fecha_lista = @fecha_lista,
                               inspector_responsable = @inspector_responsable,
                               cargo_inspector = @cargo_inspector,
                               resumen_verificacion = @resumen_verificacion,
                               observaciones_generales = @observaciones_generales,
                               resultado_general = @resultado_general,
                               items_json = @items_json,
                               updated_at = NOW(),
                               updated_by = @updated_by
                         WHERE codigo_lv = @codigo_lv;";

                    using (var cmd = new NpgsqlCommand(updateSql, cn))
                    {
                        Bind(cmd, lista, usuarioId);
                        cmd.Parameters.AddWithValue("@codigo_lv", ultima.CodigoListaVerificacion);
                        cmd.ExecuteNonQuery();
                    }

                    return ObtenerPorIdInterno(cn, ultima.CodigoListaVerificacion);
                }

                var version = ultima != null ? ultima.Version + 1 : 1;
                const string insertSql = @"
                    INSERT INTO public.aocr_tblv_operacional_eae
                    (
                        codigo_inspeccion,
                        version,
                        estado_lista,
                        nombre_eae,
                        numero_aoc_fecha_validez,
                        direccion_estado_explotador,
                        direccion_estado_reconocimiento,
                        tipos_aeronaves,
                        tipo_operacion,
                        fecha_lista,
                        inspector_responsable,
                        cargo_inspector,
                        resumen_verificacion,
                        observaciones_generales,
                        resultado_general,
                        items_json,
                        finalizado,
                        firmado_tecnico,
                        created_at,
                        created_by,
                        updated_at,
                        updated_by
                    )
                    VALUES
                    (
                        @codigo_inspeccion,
                        @version,
                        @estado_lista,
                        @nombre_eae,
                        @numero_aoc_fecha_validez,
                        @direccion_estado_explotador,
                        @direccion_estado_reconocimiento,
                        @tipos_aeronaves,
                        @tipo_operacion,
                        @fecha_lista,
                        @inspector_responsable,
                        @cargo_inspector,
                        @resumen_verificacion,
                        @observaciones_generales,
                        @resultado_general,
                        @items_json,
                        FALSE,
                        FALSE,
                        NOW(),
                        @created_by,
                        NOW(),
                        @updated_by
                    )
                    RETURNING codigo_lv;";

                using (var cmd = new NpgsqlCommand(insertSql, cn))
                {
                    Bind(cmd, lista, usuarioId);
                    cmd.Parameters.AddWithValue("@version", version);
                    var codigo = Convert.ToInt32(cmd.ExecuteScalar());
                    return ObtenerPorIdInterno(cn, codigo);
                }
            }
        }

        public void MarcarFinalizada(int codigoListaVerificacion, string rutaPdf, string estadoLista, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tblv_operacional_eae
                       SET ruta_pdf = @ruta_pdf,
                           estado_lista = @estado_lista,
                           finalizado = TRUE,
                           fecha_finalizacion = NOW(),
                           updated_at = NOW(),
                           updated_by = @updated_by
                     WHERE codigo_lv = @codigo_lv;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@ruta_pdf", (object)(rutaPdf ?? string.Empty));
                    cmd.Parameters.AddWithValue("@estado_lista", (object)(estadoLista ?? "FINALIZADA"));
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.Parameters.AddWithValue("@codigo_lv", codigoListaVerificacion);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RegistrarFirmaTecnico(int codigoListaVerificacion, string rutaDocumentoFirmado, string hashDocumento, DateTime fechaFirma, string usuarioFirma, string estadoLista, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                EnsureSchema(cn);

                const string sql = @"
                    UPDATE public.aocr_tblv_operacional_eae
                       SET ruta_documento_firmado = @ruta_documento_firmado,
                           hash_documento = @hash_documento,
                           firmado_tecnico = TRUE,
                           fecha_firma = @fecha_firma,
                           usuario_firma = @usuario_firma,
                           estado_lista = @estado_lista,
                           updated_at = NOW(),
                           updated_by = @updated_by
                     WHERE codigo_lv = @codigo_lv;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@ruta_documento_firmado", (object)(rutaDocumentoFirmado ?? string.Empty));
                    cmd.Parameters.AddWithValue("@hash_documento", (object)(hashDocumento ?? string.Empty));
                    cmd.Parameters.AddWithValue("@fecha_firma", fechaFirma);
                    cmd.Parameters.AddWithValue("@usuario_firma", (object)(usuarioFirma ?? string.Empty));
                    cmd.Parameters.AddWithValue("@estado_lista", (object)(estadoLista ?? "FIRMADA"));
                    cmd.Parameters.AddWithValue("@updated_by", usuarioId);
                    cmd.Parameters.AddWithValue("@codigo_lv", codigoListaVerificacion);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private ListaVerificacionOperacionalEae ObtenerUltimaPorInspeccionInterno(NpgsqlConnection cn, int codigoInspeccion)
        {
            const string sql = @"
                SELECT codigo_lv,
                       codigo_inspeccion,
                       version,
                       estado_lista,
                       nombre_eae,
                       numero_aoc_fecha_validez,
                       direccion_estado_explotador,
                       direccion_estado_reconocimiento,
                       tipos_aeronaves,
                       tipo_operacion,
                       fecha_lista,
                       inspector_responsable,
                       cargo_inspector,
                       resumen_verificacion,
                       observaciones_generales,
                       resultado_general,
                       items_json,
                       ruta_pdf,
                       ruta_documento_firmado,
                       hash_documento,
                       finalizado,
                       firmado_tecnico,
                       fecha_finalizacion,
                       fecha_firma,
                       usuario_firma,
                       created_at,
                       created_by,
                       updated_at,
                       updated_by
                  FROM public.aocr_tblv_operacional_eae
                 WHERE codigo_inspeccion = @codigo_inspeccion
              ORDER BY version DESC
                 LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_inspeccion", codigoInspeccion);
                using (var dr = cmd.ExecuteReader())
                {
                    return dr.Read() ? Map(dr) : null;
                }
            }
        }

        private ListaVerificacionOperacionalEae ObtenerPorIdInterno(NpgsqlConnection cn, int codigoListaVerificacion)
        {
            const string sql = @"
                SELECT codigo_lv,
                       codigo_inspeccion,
                       version,
                       estado_lista,
                       nombre_eae,
                       numero_aoc_fecha_validez,
                       direccion_estado_explotador,
                       direccion_estado_reconocimiento,
                       tipos_aeronaves,
                       tipo_operacion,
                       fecha_lista,
                       inspector_responsable,
                       cargo_inspector,
                       resumen_verificacion,
                       observaciones_generales,
                       resultado_general,
                       items_json,
                       ruta_pdf,
                       ruta_documento_firmado,
                       hash_documento,
                       finalizado,
                       firmado_tecnico,
                       fecha_finalizacion,
                       fecha_firma,
                       usuario_firma,
                       created_at,
                       created_by,
                       updated_at,
                       updated_by
                  FROM public.aocr_tblv_operacional_eae
                 WHERE codigo_lv = @codigo_lv
                 LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@codigo_lv", codigoListaVerificacion);
                using (var dr = cmd.ExecuteReader())
                {
                    return dr.Read() ? Map(dr) : null;
                }
            }
        }

        private static ListaVerificacionOperacionalEae Map(NpgsqlDataReader dr)
        {
            return new ListaVerificacionOperacionalEae
            {
                CodigoListaVerificacion = GetInt(dr, "codigo_lv"),
                CodigoInspeccion = GetInt(dr, "codigo_inspeccion"),
                Version = GetInt(dr, "version"),
                EstadoLista = GetString(dr, "estado_lista"),
                NombreEae = GetString(dr, "nombre_eae"),
                NumeroAocFechaValidez = GetString(dr, "numero_aoc_fecha_validez"),
                DireccionEstadoExplotador = GetString(dr, "direccion_estado_explotador"),
                DireccionEstadoReconocimiento = GetString(dr, "direccion_estado_reconocimiento"),
                TiposAeronaves = GetString(dr, "tipos_aeronaves"),
                TipoOperacion = GetString(dr, "tipo_operacion"),
                FechaLista = GetDateTime(dr, "fecha_lista"),
                InspectorResponsable = GetString(dr, "inspector_responsable"),
                CargoInspector = GetString(dr, "cargo_inspector"),
                ResumenVerificacion = GetString(dr, "resumen_verificacion"),
                ObservacionesGenerales = GetString(dr, "observaciones_generales"),
                ResultadoGeneral = GetString(dr, "resultado_general"),
                ItemsJson = GetString(dr, "items_json"),
                RutaPdf = GetString(dr, "ruta_pdf"),
                RutaDocumentoFirmado = GetString(dr, "ruta_documento_firmado"),
                HashDocumento = GetString(dr, "hash_documento"),
                Finalizado = GetBool(dr, "finalizado"),
                FirmadoTecnico = GetBool(dr, "firmado_tecnico"),
                FechaFinalizacion = GetDateTime(dr, "fecha_finalizacion"),
                FechaFirma = GetDateTime(dr, "fecha_firma"),
                UsuarioFirma = GetString(dr, "usuario_firma"),
                CreatedAt = GetDateTime(dr, "created_at"),
                CreatedBy = GetNullableInt(dr, "created_by"),
                UpdatedAt = GetDateTime(dr, "updated_at"),
                UpdatedBy = GetNullableInt(dr, "updated_by")
            };
        }

        private static void Bind(NpgsqlCommand cmd, ListaVerificacionOperacionalEae lista, int usuarioId)
        {
            cmd.Parameters.AddWithValue("@codigo_inspeccion", lista.CodigoInspeccion);
            cmd.Parameters.AddWithValue("@estado_lista", (object)(lista.EstadoLista ?? "BORRADOR"));
            cmd.Parameters.AddWithValue("@nombre_eae", (object)(lista.NombreEae ?? string.Empty));
            cmd.Parameters.AddWithValue("@numero_aoc_fecha_validez", (object)(lista.NumeroAocFechaValidez ?? string.Empty));
            cmd.Parameters.AddWithValue("@direccion_estado_explotador", (object)(lista.DireccionEstadoExplotador ?? string.Empty));
            cmd.Parameters.AddWithValue("@direccion_estado_reconocimiento", (object)(lista.DireccionEstadoReconocimiento ?? string.Empty));
            cmd.Parameters.AddWithValue("@tipos_aeronaves", (object)(lista.TiposAeronaves ?? string.Empty));
            cmd.Parameters.AddWithValue("@tipo_operacion", (object)(lista.TipoOperacion ?? string.Empty));
            cmd.Parameters.AddWithValue("@fecha_lista", (object)lista.FechaLista ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@inspector_responsable", (object)(lista.InspectorResponsable ?? string.Empty));
            cmd.Parameters.AddWithValue("@cargo_inspector", (object)(lista.CargoInspector ?? string.Empty));
            cmd.Parameters.AddWithValue("@resumen_verificacion", (object)(lista.ResumenVerificacion ?? string.Empty));
            cmd.Parameters.AddWithValue("@observaciones_generales", (object)(lista.ObservacionesGenerales ?? string.Empty));
            cmd.Parameters.AddWithValue("@resultado_general", (object)(lista.ResultadoGeneral ?? string.Empty));
            cmd.Parameters.AddWithValue("@items_json", (object)(lista.ItemsJson ?? "[]"));
            cmd.Parameters.AddWithValue("@created_by", usuarioId);
            cmd.Parameters.AddWithValue("@updated_by", usuarioId);
        }

        private static int GetInt(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return 0;
            }

            return dr.IsDBNull(ordinal) ? 0 : Convert.ToInt32(dr.GetValue(ordinal));
        }

        private static int? GetNullableInt(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return null;
            }

            return dr.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(dr.GetValue(ordinal));
        }

        private static string GetString(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return string.Empty;
            }

            return dr.IsDBNull(ordinal) ? string.Empty : Convert.ToString(dr.GetValue(ordinal));
        }

        private static bool GetBool(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return false;
            }

            return !dr.IsDBNull(ordinal) && Convert.ToBoolean(dr.GetValue(ordinal));
        }

        private static DateTime? GetDateTime(NpgsqlDataReader dr, string column)
        {
            int ordinal;
            if (!TryGetOrdinal(dr, column, out ordinal))
            {
                return null;
            }

            return dr.IsDBNull(ordinal) ? (DateTime?)null : Convert.ToDateTime(dr.GetValue(ordinal));
        }

        private static bool TryGetOrdinal(NpgsqlDataReader dr, string column, out int ordinal)
        {
            ordinal = -1;
            if (dr == null || string.IsNullOrWhiteSpace(column))
            {
                return false;
            }

            for (var index = 0; index < dr.FieldCount; index++)
            {
                if (string.Equals(dr.GetName(index), column, StringComparison.OrdinalIgnoreCase))
                {
                    ordinal = index;
                    return true;
                }
            }

            return false;
        }

        private static void EnsureSchema(NpgsqlConnection cn)
        {
            if (_schemaReady)
            {
                return;
            }

            lock (SyncLock)
            {
                if (_schemaReady)
                {
                    return;
                }

                const string sql = @"
                    CREATE TABLE IF NOT EXISTS public.aocr_tblv_operacional_eae
                    (
                        codigo_lv SERIAL PRIMARY KEY,
                        codigo_inspeccion INTEGER NOT NULL,
                        version INTEGER NOT NULL DEFAULT 1,
                        estado_lista VARCHAR(50) NOT NULL DEFAULT 'BORRADOR',
                        nombre_eae TEXT NULL,
                        numero_aoc_fecha_validez TEXT NULL,
                        direccion_estado_explotador TEXT NULL,
                        direccion_estado_reconocimiento TEXT NULL,
                        tipos_aeronaves TEXT NULL,
                        tipo_operacion TEXT NULL,
                        fecha_lista TIMESTAMP NULL,
                        inspector_responsable TEXT NULL,
                        cargo_inspector TEXT NULL,
                        resumen_verificacion TEXT NULL,
                        observaciones_generales TEXT NULL,
                        resultado_general VARCHAR(120) NULL,
                        items_json TEXT NULL,
                        ruta_pdf TEXT NULL,
                        ruta_documento_firmado TEXT NULL,
                        hash_documento VARCHAR(256) NULL,
                        finalizado BOOLEAN NOT NULL DEFAULT FALSE,
                        firmado_tecnico BOOLEAN NOT NULL DEFAULT FALSE,
                        fecha_finalizacion TIMESTAMP NULL,
                        fecha_firma TIMESTAMP NULL,
                        usuario_firma VARCHAR(250) NULL,
                        created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        created_by INTEGER NULL,
                        updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
                        updated_by INTEGER NULL
                    );

                    CREATE INDEX IF NOT EXISTS ix_aocr_tblv_operacional_eae_inspeccion
                        ON public.aocr_tblv_operacional_eae(codigo_inspeccion, version DESC);

                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS estado_lista VARCHAR(50) NOT NULL DEFAULT 'BORRADOR';
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS nombre_eae TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS numero_aoc_fecha_validez TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS direccion_estado_explotador TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS direccion_estado_reconocimiento TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS tipos_aeronaves TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS tipo_operacion TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS fecha_lista TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS inspector_responsable TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS cargo_inspector TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS resumen_verificacion TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS observaciones_generales TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS resultado_general VARCHAR(120) NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS items_json TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS ruta_pdf TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS ruta_documento_firmado TEXT NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS hash_documento VARCHAR(256) NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS finalizado BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS firmado_tecnico BOOLEAN NOT NULL DEFAULT FALSE;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS fecha_finalizacion TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS fecha_firma TIMESTAMP NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS usuario_firma VARCHAR(250) NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS created_by INTEGER NULL;
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP NOT NULL DEFAULT NOW();
                    ALTER TABLE public.aocr_tblv_operacional_eae ADD COLUMN IF NOT EXISTS updated_by INTEGER NULL;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.ExecuteNonQuery();
                }

                _schemaReady = true;
            }
        }
    }
}
