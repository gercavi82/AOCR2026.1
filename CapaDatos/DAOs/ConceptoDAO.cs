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

        public int Insertar(ConceptoModel concepto)
        {
            if (concepto == null) throw new ArgumentNullException(nameof(concepto));

            const string sql = @"
INSERT INTO aocr_or_concepto
(
  codigo,
  nombre,
  tipo_calculo,
  valor_base,
  porcentaje_admin,
  activo,
  orden,
  descripcion,
  por_estacion,
  por_dia,
  es_viatico
)
VALUES
(
  @Codigo,
  @Nombre,
  @TipoCalculo,
  @ValorBase,
  @PorcentajeAdmin,
  @Activo,
  @Orden,
  @Descripcion,
  @PorEstacion,
  @PorDia,
  @EsViatico
)
RETURNING id;";

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                return cn.ExecuteScalar<int>(sql, concepto);
            }
        }

        public bool Actualizar(ConceptoModel concepto)
        {
            if (concepto == null) throw new ArgumentNullException(nameof(concepto));
            if (concepto.Id <= 0) throw new ArgumentException("Id inválido", nameof(concepto));

            const string sql = @"
UPDATE aocr_or_concepto SET
  nombre = @Nombre,
  tipo_calculo = @TipoCalculo,
  valor_base = @ValorBase,
  porcentaje_admin = @PorcentajeAdmin,
  activo = @Activo,
  orden = @Orden,
  descripcion = @Descripcion,
  por_estacion = @PorEstacion,
  por_dia = @PorDia,
  es_viatico = @EsViatico
WHERE id = @Id;";

            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                return cn.Execute(sql, concepto) > 0;
            }
        }

        public void Upsert(ConceptoModel concepto)
        {
            if (concepto == null) return;

            var existing = ObtenerConceptoPorCodigo(concepto.Codigo);
            if (existing == null)
            {
                Insertar(concepto);
                return;
            }

            concepto.Id = existing.Id;
            Actualizar(concepto);
        }

        // ===== ALIASES PARA COMPATIBILIDAD (tu BL llama así) =====
        public ConceptoModel ObtenerPorId(int id) => ObtenerConceptoPorId(id);
        public ConceptoModel ObtenerPorCodigo(string codigo) => ObtenerConceptoPorCodigo(codigo);
    }
}
