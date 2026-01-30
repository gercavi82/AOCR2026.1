using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Configuration;
using Npgsql;
using Dapper;
using CapaDatos.Models;
using CapaModelo.DTOs;

namespace CapaDatos.DAOs
{
    public class OrdenRecaudacionDAO : IOrdenRecaudacionDAO
    {
        private readonly string _cn;

        public OrdenRecaudacionDAO()
        {
            _cn = ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
        }

        // ===================== Validaciones de flujo =====================

        public bool ExisteORGeneradaOPagada(int codigoUsuario)
        {
            if (codigoUsuario <= 0) return false;

            const string sql = @"
SELECT 1
FROM aocr_or_orden
WHERE codigo_usuario = @u
  AND UPPER(TRIM(estado)) IN ('GENERADA','ENVIADA','PAGADA')
LIMIT 1;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@u", codigoUsuario);
                    cn.Open();
                    return cmd.ExecuteScalar() != null;
                }
            }
            catch (Exception ex)
            {
                TraceError("ExisteORGeneradaOPagada", ex);
                return false;
            }
        }

        public bool ExisteORMinima(int codigoUsuario)
        {
            if (codigoUsuario <= 0) return false;

            const string sql = @"
SELECT 1
FROM aocr_or_orden
WHERE codigo_usuario = @u
LIMIT 1;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@u", codigoUsuario);
                    cn.Open();
                    return cmd.ExecuteScalar() != null;
                }
            }
            catch (Exception ex)
            {
                TraceError("ExisteORMinima", ex);
                return false;
            }
        }

        // ===================== Listados =====================

        public List<OrdenRecaudacionModel> ListarPorUsuario(int codigoUsuario, string estado)
        {
            return ObtenerOrdenes(codigoUsuario, estado);
        }

        public List<OrdenRecaudacionModel> ObtenerOrdenes(int? codigoUsuario, string estado)
        {
            var lista = new List<OrdenRecaudacionModel>();
            if (!codigoUsuario.HasValue || codigoUsuario.Value <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"ObtenerOrdenes: Usuario inválido - {codigoUsuario}");
                return lista;
            }

            string estadoFiltro = NormalizarEstado(estado);
            System.Diagnostics.Debug.WriteLine($"ObtenerOrdenes: Buscando órdenes para usuario {codigoUsuario.Value}, estado filtro: '{estadoFiltro}'");

            string sql = @"
SELECT
  id,
  codigo_usuario,
  codigo_solicitud,
  numero_orden,
  fecha_creacion,
  estado,
  observacion,
  subtotal,
  admin,
  total,
  lugar_emision,
  compania,
  ruc_cedula,
  correo,
  telefono,
  concepto_id
