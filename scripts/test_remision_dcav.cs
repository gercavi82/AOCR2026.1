using System;
using System.Configuration;
using System.IO;
using Npgsql;
using CapaDatos.DAOs;
using CapaDatos.Models;
class RemisionDcavTest {
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);Console.WriteLine("OK: "+msg);}
 static void Main(string[] args){
 var cs=ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString;
 var b=new NpgsqlConnectionStringBuilder(cs);if(b.Host!="127.0.0.1"||b.Port!=55440||b.Database!="remision_test")throw new Exception("Solo base temporal local.");
 using(var c=new NpgsqlConnection(cs)){c.Open();
 Action<string> sql=x=>{using(var q=new NpgsqlCommand(x,c))q.ExecuteNonQuery();};
 Func<string,object> scalar=x=>{using(var q=new NpgsqlCommand(x,c))return q.ExecuteScalar();};
 sql(File.ReadAllText(args[0]));
 sql(@"CREATE TABLE aocr_tbsolicitud(codigo_solicitud integer primary key,estado text,updated_at timestamp,updated_by text);
 CREATE TABLE aocr_tbhistorial_documental(codigo_solicitud integer,codigo_documento integer,evento text,detalle text,codigo_usuario integer,fecha_evento timestamp,created_at timestamp,created_by text);
 CREATE TABLE aocr_tbauditoria(entidad text,accion text CHECK(accion <> 'FALLAR'),usuario text,fecha timestamp,datos_nuevos jsonb);
 INSERT INTO aocr_tbsolicitud VALUES(7,'Aceptacion Documental',now(),'test'),(8,'Aceptacion Documental',now(),'test');");
 var dao=new RevisionDocumentalCoordinadorDAO();
 Func<int,TransicionCoordinadorParams> parametros=id=>new TransicionCoordinadorParams{SolicitudId=id,CoordinadorId=90,InspectorOriginalId=79,Accion="COORDINADOR_REMITIR_DIRCAV",EstadoSolicitudDestino="PENDIENTE_DIRCAV",EstadoRevisionDestino="PENDIENTE_ACEPTACION_DCAV",UsuarioLogin="test",Observacion="Prueba"};
 sql("ALTER TABLE aocr_revision_documental_coordinador ADD CONSTRAINT chk_aocr_revdoc_coord_estado CHECK(estado IN ('PENDIENTE_COORDINADOR','PENDIENTE_DIRCAV','ACEPTADA_POR_COORDINADOR'));");
 Check(!dao.EjecutarTransicionCoordinadorTransaccional(parametros(7)).Exitoso,"Reproduce restriccion antigua que bloquea DCAV");
 sql(File.ReadAllText(args[1]));
 sql(File.ReadAllText(args[1]));
 var r=dao.EjecutarTransicionCoordinadorTransaccional(parametros(7));Check(r.Exitoso,r.Mensaje);
 Check(r.Registro!=null&&r.Registro.InspectorOriginalId==79,"Crea control de coordinacion legado");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=7"))=="PENDIENTE_DIRCAV","Remite sin columna version");
 Check(Convert.ToString(scalar("SELECT datos_nuevos->>'EstadoAnterior' FROM aocr_tbauditoria LIMIT 1"))=="Aceptacion Documental","Auditoria JSONB conserva estado anterior");
 Check(!dao.EjecutarTransicionCoordinadorTransaccional(parametros(7)).Exitoso,"Segundo clic rechazado");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_tbauditoria"))==1,"Sin duplicar auditoria");
 var fail=parametros(8);fail.Accion="FALLAR";Check(!dao.EjecutarTransicionCoordinadorTransaccional(fail).Exitoso,"Fallo de auditoria revierte transaccion");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_tbsolicitud WHERE codigo_solicitud=8"))=="Aceptacion Documental","Rollback conserva solicitud");
 Check(Convert.ToInt32(scalar("SELECT count(*) FROM aocr_revision_documental_coordinador WHERE solicitud_id=8"))==0,"Rollback elimina registro parcial");
 sql(@"ALTER TABLE aocr_tbsolicitud ADD COLUMN version integer default 1;
 ALTER TABLE aocr_tbauditoria ADD COLUMN datos_previos text;
 ALTER TABLE aocr_tbauditoria ALTER COLUMN datos_nuevos TYPE text USING datos_nuevos::text;
 INSERT INTO aocr_tbsolicitud(codigo_solicitud,estado,version) VALUES(9,'Aceptacion Documental',1);");
 var next=parametros(9);next.ExpectedVersion=1;Check(dao.EjecutarTransicionCoordinadorTransaccional(next).Exitoso,"Compatible con version y auditoria texto");
 Check(Convert.ToInt32(scalar("SELECT version FROM aocr_tbsolicitud WHERE codigo_solicitud=9"))==2,"Version incrementada");
 sql("UPDATE aocr_revision_documental_coordinador SET estado='ACEPTADA_POR_DCAV' WHERE solicitud_id=9;");
 Check(Convert.ToString(scalar("SELECT estado FROM aocr_revision_documental_coordinador WHERE solicitud_id=9"))=="ACEPTADA_POR_DCAV","Restriccion permite firma DCAV despues de remision");
 }
 }
}
