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
            _cs = ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
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
                return cn.QueryFirstOrDefault<ConceptoModel>(
                    "SELECT * FROM aocr_or_concepto WHERE id = @Id",
                    new { Id = id }
                );
            }
        }

        public ConceptoModel ObtenerConceptoPorCodigo(string codigo)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                return cn.QueryFirstOrDefault<ConceptoModel>(
                    "SELECT * FROM aocr_or_concepto WHERE codigo = @Codigo",
                    new { Codigo = codigo }
                );
            }
        }

        public int CrearConcepto(ConceptoModel c)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();

                var sql = @"
INSERT INTO aocr_or_concepto
(codigo, nombre, tipo_calculo, valor_base, porcentaje_admin, activo, orden, descripcion, por_estacion, por_dia, es_viatico)
VALUES
(@Codigo, @Nombre, @TipoCalculo, @ValorBase, @PorcentajeAdmin, @Activo, @Orden, @Descripcion, @PorEstacion, @PorDia, @EsViatico)
RETURNING id;";

                return cn.ExecuteScalar<int>(sql, c);
            }
        }

        public bool ActualizarConcepto(ConceptoModel c)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();

                var sql = @"
UPDATE aocr_or_concepto SET
codigo = @Codigo,
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

                return cn.Execute(sql, c) > 0;
            }
        }

        public bool DesactivarConcepto(int id)
        {
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                return cn.Execute("UPDATE aocr_or_concepto SET activo = false WHERE id = @Id", new { Id = id }) > 0;
            }
        }
        // ================== ALIASES PARA COMPATIBILIDAD ==================
        public ConceptoModel ObtenerPorId(int id)
        {
            return ObtenerConceptoPorId(id);
        }

        public ConceptoModel ObtenerPorCodigo(string codigo)
        {
            return ObtenerConceptoPorCodigo(codigo);
        }

    }
}
