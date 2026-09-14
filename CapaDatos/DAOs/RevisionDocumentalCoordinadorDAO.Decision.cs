using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using CapaDatos.Constants;
using CapaDatos.Models;
using CapaModelo;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed partial class RevisionDocumentalCoordinadorDAO
    {
        // This operation deliberately does not call RegistrarDecision: that legacy method
        // assigns inspectors and enables LV/Informe before DIRCAV has designated them.
        public AocrWorkflowResult DecidirRevisionDocumental(int solicitudId, int usuarioId,
            string login, bool remitir, string observacion, string urlBandeja, string ip)
        {
            if (usuarioId <= 0) return AocrWorkflowResult.Error(401, "SESION_INVALIDA", "La sesión no es válida.");
            if (solicitudId <= 0) return AocrWorkflowResult.Error(400, "SOLICITUD_INVALIDA", "Solicitud inválida.");
            var texto = (observacion ?? string.Empty).Trim();
            if (texto.Length > 2000 || texto.Contains("<") || texto.Contains(">") || (!remitir && texto.Length == 0))
                return AocrWorkflowResult.Error(400, "OBSERVACION_INVALIDA", "Ingrese una observación válida; es obligatoria al devolver.");

            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    // Serialize competing decisions on the actual request row.
                    string anterior;
                    using (var cmd = new NpgsqlCommand(@"SELECT estado FROM public.aocr_tbsolicitud
WHERE codigo_solicitud=@s AND deleted_at IS NULL FOR UPDATE;", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("s", solicitudId);
                        var value = cmd.ExecuteScalar();
                        if (value == null) return AocrWorkflowResult.Error(404, "NO_EXISTE", "La solicitud no existe.");
                        anterior = Convert.ToString(value).Trim();
                    }
                    if (!EsEstadoDecisionCoordinador(anterior))
                        return AocrWorkflowResult.Error(409, "ESTADO_CAMBIO", "El expediente ya fue gestionado; recargue la bandeja.");

                    if (!UsuarioTieneRol(cn, tx, usuarioId, AocrRolesInstitucionales.Coordinador))
                        return AocrWorkflowResult.Error(403, "ROL_INVALIDO", "El usuario no tiene el rol COORDINADOR activo.");

                    int? inspector;
                    using (var cmd = new NpgsqlCommand(@"SELECT inspector_original_id FROM public.aocr_revision_documental_coordinador
WHERE solicitud_id=@s AND activo=TRUE AND documento_oficio_id IS NOT NULL FOR UPDATE;", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("s", solicitudId);
                        var value = cmd.ExecuteScalar();
                        if (value == null) return AocrWorkflowResult.Error(422, "REVISION_INCOMPLETA", "No existe una revisión con oficio para decidir.");
                        inspector = value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
                    }

                    var siguiente = remitir ? AocrEstadosProceso.PendienteDircav : AocrEstadosProceso.DevueltoInspector;
                    var key = "AC04-" + Guid.NewGuid().ToString("N");
                    var destinatarios = ObtenerDestinatariosDecision(cn, tx,
                        remitir ? AocrRolesInstitucionales.Dircav : AocrRolesInstitucionales.Inspector,
                        remitir ? (int?)null : inspector.GetValueOrDefault());
                    if (destinatarios.Count == 0)
                        return AocrWorkflowResult.Error(422, "SIN_DESTINATARIO", "No se pudo identificar un destinatario activo con correo válido. No se guardó la decisión.");

                    using (var cmd = new NpgsqlCommand(@"UPDATE public.aocr_revision_documental_coordinador
SET estado=@e, coordinador_id=@u, observacion_coordinador=@o, fecha_decision_coordinador=NOW(),
fecha_habilitacion_lv=NULL, fecha_habilitacion_informe=NULL, fecha_actualizacion=NOW()
WHERE solicitud_id=@s AND activo=TRUE;", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("s", solicitudId); cmd.Parameters.AddWithValue("u", usuarioId);
                        cmd.Parameters.AddWithValue("o", texto);
                        cmd.Parameters.AddWithValue("e", remitir ? EstadoRevisionDocumentalCoordinador.AceptadaCoordinador : EstadoRevisionDocumentalCoordinador.ObservadaCoordinador);
                        if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Conflicto de revisión documental.");
                    }
                    using (var cmd = new NpgsqlCommand(@"UPDATE public.aocr_tbsolicitud
SET estado=@e,observaciones=@o,updated_at=NOW(),updated_by=@u WHERE codigo_solicitud=@s AND estado=@anterior;", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("s", solicitudId); cmd.Parameters.AddWithValue("u", usuarioId.ToString());
                        cmd.Parameters.AddWithValue("o", texto); cmd.Parameters.AddWithValue("e", siguiente);
                        cmd.Parameters.AddWithValue("anterior", anterior);
                        if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Conflicto de estado documental.");
                    }
                    using (var cmd = new NpgsqlCommand(@"INSERT INTO public.aocr_tbhistorial_estado
(codigo_solicitud,estado_anterior,estado_nuevo,codigo_usuario,observaciones,fecha_cambio)
VALUES(@s,@a,@e,@u,@o,NOW());
INSERT INTO public.aocr_evento_workflow
(evento,event_key,correlation_id,modulo,accion,entidad,entidad_id,solicitud_id,estado_anterior,estado_nuevo,
usuario_id,usuario,rol,ip,observacion,resultado,intentos,fecha,created_at,updated_at)
VALUES(@e,@key,@key,'AC04',@e,'aocr_tbsolicitud',@s,@s,@a,@e,@u,@login,'COORDINADOR',@ip,@o,'EXITOSO',1,NOW(),NOW(),NOW());", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("s", solicitudId); cmd.Parameters.AddWithValue("u", usuarioId);
                        cmd.Parameters.AddWithValue("o", texto); cmd.Parameters.AddWithValue("a", anterior);
                        cmd.Parameters.AddWithValue("e", siguiente); cmd.Parameters.AddWithValue("key", key);
                        cmd.Parameters.AddWithValue("login", login ?? string.Empty); cmd.Parameters.AddWithValue("ip", (object)ip ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                    foreach (var destinatario in destinatarios)
                        EncolarDecision(cn, tx, solicitudId, destinatario.Key, destinatario.Value, siguiente, texto, key, urlBandeja);
                    tx.Commit();
                    return new AocrWorkflowResult { Exito=true,HttpStatusCode=200,EstadoAnterior=anterior,EstadoNuevo=siguiente,
                        CorrelationId=key,Mensaje=remitir ? "Expediente remitido a DIRCAV." : "Expediente devuelto al Inspector." };
                }
            }
        }

        private static bool EsEstadoDecisionCoordinador(string estado)
        {
            return string.Equals(estado,AocrEstadosProceso.PendienteCoordinador,StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado,EstadoSolicitud.AceptacionDocumental,StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado,AocrEstadosProceso.DevueltoCoordinador,StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado,AocrEstadosProceso.DevueltoCoordinadorPorDircav,StringComparison.OrdinalIgnoreCase);
        }

        private const string UsuariosPorRol = @"SELECT DISTINCT u.idusuario,TRIM(u.correo) correo
FROM public.usuario u JOIN public.usuario_rol ur ON u.codigousuario::text=ur.codigousuario::text
JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE UPPER(TRIM(r.descripcion))=@rol AND COALESCE(ur.activo,TRUE) AND COALESCE(r.activo,TRUE)
AND COALESCE(u.estadoactividad::text,'1')='1'";

        private static bool UsuarioTieneRol(NpgsqlConnection cn, NpgsqlTransaction tx, int usuario, string rol)
        {
            using (var cmd = new NpgsqlCommand("SELECT EXISTS("+UsuariosPorRol+" AND u.idusuario=@u);",cn,tx))
            {
                cmd.Parameters.AddWithValue("rol",rol); cmd.Parameters.AddWithValue("u",usuario);
                return Convert.ToBoolean(cmd.ExecuteScalar());
            }
        }

        private static Dictionary<int,string> ObtenerDestinatariosDecision(NpgsqlConnection cn,NpgsqlTransaction tx,string rol,int? usuario)
        {
            var result=new Dictionary<int,string>();
            using(var cmd=new NpgsqlCommand(UsuariosPorRol+(usuario.HasValue ? " AND u.idusuario=@u" : string.Empty),cn,tx))
            {
                cmd.Parameters.AddWithValue("rol",rol);
                if(usuario.HasValue) cmd.Parameters.AddWithValue("u",usuario.Value);
                using(var rd=cmd.ExecuteReader()) while(rd.Read())
                {
                    var email=Convert.ToString(rd["correo"]);
                    try { if(new MailAddress(email).Address==email) result[Convert.ToInt32(rd["idusuario"])]=email; }
                    catch(FormatException) { }
                    catch(ArgumentException) { }
                }
            }
            return result;
        }

        private static void EncolarDecision(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitud,int usuario,string correo,
            string estado,string observacion,string key,string url)
        {
            var mensaje="Solicitud "+solicitud+": "+estado+". "+observacion;
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.email_queue
(to_address,subject,body,status,solicitud_id,created_at,proximo_intento,event_key,intentos,updated_at,tipo_notificacion,correlation_id)
VALUES(@correo,@titulo,@body,'PENDIENTE',@s,NOW(),NOW(),@key||':EMAIL',0,NOW(),@estado,@corr);
INSERT INTO public.aocr_tbnotificacion
(codigousuario,titulo,mensaje,tipo,url,leida,fechacreacion,modulo,entidad_id,tipo_entidad,event_key,correlation_id,updated_at)
VALUES(@u,@titulo,@mensaje,@estado,@url,FALSE,NOW(),'AC04',@s,'SolicitudAOCR',@key||':NOTIF',@corr,NOW());",cn,tx))
            {
                cmd.Parameters.AddWithValue("correo",correo); cmd.Parameters.AddWithValue("titulo","AOCR: revisión documental");
                cmd.Parameters.AddWithValue("body","<p>"+WebUtility.HtmlEncode(mensaje)+"</p><p><a href=\""+WebUtility.HtmlEncode(url ?? string.Empty)+"\">Consultar expediente</a></p>");
                cmd.Parameters.AddWithValue("s",solicitud); cmd.Parameters.AddWithValue("u",usuario);
                cmd.Parameters.AddWithValue("key",key+":"+usuario); cmd.Parameters.AddWithValue("corr",key);
                cmd.Parameters.AddWithValue("estado",estado); cmd.Parameters.AddWithValue("mensaje",mensaje);
                cmd.Parameters.AddWithValue("url",url ?? string.Empty); cmd.ExecuteNonQuery();
            }
        }
    }
}
