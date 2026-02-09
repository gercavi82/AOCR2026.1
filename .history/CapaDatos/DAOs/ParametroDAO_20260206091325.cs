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
            return new Parametro
            {
                CodigoParametro = r["codigoparametro"] != DBNull.Value ? Convert.ToInt32(r["codigoparametro"]) : 0,
                Clave = r["clave"]?.ToString(),
                Valor = r["valor"]?.ToString(),
                Descripcion = r["descripcion"]?.ToString(),
                Activo = r["activo"] != DBNull.Value && Convert.ToBoolean(r["activo"]),

                CreatedAt = r["createdat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["createdat"]) : null,
                CreatedBy = r["createdby"] != DBNull.Value ? (int?)Convert.ToInt32(r["createdby"]) : null,
                UpdatedAt = r["updatedat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["updatedat"]) : null,
                UpdatedBy = r["updatedby"] != DBNull.Value ? (int?)Convert.ToInt32(r["updatedby"]) : null,
                DeletedAt = r["deletedat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["deletedat"]) : null,
                DeletedBy = r["deletedby"] != DBNull.Value ? (int?)Convert.ToInt32(r["deletedby"]) : null
            };
        }

        // ==============================
        // Listar
        // ==============================
        public List<Parametro> ListarTodos()
        {
            var lista = new List<Parametro>();

            const string sql = @"
                SELECT codigoparametro, clave, valor, descripcion, activo,
                       createdat, createdby, updatedat, updatedby, deletedat, deletedby
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
                SELECT codigoparametro, clave, valor, descripcion, activo,
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
                SELECT codigoparametro, clave, valor, descripcion, activo,
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
                SELECT codigoparametro, clave, valor, descripcion, activo,
                       createdat, createdby, updatedat, updatedby, deletedat, deletedby
                FROM aocr_tbparametro
                WHERE clave = @clave AND deletedat IS NULL
                ORDER BY codigoparametro DESC
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
        // Métodos especializados para configuraciones
        // ==============================
        
        /// <summary>
        /// Obtiene valores de prueba/test desde parámetros
        /// </summary>
        public Dictionary<string, string> ObtenerValoresTest()
        {
            var valores = new Dictionary<string, string>();

            const string sql = @"
                SELECT clave, valor
                FROM aocr_tbparametro
                WHERE activo = TRUE AND deletedat IS NULL
                  AND (clave LIKE 'TEST_%' OR clave LIKE 'DEMO_%')
                ORDER BY clave;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var clave = rd["clave"]?.ToString();
                        var valor = rd["valor"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(clave))
                        {
                            valores[clave] = valor ?? "";
                        }
                    }
                }
            }

            // Valores por defecto si no existen en base de datos
            if (!valores.ContainsKey("TEST_OPERADOR_DEFECTO"))
                valores["TEST_OPERADOR_DEFECTO"] = "EMPRESA DEMO S.A.";
            
            if (!valores.ContainsKey("TEST_REPRESENTANTE_DEFECTO"))
                valores["TEST_REPRESENTANTE_DEFECTO"] = "Juan Carlos Pérez Demo";

            return valores;
        }

        /// <summary>
        /// Obtiene configuración específica para PDF
        /// </summary>
        public Dictionary<string, object> ObtenerConfiguracionPDF()
        {
            var config = new Dictionary<string, object>();

            const string sql = @"
                SELECT clave, valor
                FROM aocr_tbparametro
                WHERE activo = TRUE AND deletedat IS NULL
                  AND (clave LIKE 'PDF_%' OR clave LIKE 'ROTATIVA_%')
                ORDER BY clave;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var clave = rd["clave"]?.ToString();
                        var valor = rd["valor"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(clave))
                        {
                            // Intentar convertir valores numéricos
                            if (decimal.TryParse(valor, out decimal valorDecimal))
                            {
                                config[clave] = valorDecimal;
                            }
                            else if (bool.TryParse(valor, out bool valorBool))
                            {
                                config[clave] = valorBool;
                            }
                            else
                            {
                                config[clave] = valor ?? "";
                            }
                        }
                    }
                }
            }

            // Configuración por defecto
            if (!config.ContainsKey("PDF_FORMATO"))
                config["PDF_FORMATO"] = "A4";
            
            if (!config.ContainsKey("PDF_ORIENTACION"))
                config["PDF_ORIENTACION"] = "Portrait";

            return config;
        }

        /// <summary>
        /// Obtiene montos de demostración configurables
        /// </summary>
        public Dictionary<string, decimal> ObtenerMontosDemo()
        {
            var montos = new Dictionary<string, decimal>();

            const string sql = @"
                SELECT clave, valor
                FROM aocr_tbparametro
                WHERE activo = TRUE AND deletedat IS NULL
                  AND (clave LIKE 'MONTO_%' OR clave LIKE 'TARIFA_%')
                ORDER BY clave;";

            using (var cn = CrearConexion())
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var clave = rd["clave"]?.ToString();
                        var valor = rd["valor"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(clave) && decimal.TryParse(valor, out decimal valorDecimal))
                        {
                            montos[clave] = valorDecimal;
                        }
                    }
                }
            }

            // Montos por defecto si no existen en base de datos
            if (!montos.ContainsKey("MONTO_BASE"))
                montos["MONTO_BASE"] = 100.00m;
            
            if (!montos.ContainsKey("TARIFA_SERVICIO"))
                montos["TARIFA_SERVICIO"] = 25.00m;
            
            if (!montos.ContainsKey("MONTO_IVA"))
                montos["MONTO_IVA"] = 12.00m;

            return montos;
        }
    }
}
