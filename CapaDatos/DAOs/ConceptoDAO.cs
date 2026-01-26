using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Dapper;
using Npgsql;
using CapaDatos.Models;

namespace CapaDatos.DAOs
{
    public class ConceptoDAO
    {
        private readonly string _cs;

        public ConceptoDAO()
        {
            var cstr = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            if (cstr == null || string.IsNullOrWhiteSpace(cstr.ConnectionString))
                throw new ConfigurationErrorsException("No existe la cadena de conexión 'AOCRConnection' en web.config/app.config.");

            _cs = cstr.ConnectionString;
        }

        public List<ConceptoModel> ObtenerConceptos(bool soloActivos = true)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                var sql = "SELECT * FROM aocr_or_concepto";
                if (soloActivos) sql += " WHERE activo = true";
                sql += " ORDER BY orden, codigo";
                return cn.Query<ConceptoModel>(sql).ToList();
            }
        }

        public ConceptoModel ObtenerConceptoPorId(int id)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                const string sql = "SELECT * FROM aocr_or_concepto WHERE id = @Id";
                return cn.QueryFirstOrDefault<ConceptoModel>(sql, new { Id = id });
            }
        }

        public ConceptoModel ObtenerConceptoPorCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                const string sql = "SELECT * FROM aocr_or_concepto WHERE codigo = @Codigo";
                return cn.QueryFirstOrDefault<ConceptoModel>(sql, new { Codigo = codigo });
            }
        }

        // ===== ALIASES PARA COMPATIBILIDAD (tu BL llama así) =====
        public ConceptoModel ObtenerPorId(int id) => ObtenerConceptoPorId(id);
        public ConceptoModel ObtenerPorCodigo(string codigo) => ObtenerConceptoPorCodigo(codigo);
    }
}
