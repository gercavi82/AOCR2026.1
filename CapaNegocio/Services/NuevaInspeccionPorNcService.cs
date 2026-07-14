using System;
using CapaDatos.DAOs;
using CapaModelo;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class NuevaInspeccionPorNcResultado
    {
        public bool Ok { get; set; }
        public bool Existente { get; set; }
        public string Mensaje { get; set; }
        public int CodigoSolicitud { get; set; }
        public int? CodigoInspeccion { get; set; }
        public int? CodigoOrden { get; set; }
        public string ModuloDestino { get; set; }
        public string TipoTramite { get; set; }
    }

    public sealed class NuevaInspeccionPorNcService
    {
        private readonly string _cs;
        public NuevaInspeccionPorNcService()
        {
            _cs = new CapaDatos.Services.SecureConfigurationService().GetConnectionString("PostgreSQL")
                ?? new CapaDatos.Services.SecureConfigurationService().GetConnectionString("AOCRConnection");
        }

        public NuevaInspeccionPorNcResultado Crear(int codigoSolicitudOriginal, int usuarioRt, string usuario)
        {
            var result = new NuevaInspeccionPorNcResultado();
            using (var cn = new NpgsqlConnection(_cs))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        NoConformidad nc;
                        using (var cmd = new NpgsqlCommand(@"SELECT * FROM aocr_tbnoconformidad
WHERE codigo_solicitud=@solicitud AND UPPER(tipo_ruta)='CON_INSPECCION'
ORDER BY version DESC,codigo_no_conformidad DESC LIMIT 1 FOR UPDATE;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@solicitud", codigoSolicitudOriginal);
                            using (var rd=cmd.ExecuteReader()) { if(!rd.Read()){result.Mensaje="No existe NC CON_INSPECCION.";return result;}
                                nc=new NoConformidad { CodigoNoConformidad=Convert.ToInt32(rd["codigo_no_conformidad"]), CodigoSolicitud=Convert.ToInt32(rd["codigo_solicitud"]), CodigoInspeccion=Convert.ToInt32(rd["codigo_inspeccion"]), CodigoInforme=Convert.ToInt32(rd["codigo_informe"]), Estado=Convert.ToString(rd["estado"]), TipoRuta=Convert.ToString(rd["tipo_ruta"]) }; }
                        }
                        if (!string.Equals(nc.Estado,"FIRMADA_COORDINADOR",StringComparison.OrdinalIgnoreCase)) { result.Mensaje="La NC no está firmada por Coordinación.";return result; }

                        int existente;
                        using(var cmd=new NpgsqlCommand(@"SELECT codigo_solicitud FROM aocr_tbsolicitud WHERE codigo_nc_origen=@nc AND deleted_at IS NULL
AND UPPER(COALESCE(estado,'')) NOT IN ('FINALIZADO','CANCELADA','CANCELADO','ANULADA','ANULADO','RECHAZADA','RECHAZADO') LIMIT 1;",cn,tx))
                        {cmd.Parameters.AddWithValue("@nc",nc.CodigoNoConformidad);var value=cmd.ExecuteScalar();existente=value==null?0:Convert.ToInt32(value);}
                        if(existente>0){CargarDestinoExistente(cn,result,existente);tx.Commit();result.Ok=true;result.Existente=true;result.CodigoSolicitud=existente;result.Mensaje="La solicitud ya había sido creada para esta NC.";return result;}

                        var original = new SolicitudAOCRDAO().ObtenerPorId(codigoSolicitudOriginal);
                        if(original==null || original.CodigoUsuario!=usuarioRt){result.Mensaje="El RT no es propietario de la solicitud original.";return result;}
                        string moduloDestino, tipoTramite;
                        if (!TryResolverDestino(original, out moduloDestino, out tipoTramite))
                        { result.Mensaje="El trámite no corresponde a emisión, renovación o modificación con nuevo aeropuerto."; return result; }
                        var nueva = Clonar(original,nc,usuarioRt,usuario);
                        var nuevaId = new SolicitudAOCRDAO().InsertarConReturn(cn,tx,nueva);
                        using(var cmd=new NpgsqlCommand(@"UPDATE aocr_tbsolicitud SET codigo_solicitud_origen=@origen,codigo_inspeccion_origen=@inspeccion,
codigo_informe_origen=@informe,codigo_nc_origen=@nc,modulo_origen='NUEVA_INSPECCION_POR_NC',
modulo_destino=@modulo_destino,tipo_tramite_origen=@tipo_tramite WHERE codigo_solicitud=@nueva;",cn,tx))
                        {cmd.Parameters.AddWithValue("@origen",codigoSolicitudOriginal);cmd.Parameters.AddWithValue("@inspeccion",nc.CodigoInspeccion);cmd.Parameters.AddWithValue("@informe",nc.CodigoInforme);cmd.Parameters.AddWithValue("@nc",nc.CodigoNoConformidad);cmd.Parameters.AddWithValue("@nueva",nuevaId);cmd.Parameters.AddWithValue("@modulo_destino",moduloDestino);cmd.Parameters.AddWithValue("@tipo_tramite",tipoTramite);cmd.ExecuteNonQuery();}

                        int nuevaInspeccion;
                        using(var cmd=new NpgsqlCommand(@"INSERT INTO aocr_tbinspeccion(codigo_solicitud,numero_inspeccion,tipo,fecha_programada,estado,estado_documental,created_at,created_by,updated_at,updated_by)
SELECT @nueva,'INSP-NC-'||@nc,COALESCE(tipo,1),CURRENT_DATE,'CREADA','PENDIENTE_PROGRAMACION',NOW(),@usuario,NOW(),@usuario
FROM aocr_tbinspeccion WHERE codigo_inspeccion=@origen RETURNING codigo_inspeccion;",cn,tx))
                        {cmd.Parameters.AddWithValue("@nueva",nuevaId);cmd.Parameters.AddWithValue("@nc",nc.CodigoNoConformidad);cmd.Parameters.AddWithValue("@usuario",usuarioRt.ToString());cmd.Parameters.AddWithValue("@origen",nc.CodigoInspeccion);nuevaInspeccion=Convert.ToInt32(cmd.ExecuteScalar());}

                        int? ordenId=null;
                        using(var cmd=new NpgsqlCommand(@"INSERT INTO aocr_or_orden(codigo_usuario,codigo_solicitud,numero_orden,fecha_creacion,estado,observacion,subtotal,admin,total,lugar_emision,compania,ruc_cedula,correo,telefono,concepto_id)
SELECT codigo_usuario,@nueva,'OR-NC-'||@nc||'-'||to_char(NOW(),'YYYYMMDDHH24MISS'),NOW(),'BORRADOR','Nueva inspección vinculada a NC',subtotal,admin,total,lugar_emision,compania,ruc_cedula,correo,telefono,concepto_id
FROM aocr_or_orden WHERE codigo_solicitud::text=@origen::text ORDER BY id DESC LIMIT 1 RETURNING id;",cn,tx))
                        {cmd.Parameters.AddWithValue("@nueva",nuevaId.ToString());cmd.Parameters.AddWithValue("@nc",nc.CodigoNoConformidad);cmd.Parameters.AddWithValue("@origen",codigoSolicitudOriginal);var value=cmd.ExecuteScalar();if(value!=null)ordenId=Convert.ToInt32(value);}
                        if(ordenId.HasValue) using(var cmd=new NpgsqlCommand(@"INSERT INTO aocr_or_detalle_orden(orden_id,subconcepto_id,cantidad,precio_unitario,total)
SELECT @nueva,subconcepto_id,cantidad,precio_unitario,total FROM aocr_or_detalle_orden WHERE orden_id=(SELECT id FROM aocr_or_orden WHERE codigo_solicitud::text=@origen::text AND id<>@nueva ORDER BY id DESC LIMIT 1);",cn,tx))
                        {cmd.Parameters.AddWithValue("@nueva",ordenId.Value);cmd.Parameters.AddWithValue("@origen",codigoSolicitudOriginal);cmd.ExecuteNonQuery();}

                        using(var cmd=new NpgsqlCommand(@"UPDATE aocr_tbnoconformidad SET codigo_solicitud_nueva=@nueva,codigo_inspeccion_nueva=@inspeccion,updated_at=NOW() WHERE codigo_no_conformidad=@nc;
INSERT INTO aocr_tbhistorial_estado(codigo_solicitud,estado_anterior,estado_nuevo,fecha_cambio,codigo_usuario,observaciones)
VALUES(@nueva,'','PENDIENTE',NOW(),@usuario,'Solicitud real creada por NC CON_INSPECCION. Origen='||@origen||'; NC='||@nc);",cn,tx))
                        {cmd.Parameters.AddWithValue("@nueva",nuevaId);cmd.Parameters.AddWithValue("@inspeccion",nuevaInspeccion);cmd.Parameters.AddWithValue("@nc",nc.CodigoNoConformidad);cmd.Parameters.AddWithValue("@usuario",usuarioRt);cmd.Parameters.AddWithValue("@origen",codigoSolicitudOriginal);cmd.ExecuteNonQuery();}
                        tx.Commit();
                        result.Ok=true;result.CodigoSolicitud=nuevaId;result.CodigoInspeccion=nuevaInspeccion;result.CodigoOrden=ordenId;result.ModuloDestino=moduloDestino;result.TipoTramite=tipoTramite;result.Mensaje="Nueva solicitud institucional creada correctamente.";
                    }
                    catch(PostgresException ex) when(ex.SqlState=="23505") { tx.Rollback(); return ObtenerExistente(codigoSolicitudOriginal,result); }
                    catch(Exception ex){tx.Rollback();result.Mensaje=ex.Message;}
                }
            }
            if(result.Ok) NotificacionBL.EnviarNotificacion(usuarioRt,"Nueva solicitud por NC",result.Mensaje,"INFO","/SolicitudAOCR/Detalle/"+result.CodigoSolicitud,"NO_CONFORMIDAD",result.CodigoSolicitud,"SOLICITUD");
            return result;
        }

        private NuevaInspeccionPorNcResultado ObtenerExistente(int original,NuevaInspeccionPorNcResultado r){using(var cn=new NpgsqlConnection(_cs)){cn.Open();using(var cmd=new NpgsqlCommand("SELECT codigo_solicitud FROM aocr_tbsolicitud WHERE codigo_solicitud_origen=@o AND codigo_nc_origen IS NOT NULL AND deleted_at IS NULL ORDER BY codigo_solicitud DESC LIMIT 1",cn)){cmd.Parameters.AddWithValue("@o",original);var v=cmd.ExecuteScalar();if(v!=null){r.Ok=true;r.Existente=true;r.CodigoSolicitud=Convert.ToInt32(v);CargarDestinoExistente(cn,r,r.CodigoSolicitud);r.Mensaje="La solicitud ya existe.";}}}return r;}
        private static void CargarDestinoExistente(NpgsqlConnection cn,NuevaInspeccionPorNcResultado r,int solicitud)
        {
            using(var cmd=new NpgsqlCommand(@"SELECT s.modulo_destino,s.tipo_tramite_origen,
(SELECT codigo_inspeccion FROM aocr_tbinspeccion WHERE codigo_solicitud=s.codigo_solicitud ORDER BY codigo_inspeccion DESC LIMIT 1),
(SELECT id FROM aocr_or_orden WHERE codigo_solicitud::text=s.codigo_solicitud::text ORDER BY id DESC LIMIT 1)
FROM aocr_tbsolicitud s WHERE s.codigo_solicitud=@id;",cn))
            {cmd.Parameters.AddWithValue("@id",solicitud);using(var rd=cmd.ExecuteReader()){if(rd.Read()){r.ModuloDestino=rd.IsDBNull(0)?null:rd.GetString(0);r.TipoTramite=rd.IsDBNull(1)?null:rd.GetString(1);r.CodigoInspeccion=rd.IsDBNull(2)?(int?)null:rd.GetInt32(2);r.CodigoOrden=rd.IsDBNull(3)?(int?)null:rd.GetInt32(3);}}}
        }
        public static bool TryResolverDestino(SolicitudAOCR s,out string modulo,out string tramite)
        {
            modulo=null;tramite=null;
            if(s.TipoSolicitud.GetValueOrDefault()==1){modulo="M5_SOLICITUD_INSPECCION_EMISION_RENOVACION";tramite="EMISION";return true;}
            if(s.TipoSolicitud.GetValueOrDefault()==2){modulo="M5_SOLICITUD_INSPECCION_EMISION_RENOVACION";tramite="RENOVACION";return true;}
            if(s.TipoSolicitud.GetValueOrDefault()==3 && (!string.IsNullOrWhiteSpace(s.AeropuertosEcuador)||!string.IsNullOrWhiteSpace(s.AeropuertosEcuadorOtros)))
            {modulo="M6_SOLICITUD_INSPECCION_MODIFICACION";tramite="MODIFICACION_CON_NUEVO_AEROPUERTO";return true;}
            return false;
        }
        private static SolicitudAOCR Clonar(SolicitudAOCR o,NoConformidad nc,int usuario,string actor){return new SolicitudAOCR{NumeroSolicitud=(o.NumeroSolicitud??"AOCR")+"-NC-"+nc.CodigoNoConformidad,FechaSolicitud=DateTime.Now,TipoSolicitud=o.TipoSolicitud,Estado="PENDIENTE",NombreOperador=o.NombreOperador,CodigoOaci=o.CodigoOaci,Ruc=o.Ruc,RazonSocial=o.RazonSocial,Email=o.Email,Telefono=o.Telefono,Direccion=o.Direccion,Ciudad=o.Ciudad,CodCiudad=o.CodCiudad,Provincia=o.Provincia,Pais=o.Pais,RepresentanteLegal=o.RepresentanteLegal,CedulaRepresentante=o.CedulaRepresentante,CorreoRepresentanteTecnico=o.CorreoRepresentanteTecnico,NombreComercial=o.NombreComercial,TipoOperacion=o.TipoOperacion,DescripcionOperacion=o.DescripcionOperacion,ResumenOperacionesEae=o.ResumenOperacionesEae,NumeroAOC=o.NumeroAOC,AprobacionesEspeciales=o.AprobacionesEspeciales,AprobacionesEspecialesOtros=o.AprobacionesEspecialesOtros,AeropuertosEcuador=o.AeropuertosEcuador,AeropuertosEcuadorOtros=o.AeropuertosEcuadorOtros,CompaniasSeleccionadas=o.CompaniasSeleccionadas,CodigoUsuario=usuario,Observaciones="Nueva inspección por NC "+nc.CodigoNoConformidad,UsuarioRegistro=actor};}
    }
}
