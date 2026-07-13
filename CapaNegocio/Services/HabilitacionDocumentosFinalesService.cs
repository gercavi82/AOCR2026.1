using System;
using System.Configuration;
using System.Diagnostics;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio.DTOs;
using CapaModelo.Common;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class HabilitacionDocumentosFinalesService : IHabilitacionDocumentosFinalesService
    {
        private const string MensajeInspector="El Informe Tecnico fue aprobado por DCAV. Se encuentran habilitados el AOCR y las Condiciones y Limitaciones para su revision.";
        private readonly HabilitacionDocumentosFinalesDAO _dao;
        private readonly IAocrBorradorService _aocr;
        private readonly ICondicionesBorradorService _condiciones;
        private readonly IAocrEstadoProcesoService _estados;
        private readonly ILoggingService _logger;
        private readonly string _connectionString;

        public HabilitacionDocumentosFinalesService()
            : this(new HabilitacionDocumentosFinalesDAO(),new AocrBorradorService(),new CondicionesBorradorService(),new AocrEstadoProcesoService(),LoggingServiceFactory.Create(),
                  ConfigurationManager.ConnectionStrings["AOCRConnection"] != null ? ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString : ConexionDAO.CadenaConexion) { }

        public HabilitacionDocumentosFinalesService(HabilitacionDocumentosFinalesDAO dao,IAocrBorradorService aocr,ICondicionesBorradorService condiciones,IAocrEstadoProcesoService estados,ILoggingService logger,string connectionString)
        { _dao=dao;_aocr=aocr;_condiciones=condiciones;_estados=estados;_logger=logger;_connectionString=connectionString; }

        public ResultadoHabilitacionDocumentos Habilitar(HabilitarDocumentosRequest request)
        {
            Log("[DCAV][HABILITAR_DOCUMENTOS_IN] SolicitudId="+(request!=null?request.SolicitudId:0)+"; CorrelationId="+(request!=null?request.CorrelationId:string.Empty)+";");
            var basica=ValidarRequest(request); if(basica!=null)return basica;
            try
            {
                using(var cn=new NpgsqlConnection(_connectionString))
                {
                    cn.Open(); _dao.PrepararEsquema(cn);
                    HabilitacionDocumentosSnapshot snapshot;
                    ResultadoBorradorDocumento aocr; ResultadoBorradorDocumento condiciones;
                    string emailInspector=null;
                    using(var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable))
                    {
                        try
                        {
                            snapshot=_dao.CargarParaActualizar(cn,tx,request.SolicitudId,request.InspeccionId,request.InformeTecnicoId);
                            if(snapshot==null)return Rollback(tx,404,"No existe la solicitud, inspeccion, Informe o estado central indicado.",request);
                            var hit=_dao.ObtenerIdempotencia(cn,tx,request.ClaveIdempotencia);
                            if(hit!=null && string.Equals(hit.Resultado,"EXITO",StringComparison.OrdinalIgnoreCase))
                            {
                                tx.Commit(); Log("[IDEMPOTENCY][HIT] Clave="+request.ClaveIdempotencia+";");
                                return Ok(hit.AocrId,hit.CondicionesId,hit.EstadoAnterior,hit.EstadoNuevo,true);
                            }
                            var funcional=ValidarSnapshot(snapshot,request); if(funcional!=null)return Rollback(tx,funcional.Codigo,funcional.Mensaje,request);
                            Log("[DCAV][HABILITAR_VALIDATION_OK] SolicitudId="+request.SolicitudId+"; InspectorId="+snapshot.InspectorId+";");

                            var borradorRequest=new BorradorDocumentoRequest { SolicitudId=snapshot.SolicitudId,InspeccionId=snapshot.InspeccionId,CodigoCompania=snapshot.CodigoCompania,InspectorId=snapshot.InspectorId,UsuarioCreadorId=request.UsuarioDcavId };
                            aocr=_aocr.ObtenerOCrearBorrador(cn,tx,borradorRequest);
                            if(aocr==null||!aocr.Exitoso||aocr.Documento==null)return Rollback(tx,500,"No se pudo obtener o crear el borrador AOCR.",request);
                            condiciones=_condiciones.ObtenerOCrearBorrador(cn,tx,borradorRequest);
                            if(condiciones==null||!condiciones.Exitoso||condiciones.Documento==null)return Rollback(tx,500,"No se pudo obtener o crear el borrador de Condiciones y Limitaciones.",request);

                            _dao.MarcarInformeAprobado(cn,tx,snapshot.InformeId,request.UsuarioDcavId);
                            var cambio=_estados.CambiarEstadoEnTransaccion(cn,tx,snapshot.SolicitudId,request.EstadoEsperado,AocrEstadosProceso.DocumentosHabilitadosInspector,
                                "INFORME_APROBADO_DCAV",request.UsuarioDcavId,request.Rol,request.VersionRegistro,snapshot.InspeccionId,snapshot.InformeId,
                                "AOCR y Condiciones habilitados para revision del Inspector.",request.ClaveIdempotencia,request.Ip,request.CorrelationId);
                            if(cambio==null||!cambio.Ok)return Rollback(tx,409,cambio!=null?cambio.Motivo:"Conflicto al actualizar el estado central.",request);

                            var auditoria=ConstruirAuditoria(snapshot,request,aocr.Documento.CodigoDocumento,condiciones.Documento.CodigoDocumento);
                            _dao.RegistrarAuditoria(cn,tx,request.UsuarioDcavId.ToString(),snapshot.EstadoCentral,auditoria);
                            _dao.CrearNotificacionInspector(cn,tx,snapshot.InspectorId,snapshot.SolicitudId,request.ClaveIdempotencia,request.CorrelationId);
                            _dao.RegistrarIdempotencia(cn,tx,new HabilitacionIdempotenciaRecord { Clave=request.ClaveIdempotencia,SolicitudId=snapshot.SolicitudId,AocrId=aocr.Documento.CodigoDocumento,CondicionesId=condiciones.Documento.CodigoDocumento,EstadoAnterior=snapshot.EstadoCentral,EstadoNuevo=AocrEstadosProceso.DocumentosHabilitadosInspector,Resultado="EXITO" },request.CorrelationId);
                            emailInspector=ResolverEmailInspector(snapshot.InspectorId);
                            tx.Commit();
                        }
                        catch(PostgresException ex)
                        {
                            try{tx.Rollback();}catch{}
                            if(ex.SqlState=="40001"||ex.SqlState=="23505")
                            {
                                Log("[DCAV][HABILITAR_VALIDATION_ERROR] SolicitudId="+request.SolicitudId+"; Codigo=409; Motivo=Conflicto concurrente; SqlState="+ex.SqlState+";");
                                return Error(409,"El expediente fue procesado concurrentemente; recargue para obtener el resultado vigente.");
                            }
                            LogError("[WORKFLOW][HABILITAR_DOCUMENTOS_ROLLBACK] SolicitudId="+request.SolicitudId+"; Error="+ex.Message+";",ex);
                            return Error(500,"La habilitacion fue revertida por un error interno.");
                        }
                        catch(Exception ex)
                        {
                            try{tx.Rollback();}catch{}
                            LogError("[WORKFLOW][HABILITAR_DOCUMENTOS_ROLLBACK] SolicitudId="+request.SolicitudId+"; Error="+ex.Message+";",ex);
                            return Error(500,"La habilitacion fue revertida por un error interno.");
                        }
                    }
                    EncolarCorreoPostCommit(request,snapshot,emailInspector);
                    Log("[IDEMPOTENCY][CREATED] Clave="+request.ClaveIdempotencia+";");
                    Log("[WORKFLOW][DOCUMENTOS_HABILITADOS] SolicitudId="+request.SolicitudId+"; AocrId="+aocr.Documento.CodigoDocumento+"; CondicionesId="+condiciones.Documento.CodigoDocumento+";");
                    Log("[WORKFLOW][HABILITAR_DOCUMENTOS_OK] SolicitudId="+request.SolicitudId+";");
                    return Ok(aocr.Documento.CodigoDocumento,condiciones.Documento.CodigoDocumento,snapshot.EstadoCentral,AocrEstadosProceso.DocumentosHabilitadosInspector,false);
                }
            }
            catch(Exception ex){LogError("[WORKFLOW][HABILITAR_DOCUMENTOS_ROLLBACK] SolicitudId="+request.SolicitudId+"; Error="+ex.Message+";",ex);return Error(500,"Error interno al habilitar los documentos.");}
        }

        public static string ConstruirClaveIdempotencia(int solicitudId,int inspeccionId,int informeId,int versionInforme)
        { return solicitudId+":"+inspeccionId+":"+informeId+":APROBAR_INFORME_DCAV:"+versionInforme; }

        private static ResultadoHabilitacionDocumentos ValidarRequest(HabilitarDocumentosRequest r)
        {
            if(r==null||r.SolicitudId<=0||r.InspeccionId<=0||r.InformeTecnicoId<=0||r.VersionInforme<=0||string.IsNullOrWhiteSpace(r.ClaveIdempotencia))return Error(400,"Datos de habilitacion invalidos.");
            if(r.UsuarioDcavId<=0)return Error(401,"Usuario no autenticado.");
            if(!EsRolDcav(r.Rol))return Error(403,"El rol no esta autorizado para aprobar el Informe Tecnico.");
            if(!string.Equals(r.EstadoEsperado,AocrEstadosProceso.PendienteRevisionInformeDcav,StringComparison.OrdinalIgnoreCase))return Error(409,"El estado esperado no corresponde a revision de Informe DCAV.");
            var clave=ConstruirClaveIdempotencia(r.SolicitudId,r.InspeccionId,r.InformeTecnicoId,r.VersionInforme);
            if(!string.Equals(clave,r.ClaveIdempotencia,StringComparison.Ordinal))return Error(400,"La clave de idempotencia no corresponde al expediente.");
            return null;
        }

        private static ResultadoHabilitacionDocumentos ValidarSnapshot(HabilitacionDocumentosSnapshot s,HabilitarDocumentosRequest r)
        {
            if(!string.Equals(s.EstadoCentral,r.EstadoEsperado,StringComparison.OrdinalIgnoreCase))return Error(409,"El expediente cambio de estado.");
            if(s.VersionRegistro!=r.VersionRegistro||s.VersionInforme!=r.VersionInforme)return Error(409,"La version del expediente o Informe cambio.");
            if(!s.SolicitudActiva||AocrEstadosProceso.EstadosFinales.Contains(s.EstadoCentral))return Error(422,"La solicitud esta anulada, eliminada o finalizada.");
            if(!s.InformeVigente)return Error(409,"El Informe Tecnico ya no es la version vigente.");
            if(!s.InformeFinalizado||!s.InformeFirmado||string.IsNullOrWhiteSpace(s.RutaInformeFirmado)||string.IsNullOrWhiteSpace(s.HashInforme))return Error(422,"El Informe Tecnico no esta finalizado y firmado.");
            if(!EsSatisfactorio(s.ResultadoInforme))return Error(422,"El resultado del Informe Tecnico no es satisfactorio.");
            if(s.ListaId<=0||!s.ListaFinalizada||!s.ListaFirmada||string.IsNullOrWhiteSpace(s.RutaListaFirmada)||string.IsNullOrWhiteSpace(s.HashLista))return Error(422,"La LV/EAE no esta finalizada y firmada.");
            if(s.InspectorId<=0)return Error(422,"La inspeccion no tiene Inspector asignado.");
            if(string.IsNullOrWhiteSpace(s.CodigoCompania))return Error(422,"El expediente no tiene una compania valida.");
            return null;
        }

        private static bool EsRolDcav(string rol){var t=(rol??string.Empty).Replace(" ",string.Empty).Replace("_",string.Empty).ToUpperInvariant();return t=="DIRECTORCERTIFICACIONESDCAV"||t=="DIRECTORDECERTIFICACIONESDCAV"||t=="DIRECTORDCAV"||t=="DCAV";}
        private static bool EsSatisfactorio(string resultado){var t=(resultado??string.Empty).Trim().ToUpperInvariant();return t.Contains("SATISFACTORIO")&&!t.Contains("INSATISFACTORIO");}
        private static ResultadoHabilitacionDocumentos Error(int codigo,string mensaje){return new ResultadoHabilitacionDocumentos{Exitoso=false,Codigo=codigo,Mensaje=mensaje};}
        private static ResultadoHabilitacionDocumentos Ok(int aocr,int condiciones,string anterior,string nuevo,bool repetido){return new ResultadoHabilitacionDocumentos{Exitoso=true,YaProcesado=repetido,Codigo=200,Mensaje=repetido?"La operacion ya fue procesada.":"Documentos habilitados correctamente.",AocrId=aocr,CondicionesId=condiciones,EstadoAnterior=anterior,EstadoNuevo=nuevo};}
        private ResultadoHabilitacionDocumentos Rollback(NpgsqlTransaction tx,int codigo,string mensaje,HabilitarDocumentosRequest r){try{tx.Rollback();}catch{}Log("[DCAV][HABILITAR_VALIDATION_ERROR] SolicitudId="+r.SolicitudId+"; Codigo="+codigo+"; Motivo="+mensaje+";");return Error(codigo,mensaje);}
        private static string ConstruirAuditoria(HabilitacionDocumentosSnapshot s,HabilitarDocumentosRequest r,int aocr,int condiciones){return "SolicitudId="+s.SolicitudId+";InspeccionId="+s.InspeccionId+";InformeTecnicoId="+s.InformeId+";AocrId="+aocr+";CondicionesId="+condiciones+";UsuarioDcavId="+r.UsuarioDcavId+";InspectorId="+s.InspectorId+";EstadoAnterior="+s.EstadoCentral+";EstadoNuevo="+AocrEstadosProceso.DocumentosHabilitadosInspector+";Ip="+(r.Ip??string.Empty)+";CorrelationId="+(r.CorrelationId??string.Empty)+";ClaveIdempotencia="+r.ClaveIdempotencia+";Resultado=EXITO";}
        private static string ResolverEmailInspector(int inspectorId){try{var u=UsuarioDAO.ObtenerPorId(inspectorId);return u!=null?u.Email:null;}catch{return null;}}
        private static void EncolarCorreoPostCommit(HabilitarDocumentosRequest r,HabilitacionDocumentosSnapshot s,string email){if(string.IsNullOrWhiteSpace(email))return;try{new EmailQueueService().EncolarAsync(new EmailQueueItem{Para=email,Asunto="Sistema AOCR - Documentos habilitados",Cuerpo=MensajeInspector,Estado=EstadoEmail.Pendiente,SolicitudId=s.SolicitudId,TipoNotificacion="DOCUMENTOS_HABILITADOS_INSPECTOR",EventKey=r.ClaveIdempotencia+":"+email.Trim().ToUpperInvariant(),CorrelationId=r.CorrelationId,EsHtml=false}).GetAwaiter().GetResult();}catch{}}
        private void Log(string m){try{_logger.LogInfo(m);}catch{Trace.TraceInformation(m);}}
        private void LogError(string m,Exception ex){try{_logger.LogError(ex,new LogContext{ErrorCode="HABILITAR_DOCUMENTOS"});_logger.LogWarning(m);}catch{Trace.TraceError(m);}}
    }
}