FROM aocr_or_orden
WHERE codigo_usuario = @u
";

            if (!string.IsNullOrEmpty(estadoFiltro))
                sql += " AND UPPER(TRIM(estado)) = @e ";

            sql += " ORDER BY fecha_creacion DESC;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@u", codigoUsuario.Value);
                    if (!string.IsNullOrEmpty(estadoFiltro))
                        cmd.Parameters.AddWithValue("@e", estadoFiltro);

                    System.Diagnostics.Debug.WriteLine($"ObtenerOrdenes: Ejecutando consulta SQL: {sql}");
                    cn.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        int count = 0;
                        while (r.Read())
                        {
                            lista.Add(MapOrden(r));
                            count++;
                        }
                        System.Diagnostics.Debug.WriteLine($"ObtenerOrdenes: Se encontraron {count} registros en la base de datos");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObtenerOrdenes: ERROR - {ex.Message}");
                TraceError("ObtenerOrdenes", ex);
            }

            System.Diagnostics.Debug.WriteLine($"ObtenerOrdenes: Retornando {lista.Count} órdenes");
            return lista;
        }

        public List<OrdenRecaudacionModel> BuscarOrdenes(string criterio, int? codigoUsuario)
        {
            var lista = new List<OrdenRecaudacionModel>();
            if (!codigoUsuario.HasValue || codigoUsuario.Value <= 0) return lista;

            var term = (criterio ?? "").Trim();
            if (term.Length == 0) return lista;

            // Búsqueda por: numero_orden, compania, ruc_cedula, correo, codigo_solicitud
            const string sql = @"
SELECT
  id, codigo_usuario, codigo_solicitud, numero_orden, fecha_creacion, estado,
  observacion, subtotal, admin, total, lugar_emision, compania, ruc_cedula, correo,
  telefono, concepto_id
FROM aocr_or_orden
WHERE codigo_usuario = @u
  AND (
        numero_orden ILIKE '%' || @q || '%'
     OR compania ILIKE '%' || @q || '%'
     OR ruc_cedula ILIKE '%' || @q || '%'
     OR correo ILIKE '%' || @q || '%'
     OR COALESCE(codigo_solicitud,'') ILIKE '%' || @q || '%'
  )
ORDER BY fecha_creacion DESC;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@u", codigoUsuario.Value);
                    cmd.Parameters.AddWithValue("@q", term);

                    cn.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            lista.Add(MapOrden(r));
                    }
                }
            }
            catch (Exception ex)
            {
                TraceError("BuscarOrdenes", ex);
            }

            return lista;
        }

        public Dictionary<string, object> ObtenerEstadisticas(int codigoUsuario)
        {
            // Retorna un diccionario útil para tu ViewBag.Estadisticas
            var d = new Dictionary<string, object>
            {
                ["total"] = 0,
                ["borrador"] = 0,
                ["generada"] = 0,
                ["enviada"] = 0,
                ["pagada"] = 0,
                ["anulada"] = 0,
                ["monto_total"] = 0m,
                ["monto_recaudado"] = 0m
            };

            if (codigoUsuario <= 0) return d;

            const string sql = @"
SELECT
  UPPER(TRIM(estado)) AS estado,
  COUNT(*) AS cnt,
  COALESCE(SUM(total),0) AS suma
FROM aocr_or_orden
WHERE codigo_usuario = @u
GROUP BY UPPER(TRIM(estado));";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@u", codigoUsuario);
                    cn.Open();

                    int totalCnt = 0;
                    decimal montoTotal = 0m;
                    decimal montoRecaudado = 0m;

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var estado = SafeString(r, "estado") ?? "";
                            var cnt = SafeInt(r, "cnt");
                            var suma = SafeDecimal(r, "suma");

                            totalCnt += cnt;
                            montoTotal += suma;

                            switch (estado)
                            {
                                case "BORRADOR": d["borrador"] = cnt; break;
                                case "GENERADA": d["generada"] = cnt; break;
                                case "ENVIADA": d["enviada"] = cnt; break;
                                case "PAGADA": d["pagada"] = cnt; montoRecaudado += suma; break;
                                case "ANULADA": d["anulada"] = cnt; break;
                            }
                        }
                    }

                    d["total"] = totalCnt;
                    d["monto_total"] = montoTotal;
                    d["monto_recaudado"] = montoRecaudado;
                }
            }
            catch (Exception ex)
            {
                TraceError("ObtenerEstadisticas", ex);
            }

            return d;
        }

        // ===================== Obtener por Id (con Detalles) =====================

        public OrdenRecaudacionModel ObtenerOrdenPorId(int id)
        {
            if (id <= 0) return null;

            const string sql = @"
SELECT
  id, codigo_usuario, codigo_solicitud, numero_orden, fecha_creacion, estado,
  observacion, subtotal, admin, total, lugar_emision, compania, ruc_cedula, correo,
  telefono, concepto_id
FROM aocr_or_orden
WHERE id = @id
LIMIT 1;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                {
                    cn.Open();
                    OrdenRecaudacionModel orden = null;

                    using (var cmd = new NpgsqlCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                                orden = MapOrden(r);
                        }
                    }

                    if (orden != null)
                        orden.Detalles = ObtenerDetallesOrden(cn, orden.Id);

                    return orden;
                }
            }
            catch (Exception ex)
            {
                TraceError("ObtenerOrdenPorId", ex);
                return null;
            }
        }

        private List<OrdenDetalleModel> ObtenerDetallesOrden(NpgsqlConnection cnAbierta, int ordenId)
        {
            var lista = new List<OrdenDetalleModel>();
            if (cnAbierta == null || cnAbierta.State != ConnectionState.Open) return lista;

            const string sql = @"
SELECT
  id,
  orden_id,
  concepto_id,
  concepto_codigo,
  concepto_nombre,
  descripcion,
  cantidad,
  valor_unitario,
  porcentaje_admin,
  subtotal,
  admin,
  total_linea
FROM aocr_or_orden_detalle
WHERE orden_id = @id
ORDER BY id;";

            using (var cmd = new NpgsqlCommand(sql, cnAbierta))
            {
                cmd.Parameters.AddWithValue("@id", ordenId);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new OrdenDetalleModel
                        {
                            Id = SafeInt(r, "id"),
                            OrdenId = SafeInt(r, "orden_id"),
                            ConceptoId = SafeInt(r, "concepto_id"),
                            ConceptoCodigo = SafeString(r, "concepto_codigo"),
                            ConceptoNombre = SafeString(r, "concepto_nombre"),
                            Descripcion = SafeString(r, "descripcion"),
                            Cantidad = SafeDecimal(r, "cantidad"),
                            ValorUnitario = SafeDecimal(r, "valor_unitario"),
                            PorcentajeAdmin = SafeDecimal(r, "porcentaje_admin"),
                            Subtotal = SafeDecimal(r, "subtotal"),
                            Admin = SafeDecimal(r, "admin"),
                            TotalLinea = SafeDecimal(r, "total_linea")
                        });
                    }
                }
            }

            return lista;
        }

        // ===================== Crear / Actualizar / Cambiar Estado =====================

        public int CrearOrden(OrdenRecaudacionModel orden)
        {
            if (orden == null) throw new ArgumentNullException(nameof(orden));
            if (orden.CodigoUsuario <= 0) throw new ArgumentException("CodigoUsuario inválido");
            if (string.IsNullOrWhiteSpace(orden.NumeroOrden)) throw new ArgumentException("NumeroOrden es requerido");

            const string sqlOrden = @"
INSERT INTO aocr_or_orden
(
  codigo_usuario,
  codigo_solicitud,
  numero_orden,
  fecha_creacion,
  estado,
  observacion,
  subtotal,
  admin,
  total,
  lugar_emision,
  compania,
  ruc_cedula,
  correo,
  telefono,
  concepto_id
)
VALUES
(
  @codigo_usuario,
  @codigo_solicitud,
  @numero_orden,
  COALESCE(@fecha_creacion, now()),
  @estado,
  @observacion,
  @subtotal,
  @admin,
  @total,
  @lugar_emision,
  @compania,
  @ruc_cedula,
  @correo,
  @telefono,
  @concepto_id
)
RETURNING id;";

            const string sqlDetalle = @"
INSERT INTO aocr_or_orden_detalle
(
  orden_id,
  concepto_id,
  concepto_codigo,
  concepto_nombre,
  descripcion,
  cantidad,
  valor_unitario,
  porcentaje_admin,
  subtotal,
  admin,
  total_linea
)
VALUES
(
  @orden_id,
  @concepto_id,
  @concepto_codigo,
  @concepto_nombre,
  @descripcion,
  @cantidad,
  @valor_unitario,
  @porcentaje_admin,
  @subtotal,
  @admin,
  @total_linea
);";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        int idOrden;
                        using (var cmd = new NpgsqlCommand(sqlOrden, cn, tx))
                        {
                            AddOrdenParams(cmd, orden);
                            idOrden = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        if (orden.Detalles != null)
                        {
                            foreach (var d in orden.Detalles)
                            {
                                using (var cmdD = new NpgsqlCommand(sqlDetalle, cn, tx))
                                {
                                    cmdD.Parameters.AddWithValue("@orden_id", idOrden);
                                    cmdD.Parameters.AddWithValue("@concepto_id", d.ConceptoId);
                                    cmdD.Parameters.AddWithValue("@concepto_codigo", (object)(d.ConceptoCodigo ?? "") ?? DBNull.Value);
                                    cmdD.Parameters.AddWithValue("@concepto_nombre", (object)(d.ConceptoNombre ?? "") ?? DBNull.Value);
                                    cmdD.Parameters.AddWithValue("@descripcion", (object)(d.Descripcion ?? "") ?? DBNull.Value);
                                    cmdD.Parameters.AddWithValue("@cantidad", d.Cantidad);
                                    cmdD.Parameters.AddWithValue("@valor_unitario", d.ValorUnitario);
                                    cmdD.Parameters.AddWithValue("@porcentaje_admin", d.PorcentajeAdmin);
                                    cmdD.Parameters.AddWithValue("@subtotal", d.Subtotal);
                                    cmdD.Parameters.AddWithValue("@admin", d.Admin);
                                    cmdD.Parameters.AddWithValue("@total_linea", d.TotalLinea);
                                    cmdD.ExecuteNonQuery();
                                }
                            }
                        }

                        tx.Commit();
                        return idOrden;
                    }
                }
            }
            catch (Exception ex)
            {
                TraceError("CrearOrden", ex);
                throw; // mejor re-lanzar para que el Controller muestre error real
            }
        }

        public bool ActualizarOrden(OrdenRecaudacionModel orden)
        {
            if (orden == null) throw new ArgumentNullException(nameof(orden));
            if (orden.Id <= 0) throw new ArgumentException("Id inválido");
            if (orden.CodigoUsuario <= 0) throw new ArgumentException("CodigoUsuario inválido");

            const string sqlUpdate = @"
UPDATE aocr_or_orden SET
  codigo_solicitud = @codigo_solicitud,
  numero_orden     = @numero_orden,
  estado           = @estado,
  observacion      = @observacion,
  subtotal         = @subtotal,
  admin            = @admin,
  total            = @total,
  lugar_emision    = @lugar_emision,
  compania         = @compania,
  ruc_cedula       = @ruc_cedula,
  correo           = @correo,
  telefono         = @telefono,
  concepto_id      = @concepto_id
WHERE id = @id
  AND codigo_usuario = @codigo_usuario;";

            const string sqlDelDetalles = @"DELETE FROM aocr_or_orden_detalle WHERE orden_id = @id;";

            const string sqlInsDetalle = @"
INSERT INTO aocr_or_orden_detalle
(
  orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion,
  cantidad, valor_unitario, porcentaje_admin, subtotal, admin, total_linea
)
VALUES
(
  @orden_id, @concepto_id, @concepto_codigo, @concepto_nombre, @descripcion,
  @cantidad, @valor_unitario, @porcentaje_admin, @subtotal, @admin, @total_linea
);";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        int rows;
                        using (var cmd = new NpgsqlCommand(sqlUpdate, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", orden.Id);
                            AddOrdenParams(cmd, orden);
                            rows = cmd.ExecuteNonQuery();
                        }

                        if (rows <= 0)
                        {
                            tx.Rollback();
                            return false;
                        }

                        using (var cmdDel = new NpgsqlCommand(sqlDelDetalles, cn, tx))
                        {
                            cmdDel.Parameters.AddWithValue("@id", orden.Id);
                            cmdDel.ExecuteNonQuery();
                        }

                        if (orden.Detalles != null)
                        {
                            foreach (var d in orden.Detalles)
                            {
                                using (var cmdD = new NpgsqlCommand(sqlInsDetalle, cn, tx))
                                {
                                    cmdD.Parameters.AddWithValue("@orden_id", orden.Id);
                                    cmdD.Parameters.AddWithValue("@concepto_id", d.ConceptoId);
                                    cmdD.Parameters.AddWithValue("@concepto_codigo", (object)(d.ConceptoCodigo ?? "") ?? DBNull.Value);
                                    cmdD.Parameters.AddWithValue("@concepto_nombre", (object)(d.ConceptoNombre ?? "") ?? DBNull.Value);
                                    cmdD.Parameters.AddWithValue("@descripcion", (object)(d.Descripcion ?? "") ?? DBNull.Value);
                                    cmdD.Parameters.AddWithValue("@cantidad", d.Cantidad);
                                    cmdD.Parameters.AddWithValue("@valor_unitario", d.ValorUnitario);
                                    cmdD.Parameters.AddWithValue("@porcentaje_admin", d.PorcentajeAdmin);
                                    cmdD.Parameters.AddWithValue("@subtotal", d.Subtotal);
                                    cmdD.Parameters.AddWithValue("@admin", d.Admin);
                                    cmdD.Parameters.AddWithValue("@total_linea", d.TotalLinea);
                                    cmdD.ExecuteNonQuery();
                                }
                            }
                        }

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                TraceError("ActualizarOrden", ex);
                return false;
            }
        }

        public bool CambiarEstadoOrden(int id, string nuevoEstado)
        {
            if (id <= 0) return false;
            var estado = NormalizarEstado(nuevoEstado);
            if (string.IsNullOrWhiteSpace(estado)) return false;

            const string sql = @"
UPDATE aocr_or_orden
SET estado = @e
WHERE id = @id;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@e", estado);
                    cn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                TraceError("CambiarEstadoOrden", ex);
                return false;
            }
        }

        // ===================== Pagos =====================

        public bool RegistrarPago(int idOrden, PagoModel pago)
        {
            if (idOrden <= 0 || pago == null) return false;

            // IMPORTANTE: tu controller trata "idOrden" como codigo_solicitud en aocr_tbpago
            const string sql = @"
INSERT INTO aocr_tbpago
(
  codigo_solicitud,
  numero_factura,
  monto,
  moneda,
  concepto,
  metodo_pago,
  estado,
  fecha_pago,
  fecha_validacion,
  validado_por,
  observaciones,
  comprobante_ruta
)
VALUES
(
  @codigo_solicitud,
  @numero_factura,
  @monto,
  @moneda,
  @concepto,
  @metodo_pago,
  @estado,
  @fecha_pago,
  @fecha_validacion,
  @validado_por,
  @observaciones,
  @comprobante_ruta
);";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", idOrden);
                    cmd.Parameters.AddWithValue("@numero_factura", (object)(pago.NumeroFactura ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@monto", pago.Monto);
                    cmd.Parameters.AddWithValue("@moneda", (object)(pago.Moneda ?? "USD") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@concepto", (object)(pago.Concepto ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@metodo_pago", (object)(pago.MetodoPago ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@estado", (object)(pago.Estado ?? "Pendiente") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_pago", (object)pago.FechaPago ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fecha_validacion", (object)pago.FechaValidacion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@validado_por", (object)(pago.ValidadoPor ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", (object)(pago.Observaciones ?? "") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@comprobante_ruta", (object)(pago.ComprobanteRuta ?? "") ?? DBNull.Value);

                    cn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                TraceError("RegistrarPago", ex);
                return false;
            }
        }

        public bool RegistrarPagoYActualizarEstado(int idOrden, PagoModel pago, string nuevoEstado)
        {
            if (idOrden <= 0 || pago == null) return false;
            var estado = NormalizarEstado(nuevoEstado);
            if (string.IsNullOrWhiteSpace(estado)) return false;

            const string sqlPago = @"
INSERT INTO aocr_tbpago
(
  codigo_solicitud,
  numero_factura,
  monto,
  moneda,
  concepto,
  metodo_pago,
  estado,
  fecha_pago,
  fecha_validacion,
  validado_por,
  observaciones,
  comprobante_ruta
)
VALUES
(
  @codigo_solicitud,
  @numero_factura,
  @monto,
  @moneda,
  @concepto,
  @metodo_pago,
  @estado,
  @fecha_pago,
  @fecha_validacion,
  @validado_por,
  @observaciones,
  @comprobante_ruta
);";

            const string sqlEstado = @"
UPDATE aocr_or_orden
SET estado = @e
WHERE id = @id;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        using (var cmd = new NpgsqlCommand(sqlPago, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@codigo_solicitud", idOrden);
                            cmd.Parameters.AddWithValue("@numero_factura", (object)(pago.NumeroFactura ?? "") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@monto", pago.Monto);
                            cmd.Parameters.AddWithValue("@moneda", (object)(pago.Moneda ?? "USD") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@concepto", (object)(pago.Concepto ?? "") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@metodo_pago", (object)(pago.MetodoPago ?? "") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@estado", (object)(pago.Estado ?? "Pendiente") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@fecha_pago", (object)pago.FechaPago ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@fecha_validacion", (object)pago.FechaValidacion ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@validado_por", (object)(pago.ValidadoPor ?? "") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@observaciones", (object)(pago.Observaciones ?? "") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@comprobante_ruta", (object)(pago.ComprobanteRuta ?? "") ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmdEstado = new NpgsqlCommand(sqlEstado, cn, tx))
                        {
                            cmdEstado.Parameters.AddWithValue("@id", idOrden);
                            cmdEstado.Parameters.AddWithValue("@e", estado);
                            cmdEstado.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                TraceError("RegistrarPagoYActualizarEstado", ex);
                return false;
            }
        }

        // ===================== Legacy DataTable (si aún lo usas) =====================

        public DataTable ObtenerOrdenesPorUsuario(int codigoUsuario)
        {
            var dt = new DataTable();
            if (codigoUsuario <= 0) return dt;

            const string sql = @"
SELECT *
FROM aocr_or_orden
WHERE codigo_usuario = @u
ORDER BY fecha_creacion DESC;";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@u", codigoUsuario);
                    using (var da = new NpgsqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                TraceError("ObtenerOrdenesPorUsuario", ex);
            }

            return dt;
        }

        // ===================== PDF =====================

        public OrdenRecaudacionPdfDto ObtenerDatosParaPdf(int ordenId, int usuarioId)
        {
            try
            {
                var orden = ObtenerOrdenPorId(ordenId);
                if (orden == null || orden.CodigoUsuario != usuarioId) return default(OrdenRecaudacionPdfDto);

                // Obtener información del usuario inspector
                var usuarioInspector = UsuarioDAO.ObtenerPorId(orden.CodigoUsuario);

                var dto = new OrdenRecaudacionPdfDto
                {
                    OrdenId = orden.Id,
                    NumeroOrden = orden.NumeroOrden,
                    FechaEmision = orden.FechaCreacion,
                    LugarEmision = orden.LugarEmision ?? "Quito, Ecuador",

                    NombreCompania = orden.Compania ?? "",
                    Ruc = orden.RucCedula ?? "",
                    Email = orden.Correo ?? "",
                    Telefono = orden.Telefono ?? "",

                    Referencia = $"Orden de Recaudación {orden.NumeroOrden}",
                    Observacion = orden.Observacion ?? "",

                    NombreInspector = usuarioInspector?.NombreCompleto ?? "Inspector",
                    CargoInspector = "Inspector de Aviación Civil",

                    // Valores de compatibilidad con PdfGeneratorService
                    ConceptoPrincipal = "Inspección y Certificación AOCR",
                    Estaciones = 1, // Valor por defecto
                    Dias = 1, // Valor por defecto
                    ValorInspecciones = 500.00m, // Inspecciones: precio fijo por estación
                    ValorViaticos = 0m, // Viáticos: se establece en 0 según el ejemplo
                    ValorBase = orden.Subtotal - 500.00m
                };

                // Mapear detalles de la orden
                if (orden.Detalles != null && orden.Detalles.Count > 0)
                {
                    foreach (var detalle in orden.Detalles)
                    {
                        dto.Detalles.Add(new OrdenRecaudacionPdfDetalleDto
                        {
                            CodigoConcepto = detalle.ConceptoCodigo ?? "",
                            NombreConcepto = detalle.ConceptoNombre ?? detalle.Descripcion ?? "",
                            Cantidad = (int)detalle.Cantidad,
                            ValorUnitario = detalle.ValorUnitario,
                            PorcentajeAdmin = detalle.PorcentajeAdmin,
                            SubtotalLinea = detalle.Subtotal,
                            AdminLinea = detalle.Admin,
                            ValorTotal = detalle.TotalLinea
                        });
                    }
                }

                // Calcular totales
                dto.CalcularTotales();

                return dto;
            }
            catch (Exception ex)
            {
                TraceError("ObtenerDatosParaPdf", ex);
                return default(OrdenRecaudacionPdfDto);
            }
        }

        // ===================== Wrappers compatibilidad =====================

        public OrdenRecaudacionModel ObtenerPorId(int id) => ObtenerOrdenPorId(id);
        public int Insertar(OrdenRecaudacionModel orden) => CrearOrden(orden);
        public bool Actualizar(OrdenRecaudacionModel orden) => ActualizarOrden(orden);
        public bool CambiarEstado(int id, string estado) => CambiarEstadoOrden(id, estado);

        // ===================== Helpers =====================

        private static void AddOrdenParams(NpgsqlCommand cmd, OrdenRecaudacionModel orden)
        {
            // Nota: @id se agrega aparte en update
            cmd.Parameters.AddWithValue("@codigo_usuario", orden.CodigoUsuario);
            cmd.Parameters.AddWithValue("@codigo_solicitud", (object)(orden.CodigoSolicitud ?? "") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@numero_orden", orden.NumeroOrden);
            cmd.Parameters.AddWithValue("@fecha_creacion", (object)orden.FechaCreacion ?? DBNull.Value);

            var estado = NormalizarEstado(orden.Estado) ?? "BORRADOR";
            cmd.Parameters.AddWithValue("@estado", estado);

            cmd.Parameters.AddWithValue("@observacion", (object)(orden.Observacion ?? "") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@subtotal", orden.Subtotal);
            cmd.Parameters.AddWithValue("@admin", orden.Admin);
            cmd.Parameters.AddWithValue("@total", orden.Total);
            cmd.Parameters.AddWithValue("@lugar_emision", (object)(orden.LugarEmision ?? "") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@compania", (object)(orden.Compania ?? "") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ruc_cedula", (object)(orden.RucCedula ?? "") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@correo", (object)(orden.Correo ?? "") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@telefono", (object)(orden.Telefono ?? "") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@concepto_id", (object)orden.ConceptoId ?? DBNull.Value);
        }

        private static OrdenRecaudacionModel MapOrden(IDataRecord r)
        {
            // OJO: ajusta nombres si tu Model usa otros
            return new OrdenRecaudacionModel
            {
                Id = SafeInt(r, "id"),
                CodigoUsuario = SafeInt(r, "codigo_usuario"),
                CodigoSolicitud = SafeString(r, "codigo_solicitud"),
                NumeroOrden = SafeString(r, "numero_orden"),
                FechaCreacion = SafeDateTime(r, "fecha_creacion") ?? DateTime.MinValue,
                Estado = SafeString(r, "estado"),
                Observacion = SafeString(r, "observacion"),
                Subtotal = SafeDecimal(r, "subtotal"),
                Admin = SafeDecimal(r, "admin"),
                Total = SafeDecimal(r, "total"),
                LugarEmision = SafeString(r, "lugar_emision"),
                Compania = SafeString(r, "compania"),
                RucCedula = SafeString(r, "ruc_cedula"),
                Correo = SafeString(r, "correo"),
                Telefono = SafeString(r, "telefono"),
                ConceptoId = SafeNullableInt(r, "concepto_id"),
                Detalles = new List<OrdenDetalleModel>()
            };
        }

        private static string NormalizarEstado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return null;
            var e = estado.Trim().ToUpperInvariant();

            switch (e)
            {
                case "BORRADOR":
                case "GENERADA":
                case "ENVIADA":
                case "PAGADA":
                case "ANULADA":
                case "COMPLETADA":
                case "ORDEN DE RECAUDACIÓN REQUERIDA":
                    return estado.Trim(); // Preserve original case for this status
                default:
                    return null;
            }
        }

        private static string SafeString(IDataRecord r, string col)
            => r[col] == DBNull.Value ? null : r[col].ToString();

        private static int SafeInt(IDataRecord r, string col)
            => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

        private static int? SafeNullableInt(IDataRecord r, string col)
            => r[col] == DBNull.Value ? (int?)null : Convert.ToInt32(r[col]);

        private static decimal SafeDecimal(IDataRecord r, string col)
            => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

        private static DateTime? SafeDateTime(IDataRecord r, string col)
            => r[col] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r[col]);

        private static void TraceError(string metodo, Exception ex)
        {
            // En producción: ideal enviar a tabla aocr_tblog o un logger (Serilog/NLog)
            System.Diagnostics.Trace.TraceError($"[OrdenRecaudacionDAO.{metodo}] {ex}");
        }
        public bool Ping()
        {
            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand("SELECT 1;", cn))
                {
                    cn.Open();
                    var r = cmd.ExecuteScalar();
                    return r != null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[OrdenRecaudacionDAO.Ping] " + ex);
                return false;
            }
        }

        // Método para obtener todas las órdenes (para roles administrativos)
        public List<OrdenRecaudacionModel> ObtenerTodasLasOrdenes(string estado = null)
        {
            var lista = new List<OrdenRecaudacionModel>();
            string estadoFiltro = NormalizarEstado(estado);
            System.Diagnostics.Debug.WriteLine($"ObtenerTodasLasOrdenes: Buscando todas las órdenes, estado filtro: '{estadoFiltro}'");

            string sql = @"
SELECT
  id,
  codigo_usuario,
  codigo_solicitud,
  numero_orden,
  fecha_creacion,
  estado,
  observacion,
  subtotal,
  admin,
  total,
  lugar_emision,
  compania,
  ruc_cedula,
  correo,
  telefono,
  concepto_id
FROM aocr_or_orden
";

            if (!string.IsNullOrEmpty(estadoFiltro))
            {
                sql += " WHERE UPPER(TRIM(estado)) = @estado";
            }

            sql += " ORDER BY fecha_creacion DESC";

            try
            {
                using (var cn = new NpgsqlConnection(_cn))
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    if (!string.IsNullOrEmpty(estadoFiltro))
                    {
                        cmd.Parameters.AddWithValue("@estado", estadoFiltro);
                    }
                    cn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(MapOrden(reader));
                        }
                    }
                }
                System.Diagnostics.Debug.WriteLine($"ObtenerTodasLasOrdenes: Encontradas {lista.Count} órdenes");
            }
            catch (Exception ex)
            {
                TraceError("ObtenerTodasLasOrdenes", ex);
            }

            return lista;
        }
    }
}
