using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using CapaModelo;

namespace CapaDatos.DAOs
{
    public class CertificadoDAO
    {
        public DatosDocumentalesAocr ObtenerDatosDocumentales(int solicitudId)
        {
            using(var cn=CrearConexion()) using(var cmd=new NpgsqlCommand("SELECT to_jsonb(s)->>'datos_documentales_certificado' FROM public.aocr_tbsolicitud s WHERE codigo_solicitud=@id",cn)) {
                cmd.Parameters.AddWithValue("@id",solicitudId);cn.Open();var json=Convert.ToString(cmd.ExecuteScalar());
                return string.IsNullOrWhiteSpace(json)?new DatosDocumentalesAocr():Newtonsoft.Json.JsonConvert.DeserializeObject<DatosDocumentalesAocr>(json)??new DatosDocumentalesAocr();
            }
        }

        public void GuardarDatosDocumentales(int solicitudId,DatosDocumentalesAocr datos,int usuarioId)
        {
            using(var cn=CrearConexion()) {cn.Open();using(var tx=cn.BeginTransaction()) {
                using(var cmd=new NpgsqlCommand("SELECT codigo_solicitud FROM public.aocr_tbsolicitud WHERE codigo_solicitud=@id FOR UPDATE",cn,tx)) {cmd.Parameters.AddWithValue("@id",solicitudId);if(cmd.ExecuteScalar()==null)throw new InvalidOperationException("Solicitud inexistente.");}
                ExigirDocumentoEditable(cn,tx,solicitudId);
                using(var cmd=new NpgsqlCommand("SELECT NULLIF(datos_documentales_certificado->>'SolicitudOriginalId','')::integer FROM public.aocr_tbsolicitud WHERE codigo_solicitud=@id AND NULLIF(numero_aocr,'') IS NOT NULL",cn,tx)) {
                    cmd.Parameters.AddWithValue("@id",solicitudId);var origen=cmd.ExecuteScalar();
                    if(origen!=null && origen!=DBNull.Value && datos.SolicitudOriginalId!=Convert.ToInt32(origen))throw new InvalidOperationException("La referencia al certificado original es inmutable una vez asignado el número.");
                }
                using(var cmd=new NpgsqlCommand("UPDATE public.aocr_tbsolicitud SET datos_documentales_certificado=@datos::jsonb,updated_at=NOW(),updated_by=@usuario WHERE codigo_solicitud=@id",cn,tx)) {
                    cmd.Parameters.AddWithValue("@id",solicitudId);cmd.Parameters.AddWithValue("@usuario",usuarioId.ToString());cmd.Parameters.AddWithValue("@datos",Newtonsoft.Json.JsonConvert.SerializeObject(datos));cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }}
        }

        private static void ExigirDocumentoEditable(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId)
        {
            using(var cmd=new NpgsqlCommand(@"SELECT EXISTS(SELECT 1 FROM public.aocr_tbdocumento_generado WHERE codigo_solicitud=@id AND vigente=TRUE AND (bloqueado=TRUE OR NULLIF(ruta_pdf_firmado,'') IS NOT NULL))
OR EXISTS(SELECT 1 FROM public.aocr_tbfirma_documento WHERE codigo_solicitud=@id)
OR EXISTS(SELECT 1 FROM public.aocr_tbcertificado c WHERE codigo_solicitud=@id AND NULLIF(to_jsonb(c)->>'firmado_por','') IS NOT NULL)",cn,tx)) {
                cmd.Parameters.AddWithValue("@id",solicitudId);if(Convert.ToBoolean(cmd.ExecuteScalar()))throw new InvalidOperationException("Los documentos enviados o firmados son inmutables. No se pueden modificar sus datos.");
            }
        }

        public string ObtenerNumeroPersistido(int solicitudId)
        {
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand("SELECT numero_aocr FROM public.aocr_tbsolicitud WHERE codigo_solicitud=@id",cn)) {cmd.Parameters.AddWithValue("@id",solicitudId);cn.Open();return Convert.ToString(cmd.ExecuteScalar());}
        }

        public List<Tuple<int,string>> ListarOriginales(int solicitudId)
        {
            var resultado=new List<Tuple<int,string>>();
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(@"SELECT s.codigo_solicitud,COALESCE(NULLIF(s.numero_aocr,''),c.numero_certificado)||' — '||s.numero_solicitud
FROM public.aocr_tbsolicitud actual JOIN public.aocr_tbsolicitud s ON UPPER(TRIM(s.codigo_oaci))=UPPER(TRIM(actual.codigo_oaci)) AND (COALESCE(TRIM(actual.ruc),'')='' OR TRIM(s.ruc)=TRIM(actual.ruc))
LEFT JOIN LATERAL(SELECT numero_certificado FROM public.aocr_tbcertificado c WHERE c.codigo_solicitud=s.codigo_solicitud ORDER BY codigo_certificado DESC LIMIT 1)c ON TRUE
WHERE actual.codigo_solicitud=@id AND s.codigo_solicitud<>@id AND s.tipo_solicitud=1 AND COALESCE(NULLIF(s.numero_aocr,''),NULLIF(c.numero_certificado,'')) IS NOT NULL ORDER BY s.codigo_solicitud",cn)) {
                cmd.Parameters.AddWithValue("@id",solicitudId);cn.Open();using(var r=cmd.ExecuteReader())while(r.Read())resultado.Add(Tuple.Create(r.GetInt32(0),r.GetString(1)));
            }return resultado;
        }

        public string AsegurarNumeroOriginal(int solicitudId,int usuarioId)
        {
            using(var cn=CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction()) {
                // One lock serializes allocation and origin resolution; no FR3 sequence is touched.
                using(var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(12920261027)",cn,tx))cmd.ExecuteNonQuery();
                string numero,oaci,ruc,numeroTramite;int tipo;int? original;
                using(var cmd=new NpgsqlCommand(@"SELECT numero_aocr,codigo_oaci,ruc,tipo_solicitud,NULLIF(datos_documentales_certificado->>'SolicitudOriginalId','')::integer origen,numero_solicitud
FROM public.aocr_tbsolicitud WHERE codigo_solicitud=@id FOR UPDATE",cn,tx)) {
                    cmd.Parameters.AddWithValue("@id",solicitudId);using(var r=cmd.ExecuteReader()){if(!r.Read())throw new InvalidOperationException("Solicitud inexistente.");numero=Convert.ToString(r[0]);oaci=Convert.ToString(r[1]).Trim().ToUpperInvariant();ruc=Convert.ToString(r[2]).Trim();tipo=Convert.ToInt32(r[3]);original=r.IsDBNull(4)?(int?)null:r.GetInt32(4);numeroTramite=Convert.ToString(r[5]);}
                }
                if(!string.IsNullOrWhiteSpace(numero)){tx.Commit();return numero;}
                ExigirDocumentoEditable(cn,tx,solicitudId);
                if(!System.Text.RegularExpressions.Regex.IsMatch(oaci,"^[A-Z0-9]{3}$"))throw new InvalidOperationException("Registre el código OACI del explotador (3 caracteres) antes de asignar el AOCR.");
                if(tipo==1) {
                    numero=ConstruirNumeroDesdeTramiteOriginal(oaci,numeroTramite);
                } else if(tipo==2 || tipo==3) {
                    var origenes=new List<Tuple<int,string>>();
                    using(var cmd=new NpgsqlCommand(@"SELECT s.codigo_solicitud,COALESCE(NULLIF(s.numero_aocr,''),NULLIF(c.numero_certificado,'')) numero
FROM public.aocr_tbsolicitud s LEFT JOIN LATERAL(SELECT numero_certificado FROM public.aocr_tbcertificado c WHERE c.codigo_solicitud=s.codigo_solicitud ORDER BY codigo_certificado DESC LIMIT 1)c ON TRUE
WHERE s.tipo_solicitud=1 AND s.codigo_solicitud<>@id AND UPPER(TRIM(s.codigo_oaci))=@oaci
AND (@ruc='' OR TRIM(COALESCE(s.ruc,''))=@ruc) AND (@origen IS NULL OR s.codigo_solicitud=@origen)
AND COALESCE(NULLIF(s.numero_aocr,''),NULLIF(c.numero_certificado,'')) IS NOT NULL",cn,tx)) {
                        cmd.Parameters.AddWithValue("@id",solicitudId);cmd.Parameters.AddWithValue("@oaci",oaci);cmd.Parameters.AddWithValue("@ruc",ruc);cmd.Parameters.AddWithValue("@origen",NpgsqlTypes.NpgsqlDbType.Integer,(object)original??DBNull.Value);
                        using(var r=cmd.ExecuteReader())while(r.Read())origenes.Add(Tuple.Create(r.GetInt32(0),r.GetString(1)));
                    }
                    if(origenes.Count!=1)throw new InvalidOperationException("Seleccione una solicitud original del mismo explotador con AOCR asignado. No se generará un nuevo consecutivo.");
                    original=origenes[0].Item1;numero=origenes[0].Item2;
                } else throw new InvalidOperationException("Tipo de trámite inválido.");
                using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_tbsolicitud SET numero_aocr=@numero,
datos_documentales_certificado=CASE WHEN @origen IS NULL THEN COALESCE(datos_documentales_certificado,'{}'::jsonb) ELSE jsonb_set(COALESCE(datos_documentales_certificado,'{}'::jsonb),'{SolicitudOriginalId}',to_jsonb(@origen::integer)) END
WHERE codigo_solicitud=@id",cn,tx)) {cmd.Parameters.AddWithValue("@id",solicitudId);cmd.Parameters.AddWithValue("@numero",numero);cmd.Parameters.AddWithValue("@origen",NpgsqlTypes.NpgsqlDbType.Integer,(object)original??DBNull.Value);cmd.ExecuteNonQuery();}
                using(var cmd=new NpgsqlCommand("UPDATE public.aocr_tbcertificado SET numero_certificado=@numero WHERE codigo_solicitud=@id",cn,tx)){cmd.Parameters.AddWithValue("@id",solicitudId);cmd.Parameters.AddWithValue("@numero",numero);cmd.ExecuteNonQuery();}
                tx.Commit();return numero;
            }}
        }

        public static string ConstruirNumeroDesdeTramiteOriginal(string codigoOaci, string numeroTramite)
        {
            var oaci=(codigoOaci??string.Empty).Trim().ToUpperInvariant();
            if(!System.Text.RegularExpressions.Regex.IsMatch(oaci,"^[A-Z0-9]{3}$"))
                throw new InvalidOperationException("Registre el código OACI del explotador (3 caracteres) antes de asignar el AOCR.");
            // Institutional and legacy request numbers end in AOCR003 or AOCR-003.
            // Preserve all original digits; never allocate a second counter or use a DB id.
            var match=System.Text.RegularExpressions.Regex.Match((numeroTramite??string.Empty).Trim(),@"AOCR-?(?<consecutivo>[0-9]+)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if(!match.Success || match.Groups["consecutivo"].Value.TrimStart('0').Length==0)
                throw new InvalidOperationException("El trámite original debe tener un consecutivo AOCR válido antes de generar el certificado.");
            return "DGAC-129-"+oaci+"-"+match.Groups["consecutivo"].Value.PadLeft(3,'0');
        }

        private const string TablaCertificado = "aocr_tbcertificado";

        private sealed class CertificadoSchema
        {
            public string Tabla;
            public string CodigoCertificado;
            public string CodigoSolicitud;
            public string NumeroCertificado;
            public string Tipo;
            public string Estado;
            public string FechaEmision;
            public string FechaVencimiento;
            public string RutaDocumento;
            public string Observaciones;
            public string EmitidoPor;
            public string AprobadoPor;
            public string CreatedAt;
            public string CreatedBy;
            public string UpdatedAt;
            public string UpdatedBy;
            public bool UsaEstadosLegados;
        }

        private NpgsqlConnection CrearConexion()
        {
            var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new Exception("No existe la cadena de conexión 'AOCRConnection' en el config.");
            }

            return new NpgsqlConnection(cs);
        }

        public int Crear(Certificado cert)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var schema = ResolverSchema(cn);
                var estadoPersistido = TraducirEstadoParaPersistencia(cert != null ? cert.Estado : null, schema.UsaEstadosLegados);

                var columnas = new List<string>();
                var valores = new List<string>();

                AgregarParametro(columnas, valores, schema.CodigoSolicitud, "@codSol");
                AgregarParametro(columnas, valores, schema.NumeroCertificado, "@num");
                AgregarParametro(columnas, valores, schema.Tipo, "@tipo");
                if (!string.IsNullOrWhiteSpace(estadoPersistido))
                {
                    AgregarParametro(columnas, valores, schema.Estado, "@estado");
                }
                AgregarParametro(columnas, valores, schema.FechaEmision, "@fe");
                AgregarParametro(columnas, valores, schema.FechaVencimiento, "@fv");
                AgregarParametro(columnas, valores, schema.RutaDocumento, "@ruta");
                AgregarParametro(columnas, valores, schema.Observaciones, "@obs");
                AgregarParametro(columnas, valores, schema.EmitidoPor, "@emit");
                AgregarParametro(columnas, valores, schema.AprobadoPor, "@aprob");
                AgregarParametro(columnas, valores, schema.CreatedAt, "@cat");
                AgregarParametro(columnas, valores, schema.CreatedBy, "@cby");
                AgregarParametro(columnas, valores, schema.UpdatedAt, "@uat");
                AgregarParametro(columnas, valores, schema.UpdatedBy, "@uby");

                if (columnas.Count == 0)
                {
                    return 0;
                }

                var sql = $@"
                    INSERT INTO {schema.Tabla}
                    ({string.Join(", ", columnas)})
                    VALUES
                    ({string.Join(", ", valores)})
                    RETURNING {schema.CodigoCertificado};";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@codSol", cert.CodigoSolicitud);
                    cmd.Parameters.AddWithValue("@num", (object)cert.NumeroCertificado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipo", (object)cert.Tipo ?? "AOCR");
                    cmd.Parameters.AddWithValue("@estado", (object)estadoPersistido ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fe", (object)cert.FechaEmision ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@fv", (object)cert.FechaVencimiento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta", (object)cert.RutaDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@obs", (object)cert.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@emit", (object)cert.EmitidoPor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@aprob", (object)cert.AprobadoPor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cat", (object)cert.CreatedAt ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@cby", cert.CreatedBy);
                    cmd.Parameters.AddWithValue("@uat", (object)cert.UpdatedAt ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@uby", cert.UpdatedBy);

                    var id = cmd.ExecuteScalar();
                    return (id == null || id == DBNull.Value) ? 0 : Convert.ToInt32(id);
                }
            }
        }

        public bool Actualizar(Certificado cert)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var schema = ResolverSchema(cn);
                var estadoPersistido = TraducirEstadoParaPersistencia(cert != null ? cert.Estado : null, schema.UsaEstadosLegados);
                var sets = new List<string>();

                if (!string.IsNullOrWhiteSpace(estadoPersistido))
                {
                    AgregarSet(sets, schema.Estado, "@estado");
                }
                AgregarSet(sets, schema.RutaDocumento, "@ruta");
                AgregarSet(sets, schema.Observaciones, "@obs");
                AgregarSet(sets, schema.EmitidoPor, "@emit");
                AgregarSet(sets, schema.AprobadoPor, "@aprob");
                AgregarSet(sets, schema.FechaEmision, "@fe");
                AgregarSet(sets, schema.FechaVencimiento, "@fv");
                AgregarSet(sets, schema.UpdatedAt, "@uat");
                AgregarSet(sets, schema.UpdatedBy, "@uby");

                if (sets.Count == 0)
                {
                    return false;
                }

                var sql = $@"
                    UPDATE {schema.Tabla}
                    SET {string.Join(", ", sets)}
                    WHERE {schema.CodigoCertificado}=@id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", cert.CodigoCertificado);
                    cmd.Parameters.AddWithValue("@estado", (object)estadoPersistido ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ruta", (object)cert.RutaDocumento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@obs", (object)cert.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@emit", (object)cert.EmitidoPor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@aprob", (object)cert.AprobadoPor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fe", (object)cert.FechaEmision ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fv", (object)cert.FechaVencimiento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@uat", DateTime.Now);
                    cmd.Parameters.AddWithValue("@uby", cert.UpdatedBy);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public Certificado ObtenerPorId(int id)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var schema = ResolverSchema(cn);
                var sql = ConstruirSelect(schema) + $@"
                    WHERE {schema.CodigoCertificado}=@id;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Map(rd) : null;
                    }
                }
            }
        }

        public Certificado ObtenerPorSolicitud(int solicitudId)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var schema = ResolverSchema(cn);
                var sql = ConstruirSelect(schema) + $@"
                    WHERE {schema.CodigoSolicitud}=@id
                    ORDER BY {schema.CodigoCertificado} DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", solicitudId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Map(rd) : null;
                    }
                }
            }
        }

        public Certificado ObtenerPorNumero(string numeroCertificado)
        {
            using (var cn = CrearConexion())
            {
                cn.Open();
                var schema = ResolverSchema(cn);
                var sql = ConstruirSelect(schema) + $@"
                    WHERE {schema.NumeroCertificado}=@numero
                    ORDER BY {schema.CodigoCertificado} DESC
                    LIMIT 1;";

                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@numero", numeroCertificado);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Map(rd) : null;
                    }
                }
            }
        }

        private static Certificado Map(IDataRecord rd)
        {
            var rutaDocumento = rd["rutadocumento"] != DBNull.Value ? rd["rutadocumento"].ToString() : null;
            var estadoPersistido = rd["estado"] != DBNull.Value ? rd["estado"].ToString() : null;

            return new Certificado
            {
                CodigoCertificado = rd["codigocertificado"] != DBNull.Value ? Convert.ToInt32(rd["codigocertificado"]) : 0,
                CodigoSolicitud = rd["codigosolicitud"] != DBNull.Value ? Convert.ToInt32(rd["codigosolicitud"]) : 0,
                NumeroCertificado = rd["numerocertificado"] != DBNull.Value ? rd["numerocertificado"].ToString() : null,
                Tipo = rd["tipo"] != DBNull.Value ? rd["tipo"].ToString() : null,
                Estado = TraducirEstadoDesdePersistencia(estadoPersistido, rutaDocumento),
                FechaEmision = rd["fechaemision"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fechaemision"]) : null,
                FechaVencimiento = rd["fechavencimiento"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["fechavencimiento"]) : null,
                RutaDocumento = rutaDocumento,
                Observaciones = rd["observaciones"] != DBNull.Value ? rd["observaciones"].ToString() : null,
                EmitidoPor = rd["emitidopor"] != DBNull.Value ? rd["emitidopor"].ToString() : null,
                AprobadoPor = rd["aprobadopor"] != DBNull.Value ? rd["aprobadopor"].ToString() : null,
                CreatedAt = rd["createdat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["createdat"]) : null,
                CreatedBy = rd["createdby"] != DBNull.Value ? Convert.ToInt32(rd["createdby"]) : 0,
                UpdatedAt = rd["updatedat"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(rd["updatedat"]) : null,
                UpdatedBy = rd["updatedby"] != DBNull.Value ? Convert.ToInt32(rd["updatedby"]) : 0
            };
        }

        private static string ConstruirSelect(CertificadoSchema schema)
        {
            return $@"
                SELECT {SelectExpr(schema.CodigoCertificado, "codigocertificado", "0")},
                       {SelectExpr(schema.CodigoSolicitud, "codigosolicitud", "0")},
                       {SelectExpr(schema.NumeroCertificado, "numerocertificado", "NULL::text")},
                       {SelectExpr(schema.Tipo, "tipo", "'AOCR'")},
                       {SelectExpr(schema.Estado, "estado", "'GENERADO'")},
                       {SelectExpr(schema.FechaEmision, "fechaemision", "NULL::timestamp")},
                       {SelectExpr(schema.FechaVencimiento, "fechavencimiento", "NULL::timestamp")},
                       {SelectExpr(schema.RutaDocumento, "rutadocumento", "NULL::text")},
                       {SelectExpr(schema.Observaciones, "observaciones", "NULL::text")},
                       {SelectExpr(schema.EmitidoPor, "emitidopor", "NULL::text")},
                       {SelectExpr(schema.AprobadoPor, "aprobadopor", "NULL::text")},
                       {SelectExpr(schema.CreatedAt, "createdat", "NULL::timestamp")},
                       {SelectExpr(schema.CreatedBy, "createdby", "0")},
                       {SelectExpr(schema.UpdatedAt, "updatedat", "NULL::timestamp")},
                       {SelectExpr(schema.UpdatedBy, "updatedby", "0")}
                FROM {schema.Tabla}";
        }

        private static string SelectExpr(string columna, string alias, string fallbackSql)
        {
            return string.IsNullOrWhiteSpace(columna)
                ? $"{fallbackSql} AS {alias}"
                : $"{columna} AS {alias}";
        }

        private static void AgregarParametro(List<string> columnas, List<string> valores, string columna, string valor)
        {
            if (string.IsNullOrWhiteSpace(columna))
            {
                return;
            }

            columnas.Add(columna);
            valores.Add(valor);
        }

        private static void AgregarSet(List<string> sets, string columna, string valor)
        {
            if (string.IsNullOrWhiteSpace(columna))
            {
                return;
            }

            sets.Add($"{columna}={valor}");
        }

        private static string TraducirEstadoDesdePersistencia(string estadoPersistido, string rutaDocumento)
        {
            if (string.IsNullOrWhiteSpace(estadoPersistido))
            {
                return string.IsNullOrWhiteSpace(rutaDocumento) ? "GENERADO" : "APROBADO";
            }

            switch (estadoPersistido.Trim().ToUpperInvariant())
            {
                case "VIGENTE":
                    return string.IsNullOrWhiteSpace(rutaDocumento) ? "GENERADO" : "APROBADO";
                case "VENCIDO":
                    return "VENCIDO";
                case "SUSPENDIDO":
                    return "SUSPENDIDO";
                case "REVOCADO":
                    return "ANULADO";
                default:
                    return estadoPersistido;
            }
        }

        private static string TraducirEstadoParaPersistencia(string estado, bool usaEstadosLegados)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return null;
            }

            if (!usaEstadosLegados)
            {
                return estado;
            }

            switch (estado.Trim().ToUpperInvariant())
            {
                case "GENERADO":
                    return null;
                case "APROBADO":
                case "VIGENTE":
                    return "Vigente";
                case "VENCIDO":
                    return "Vencido";
                case "SUSPENDIDO":
                    return "Suspendido";
                case "ANULADO":
                case "RECHAZADO":
                case "REVOCADO":
                    return "Revocado";
                default:
                    return estado;
            }
        }

        private static CertificadoSchema ResolverSchema(NpgsqlConnection cn)
        {
            return new CertificadoSchema
            {
                Tabla = TablaCertificado,
                CodigoCertificado = ResolverColumna(cn, TablaCertificado, "codigo_certificado", "codigocertificado"),
                CodigoSolicitud = ResolverColumna(cn, TablaCertificado, "codigo_solicitud", "codigosolicitud"),
                NumeroCertificado = ResolverColumnaOpcional(cn, TablaCertificado, "numero_certificado", "numerocertificado"),
                Tipo = ResolverColumnaOpcional(cn, TablaCertificado, "tipo", "tipo_certificado", "clase_certificado"),
                Estado = ResolverColumnaOpcional(cn, TablaCertificado, "estado", "estado_certificado"),
                FechaEmision = ResolverColumnaOpcional(cn, TablaCertificado, "fecha_emision", "fechaemision"),
                FechaVencimiento = ResolverColumnaOpcional(cn, TablaCertificado, "fecha_vencimiento", "fechavencimiento"),
                RutaDocumento = ResolverColumnaOpcional(cn, TablaCertificado, "ruta_documento", "rutadocumento", "ruta_pdf"),
                Observaciones = ResolverColumnaOpcional(cn, TablaCertificado, "observaciones"),
                EmitidoPor = ResolverColumnaOpcional(cn, TablaCertificado, "emitido_por", "emitidopor", "firmado_por"),
                AprobadoPor = ResolverColumnaOpcional(cn, TablaCertificado, "aprobado_por", "aprobadopor"),
                CreatedAt = ResolverColumnaOpcional(cn, TablaCertificado, "created_at", "createdat", "fecha_creacion"),
                CreatedBy = ResolverColumnaOpcional(cn, TablaCertificado, "created_by", "createdby", "usuario_creacion"),
                UpdatedAt = ResolverColumnaOpcional(cn, TablaCertificado, "updated_at", "updatedat", "fecha_actualizacion"),
                UpdatedBy = ResolverColumnaOpcional(cn, TablaCertificado, "updated_by", "updatedby", "usuario_actualizacion"),
                UsaEstadosLegados = TieneConstraintEstadosLegados(cn, TablaCertificado)
            };
        }

        private static bool TieneConstraintEstadosLegados(NpgsqlConnection cn, string tabla)
        {
            const string sql = @"
                SELECT pg_get_constraintdef(c.oid)
                FROM pg_constraint c
                WHERE c.conrelid = to_regclass(@tabla)
                  AND c.contype = 'c';";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var definicion = rd[0] != DBNull.Value ? rd[0].ToString() : null;
                        if (string.IsNullOrWhiteSpace(definicion))
                        {
                            continue;
                        }

                        if (definicion.IndexOf("'Vigente'", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            definicion.IndexOf("'APROBADO'", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string ResolverColumna(NpgsqlConnection cn, string tabla, params string[] candidatos)
        {
            var columna = ResolverColumnaOpcional(cn, tabla, candidatos);
            return string.IsNullOrWhiteSpace(columna) ? candidatos[0] : columna;
        }

        private static string ResolverColumnaOpcional(NpgsqlConnection cn, string tabla, params string[] candidatos)
        {
            foreach (var candidato in candidatos)
            {
                if (ExisteColumna(cn, tabla, candidato))
                {
                    return candidato;
                }
            }

            return null;
        }

        private static bool ExisteColumna(NpgsqlConnection cn, string tabla, string columna)
        {
            const string sql = @"
                SELECT 1
                FROM pg_attribute a
                WHERE a.attrelid = to_regclass(@tabla)
                  AND a.attname = @columna
                  AND a.attnum > 0
                  AND NOT a.attisdropped
                LIMIT 1;";

            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                cmd.Parameters.AddWithValue("@columna", columna);
                return cmd.ExecuteScalar() != null;
            }
        }
    }
}
