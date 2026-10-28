using System;
using System.Configuration;
using System.IO;
using Npgsql;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaNegocio.Services;
using CapaDatos.Services;
using System.Collections.Generic;
using System.Linq;
class RemisionDcavTest {
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);Console.WriteLine("OK: "+msg);}
 static void Main(string[] args){
 var cs=ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
 var b=new NpgsqlConnectionStringBuilder(cs);if(b.Host!="127.0.0.1"||b.Port!=55440||b.Database!="remision_test")throw new Exception("Solo base temporal local.");
 using(var c=new NpgsqlConnection(cs)){c.Open();
 Action<string> sql=x=>{using(var q=new NpgsqlCommand(x,c))q.ExecuteNonQuery();};
 Func<string,object> scalar=x=>{using(var q=new NpgsqlCommand(x,c))return q.ExecuteScalar();};
 sql(File.ReadAllText(args[0]));
 sql(@"CREATE TABLE aocr_tbnotificacion(codigonotificacion serial primary key,codigousuario integer,titulo text,mensaje text,tipo text,url text,fechacreacion timestamp,leida boolean,modulo text,entidad_id integer,tipo_entidad text,fecha_lectura timestamp);
 CREATE TABLE usuario(idusuario integer,codigousuario text,nombreusuario text,apellidousuario text,rol text,correo text,estadoactividad integer);
 CREATE TABLE rol(codigorol integer,descripcion text,activo boolean);
 CREATE TABLE usuario_rol(codigousuario text,codigorol integer,activo boolean);
 INSERT INTO usuario VALUES(97,'GEN_DIRCAV','DCAV','Prueba','DIRCAV','dcav@example.test',1),(98,'BAJA','Inactivo','Prueba','DIRCAV','baja@example.test',0),(99,'GEN_DIRDAC','DIRDAC','Prueba','DIRDAC','dirdac@example.test',1);
 INSERT INTO rol VALUES(1,'DIRCAV',true),(2,'DIRDAC',true);
 INSERT INTO usuario_rol VALUES('GEN_DIRCAV',1,true),('BAJA',1,true),('GEN_DIRDAC',2,true);");
 sql(@"CREATE TABLE aocr_tbsolicitud(codigo_solicitud integer primary key,estado text,updated_at timestamp,updated_by text);
 CREATE TABLE aocr_tbhistorial_documental(codigo_solicitud integer,codigo_documento integer,evento text,detalle text,codigo_usuario integer,fecha_evento timestamp,created_at timestamp,created_by text);
 CREATE TABLE aocr_tbauditoria(codigo_auditoria serial primary key,tabla_afectada varchar(100) NOT NULL,registro_id integer,accion varchar(50) NOT NULL CHECK(accion <> 'FALLAR'),usuario varchar(100) NOT NULL,fecha_accion timestamp default now(),fecha_hora timestamp default now(),ip_address varchar(50),datos_anteriores jsonb,datos_nuevos jsonb,descripcion text,detalle text,modulo varchar(100),resultado varchar(20),mensaje_error text,usuario_id integer,fecha timestamp,entidad varchar(100));
 INSERT INTO aocr_tbsolicitud VALUES(7,'Aceptacion Documental',now(),'test'),(8,'Aceptacion Documental',now(),'test');");
 sql("ALTER TABLE aocr_tbsolicitud ADD COLUMN fecha_solicitud timestamp DEFAULT now();");
 var destinos=new NotificacionDestinatarioPolicyService().ResolverDestinatarios(new SolicitudAOCR{CodigoSolicitud=7},null,NotificacionDestinatarioPolicyService.GrupoDcav);
 Check(destinos.Count==1&&destinos[0].Email=="dcav@example.test","Resuelve DCAV activo sin incluir DIRDAC ni cuentas inactivas");
 var dao=new RevisionDocumentalCoordinadorDAO();
 Func<int,TransicionCoordinadorParams> parametros=id=>new TransicionCoordinadorParams{SolicitudId=id,CoordinadorId=90,InspectorOriginalId=79,Accion="COORDINADOR_REMITIR_DIRCAV",EstadoSolicitudDestino="PENDIENTE_DIRCAV",EstadoRevisionDestino="PENDIENTE_ACEPTACION_DCAV",UsuarioLogin="test",Observacion="Prueba"};
 sql("ALTER TABLE aocr_revision_documental_coordinador ADD CONSTRAINT chk_aocr_revdoc_coord_estado CHECK(estado IN ('PENDIENTE_COORDINADOR','PENDIENTE_DIRCAV','ACEPTADA_POR_COORDINADOR'));");
 Check(!dao.EjecutarTransicionCoordinadorTransaccional(parametros(7)).Exitoso,"Reproduce restriccion antigua que bloquea DCAV");
 sql(File.ReadAllText(args[1]));
 sql(File.ReadAllText(args[1]));
 Func<int,TransicionCoordinadorParams> conAvisos=id=>{
 var p=parametros(id);
 p.NotificacionesInternas=new List<Notificacion>{new Notificacion{CodigoUsuario=97,Titulo="Documentos pendientes de aprobacion",Mensaje="AOCR003 pendiente en DCAV",Tipo="INFO",Url="/aocr/Dircav/Bandeja?tab=documentacion",Modulo="DIRCAV",EntidadId=id,TipoEntidad="SOLICITUD",FechaCreacion=DateTime.Now}};
 p.CorreosAdicionales=new List<EmailQueueItem>{new EmailQueueItem{Para="dcav@example.test",Asunto="Documentos pendientes de aprobacion",Cuerpo="Revise AOCR003 en DCAV / Documentacion",SolicitudId=id,TipoNotificacion="SOLICITUD_PENDIENTE_DIRCAV",EventKey="DCAV:"+id}};
 return p;};
 var r=dao.EjecutarTransicionCoordinadorTransaccional(conAvisos(7));Check(r.Exitoso,r.Mensaje);
 Check(new DircavBandejaService().ObtenerDocumentacionPendienteAceptacion().Any(s=>s.CodigoSolicitud==7),"Solicitud remitida aparece en bandeja real DCAV Documentacion");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbnotificacion WHERE codigousuario=97 AND entidad_id=7 AND leida=false AND url='/aocr/Dircav/Bandeja?tab=documentacion'"))==1,"Campana DCAV con enlace a Documentacion");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE to_address='dcav@example.test' AND solicitud_id=7 AND status='PENDIENTE'"))==1,"Correo DCAV encolado en la transaccion");
 Check(r.Registro!=null&&r.Registro.InspectorOriginalId==79,"Crea control de coordinacion legado");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=7"))=="PENDIENTE_DIRCAV","Remite sin columna version");
 Check(Convert.ToString(scalar("SELECT datos_nuevos->>'EstadoAnterior' FROM aocr_tbauditoria LIMIT 1"))=="Aceptacion Documental","Auditoria JSONB conserva estado anterior");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbauditoria WHERE tabla_afectada='aocr_tbsolicitud' AND registro_id=7 AND usuario_id=90 AND datos_anteriores->>'Estado'='Aceptacion Documental' AND fecha_accion IS NOT NULL AND fecha_hora IS NOT NULL"))==1,"Auditoria completa con esquema del servidor");
 Check(!dao.EjecutarTransicionCoordinadorTransaccional(conAvisos(7)).Exitoso,"Segundo clic rechazado");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbnotificacion"))==1&&Convert.ToInt32(scalar("SELECT count(*) FROM email_queue"))==1,"Segundo clic no duplica campana ni correo");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbauditoria"))==1,"Sin duplicar auditoria");
 var fail=parametros(8);fail.Accion="FALLAR";Check(!dao.EjecutarTransicionCoordinadorTransaccional(fail).Exitoso,"Fallo de auditoria revierte transaccion");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=8"))=="Aceptacion Documental","Rollback conserva solicitud");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_revision_documental_coordinador WHERE solicitud_id=8"))==0,"Rollback elimina registro parcial");
 sql("ALTER TABLE email_queue ADD CONSTRAINT prueba_fallo_correo CHECK(subject <> 'FALLAR');");
 var falloCorreo=conAvisos(8);falloCorreo.CorreosAdicionales[0].Asunto="FALLAR";
 Check(!dao.EjecutarTransicionCoordinadorTransaccional(falloCorreo).Exitoso,"Fallo de correo revierte la remision");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbnotificacion WHERE entidad_id=8"))==0&&Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=8"))==0&&Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbauditoria WHERE registro_id=8"))==0,"Rollback elimina aviso, correo y auditoria parciales");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=8"))=="Aceptacion Documental","Solicitud se conserva si no se puede registrar aviso DCAV");
 sql(@"ALTER TABLE aocr_tbsolicitud ADD COLUMN version integer default 1;
 ALTER TABLE aocr_tbauditoria DROP COLUMN tabla_afectada;
 ALTER TABLE aocr_tbauditoria DROP COLUMN datos_anteriores;
 ALTER TABLE aocr_tbauditoria ADD COLUMN datos_previos text;
 ALTER TABLE aocr_tbauditoria ALTER COLUMN datos_nuevos TYPE text USING datos_nuevos::text;
 INSERT INTO aocr_tbsolicitud(codigo_solicitud,estado,version) VALUES(9,'Aceptacion Documental',1);");
 var next=parametros(9);next.ExpectedVersion=1;Check(dao.EjecutarTransicionCoordinadorTransaccional(next).Exitoso,"Compatible con version y auditoria texto");
 Check(Convert.ToInt32(scalar("SELECT version FROM aocr_tbsolicitud WHERE codigo_solicitud=9"))==2,"Version incrementada");
 sql("UPDATE aocr_revision_documental_coordinador SET estado='ACEPTADA_POR_DCAV' WHERE solicitud_id=9;");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_revision_documental_coordinador WHERE solicitud_id=9"))=="ACEPTADA_POR_DCAV","Restriccion permite firma DCAV despues de remision");
 sql(@"CREATE TABLE aocr_tbsolicitud_estacion(id serial primary key,solicitud_id integer,estacion_codigo text,estacion_nombre text,fecha_inicio timestamp,fecha_fin timestamp,inspector_id integer,inspector_nombre text,inspeccion_id integer,estado text,version integer,activo boolean,created_at timestamp,updated_at timestamp,created_by text,updated_by text);
 ALTER TABLE aocr_tbsolicitud_estacion ADD COLUMN observacion text, ADD COLUMN creado_en timestamp, ADD COLUMN creado_por text, ADD COLUMN actualizado_en timestamp, ADD COLUMN actualizado_por text;
 INSERT INTO aocr_tbsolicitud_estacion(solicitud_id,estacion_codigo,estacion_nombre,fecha_inicio,fecha_fin,activo) VALUES(7,'UIO','Quito','2026-10-21','2026-10-21',true);
 ALTER TABLE aocr_tbsolicitud ADD COLUMN deleted_at timestamp;
 ALTER TABLE aocr_tbsolicitud ADD COLUMN tecnico_responsable_cedula text;
 ALTER TABLE aocr_tbsolicitud ADD COLUMN tecnico_responsable_nombre text;
 UPDATE aocr_tbsolicitud SET tecnico_responsable_cedula='1700000000',tecnico_responsable_nombre='Inspector Prueba' WHERE codigo_solicitud=7;");
 var oficio=new DesignacionDocumentoService();
 var pdf=oficio.GenerarVistaPrevia(7,97,"DIRCAV");
 Check(pdf.Length>1000&&System.Text.Encoding.ASCII.GetString(pdf,0,4)=="%PDF","Vista previa PDF sin designacion formal para expediente remitido");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdesignacion_inspector WHERE solicitud_id=7"))==0,"Vista previa no crea designacion ni firma");
 var bloqueada=false;try{oficio.ConstruirDatosDesignacion(7);}catch(InvalidOperationException){bloqueada=true;}
 Check(bloqueada,"Construccion oficial sigue exigiendo designacion formal");
 var denegada=false;try{oficio.GenerarVistaPrevia(7,99,"DIRDAC");}catch(UnauthorizedAccessException){denegada=true;}
 Check(denegada,"Vista previa mantiene segregacion DIRDAC");
 sql(@"ALTER TABLE aocr_tbsolicitud DROP COLUMN version;
 ALTER TABLE aocr_tbsolicitud ADD COLUMN observaciones text;
 ALTER TABLE aocr_tbauditoria ADD COLUMN tabla_afectada varchar(100) NOT NULL DEFAULT 'prueba';
 ALTER TABLE aocr_tbauditoria ALTER COLUMN tabla_afectada DROP DEFAULT;
 ALTER TABLE aocr_tbauditoria DROP COLUMN datos_previos;
 ALTER TABLE aocr_tbauditoria ADD COLUMN datos_anteriores jsonb;
 ALTER TABLE aocr_tbauditoria ALTER COLUMN datos_nuevos TYPE jsonb USING to_jsonb(datos_nuevos);
 CREATE TABLE aocr_tbdocumento(codigo_documento serial primary key,codigo_solicitud integer,estado text);
 INSERT INTO aocr_tbsolicitud(codigo_solicitud,estado) VALUES(10,'PENDIENTE_DIRCAV'),(11,'DEVUELTO_COORDINADOR'),(12,'PENDIENTE_DIRCAV'),(13,'PENDIENTE_DIRCAV'),(14,'PENDIENTE_DIRCAV');
 INSERT INTO aocr_tbdocumento(codigo_solicitud,estado) VALUES(12,'OBSERVADO');
 INSERT INTO aocr_revision_documental_coordinador(solicitud_id,inspector_original_id,estado,activo) VALUES(13,79,'PENDIENTE_ACEPTACION_DCAV',true);");
 var dcav=new AocrDesignacionDAO();
 Func<int,DircavAceptarDocumentacionParams> aceptar=id=>new DircavAceptarDocumentacionParams{SolicitudId=id,DircavUsuarioId=97,DircavUsuarioNombre="DCAV prueba",Observacion="Revision DCAV"};
 var aceptado=dcav.EjecutarAceptacionDocumentalTransaccional(aceptar(10));
 Check(aceptado.Exitoso,"Aceptacion DCAV con tabla singular, sin version y auditoria JSONB: "+aceptado.Mensaje);
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbauditoria WHERE registro_id=10 AND tabla_afectada='aocr_tbsolicitud' AND accion='ACEPTAR_DOCUMENTACION'"))==1,"Auditoria de aceptacion con tabla afectada e identificador");
 Check(dcav.EjecutarAceptacionDocumentalTransaccional(aceptar(10)).HttpStatusCode==409,"Aceptacion repetida rechazada sin duplicados");
 Check(dcav.EjecutarAceptacionDocumentalTransaccional(aceptar(11)).HttpStatusCode==409,"Expediente devuelto no se acepta antes de nueva remision");
 Check(!dcav.EjecutarAceptacionDocumentalTransaccional(aceptar(12)).Exitoso,"Documentos observados bloquean aceptacion");
 sql("ALTER TABLE aocr_tbauditoria ADD CONSTRAINT prueba_rollback_dcav CHECK(registro_id NOT IN (13,14));");
 Check(!dcav.EjecutarAceptacionDocumentalTransaccional(aceptar(13)).Exitoso,"Fallo de auditoria cancela aceptacion");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=13"))=="PENDIENTE_DIRCAV"&&Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbhistorial_documental WHERE codigo_solicitud=13"))==0,"Aceptacion fallida no guarda estado ni historial parcial");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_revision_documental_coordinador WHERE solicitud_id=13 AND estado='PENDIENTE_ACEPTACION_DCAV' AND fecha_habilitacion_lv IS NULL"))==1,"Rollback de aceptacion mantiene LV bloqueada");
 Check(dcav.DevolverCoordinadorTransaccional(aceptar(10)).Exitoso,"Devolucion y auditoria se guardan juntas");
 Check(!dcav.DevolverCoordinadorTransaccional(aceptar(14)).Exitoso&&Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=14"))=="PENDIENTE_DIRCAV","Devolucion fallida no cambia estado");
 Check(dao.EjecutarTransicionCoordinadorTransaccional(parametros(10)).Exitoso,"Coordinador puede remitir de nuevo expediente devuelto");
 sql(@"ALTER TABLE aocr_tbsolicitud ADD COLUMN estado_documental text;
 INSERT INTO aocr_tbsolicitud(codigo_solicitud,estado,estado_documental) VALUES(15,'PENDIENTE_DIRCAV',NULL),(16,'PENDIENTE_DIRCAV','OBSERVADO');
 INSERT INTO aocr_revision_documental_coordinador(solicitud_id,inspector_original_id,estado,activo) VALUES(15,79,'PENDIENTE_ACEPTACION_DCAV',true);
 CREATE TABLE aocr_tbinspeccion(codigo_inspeccion integer,codigo_solicitud integer,estado_documental text);
 INSERT INTO aocr_tbinspeccion VALUES(150,15,'EN_REVISION_DOCUMENTAL');
 ALTER TABLE aocr_tbsolicitud ADD COLUMN codigo_usuario integer;
 INSERT INTO usuario VALUES(90,'RT_PRUEBA','RT','Prueba','RT','rt@example.test',1),(91,'RT_AJENO','RT','Ajeno','RT','ajeno@example.test',1);
 UPDATE aocr_tbsolicitud SET codigo_usuario=90 WHERE codigo_solicitud=15;");
 Check(dcav.EjecutarAceptacionDocumentalTransaccional(aceptar(15)).Exitoso,"Aceptacion compatible cuando estado_documental existe");
 Check(Convert.ToString(scalar("SELECT estado_documental FROM aocr_tbsolicitud WHERE codigo_solicitud=15"))=="ACEPTADO_DIRCAV","Conserva actualizacion de estado documental cuando existe");
 Check(new RevisionDocumentalCoordinadorService().EstaAceptadaParaInspector(15,79),"Aceptacion DCAV habilita control LV antes de designar o firmar");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=15 AND to_address='rt@example.test' AND tipo_notificacion='SOLICITUD_DOCUMENTACION_ACEPTADA_DIRCAV_RT'"))==1,"Aceptacion notifica al RT propietario");
 Check(Convert.ToString(scalar("SELECT estado_documental FROM aocr_tbinspeccion WHERE codigo_inspeccion=150"))=="ACEPTADA","Aceptacion cierra tambien estado documental de la inspeccion");
 Check(!new RevisionDocumentalCoordinadorService().EstaAceptadaParaInspector(15,98),"Aceptacion no habilita LV de inspector ajeno");
 var fechaLvAceptacion=Convert.ToDateTime(scalar("SELECT fecha_habilitacion_lv FROM aocr_revision_documental_coordinador WHERE solicitud_id=15"));
 var faseCerrada=new EstadoRevisionDocumental{TotalDocumentosVigentes=1,DocumentacionAprobada=true};
 typeof(CapaNegocio.SolicitudAocrInfraBL).GetMethod("ConfigurarFlujoDocumental",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{faseCerrada,new SolicitudAOCR{Estado="DOCUMENTACION_ACEPTADA_DIRCAV"},new List<Inspeccion>()});
 Check(faseCerrada.FlujoDocumentalCodigo=="CERRADA_POR_DCAV"&&!faseCerrada.VisibleEnBandejaCoordinador&&!faseCerrada.VisibleEnBandejaInspector&&!faseCerrada.VisibleEnBandejaRt,"Aceptacion retira fase cerrada de bandejas de revision documental");
 var decisionDoc=typeof(CapaNegocio.SolicitudAocrInfraBL).GetMethod("ObtenerDecisionRevisionDocumental",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
 Func<string,bool,string> evaluarDoc=(estado,cierre)=>Convert.ToString(decisionDoc.Invoke(null,new object[]{new Documento{CodigoDocumento=1,Estado=estado},new Dictionary<int,Tuple<string,string>>(),true,cierre}));
 Check(evaluarDoc("Aprobado",true)=="ACEPTADO"&&evaluarDoc("ACEPTADO",true)=="ACEPTADO","Cierre DCAV reconoce documentos aceptados sin exigir segunda revision del inspector");
 Check(evaluarDoc("Aprobado",false)=="","Sin cierre DCAV se conserva revision obligatoria del inspector");
 Check(evaluarDoc("OBSERVADO",true)=="OBSERVADO"&&evaluarDoc("SUBSANADO",true)=="SUBSANADO","Observados y subsanados posteriores siguen bloqueando LV");
 Check(!dcav.EjecutarAceptacionDocumentalTransaccional(aceptar(16)).Exitoso,"Estado documental observado sigue bloqueando aceptacion");
 var estadosAntes=Convert.ToString(scalar("SELECT string_agg(codigo_solicitud::text||':'||estado,',' ORDER BY codigo_solicitud) FROM aocr_tbsolicitud"));
 sql(File.ReadAllText(args[2]));sql(File.ReadAllText(args[2]));
 Check(Convert.ToInt32(scalar("SELECT version FROM aocr_tbsolicitud WHERE codigo_solicitud=7"))==1,"Migracion agrega version inicial");
 Check(Convert.ToString(scalar("SELECT estado_documental FROM aocr_tbsolicitud WHERE codigo_solicitud=15"))=="ACEPTADO_DIRCAV","Migracion repetida conserva datos documentales existentes");
 Check(estadosAntes==Convert.ToString(scalar("SELECT string_agg(codigo_solicitud::text||':'||estado,',' ORDER BY codigo_solicitud) FROM aocr_tbsolicitud")),"Migracion no modifica estados de expedientes");
 sql(@"ALTER TABLE aocr_tbsolicitud ADD COLUMN codigo_tecnico integer, ADD COLUMN tecnico_responsable_tipo text, ADD COLUMN inspector_apoyo_cedula text, ADD COLUMN inspector_apoyo_nombre text, ADD COLUMN inspector_apoyo_tipo text;
 INSERT INTO aocr_tbsolicitud_estacion(solicitud_id,estacion_codigo,estacion_nombre,fecha_inicio,fecha_fin,activo,version,estado) VALUES(15,'UIO','Quito','2026-10-21','2026-10-21',true,1,'PROGRAMADA');");
 Func<int,DircavDesignarInspectorParams> designar=id=>new DircavDesignarInspectorParams{SolicitudId=id,InspectorId=79,InspectorCedula="1700000000",InspectorNombre="Inspector Prueba",InspectorTipo="OPS",DircavUsuarioId=97,DircavUsuarioNombre="DCAV prueba",Motivo="Designacion de prueba"};
 var designado=dcav.EjecutarDesignacionTransaccional(designar(15));
 Check(designado.Exitoso,"Designacion completa sin tecnico_responsable_id: "+designado.Mensaje);
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=15 AND to_address='rt@example.test' AND tipo_notificacion='SOLICITUD_DESIGNACION_INSPECTOR_REGISTRADA_RT'"))==1,"Designacion notifica al RT antes de firma");
 Check(Convert.ToInt32(scalar("SELECT codigo_tecnico FROM aocr_tbsolicitud WHERE codigo_solicitud=15"))==79,"Inspector queda registrado en codigo_tecnico");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdesignacion_inspector WHERE solicitud_id=15 AND vigente=true AND firmado=false"))==1,"Designacion formal unica pendiente de firma");
 Check(Convert.ToInt32(scalar("SELECT inspector_id FROM aocr_tbsolicitud_estacion WHERE solicitud_id=15 LIMIT 1"))==79,"Designacion actualiza inspector de estacion");
 Check(dcav.EjecutarDesignacionTransaccional(designar(15)).Exitoso&&Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdesignacion_inspector WHERE solicitud_id=15 AND vigente=true"))==1,"Repetir designacion no duplica registro vigente");
 sql("INSERT INTO aocr_tbsolicitud(codigo_solicitud,estado) SELECT 17,'DOCUMENTACION_ACEPTADA_DIRCAV'; ALTER TABLE aocr_tbauditoria ADD CONSTRAINT prueba_fallo_designacion CHECK(registro_id <> 17);");
 Check(!dcav.EjecutarDesignacionTransaccional(designar(17)).Exitoso,"Fallo de auditoria revierte designacion");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdesignacion_inspector WHERE solicitud_id=17"))==0&&Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=17"))=="DOCUMENTACION_ACEPTADA_DIRCAV","Sin designacion parcial ni cambio de estado al fallar");
 sql(@"INSERT INTO usuario VALUES(79,'INSPECTOR','Inspector','Prueba','Inspector','inspector@example.test',1);");
 var firma=new DircavFirmarDesignacionParams{SolicitudId=15,EstacionId=null,DircavUsuarioId=97,DircavUsuarioNombre="DCAV prueba",RutaDocumentoFirmado="/prueba/firmado.pdf",HashDocumento="HASH_TRANSACCION_SIMULADA",TamanioBytes=1024,CodigoVerificacion="PRUEBA"};
 var crearCorreos=typeof(DesignacionDocumentoService).GetMethod("CrearCorreosOficioFirmado",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
 firma.CorreosDocumentoFirmado=(List<EmailQueueItem>)crearCorreos.Invoke(null,new object[]{15,1,"AOCR-PRUEBA","/prueba/firmado.pdf","Oficio_Firmado.pdf","HASH_TRANSACCION_SIMULADA",1024L,new List<NotificacionDestinatario>{new NotificacionDestinatario{Email="rt@example.test",Nombre="RT"},new NotificacionDestinatario{Email="inspector@example.test",Nombre="Inspector"},new NotificacionDestinatario{Email="INSPECTOR@example.test",Nombre="Duplicado"}}});
 Check(firma.CorreosDocumentoFirmado.Count==2,"Correos del oficio para RT e Inspector sin duplicados");
 var firmaGuardada=dcav.EjecutarFirmaDesignacionTransaccional(firma);
 Check(firmaGuardada.Exitoso,"Persistencia de firma con estacion nula: "+firmaGuardada.Mensaje);
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdesignacion_inspector WHERE solicitud_id=15 AND firmado=true"))==1,"Marca firma persistida en base temporal");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud_estacion WHERE solicitud_id=15 LIMIT 1"))=="DESIGNADO","Firma general actualiza estacion sin error de parametro");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_revision_documental_coordinador WHERE solicitud_id=15"))=="ACEPTADA_POR_DCAV"&&Convert.ToDateTime(scalar("SELECT fecha_habilitacion_lv FROM aocr_revision_documental_coordinador WHERE solicitud_id=15"))==fechaLvAceptacion,"Firma conserva cierre y habilitacion de LV de la aceptacion");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=15 AND to_address='inspector@example.test'"))==1,"Firma encola aviso al inspector");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue q JOIN email_attachment a ON a.email_queue_id=q.id WHERE q.solicitud_id=15 AND q.to_address IN ('rt@example.test','inspector@example.test') AND a.file_name='Oficio_Firmado.pdf' AND a.content_type='application/pdf' AND a.sha256='HASH_TRANSACCION_SIMULADA'"))==2,"Ambos correos incluyen el mismo PDF firmado con hash");
 Check(dcav.EjecutarFirmaDesignacionTransaccional(firma).EsIdempotente,"Segundo registro de firma no duplica efectos");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_attachment a JOIN email_queue q ON q.id=a.email_queue_id WHERE q.solicitud_id=15"))==2,"Repetir firma no duplica correos ni adjuntos");
 sql(@"UPDATE aocr_tbsolicitud SET codigo_usuario=90 WHERE codigo_solicitud=15;
 INSERT INTO aocr_tbsolicitud(codigo_solicitud,estado,codigo_usuario) VALUES(18,'DOCUMENTACION_ACEPTADA_DIRCAV',90);");
 Check(dcav.EjecutarDesignacionTransaccional(designar(18)).Exitoso,"Prepara oficio no firmado del mismo RT");
 Check(dcav.ListarOficiosFirmadosUsuario(90,true,false).Count==1,"RT ve solo oficio propio firmado, excluye pendiente");
 Check(dcav.ListarOficiosFirmadosUsuario(79,false,true).Count==1,"Inspector asignado ve oficio firmado en bandeja");
 Check(dcav.ListarOficiosFirmadosUsuario(91,true,false).Count==0&&dcav.ListarOficiosFirmadosUsuario(98,false,true).Count==0,"Bandejas no muestran oficios de expedientes ajenos");
 var archivoPrueba=Path.Combine(Path.GetTempPath(),"aocr_descarga_test_"+Guid.NewGuid().ToString("N")+".pdf");
 try{
 var contenido=System.Text.Encoding.ASCII.GetBytes("%PDF-prueba-descarga-exacta");File.WriteAllBytes(archivoPrueba,contenido);
 using(var q=new NpgsqlCommand("UPDATE aocr_tbdesignacion_inspector SET ruta_documento_firmado=@ruta WHERE solicitud_id=15",c)){q.Parameters.AddWithValue("@ruta",archivoPrueba);q.ExecuteNonQuery();}
 string descarga;
 Check(oficio.ObtenerDocumentoParaDescarga(15,90,"RT","RT_PRUEBA",out descarga).SequenceEqual(contenido),"RT descarga bytes exactos del documento guardado");
 Check(oficio.ObtenerDocumentoParaDescarga(15,79,"Inspector","INSPECTOR",out descarga).SequenceEqual(contenido),"Inspector descarga el mismo archivo firmado");
 var rtAjenoBloqueado=false;try{oficio.ObtenerDocumentoParaDescarga(15,91,"RT","RT_AJENO",out descarga);}catch(UnauthorizedAccessException){rtAjenoBloqueado=true;}
 Check(rtAjenoBloqueado,"Descarga directa bloqueada para RT ajeno");
 var inspectorAjenoBloqueado=false;try{oficio.ObtenerDocumentoParaDescarga(15,98,"Inspector","AJENO",out descarga);}catch(UnauthorizedAccessException){inspectorAjenoBloqueado=true;}
 Check(inspectorAjenoBloqueado,"Descarga directa bloqueada para inspector ajeno");
 File.Delete(archivoPrueba);
 var sinSustituto=false;try{oficio.ObtenerDocumentoParaDescarga(15,90,"RT","RT_PRUEBA",out descarga);}catch(FileNotFoundException){sinSustituto=true;}
 Check(sinSustituto,"Archivo firmado faltante no se reemplaza por PDF regenerado");
 }finally{if(File.Exists(archivoPrueba))File.Delete(archivoPrueba);}
 sql("UPDATE aocr_revision_documental_coordinador SET estado='PENDIENTE_ACEPTACION_DCAV',fecha_habilitacion_lv=NULL,fecha_habilitacion_informe=NULL WHERE solicitud_id=15; UPDATE aocr_tbinspeccion SET estado_documental='EN_REVISION_DOCUMENTAL' WHERE codigo_solicitud=15;");
 var informesDao=new InspeccionInformeDAO();
 var informeQuito=informesDao.GuardarBorrador(new InspeccionInformeTecnico{CodigoInspeccion=150,EstacionId=101,Titulo="Informe Quito",Resumen="Solo Quito"},79);
 var informeGye=informesDao.GuardarBorrador(new InspeccionInformeTecnico{CodigoInspeccion=150,EstacionId=102,Titulo="Informe Guayaquil",Resumen="Solo Guayaquil"},79);
 Check(informeQuito.CodigoInforme!=informeGye.CodigoInforme&&informeQuito.EstacionId==101&&informeGye.EstacionId==102,"Cada estacion guarda su propio informe con identidad independiente");
 informesDao.MarcarFinalizado(informeQuito.CodigoInforme,"/prueba/quito.pdf",false,"INFORME_GENERADO",79);
 informesDao.RegistrarFirmaInspector(informeQuito.CodigoInforme,"/prueba/quito_firmado.pdf","HASH_QUITO",DateTime.Now,"Inspector prueba","FIRMADO_INSPECTOR",79);
 informeGye.Resumen="Avance Guayaquil";
 var gyeEditado=informesDao.GuardarBorrador(informeGye,79);
 var quitoFirmado=informesDao.ObtenerUltimoPorInspeccion(150,101);
 Check(gyeEditado.CodigoInforme==informeGye.CodigoInforme&&gyeEditado.Resumen=="Avance Guayaquil"&&!gyeEditado.FirmadoInspector,"Guayaquil sigue editable despues de firmar Quito");
 Check(quitoFirmado.FirmadoInspector&&quitoFirmado.Resumen=="Solo Quito"&&quitoFirmado.RutaDocumentoFirmado=="/prueba/quito_firmado.pdf","Guardar Guayaquil conserva contenido y firma de Quito");
 Check(informesDao.ObtenerUltimoPorInspeccion(150,103)==null,"Estacion nueva no hereda informe ni firma de otra");
 Check(informesDao.ListarUltimosPorEstacion(150).Count==2,"Bandeja recupera los dos informes de estacion");
 Check(informesDao.ListarPendientesFirmaDirdac().Any(i=>i.CodigoInforme==informeQuito.CodigoInforme),"Informe firmado por estacion aparece en revision institucional");
 informesDao.MarcarFinalizado(informeGye.CodigoInforme,"/prueba/gye.pdf",false,"INFORME_GENERADO",79);
 informesDao.RegistrarFirmaInspector(informeGye.CodigoInforme,"/prueba/gye_firmado.pdf","HASH_GYE",DateTime.Now,"Inspector prueba","FIRMADO_INSPECTOR",79);
 Check(informesDao.ListarPendientesFirmaDirdac().Count(i=>i.CodigoInspeccion==150)==2,"Revision institucional incluye ambos informes firmados de la misma inspeccion");
 informesDao.RegistrarFirmaDirdac(informeQuito.CodigoInforme,"/prueba/quito_aprobado.pdf","HASH_QUITO_FINAL",DateTime.Now,"Direccion prueba","FIRMADO_FINAL",97);
 Check(informesDao.ObtenerPorId(informeQuito.CodigoInforme).FirmadoDirdac&&!informesDao.ObtenerPorId(informeGye.CodigoInforme).FirmadoDirdac,"Decision institucional de Quito no aprueba Guayaquil");
 var migracionInformes=File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[2]),"20261021_informes_por_estacion.sql"));
 sql(migracionInformes);sql(migracionInformes);
 Check(informesDao.ObtenerPorId(informeQuito.CodigoInforme).HashDocumento=="HASH_QUITO_FINAL","Migracion repetible conserva firma y contenido de informe existente");
 var regularizar=File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[2]),"20261020_habilitar_lv_aceptadas_dcav.sql"));
 sql(regularizar);sql(regularizar);
 Check(new RevisionDocumentalCoordinadorService().EstaAceptadaParaInspector(15,79)&&Convert.ToString(scalar("SELECT estado_documental FROM aocr_tbinspeccion WHERE codigo_inspeccion=150"))=="ACEPTADA","Regularizacion repetible habilita expediente previamente aceptado");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_revision_documental_coordinador WHERE solicitud_id=13"))=="PENDIENTE_ACEPTACION_DCAV","Regularizacion no acepta expedientes pendientes");

 // Paquete final: pruebas reales en PostgreSQL desechable, sin SMTP.
 sql(@"ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS tipo_solicitud integer DEFAULT 1;
 ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS numero_solicitud text;
 ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS razon_social text;
 ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS nombre_operador text;
 ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS deleted_at timestamp;
 ALTER TABLE aocr_tbinspeccion ADD COLUMN IF NOT EXISTS numero_inspeccion text;
 ALTER TABLE aocr_tbinspeccion ADD COLUMN IF NOT EXISTS codigo_inspector integer;
 UPDATE aocr_tbinspeccion SET codigo_inspector=79,numero_inspeccion='INS-150' WHERE codigo_inspeccion=150;
 UPDATE aocr_tbsolicitud SET codigo_usuario=90,tipo_solicitud=1,numero_solicitud='AOCR-PRUEBA-15' WHERE codigo_solicitud=15;
 DELETE FROM aocr_tbsolicitud_estacion WHERE solicitud_id=15;
 INSERT INTO aocr_tbsolicitud_estacion(id,solicitud_id,estacion_codigo,inspeccion_id,activo) VALUES(101,15,'UIO',150,true),(102,15,'GYE',150,true);
 UPDATE aocr_tbinforme_inspeccion SET resultado='SATISFACTORIO' WHERE codigo_inspeccion=150;
 CREATE TABLE IF NOT EXISTS aocr_proceso_estado(id serial primary key,solicitud_id integer,inspeccion_id integer,estado_actual text,etapa_actual text,rol_responsable text,observacion text,activo boolean,version integer,fecha_estado timestamp,created_at timestamp,created_by integer,updated_at timestamp,updated_by integer);
 UPDATE aocr_proceso_estado SET activo=false WHERE solicitud_id=15;
 INSERT INTO aocr_proceso_estado(solicitud_id,inspeccion_id,estado_actual,activo,version) VALUES(15,150,'DOCUMENTOS_FINALES_POR_GENERAR',true,1);
 CREATE TABLE aocr_tbdocumento_generado(codigo_documento serial primary key,codigo_solicitud integer,codigo_inspeccion integer,tipo_documento text,numero_aocr text,nombre_archivo text,ruta_documento text,tamanio_pdf bigint,estado text,fecha_generacion timestamp,codigo_usuario integer,usuario_nombre text,created_at timestamp,hash_pdf_firmado text,codigo_usuario_liberacion integer);
 CREATE TABLE aocr_tbnoconformidad(codigo_solicitud integer,codigo_inspeccion integer,codigo_inspeccion_nueva integer,fecha_cierre timestamp,estado text);
 INSERT INTO usuario VALUES(96,'COORD','Coordinador','Prueba','COORDINADOR','coordinador@example.test',1);
 INSERT INTO rol VALUES(3,'COORDINADOR',true),(4,'INSPECTOR',true); INSERT INTO usuario_rol VALUES('COORD',3,true),('INSPECTOR',4,true);");
 var paquetes=new DocumentosFinalesWorkflowDAO();
 paquetes.ObtenerVigente(15,"RECONOCIMIENTO"); // Construye columnas adicionales y outbox.
 sql(@"INSERT INTO aocr_tbdocumento_generado(codigo_solicitud,codigo_inspeccion,tipo_documento,ruta_documento,tamanio_pdf,estado,completo,hash_pdf)
 VALUES(15,150,'RECONOCIMIENTO','/prueba/aocr.pdf',10,'GENERADO',true,'HASH_AOCR'),(15,150,'CONDICIONES_LIMITACIONES','/prueba/cl.pdf',10,'GENERADO',true,'HASH_CL');
 ALTER TABLE aocr_tbdocumento_generado ADD CONSTRAINT ck_documento_final_estado CHECK(estado IN ('GENERADO','AOCR_LISTO_PARA_FIRMA','PENDIENTE_FIRMA_CONDICIONES_DCAV'));");
 var root=Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(args[0])));
 sql(File.ReadAllText(Path.Combine(root,"sql/20261022_revision_paquete_coordinador.sql")));
 sql(File.ReadAllText(Path.Combine(root,"sql/20261022_revision_paquete_coordinador.sql")));
 Func<DocumentoFinalEnvioRequest> envioPaquete=()=>{
 var ad=paquetes.ObtenerVigente(15,"RECONOCIMIENTO");var cd=paquetes.ObtenerVigente(15,"CONDICIONES_LIMITACIONES");
 Func<AocrDocumentoGenerado,DocumentoFinalEvidencia> ev=d=>new DocumentoFinalEvidencia{DocumentoId=d.CodigoDocumento,InspeccionId=150,Version=d.VersionDocumento,TipoDocumento=d.TipoDocumento,RutaPdf=d.RutaDocumento,HashPdf=d.HashPdf,TamanioPdf=d.TamanioPdf.Value};
 return new DocumentoFinalEnvioRequest{SolicitudId=15,InspeccionId=150,InspectorId=79,InspectorNombre="Inspector",Aocr=ev(ad),Condiciones=ev(cd),RequiereAocr=true,RequiereCondiciones=true};};
 sql("UPDATE aocr_tbinforme_inspeccion SET firmado_inspector=false WHERE estacion_id=102;");
 var rechazo=false;try{paquetes.FinalizarYEncolar(envioPaquete());}catch(InvalidOperationException){rechazo=true;}
 Check(rechazo&&Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdocumento_generado WHERE bloqueado=true"))==0,"Informe GYE pendiente bloquea envio y revierte paquete completo");
 sql("UPDATE aocr_tbinforme_inspeccion SET firmado_inspector=true WHERE estacion_id=102;");
 sql("UPDATE rol SET descripcion='CoordinadorInspecciones' WHERE codigorol=3; ALTER TABLE email_queue ADD CONSTRAINT prueba_error_envio CHECK(tipo_notificacion<>'CONDICIONES_PENDIENTES_REVISION_COORDINADOR')");
 rechazo=false;try{paquetes.FinalizarYEncolar(envioPaquete());}catch(Npgsql.PostgresException){rechazo=true;}
 Check(rechazo&&paquetes.ObtenerEstado(15)=="DOCUMENTOS_FINALES_POR_GENERAR"&&Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=15 AND tipo_notificacion LIKE '%REVISION_COORDINADOR'"))==0&&Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbnotificacion WHERE entidad_id=15 AND tipo LIKE '%REVISION_COORDINADOR'"))==0,"Fallo al encolar segundo documento revierte paquete, ambos correos y ambos avisos internos");
 sql("ALTER TABLE email_queue DROP CONSTRAINT prueba_error_envio");
 var enviado=paquetes.FinalizarYEncolar(envioPaquete());
 Check(enviado.Exitoso&&paquetes.ObtenerEstado(15)=="DOCUMENTOS_FINALES_COORDINADOR","Paquete se remite al Coordinador, no a DCAV ni DIRDAC");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=15 AND tipo_notificacion IN ('AOCR_PENDIENTE_REVISION_COORDINADOR','CONDICIONES_PENDIENTES_REVISION_COORDINADOR') AND to_address='coordinador@example.test'"))==2,"Avisos de ambos documentos al Coordinador en la transaccion");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbnotificacion WHERE entidad_id=15 AND codigousuario=96 AND tipo IN ('AOCR_PENDIENTE_REVISION_COORDINADOR','CONDICIONES_PENDIENTES_REVISION_COORDINADOR')"))==2,"Bandeja interna del Coordinador con rol CoordinadorInspecciones recibe ambos avisos");
 Check(paquetes.FinalizarYEncolar(envioPaquete()).Idempotente,"Doble clic no duplica paquete ni avisos");
 var verPaquete=paquetes.ObtenerVigente(15,"CONDICIONES_LIMITACIONES").VersionConcurrencia;
 rechazo=false;try{paquetes.RevisarPaqueteCoordinador(15,150,97,"DIRCAV",verPaquete,false,"","");}catch(UnauthorizedAccessException){rechazo=true;}
 Check(rechazo,"DCAV no puede saltarse la revision del Coordinador");
 rechazo=false;try{paquetes.RevisarPaqueteCoordinador(15,150,96,"COORDINADOR",verPaquete-1,false,"","");}catch(InvalidOperationException){rechazo=true;}
 Check(rechazo&&paquetes.ObtenerEstado(15)=="DOCUMENTOS_FINALES_COORDINADOR","Version obsoleta no remite ni cambia estado");
 var revisado=paquetes.RevisarPaqueteCoordinador(15,150,96,"COORDINADOR",verPaquete,false,"Revision conforme","");
 Check(revisado.Exitoso&&paquetes.ObtenerEstado(15)=="DOCUMENTOS_FINALES_EN_FIRMA","Coordinador revisa y remite a DCAV");
 Check(paquetes.ObtenerVigente(15,"CONDICIONES_LIMITACIONES").Estado=="PENDIENTE_FIRMA_CONDICIONES_DCAV"&&paquetes.ObtenerVigente(15,"RECONOCIMIENTO").Estado=="AOCR_LISTO_PARA_FIRMA","CL habilitada a DCAV; AOCR aun no se envia a DIRDAC");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=15 AND tipo_notificacion='AVANCE_AOCR' AND to_address='rt@example.test'"))>=2,"RT recibe avances del envio y revision del paquete");

 // Devolucion y reenvio: no reescribe informes firmados ni pierde avisos.
 sql("UPDATE aocr_proceso_estado SET estado_actual='DOCUMENTOS_FINALES_COORDINADOR' WHERE solicitud_id=15 AND activo=true; UPDATE aocr_tbdocumento_generado SET estado='DOCUMENTOS_FINALES_COORDINADOR' WHERE codigo_solicitud=15;");
 verPaquete=paquetes.ObtenerVigente(15,"CONDICIONES_LIMITACIONES").VersionConcurrencia;
 rechazo=false;try{paquetes.RevisarPaqueteCoordinador(15,150,96,"COORDINADOR",verPaquete,true,"","");}catch(InvalidOperationException){rechazo=true;}
 Check(rechazo,"Devolucion exige motivo antes de modificar el paquete");
 Check(paquetes.RevisarPaqueteCoordinador(15,150,96,"COORDINADOR",verPaquete,true,"Corregir datos","").Exitoso,"Coordinador devuelve paquete al Inspector");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdocumento_generado WHERE codigo_solicitud=15 AND bloqueado=false AND estado='DOCUMENTOS_FINALES_DEVUELTOS'"))==2,"Devolucion desbloquea ambas versiones sin tocar firmas de informes");
 Check(paquetes.FinalizarYEncolar(envioPaquete()).Exitoso,"Inspector puede volver a remitir el paquete devuelto");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE solicitud_id=15 AND tipo_notificacion='AOCR_PENDIENTE_REVISION_COORDINADOR' AND to_address='coordinador@example.test'"))==2,"Reenvio genera nuevo aviso; doble clic anterior no duplicaba");
 // Jefaturas: invoca la misma operacion transaccional usada por la entrega final.
 sql(File.ReadAllText(Path.Combine(root,"sql/20261022_destinatarios_jefaturas_aeropuertos.sql")));
 sql("INSERT INTO aocr_jefatura_aeropuerto_correo(estacion_codigo,correo,nombre) VALUES('UIO','jefatura.uio@example.test','Jefatura UIO'),('GYE','jefatura.gye@example.test','Jefatura GYE');");
 var entregaTipo=typeof(EntregaFinalDAO);
 Func<string,Dictionary<string,object>,object> privado=(nombre,campos)=>{var t=entregaTipo.GetNestedType(nombre,System.Reflection.BindingFlags.NonPublic);var o=Activator.CreateInstance(t,true);foreach(var campo in campos)t.GetField(campo.Key).SetValue(o,campo.Value);return o;};
 var expEntrega=privado("Expediente",new Dictionary<string,object>{{"SolicitudId",15},{"NumeroSolicitud","AOCR-15"}});
 var docA=privado("DocumentoEntrega",new Dictionary<string,object>{{"Version",1},{"Ruta","/App_Data/Uploads/AOCR/Firmados/aocr.pdf"},{"Nombre","AOCR.pdf"},{"Tamanio",10L},{"HashFirmado","HASH_A"}});
 var docCl=privado("DocumentoEntrega",new Dictionary<string,object>{{"Version",1},{"Ruta","/App_Data/Uploads/AOCR/Firmados/cl.pdf"},{"Nombre","CL.pdf"},{"Tamanio",11L},{"HashFirmado","HASH_CL"}});
 var encolarJefaturas=entregaTipo.GetMethod("EncolarJefaturas",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 using(var tx=c.BeginTransaction()) {encolarJefaturas.Invoke(null,new object[]{c,tx,expEntrega,docA,docCl,new SolicitarEntregaFinalRequest{SolicitudId=15},"PRUEBA-JEFATURAS"});tx.Commit();}
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE tipo_notificacion='ENTREGA_FINAL_JEFATURA' AND solicitud_id=15"))==2,"Una copia final para cada jefatura de estacion involucrada");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_attachment a JOIN email_queue q ON q.id=a.email_queue_id WHERE q.tipo_notificacion='ENTREGA_FINAL_JEFATURA'"))==4,"Cada jefatura recibe AOCR y CL adjuntos");
 using(var tx=c.BeginTransaction()) {encolarJefaturas.Invoke(null,new object[]{c,tx,expEntrega,docA,docCl,new SolicitarEntregaFinalRequest{SolicitudId=15},"PRUEBA-JEFATURAS"});tx.Commit();}
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM email_queue WHERE tipo_notificacion='ENTREGA_FINAL_JEFATURA' AND solicitud_id=15"))==2,"Reintento de entrega a jefaturas no duplica copias");

 // Observaciones 19: datos documentales y numeracion AOCR; siempre base local.
 sql(@"ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS numero_aocr varchar(80);
 ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS codigo_oaci varchar(10);
 ALTER TABLE aocr_tbsolicitud ADD COLUMN IF NOT EXISTS ruc varchar(100);
 CREATE TABLE IF NOT EXISTS aocr_tbcertificado(codigo_certificado serial primary key,codigo_solicitud integer UNIQUE,numero_certificado varchar(80) CONSTRAINT aocr_tbcertificado_numero_certificado_key UNIQUE,firmado_por text,ruta_pdf text);
 CREATE TABLE IF NOT EXISTS aocr_tbfirma_documento(codigo_solicitud integer);
 INSERT INTO aocr_tbsolicitud(codigo_solicitud,tipo_solicitud,codigo_oaci,ruc,numero_solicitud) VALUES
 (201,1,'QTR','QTR-TEST','DGAC-GOP-2026-AOCR003'),(202,2,'QTR','QTR-TEST','DGAC-GOP-2026-AOCR202'),(203,3,'QTR','QTR-TEST','DGAC-GOP-2026-AOCR203'),(204,2,'XXX','OTRO','DGAC-GOP-2026-AOCR204'),(205,1,'QTR','QTR-TEST','DGAC-GOP-2026-AOCR005'),(207,1,'AAA','OTRO','DGAC-GOP-2026-AOCR007');");
 sql(File.ReadAllText(Path.Combine(root,"sql/20261027_datos_documentales_certificado.sql")));
 sql(File.ReadAllText(Path.Combine(root,"sql/20261027_datos_documentales_certificado.sql")));
 var certDatos=new CertificadoDAO();
 var datos19=new DatosDocumentalesAocr {PermisoCnacNumero="CNAC-PRUEBA-19",PermisoCnacEmision=new DateTime(2026,10,26),PermisoCnacVencimiento=new DateTime(2029,10,26),AocVigenteHasta=new DateTime(2030,1,1),PaisBandera="Qatar",
 Explotador=new ContactoDocumentalAocr{Nombre="Explotador origen",Direccion="Base en Doha",Telefono="111",Correo="operador@example.test"},
 GerenteOperaciones=new ContactoDocumentalAocr{Nombre="Gerente correcto",Direccion="Gerencia Doha",Telefono="222",Correo="gerente@example.test"},
 RepresentanteTecnico=new ContactoDocumentalAocr{Nombre="RT EAE correcto",Direccion="RT Doha",Telefono="333",Correo="rt.eae@example.test"},
 RepresentanteLegalEcuador=new ContactoDocumentalAocr{Nombre="Representante Ecuador",Direccion="Quito legal",Telefono="444",Correo="legal@example.test"},
 Estaciones=new List<OperacionEstacionDocumentalAocr>{new OperacionEstacionDocumentalAocr{EstacionId=101,Codigo="R"},new OperacionEstacionDocumentalAocr{EstacionId=102,Codigo="ET"}}};
 certDatos.GuardarDatosDocumentales(201,datos19,79);
 var recDatos=certDatos.ObtenerDatosDocumentales(201);
 Check(recDatos.PermisoCnacNumero==datos19.PermisoCnacNumero&&recDatos.PermisoCnacEmision==datos19.PermisoCnacEmision&&recDatos.PermisoCnacVencimiento==datos19.PermisoCnacVencimiento,"Permiso CNAC y fechas persisten tras recarga PostgreSQL");
 Check(recDatos.GerenteOperaciones.Nombre=="Gerente correcto"&&recDatos.RepresentanteTecnico.Nombre=="RT EAE correcto"&&recDatos.RepresentanteLegalEcuador.Nombre=="Representante Ecuador"&&recDatos.Explotador.Direccion=="Base en Doha","Contactos diferenciados persisten sin intercambiar Inspector ni contacto Ecuador");
 Check(recDatos.Estaciones.Single(e=>e.EstacionId==101).Codigo=="R"&&recDatos.Estaciones.Single(e=>e.EstacionId==102).Codigo=="ET","Operaciones distintas por estacion se conservan al recargar");
 var original19=certDatos.AsegurarNumeroOriginal(201,79);
 sql("INSERT INTO aocr_tbcertificado(codigo_solicitud,numero_certificado) VALUES(201,'DGAC-129-QTR-003'),(202,'BORRADOR-202'),(203,'BORRADOR-203')");
 Check(original19=="DGAC-129-QTR-003"&&certDatos.AsegurarNumeroOriginal(201,79)==original19,"Emision conserva el consecutivo AOCR003 original, no el id 201 ni un contador nuevo");
 var contador19=Convert.ToInt64(scalar("SELECT last_value FROM aocr_numero_certificado_seq"));
 Check(certDatos.AsegurarNumeroOriginal(202,79)==original19,"Renovacion conserva exactamente el AOCR original");
 Check(certDatos.AsegurarNumeroOriginal(203,79)==original19,"Modificacion conserva exactamente el AOCR original");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbcertificado WHERE numero_certificado='DGAC-129-QTR-003'"))==3,"Versiones de emision, renovacion y modificacion comparten numero sin conflicto de unicidad");
 Check(Convert.ToInt64(scalar("SELECT last_value FROM aocr_numero_certificado_seq"))==contador19,"Renovacion y modificacion no generan nuevos consecutivos");
 rechazo=false;try{certDatos.AsegurarNumeroOriginal(204,79);}catch(InvalidOperationException){rechazo=true;}
 Check(rechazo,"Explotador ajeno sin original se rechaza sin generar un numero nuevo");
 var tareas19=new[]{System.Threading.Tasks.Task.Run(()=>new CertificadoDAO().AsegurarNumeroOriginal(205,79)),System.Threading.Tasks.Task.Run(()=>new CertificadoDAO().AsegurarNumeroOriginal(207,79))};
 System.Threading.Tasks.Task.WaitAll(tareas19);
 Check(tareas19[0].Result=="DGAC-129-QTR-005"&&tareas19[1].Result=="DGAC-129-AAA-007"&&Convert.ToInt64(scalar("SELECT last_value FROM aocr_numero_certificado_seq"))==contador19,"Emisiones concurrentes conservan sus consecutivos originales sin consumir secuencia nueva");
 rechazo=false;try{sql("UPDATE aocr_tbsolicitud SET numero_aocr='CAMBIO-INDEBIDO' WHERE codigo_solicitud=201");}catch(Npgsql.PostgresException){rechazo=true;}
 Check(rechazo&&certDatos.ObtenerNumeroPersistido(201)==original19,"Base rechaza alteracion del numero original ya asignado");
 sql("INSERT INTO aocr_tbfirma_documento(codigo_solicitud) VALUES(201)");
 rechazo=false;try{certDatos.GuardarDatosDocumentales(201,new DatosDocumentalesAocr{PermisoCnacNumero="NO GUARDAR"},79);}catch(InvalidOperationException){rechazo=true;}
 Check(rechazo&&certDatos.ObtenerDatosDocumentales(201).PermisoCnacNumero=="CNAC-PRUEBA-19","Certificado historico firmado conserva sus datos y no se modifica");
 sql(File.ReadAllText(Path.Combine(root,"sql/20261027_datos_documentales_certificado.sql")));
 Check(Convert.ToInt64(scalar("SELECT last_value FROM aocr_numero_certificado_seq"))>=contador19,"Reaplicar migracion no reinicia numeracion ni modifica historicos");

 // Regresion del 27/10: reproducir los dos indices unicos reales del servidor.
 sql(@"ALTER TABLE aocr_tbdocumento_generado DROP CONSTRAINT ck_documento_final_estado;
 ALTER TABLE aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS eliminado boolean NOT NULL DEFAULT false;
 CREATE UNIQUE INDEX IF NOT EXISTS ux_documento_final_vigente ON aocr_tbdocumento_generado(codigo_solicitud,upper(tipo_documento)) WHERE vigente=true;
 CREATE UNIQUE INDEX uq_aocr_doc_vigente_expediente_tipo ON aocr_tbdocumento_generado(codigo_solicitud,codigo_inspeccion,upper(trim(tipo_documento))) WHERE vigente=true AND eliminado=false AND codigo_inspeccion IS NOT NULL AND upper(trim(tipo_documento)) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES');");
 var pdfDao=new AocrDocumentoGeneradoDAO();
 Func<string,string,AocrDocumentoGenerado> nuevoPdf=(tipo,hash)=>new AocrDocumentoGenerado{CodigoSolicitud=202,CodigoInspeccion=150,TipoDocumento=tipo,NumeroAocr=original19,RutaDocumento="/prueba/"+hash+".pdf",NombreArchivo=hash+".pdf",TamanioPdf=100,Estado="GENERADO",Completo=true,HashPdf=hash};
 foreach(var tipoPdf in new[]{"RECONOCIMIENTO","CONDICIONES_LIMITACIONES"}) {
 pdfDao.Registrar(nuevoPdf(tipoPdf,"ANTERIOR"));pdfDao.Registrar(nuevoPdf(tipoPdf,"ACTUALIZADO"));
 var vigentePdf=pdfDao.ObtenerUltimoPorSolicitudTipo(202,tipoPdf);
 Check(vigentePdf.HashPdf=="ACTUALIZADO"&&vigentePdf.VersionDocumento==2&&vigentePdf.RutaDocumento.Contains("ACTUALIZADO"),"Regenerar "+tipoPdf+" actualiza PDF vigente con ambos indices unicos reales");
 pdfDao.Registrar(nuevoPdf(tipoPdf,"ACTUALIZADO"));
 Check(pdfDao.ObtenerUltimoPorSolicitudTipo(202,tipoPdf).VersionDocumento==2,"Reintento de misma evidencia no duplica "+tipoPdf);
 }
 sql("ALTER TABLE aocr_tbdocumento_generado ADD CONSTRAINT prueba_rechazo_hash CHECK(hash_pdf<>'RECHAZADO')");
 rechazo=false;try{pdfDao.Registrar(nuevoPdf("RECONOCIMIENTO","RECHAZADO"));}catch(Npgsql.PostgresException){rechazo=true;}
 Check(rechazo&&pdfDao.ObtenerUltimoPorSolicitudTipo(202,"RECONOCIMIENTO").HashPdf=="ACTUALIZADO","Fallo de insercion revierte retiro de version anterior y mantiene PDF vigente");
 sql("UPDATE aocr_tbdocumento_generado SET bloqueado=true WHERE codigo_solicitud=202 AND vigente=true AND tipo_documento='RECONOCIMIENTO'");
 rechazo=false;try{pdfDao.Registrar(nuevoPdf("RECONOCIMIENTO","NO_REEMPLAZAR"));}catch(InvalidOperationException){rechazo=true;}
 Check(rechazo&&pdfDao.ObtenerUltimoPorSolicitudTipo(202,"RECONOCIMIENTO").HashPdf=="ACTUALIZADO","Documento enviado permanece protegido ante regeneracion");
 sql("UPDATE aocr_tbdocumento_generado SET ruta_pdf_firmado='/prueba/firmado.pdf' WHERE codigo_solicitud=202 AND vigente=true AND tipo_documento='CONDICIONES_LIMITACIONES'");
 rechazo=false;try{pdfDao.Registrar(nuevoPdf("CONDICIONES_LIMITACIONES","NO_REEMPLAZAR"));}catch(InvalidOperationException){rechazo=true;}
 Check(rechazo&&pdfDao.ObtenerUltimoPorSolicitudTipo(202,"CONDICIONES_LIMITACIONES").HashPdf=="ACTUALIZADO","Documento firmado permanece protegido ante regeneracion");

 // Regularizacion limitada a borradores: copia de la regla del tramite original.
 sql(@"INSERT INTO aocr_tbsolicitud(codigo_solicitud,tipo_solicitud,codigo_oaci,ruc,numero_solicitud,numero_aocr,estado) VALUES
 (220,1,'QTR','REG-220','DGAC-GOP-2026-AOCR020','DGAC-129-QTR-900','DOCUMENTOS_FINALES_POR_GENERAR'),
 (221,2,'QTR','REG-220','DGAC-GOP-2026-AOCR021','DGAC-129-QTR-900','DOCUMENTOS_FINALES_POR_GENERAR'),
 (222,1,'AAA','REG-222','DGAC-GOP-2026-AOCR022','DGAC-129-AAA-902','FINALIZADO'),
 (223,1,'BBB','REG-223','DGAC-GOP-2026-AOCR023','DGAC-129-BBB-903','DOCUMENTOS_FINALES_POR_GENERAR'),
 (224,2,'BBB','REG-223','DGAC-GOP-2026-AOCR024','DGAC-129-BBB-903','DOCUMENTOS_FINALES_COORDINADOR'),
 (225,1,'CCC','REG-225','DGAC-GOP-2026-AOCR025','DGAC-129-CCC-905','DOCUMENTOS_FINALES_POR_GENERAR');
 INSERT INTO aocr_tbcertificado(codigo_solicitud,numero_certificado,ruta_pdf,firmado_por) VALUES
 (220,'DGAC-129-QTR-900','/prueba/antiguo.pdf',NULL),(222,'DGAC-129-AAA-902','/prueba/firmado.pdf','99');
 INSERT INTO aocr_tbdocumento_generado(codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,ruta_documento,estado,vigente,bloqueado,hash_pdf)
 VALUES(220,2200,'RECONOCIMIENTO','DGAC-129-QTR-900','/prueba/antiguo.pdf','GENERADO',true,false,'NUMERO_ANTERIOR'),
 (224,2240,'RECONOCIMIENTO','DGAC-129-BBB-903','/prueba/enviado.pdf','DOCUMENTOS_FINALES_COORDINADOR',true,true,'BLOQUEADO');
 CREATE TABLE aocr_tbcondiciones_limitaciones(codigo_solicitud integer,ruta_pdf_firmado text,fecha_firma_dircav timestamp,estado text,vigente boolean DEFAULT true);
 INSERT INTO aocr_tbcondiciones_limitaciones(codigo_solicitud,estado) VALUES(225,'CL_PENDIENTE_COORDINADOR'),(220,'CL_BORRADOR');");
 var regularizacionNumero=File.ReadAllText(Path.Combine(root,"sql/20261028_numero_aocr_consecutivo_original.sql"));
 sql(regularizacionNumero);
 Check(certDatos.ObtenerNumeroPersistido(220)=="DGAC-129-QTR-020"&&certDatos.ObtenerNumeroPersistido(221)=="DGAC-129-QTR-020","Regularizacion corrige emision y renovacion borradores conservando el consecutivo original 020");
 Check(Convert.ToString(scalar("SELECT numero_certificado FROM aocr_tbcertificado WHERE codigo_solicitud=220"))=="DGAC-129-QTR-020"&&scalar("SELECT ruta_pdf FROM aocr_tbcertificado WHERE codigo_solicitud=220")==DBNull.Value,"Certificado borrador queda alineado y exige regenerar archivo");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbdocumento_generado WHERE codigo_solicitud=220 AND NOT vigente AND numero_aocr='DGAC-129-QTR-900' AND ruta_documento='/prueba/antiguo.pdf'"))==1,"PDF antiguo conserva numero y ruta en el historial sin reescribirlo");
 Check(certDatos.ObtenerNumeroPersistido(222)=="DGAC-129-AAA-902"&&Convert.ToString(scalar("SELECT ruta_pdf FROM aocr_tbcertificado WHERE codigo_solicitud=222"))=="/prueba/firmado.pdf","Regularizacion no altera certificado firmado");
 Check(certDatos.ObtenerNumeroPersistido(223)=="DGAC-129-BBB-903"&&certDatos.ObtenerNumeroPersistido(224)=="DGAC-129-BBB-903","Renovacion enviada protege el numero de todo el grupo original");
 Check(certDatos.ObtenerNumeroPersistido(225)=="DGAC-129-CCC-905","Paquete CL heredado remitido tambien protege su numeracion");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbcondiciones_limitaciones WHERE codigo_solicitud=220 AND vigente=false"))==1,"Borrador CL heredado queda archivado para evitar reutilizar el numero anterior");
 sql(regularizacionNumero);
 rechazo=false;try{sql("UPDATE aocr_tbsolicitud SET numero_aocr='OTRO' WHERE codigo_solicitud=220");}catch(Npgsql.PostgresException){rechazo=true;}
 Check(rechazo&&certDatos.ObtenerNumeroPersistido(220)=="DGAC-129-QTR-020","Regularizacion idempotente restaura proteccion de inmutabilidad");

 }
 }
}
