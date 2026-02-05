using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    /// <summary>
    /// DAO de Parámetros para PostgreSQL
    /// Compatible .NET Framework 4.7.2
    /// </summary>
    public class ParametroDAO
    {
        // ==============================
        // Conexión directa desde config
        // ==============================
        private NpgsqlConnection CrearConexion()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en el config.");

            return new NpgsqlConnection(cs);
        }

        // ==============================
        // Mapeo
        // Ajusta nombres si tu tabla difiere
        // ==============================
        private Parametro MapParametro(IDataRecord r)
        {
            // Helper method to safely get column value
            object SafeGetColumn(string columnName)
            {
                try
                {
                    for (int i = 0; i < r.FieldCount; i++)
                    {
                        if (string.Equals(r.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return r[i];
                        }
                    }
                    return DBNull.Value;
                }
                catch
                {
                    return DBNull.Value;
                }
            }

            return new Parametro
            {
                // Columnas principales (siempre presentes)
                CodigoParametro = SafeGetColumn("codigo_parametro") != DBNull.Value ? Convert.ToInt32(SafeGetColumn("codigo_parametro")) : 0,
                Clave = SafeGetColumn("clave")?.ToString(),
                Valor = SafeGetColumn("valor")?.ToString(),
                Descripcion = SafeGetColumn("descripcion")?.ToString(),
                Activo = SafeGetColumn("activo") != DBNull.Value && Convert.ToBoolean(SafeGetColumn("activo")),

                // Nuevas columnas del esquema dinámico
                CodigoParametroStr = SafeGetColumn("codigoparametro")?.ToString(),
                ValorParametro = SafeGetColumn("valorparametro") != DBNull.Value ? (decimal?)Convert.ToDecimal(SafeGetColumn("valorparametro")) : null,
                DescripcionParametro = SafeGetColumn("descripcionparametro")?.ToString(),
                ActivoNuevo = SafeGetColumn("activo") != DBNull.Value ? (bool?)Convert.ToBoolean(SafeGetColumn("activo")) : null,
                CreatedAtNuevo = SafeGetColumn("createdat") != DBNull.Value ? (DateTime?)Convert.ToDateTime(SafeGetColumn("createdat")) : null,
                UpdatedAtNuevo = SafeGetColumn("updatedat") != DBNull.Value ? (DateTime?)Convert.ToDateTime(SafeGetColumn("updatedat")) : null,
                CreatedByStr = SafeGetColumn("createdby")?.ToString(),
                UpdatedByStr = SafeGetColumn("updatedby")?.ToString(),
                DeletedAtNuevo = SafeGetColumn("deletedat") != DBNull.Value ? (DateTime?)Convert.ToDateTime(SafeGetColumn("deletedat")) : null,
                DeletedByStr = SafeGetColumn("deletedby")?.ToString(),

                // Columnas legacy (compatibilidad hacia atrás - solo si existen)
                CreatedAt = SafeGetColumn("created_at") != DBNull.Value ? (DateTime?)Convert.ToDateTime(SafeGetColumn("created_at")) : null,
                CreatedBy = SafeGetColumn("created_by") != DBNull.Value ? (int?)Convert.ToInt32(SafeGetColumn("created_by")) : null,
                UpdatedAt = SafeGetColumn("updated_at") != DBNull.Value ? (DateTime?)Convert.ToDateTime(SafeGetColumn("updated_at")) : null,
                UpdatedBy = SafeGetColumn("updated_by") != DBNull.Value ? (int?)Convert.ToInt32(SafeGetColumn("updated_by")) : null,
                DeletedAt = SafeGetColumn("deletedat") != DBNull.Value ? (DateTime?)Convert.ToDateTime(SafeGetColumn("deletedat")) : null,
                DeletedBy = SafeGetColumn("deletedby") != DBNull.Value ? (int?)Convert.ToInt32(SafeGetColumn("deletedby")) : null
            };
        }

        // ==============================
        // Listar
        // ==============================
        public List<Parametro> ListarTodos()
        {
            var lista = new List<Parametro>();

            const string sql = @"
                SELECT codigo_parametro, clave, valor, descripcion, tipo, tipo_dato, categoria, editable, modificable,
                       created_at, updated_at, updated_by, codigoparametro, valorparametro, descripcionparametro,
                       activo, createdat, updatedat, createdby, updatedby, deletedat, deletedby
                FROM aocr_tbparametro
                ORDER BY clave;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapParametro(rd));
                }
            }

            return lista;
        }

        public List<Parametro> ListarActivos()
        {
            var lista = new List<Parametro>();

            const string sql = @"
                SELECT codigo_parametro, clave, valor, descripcion, activo,
                       codigoparametro, valorparametro, descripcionparametro,
                       createdat, createdby, updatedat, updatedby, deletedat, deletedby
                FROM aocr_tbparametro
                WHERE activo = TRUE AND deletedat IS NULL
                ORDER BY clave;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapParametro(rd));
                }
            }

            return lista;
        }

        // ==============================
        // Obtener por ID
        // ==============================
        public Parametro ObtenerPorId(int codigoParametro)
        {
            const string sql = @"
                SELECT codigo_parametro, clave, valor, descripcion, activo,
                       codigoparametro, valorparametro, descripcionparametro,
                       createdat, createdby, updatedat, updatedby, deletedat, deletedby
                FROM aocr_tbparametro
                WHERE codigoparametro = @id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@id", codigoParametro);

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                        return MapParametro(rd);
                }
            }

            return null;
        }

        // ==============================
        // Obtener por Clave
        // ==============================
        public Parametro ObtenerPorClave(string clave)
        {
            const string sql = @"
                SELECT codigo_parametro, clave, valor, descripcion, tipo, tipo_dato, categoria, editable, modificable,
                       created_at, updated_at, updated_by, codigoparametro, valorparametro, descripcionparametro,
                       activo, createdat, updatedat, createdby, updatedby, deletedat, deletedby
                FROM aocr_tbparametro
                WHERE (clave = @clave OR codigoparametro = @clave) AND deletedat IS NULL
                ORDER BY codigo_parametro DESC
                LIMIT 1;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@clave", clave ?? string.Empty);

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                        return MapParametro(rd);
                }
            }

            return null;
        }

        /// <summary>
        /// Obtiene parámetros cuya clave contenga un patrón específico
        /// Útil para obtener grupos de parámetros relacionados (ej: BANCO_*, TARIFA_*)
        /// </summary>
        /// <param name="patron">Patrón a buscar en la clave</param>
        /// <returns>Lista de parámetros que coinciden con el patrón</returns>
        public List<Parametro> ObtenerPorClavePattern(string patron)
        {
            var lista = new List<Parametro>();

            const string sql = @"
                SELECT codigoparametro, clave, valor, descripcion, activo,
                       createdat, createdby, updatedat, updatedby, deletedat, deletedby
                FROM aocr_tbparametro
                WHERE clave LIKE @patron AND deletedat IS NULL AND activo = TRUE
                ORDER BY clave;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@patron", $"{patron}%");

                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                        lista.Add(MapParametro(rd));
                }
            }

            return lista;
        }

        // ==============================
        // Crear
        // ==============================
        public bool Crear(Parametro p, int codigoUsuario)
        {
            const string sql = @"
                INSERT INTO aocr_tbparametro
                (clave, valor, descripcion, activo, createdat, createdby)
                VALUES
                (@clave, @valor, @descripcion, @activo, @createdat, @createdby);";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@clave", (object)p.Clave ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@valor", (object)p.Valor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@descripcion", (object)p.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@activo", p.Activo);
                cmd.Parameters.AddWithValue("@createdat", DateTime.Now);
                cmd.Parameters.AddWithValue("@createdby", codigoUsuario);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ==============================
        // Actualizar
        // ==============================
        public bool Actualizar(Parametro p, int codigoUsuario)
        {
            const string sql = @"
                UPDATE aocr_tbparametro
                SET clave = @clave,
                    valor = @valor,
                    descripcion = @descripcion,
                    activo = @activo,
                    updatedat = @updatedat,
                    updatedby = @updatedby
                WHERE codigoparametro = @id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@clave", (object)p.Clave ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@valor", (object)p.Valor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@descripcion", (object)p.Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@activo", p.Activo);
                cmd.Parameters.AddWithValue("@updatedat", DateTime.Now);
                cmd.Parameters.AddWithValue("@updatedby", codigoUsuario);
                cmd.Parameters.AddWithValue("@id", p.CodigoParametro);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ==============================
        // Eliminar Soft
        // ==============================
        public bool EliminarSoft(int codigoParametro, int codigoUsuario)
        {
            const string sql = @"
                UPDATE aocr_tbparametro
                SET deletedat = @dt,
                    deletedby = @db,
                    activo = FALSE
                WHERE codigoparametro = @id;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@dt", DateTime.Now);
                cmd.Parameters.AddWithValue("@db", codigoUsuario);
                cmd.Parameters.AddWithValue("@id", codigoParametro);

                cn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // ==============================
        // MÉTODOS ESPECÍFICOS PARA VALORES DE TEST Y CONFIGURACIÓN
        // ==============================

        /// <summary>
        /// Obtiene valores de test configurables para formularios JavaScript
        /// Elimina la dependencia de valores hardcodeados
        /// </summary>
        public Dictionary<string, string> ObtenerValoresTest()
        {
            var valores = new Dictionary<string, string>();

            var parametros = ObtenerPorClavePattern("TEST_");
            foreach (var param in parametros)
            {
                valores[param.Clave] = param.Valor;
            }

            // Valores por defecto si no están configurados
            if (!valores.ContainsKey("TEST_OPERADOR_DEFECTO"))
                valores["TEST_OPERADOR_DEFECTO"] = "EMPRESA DEMO S.A.";
            
            if (!valores.ContainsKey("TEST_REPRESENTANTE_DEFECTO"))
                valores["TEST_REPRESENTANTE_DEFECTO"] = "Juan Carlos Pérez Demo";
            
            if (!valores.ContainsKey("TEST_EMAIL_DEFECTO"))
                valores["TEST_EMAIL_DEFECTO"] = "demo@ejemplo-dgac.gob.ec";

            if (!valores.ContainsKey("TEST_RUC_DEFECTO"))
                valores["TEST_RUC_DEFECTO"] = "1790000000001";

            if (!valores.ContainsKey("TEST_TELEFONO_DEFECTO"))
                valores["TEST_TELEFONO_DEFECTO"] = "02-2234567";

            return valores;
        }

        /// <summary>
        /// Obtiene configuración para footers de PDF
        /// Elimina textos hardcodeados en generación de PDFs
        /// </summary>
        public Dictionary<string, string> ObtenerConfiguracionPDF()
        {
            var config = new Dictionary<string, string>();

            var parametros = ObtenerPorClavePattern("PDF_");
            foreach (var param in parametros)
            {
                config[param.Clave] = param.Valor;
            }

            // Valores por defecto si no están configurados
            if (!config.ContainsKey("PDF_FOOTER_LINEA_1"))
                config["PDF_FOOTER_LINEA_1"] = "Dirección General de Aviación Civil del Ecuador";
            
            if (!config.ContainsKey("PDF_FOOTER_LINEA_2"))
                config["PDF_FOOTER_LINEA_2"] = "Sistema AOCR - Autorización de Explotador de Servicios Aéreos";
            
            if (!config.ContainsKey("PDF_TITULO_PRINCIPAL"))
                config["PDF_TITULO_PRINCIPAL"] = "DIRECCIÓN GENERAL DE AVIACIÓN CIVIL";

            return config;
        }

        /// <summary>
        /// Obtiene montos de demostración configurables
        /// Elimina valores como $80.00 hardcodeados
        /// </summary>
        public Dictionary<string, decimal> ObtenerMontosDemo()
        {
            var montos = new Dictionary<string, decimal>();

            var parametros = ObtenerPorClavePattern("MONTO_DEMO_");
            foreach (var param in parametros)
            {
                if (decimal.TryParse(param.Valor, out decimal monto))
                {
                    montos[param.Clave] = monto;
                }
            }

            // Valores por defecto si no están configurados
            if (!montos.ContainsKey("MONTO_DEMO_DEFECTO"))
                montos["MONTO_DEMO_DEFECTO"] = 125.50m;
            
            if (!montos.ContainsKey("MONTO_IVA_DEMO_DEFECTO"))
                montos["MONTO_IVA_DEMO_DEFECTO"] = 15.06m;
            
            if (!montos.ContainsKey("MONTO_SUBTOTAL_DEMO_DEFECTO"))
                montos["MONTO_SUBTOTAL_DEMO_DEFECTO"] = 110.44m;

            return montos;
        }

        /// <summary>
        /// Obtiene parámetros de cálculo para órdenes de recaudación
        /// Elimina valores hardcodeados como $500 por estación, $80 por día, 8% admin
        /// </summary>
        public Dictionary<string, decimal> ObtenerParametrosCalculoOrden()
        {
            var parametros = new Dictionary<string, decimal>();

            var config = ObtenerPorClavePattern("CALCULO_");
            foreach (var param in config)
            {
                if (decimal.TryParse(param.Valor, out decimal valor))
                {
                    parametros[param.Clave] = valor;
                }
            }

            // Valores por defecto si no están configurados en la base de datos
            if (!parametros.ContainsKey("CALCULO_VALOR_POR_ESTACION"))
                parametros["CALCULO_VALOR_POR_ESTACION"] = 500m;
            
            if (!parametros.ContainsKey("CALCULO_VALOR_POR_DIA_VIATICO"))
                parametros["CALCULO_VALOR_POR_DIA_VIATICO"] = 80m;
            
            if (!parametros.ContainsKey("CALCULO_PORCENTAJE_GASTOS_ADMIN"))
                parametros["CALCULO_PORCENTAJE_GASTOS_ADMIN"] = 8m; // 8%

            return parametros;
        }
    }
}
