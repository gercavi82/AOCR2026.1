using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Npgsql;
using CapaDatos.Models;
using CapaModelo.DTOs;

namespace CapaDatos.DAOs
{
    public class OrdenRecaudacionDAO : IOrdenRecaudacionDAO
    {
        private readonly string _connectionString;

        public OrdenRecaudacionDAO()
        {
            _connectionString = System.Configuration.ConfigurationManager
                .ConnectionStrings["AOCRConnection"].ConnectionString;
        }

        // ============================
        // VALIDACIONES
        // ============================

        public bool ExisteORGeneradaOPagada(int codigoUsuario)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = @"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM aocr_or_orden
                        WHERE codigo_usuario = @CodigoUsuario
                          AND estado IN ('GENERADA','PAGADA')
                    ) THEN TRUE ELSE FALSE END;";

                return cn.ExecuteScalar<bool>(sql, new { CodigoUsuario = codigoUsuario });
            }
        }

        public bool ExisteORMinima(int codigoUsuario)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = @"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM aocr_or_orden
                        WHERE codigo_usuario = @CodigoUsuario
                          AND estado = 'BORRADOR'
                    ) THEN TRUE ELSE FALSE END;";

                return cn.ExecuteScalar<bool>(sql, new { CodigoUsuario = codigoUsuario });
            }
        }

        // ============================
        // CRUD
        // ============================

        public List<OrdenRecaudacionModel> ObtenerOrdenes(int? codigoUsuario, string estado)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = @"
                    SELECT 
                        o.*,
                        u.nombreusuario,
                        u.correo
                    FROM aocr_or_orden o
                    LEFT JOIN usuario u ON o.codigo_usuario = u.idusuario
                    WHERE (@CodigoUsuario IS NULL OR o.codigo_usuario = @CodigoUsuario)
                      AND (@Estado IS NULL OR o.estado = @Estado)
                    ORDER BY o.fecha_creacion DESC;";

                var ordenes = cn.Query<OrdenRecaudacionModel>(sql, new
                {
                    CodigoUsuario = codigoUsuario,
                    Estado = estado
                }).ToList();

                // Cargar detalles + concepto
                for (int i = 0; i < ordenes.Count; i++)
                {
                    var ord = ordenes[i];
                    ord.Detalles = ObtenerDetallesOrden(ord.Id, cn);

                    if (ord.ConceptoId.HasValue)
                        ord.Concepto = ObtenerConcepto(ord.ConceptoId.Value, cn);
                }

                return ordenes;
            }
        }

        public OrdenRecaudacionModel ObtenerOrdenPorId(int id)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = @"
                    SELECT 
                        o.*,
                        u.nombreusuario,
                        u.correo
                    FROM aocr_or_orden o
                    LEFT JOIN usuario u ON o.codigo_usuario = u.idusuario
                    WHERE o.id = @Id
                    LIMIT 1;";

                var orden = cn.QueryFirstOrDefault<OrdenRecaudacionModel>(sql, new { Id = id });

                if (orden != null)
                {
                    orden.Detalles = ObtenerDetallesOrden(id, cn);

                    if (orden.ConceptoId.HasValue)
                        orden.Concepto = ObtenerConcepto(orden.ConceptoId.Value, cn);
                }

                return orden;
            }
        }

        public int CrearOrden(OrdenRecaudacionModel orden)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        string numeroOrden = GenerarNumeroOrden(cn, tx);

                        const string sqlOrden = @"
                            INSERT INTO aocr_or_orden (
                                codigo_usuario, codigo_solicitud, numero_orden, fecha_creacion,
                                estado, observacion, subtotal, admin, total, lugar_emision,
                                compania, ruc_cedula, correo, telefono, concepto_id
                            ) VALUES (
                                @CodigoUsuario, @CodigoSolicitud, @NumeroOrden, @FechaCreacion,
                                @Estado, @Observacion, @Subtotal, @Admin, @Total, @LugarEmision,
                                @Compania, @RucCedula, @Correo, @Telefono, @ConceptoId
                            )
                            RETURNING id;";

                        orden.NumeroOrden = numeroOrden;
                        orden.FechaCreacion = DateTime.Now;

                        int idOrden = cn.ExecuteScalar<int>(sqlOrden, new
                        {
                            orden.CodigoUsuario,
                            orden.CodigoSolicitud,
                            orden.NumeroOrden,
                            orden.FechaCreacion,
                            orden.Estado,
                            orden.Observacion,
                            orden.Subtotal,
                            orden.Admin,
                            orden.Total,
                            orden.LugarEmision,
                            orden.Compania,
                            orden.RucCedula,
                            orden.Correo,
                            orden.Telefono,
                            orden.ConceptoId
                        }, tx);

                        if (orden.Detalles != null && orden.Detalles.Any())
                        {
                            const string sqlDetalle = @"
                                INSERT INTO aocr_or_orden_detalle (
                                    orden_id, concepto_id, concepto_codigo, concepto_nombre,
                                    descripcion, cantidad, valor_unitario, porcentaje_admin,
                                    subtotal, admin, total_linea
                                ) VALUES (
                                    @OrdenId, @ConceptoId, @ConceptoCodigo, @ConceptoNombre,
                                    @Descripcion, @Cantidad, @ValorUnitario, @PorcentajeAdmin,
                                    @Subtotal, @Admin, @TotalLinea
                                );";

                            foreach (var d in orden.Detalles)
                            {
                                cn.Execute(sqlDetalle, new
                                {
                                    OrdenId = idOrden,
                                    d.ConceptoId,
                                    d.ConceptoCodigo,
                                    d.ConceptoNombre,
                                    d.Descripcion,
                                    d.Cantidad,
                                    d.ValorUnitario,
                                    d.PorcentajeAdmin,
                                    d.Subtotal,
                                    d.Admin,
                                    d.TotalLinea
                                }, tx);
                            }
                        }

                        tx.Commit();
                        return idOrden;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool ActualizarOrden(OrdenRecaudacionModel orden)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = @"
                    UPDATE aocr_or_orden SET
                        estado = @Estado,
                        observacion = @Observacion,
                        subtotal = @Subtotal,
                        admin = @Admin,
                        total = @Total,
                        lugar_emision = @LugarEmision,
                        compania = @Compania,
                        ruc_cedula = @RucCedula,
                        correo = @Correo,
                        telefono = @Telefono,
                        concepto_id = @ConceptoId
                    WHERE id = @Id;";

                int affected = cn.Execute(sql, new
                {
                    orden.Estado,
                    orden.Observacion,
                    orden.Subtotal,
                    orden.Admin,
                    orden.Total,
                    orden.LugarEmision,
                    orden.Compania,
                    orden.RucCedula,
                    orden.Correo,
                    orden.Telefono,
                    orden.ConceptoId,
                    orden.Id
                });

                return affected > 0;
            }
        }

        public bool CambiarEstadoOrden(int id, string nuevoEstado)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = "UPDATE aocr_or_orden SET estado = @Estado WHERE id = @Id;";
                return cn.Execute(sql, new { Estado = nuevoEstado, Id = id }) > 0;
            }
        }

        // ============================
        // BUSCAR / ESTADÍSTICAS / PAGOS
        // ============================

        public List<OrdenRecaudacionModel> BuscarOrdenes(string criterio, int? codigoUsuario)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = @"
                    SELECT 
                        o.*,
                        u.nombreusuario,
                        u.correo
                    FROM aocr_or_orden o
                    LEFT JOIN usuario u ON o.codigo_usuario = u.idusuario
                    WHERE (@CodigoUsuario IS NULL OR o.codigo_usuario = @CodigoUsuario)
                      AND (
                           o.numero_orden ILIKE '%' || @Criterio || '%'
                        OR o.compania    ILIKE '%' || @Criterio || '%'
                        OR o.ruc_cedula   ILIKE '%' || @Criterio || '%'
                        OR o.correo       ILIKE '%' || @Criterio || '%'
                      )
                    ORDER BY o.fecha_creacion DESC;";

                return cn.Query<OrdenRecaudacionModel>(sql, new
                {
                    CodigoUsuario = codigoUsuario,
                    Criterio = (criterio ?? "").Trim()
                }).ToList();
            }
        }

        public Dictionary<string, object> ObtenerEstadisticas(int codigoUsuario)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sql = @"
                    SELECT 
                        COUNT(*)::int as Total,
                        SUM(CASE WHEN estado = 'GENERADA' THEN 1 ELSE 0 END)::int as Generadas,
                        SUM(CASE WHEN estado = 'ENVIADA'  THEN 1 ELSE 0 END)::int as Enviadas,
                        SUM(CASE WHEN estado = 'PAGADA'   THEN 1 ELSE 0 END)::int as Pagadas,
                        SUM(CASE WHEN estado = 'BORRADOR' THEN 1 ELSE 0 END)::int as Borradores,
                        SUM(CASE WHEN estado = 'ANULADA'  THEN 1 ELSE 0 END)::int as Anuladas,
                        COALESCE(SUM(total),0)::numeric as TotalMonto,
                        COALESCE(SUM(CASE WHEN estado = 'PAGADA' THEN total ELSE 0 END),0)::numeric as TotalPagado,
                        COALESCE(SUM(CASE WHEN estado NOT IN ('PAGADA','ANULADA') THEN total ELSE 0 END),0)::numeric as TotalPendiente
                    FROM aocr_or_orden
                    WHERE codigo_usuario = @CodigoUsuario;";

                var row = cn.QueryFirstOrDefault(sql, new { CodigoUsuario = codigoUsuario });
                if (row == null) return new Dictionary<string, object>();

                var dict = (IDictionary<string, object>)row;
                return dict.ToDictionary(k => k.Key, v => v.Value);
            }
        }

        public bool RegistrarPago(int idOrden, PagoModel pago)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        const string sqlPago = @"
                            INSERT INTO aocr_tbpago (
                                codigo_solicitud, numero_factura, monto, moneda,
                                concepto, metodo_pago, estado, fecha_pago,
                                fecha_validacion, validado_por, observaciones,
                                comprobante_ruta
                            ) VALUES (
                                @CodigoSolicitud, @NumeroFactura, @Monto, @Moneda,
                                @Concepto, @MetodoPago, @Estado, @FechaPago,
                                @FechaValidacion, @ValidadoPor, @Observaciones,
                                @ComprobanteRuta
                            );";

                        cn.Execute(sqlPago, new
                        {
                            pago.CodigoSolicitud,
                            pago.NumeroFactura,
                            pago.Monto,
                            pago.Moneda,
                            pago.Concepto,
                            pago.MetodoPago,
                            pago.Estado,
                            pago.FechaPago,
                            pago.FechaValidacion,
                            pago.ValidadoPor,
                            pago.Observaciones,
                            pago.ComprobanteRuta
                        }, tx);

                        decimal totalOrden = cn.ExecuteScalar<decimal>(
                            "SELECT COALESCE(total,0) FROM aocr_or_orden WHERE id = @Id;",
                            new { Id = idOrden },
                            tx
                        );

                        if (totalOrden > 0m && pago.Monto >= totalOrden)
                        {
                            cn.Execute(
                                "UPDATE aocr_or_orden SET estado = 'PAGADA' WHERE id = @Id;",
                                new { Id = idOrden },
                                tx
                            );
                        }

                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        // ============================
        // DATATABLE (Dashboard viejo)
        // ============================

        public DataTable ObtenerOrdenesPorUsuario(int codigoUsuario)
        {
            var dt = new DataTable();

            using (var cn = new NpgsqlConnection(_connectionString))
            using (var cmd = new NpgsqlCommand())
            using (var da = new NpgsqlDataAdapter(cmd))
            {
                cn.Open();
                cmd.Connection = cn;

                cmd.CommandText = @"
                    SELECT 
                        id,
                        numero_orden,
                        fecha_creacion,
                        estado,
                        subtotal,
                        admin,
                        total,
                        observacion
                    FROM aocr_or_orden
                    WHERE codigo_usuario = @CodigoUsuario
                    ORDER BY fecha_creacion DESC;";

                cmd.Parameters.AddWithValue("@CodigoUsuario", codigoUsuario);
                da.Fill(dt);
            }

            return dt;
        }

        // ============================
        // PDF: Datos para iTextSharp
        // ============================

        public OrdenRecaudacionPdfDto ObtenerDatosParaPdf(int ordenId, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(_connectionString))
            {
                cn.Open();

                const string sqlCab = @"
                    SELECT
                        o.id,
                        o.numero_orden,
                        o.fecha_creacion,
                        o.lugar_emision,
                        o.compania   AS nombrecompania,
                        o.ruc_cedula AS ruc,
                        o.correo     AS email,
                        o.telefono,
                        o.observacion
                    FROM aocr_or_orden o
                    WHERE o.id = @OrdenId
                      AND o.codigo_usuario = @UsuarioId
                    LIMIT 1;";

                var cab = cn.QueryFirstOrDefault(sqlCab, new { OrdenId = ordenId, UsuarioId = usuarioId });
                if (cab == null) return null;

                const string sqlDet = @"
                    SELECT
                        COALESCE(concepto_codigo,'') AS codigoconcepto,
                        COALESCE(concepto_nombre,'') AS nombreconcepto,
                        COALESCE(cantidad,0)         AS cantidad,
                        COALESCE(valor_unitario,0)   AS valorunitario,
                        COALESCE(porcentaje_admin,0) AS porcentajeadmin,
                        COALESCE(subtotal,0)         AS subtotal,
                        COALESCE(admin,0)            AS admin,
                        COALESCE(total_linea,0)      AS total_linea
                    FROM aocr_or_orden_detalle
                    WHERE orden_id = @OrdenId
                    ORDER BY id;";

                var detalles = cn.Query(sqlDet, new { OrdenId = ordenId })
                    .Select(d => new OrdenRecaudacionPdfDetalleDto
                    {
                        CodigoConcepto = (string)d.codigoconcepto,
                        NombreConcepto = (string)d.nombreconcepto,
                        Cantidad = Convert.ToInt32(d.cantidad),
                        ValorUnitario = Convert.ToDecimal(d.valorunitario),
                        PorcentajeAdmin = Convert.ToDecimal(d.porcentajeadmin),
                        SubtotalLinea = Convert.ToDecimal(d.subtotal),
                        AdminLinea = Convert.ToDecimal(d.admin),
                        ValorTotal = Convert.ToDecimal(d.total_linea)
                    })
                    .ToList();

                var dto = new OrdenRecaudacionPdfDto();
                dto.OrdenId = Convert.ToInt32(cab.id);
                dto.NumeroOrden = cab.numero_orden == null ? "" : (string)cab.numero_orden;
                dto.FechaEmision = Convert.ToDateTime(cab.fecha_creacion);
                dto.LugarEmision = cab.lugar_emision == null ? "" : (string)cab.lugar_emision;

                dto.NombreCompania = cab.nombrecompania == null ? "" : (string)cab.nombrecompania;
                dto.Ruc = cab.ruc == null ? "" : (string)cab.ruc;
                dto.Email = cab.email == null ? "" : (string)cab.email;
                dto.Telefono = cab.telefono == null ? "" : (string)cab.telefono;

                dto.Observacion = cab.observacion == null ? "" : (string)cab.observacion;
                dto.Referencia = "";

                // ✅ NO asignar dto.Detalles si es readonly: usar Add
                for (int i = 0; i < detalles.Count; i++)
                    dto.Detalles.Add(detalles[i]);

                // Inspector (si aplica)
                dto.NombreInspector = "";
                dto.CargoInspector = "";

                // ✅ Totales NO se setean aquí: tu OrdenPdfService llama dto.CalcularTotales()
                return dto;
            }
        }

        // ============================
        // WRAPPERS BL (para compile)
        // ============================

        public List<OrdenRecaudacionModel> ListarPorUsuario(int codigoUsuario, string estado)
        {
            return ObtenerOrdenes(codigoUsuario, estado);
        }

        public OrdenRecaudacionModel ObtenerPorId(int id)
        {
            return ObtenerOrdenPorId(id);
        }

        public int Insertar(OrdenRecaudacionModel orden)
        {
            return CrearOrden(orden);
        }

        public bool Actualizar(OrdenRecaudacionModel orden)
        {
            return ActualizarOrden(orden);
        }

        public bool CambiarEstado(int id, string estado)
        {
            return CambiarEstadoOrden(id, estado);
        }

        // ============================
        // PRIVADOS
        // ============================

        private List<OrdenDetalleModel> ObtenerDetallesOrden(int ordenId, NpgsqlConnection cn)
        {
            const string sql = @"
                SELECT *
                FROM aocr_or_orden_detalle
                WHERE orden_id = @OrdenId
                ORDER BY id;";

            return cn.Query<OrdenDetalleModel>(sql, new { OrdenId = ordenId }).ToList();
        }

        private ConceptoModel ObtenerConcepto(int conceptoId, NpgsqlConnection cn)
        {
            const string sql = "SELECT * FROM aocr_or_concepto WHERE id = @ConceptoId LIMIT 1;";
            return cn.QueryFirstOrDefault<ConceptoModel>(sql, new { ConceptoId = conceptoId });
        }

        private string GenerarNumeroOrden(NpgsqlConnection cn, NpgsqlTransaction tx)
        {
            // OJO: COUNT no es 100% concurrente. Mejor sería una SEQUENCE, pero esto compila y funciona.
            const string sql = @"
                SELECT
                    'ORD-' || to_char(now(), 'YYYY') || '-' ||
                    lpad((COUNT(*) + 1)::text, 4, '0')
                FROM aocr_or_orden
                WHERE extract(year from fecha_creacion) = extract(year from now());";

            var numero = cn.ExecuteScalar<string>(sql, null, tx);
            if (string.IsNullOrWhiteSpace(numero))
                return "ORD-" + DateTime.Now.ToString("yyyy") + "-0001";

            return numero;
        }
    }
}
