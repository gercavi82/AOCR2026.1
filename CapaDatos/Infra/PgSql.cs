namespace CapaDatos.Db
{
    public static class PgSql
    {
        // ORDEN (aocr_or_orden)
        public const string Orden_SelectByUser = @"
            SELECT *
            FROM aocr_or_orden
            WHERE codigo_usuario = @codigo_usuario
              AND (@estado IS NULL OR estado = @estado)
            ORDER BY fecha_creacion DESC;";

        public const string Orden_SelectById = @"
            SELECT *
            FROM aocr_or_orden
            WHERE id = @id;";

        public const string Orden_Insert = @"
            INSERT INTO aocr_or_orden
            (codigo_usuario, codigo_solicitud, numero_orden, fecha_creacion, estado, observacion,
             subtotal, admin, total, lugar_emision, compania, ruc_cedula, correo, telefono, concepto_id)
            VALUES
            (@CodigoUsuario, @CodigoSolicitud, @NumeroOrden, now(), @Estado, @Observacion,
             @Subtotal, @Admin, @Total, @LugarEmision, @Compania, @RucCedula, @Correo, @Telefono, @ConceptoId)
            RETURNING id;";

        public const string Orden_Update = @"
            UPDATE aocr_or_orden SET
                codigo_solicitud = @CodigoSolicitud,
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

        public const string Orden_UpdateEstado = @"
            UPDATE aocr_or_orden SET
                estado = @estado
            WHERE id = @id;";

        // DETALLE (aocr_or_orden_detalle)
        public const string Detalle_SelectByOrden = @"
            SELECT *
            FROM aocr_or_orden_detalle
            WHERE orden_id = @orden_id
            ORDER BY id;";

        public const string Detalle_DeleteByOrden = @"
            DELETE FROM aocr_or_orden_detalle
            WHERE orden_id = @orden_id;";

        public const string Detalle_Insert = @"
            INSERT INTO aocr_or_orden_detalle
            (orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion,
             cantidad, valor_unitario, porcentaje_admin, subtotal, admin, total_linea)
            VALUES
            (@OrdenId, @ConceptoId, @ConceptoCodigo, @ConceptoNombre, @Descripcion,
             @Cantidad, @ValorUnitario, @PorcentajeAdmin, @Subtotal, @Admin, @TotalLinea);";

        // CONCEPTO (aocr_or_concepto)
        public const string Concepto_SelectActivos = @"
            SELECT *
            FROM aocr_or_concepto
            WHERE activo = true
            ORDER BY orden, nombre;";

        public const string Concepto_SelectById = @"
            SELECT *
            FROM aocr_or_concepto
            WHERE id = @id;";

        // HISTORIAL (tienes aocr_tbhistorial_estado para solicitudes; para OR sugerimos uno propio)
        // Si NO quieres crear tabla nueva, guarda historial en aocr_tblog o aocr_tbauditoria.
    }
}
