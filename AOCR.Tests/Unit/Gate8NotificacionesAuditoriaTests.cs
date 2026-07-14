using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
 [TestClass] public class Gate8NotificacionesAuditoriaTests
 {
  [TestMethod] public void MismoEventKey_NoDuplicaCorreo(){var f=Fix();f.S.PublicarPostCommit(R("NC_GENERADA:1:1"));f.S.PublicarPostCommit(R("NC_GENERADA:1:1"));Assert.AreEqual(1,f.Q.Count);}
  [TestMethod] public void NuevaVersion_GeneraKeyDiferente(){Assert.AreNotEqual(Gate8Eventos.Key("NC_GENERADA",1,1),Gate8Eventos.Key("NC_GENERADA",1,2));}
  [TestMethod] public void FalloCorreo_NoRevierteNc(){FalloNoRevierte("NC_GENERADA");}
  [TestMethod] public void FalloCorreo_NoRevierteNuevaSolicitud(){FalloNoRevierte("NUEVA_SOLICITUD_CREADA");}
  [TestMethod] public void FalloCorreo_NoRevierteCierre(){FalloNoRevierte("NC_CERRADA");}
  [TestMethod] public void Rt_RecibeSoloExpedienteRelacionado(){var x=R("NC_NOTIFICADA_RT:7:1");x.Registro.SolicitudId=7;var f=Fix();f.S.PublicarPostCommit(x);Assert.AreEqual(7,f.Q.Last.SolicitudId);}
  [TestMethod] public void InspectorAsignado_RecibeSubsanacion(){var f=Fix();var x=R("SUBSANACION_ENVIADA_INSPECTOR:1:2");x.UsuarioNotificacionInterna=22;f.S.PublicarPostCommit(x);Assert.AreEqual(22,f.InternalUser);}
  [TestMethod] public void Coordinador_RecibeNc(){var f=Fix();var x=R("NC_ENVIADA_COORDINADOR:1:1");x.UsuarioNotificacionInterna=33;f.S.PublicarPostCommit(x);Assert.AreEqual(33,f.InternalUser);}
  [TestMethod] public void Dirdac_RecibeDocumentosCorrectos(){CollectionAssert.Contains(Gate8Eventos.Todos,"DOCUMENTOS_ENVIADOS_DIRDAC");}
  [TestMethod] public void Modulo8_NoAdjuntaAocr(){var p=new AocrCierrePorTipoTramiteService().Resolver(new CapaModelo.SolicitudAOCR{TipoSolicitud=3});Assert.IsFalse(p.GenerarAocr);Assert.IsTrue(p.GenerarCondiciones);}
  [TestMethod] public void Modulo7_AdjuntaAocrYCondiciones(){var p=new AocrCierrePorTipoTramiteService().Resolver(new CapaModelo.SolicitudAOCR{TipoSolicitud=1});Assert.IsTrue(p.GenerarAocr);Assert.IsTrue(p.GenerarCondiciones);}
  [TestMethod] public void CorrelationId_SeConserva(){Assert.AreEqual("corr-1",Gate8Eventos.Correlation("corr-1",9));}
  [TestMethod] public void Auditoria_RegistraEstados(){Gate8EventoRegistro a=null;var f=Fix(x=>a=x);var r=R("NC_CERRADA:1:2");r.Registro.EstadoAnterior="ABIERTA";r.Registro.EstadoNuevo="CERRADA";f.S.PublicarPostCommit(r);Assert.AreEqual("ABIERTA",a.EstadoAnterior);Assert.AreEqual("CERRADA",a.EstadoNuevo);}
  [TestMethod] public void Auditoria_RegistraHashYVersion(){Gate8EventoRegistro a=null;var f=Fix(x=>a=x);var r=R("NC_FIRMADA_INSPECTOR:1:2:h");r.Registro.Hash="h";r.Registro.Version=2;f.S.PublicarPostCommit(r);Assert.AreEqual("h",a.Hash);Assert.AreEqual(2,a.Version);}
  [TestMethod] public void Reintento_IncrementaIntentoSinDuplicarEfectivo(){var f=Fix();f.S.PublicarPostCommit(R("NC_CERRADA:1:3"));var z=f.S.PublicarPostCommit(R("NC_CERRADA:1:3"));Assert.IsTrue(z.Duplicado);Assert.AreEqual(2,f.R.Attempts);Assert.AreEqual(1,f.Q.Count);}
  private static void FalloNoRevierte(string e){var f=Fix();f.Q.Throw=true;var principalConfirmada=true;var z=f.S.PublicarPostCommit(R(e+":1:1"));Assert.IsTrue(principalConfirmada);Assert.IsTrue(z.EventoNuevo);StringAssert.Contains(z.ErrorNotificacion,"queue");}
  private static Gate8EventoRequest R(string key){return new Gate8EventoRequest{Registro=new Gate8EventoRegistro{Evento=key.Split(':')[0],EventKey=key,CorrelationId="corr-1",SolicitudId=1,Resultado="REGISTRADO"},Correo=new EmailQueueItem{Para="destino@aviacioncivil.gob.ec",Asunto="a",Cuerpo="b"}};}
  private static Fixture Fix(Action<Gate8EventoRegistro> audit=null){var r=new Repo();var q=new Queue();var f=new Fixture{R=r,Q=q};f.S=new Gate8WorkflowEventService(r,q,(u,t,m,s)=>f.InternalUser=u,audit);return f;}
  private sealed class Fixture{public Repo R;public Queue Q;public Gate8WorkflowEventService S;public int InternalUser;}
  private sealed class Repo:IGate8EventoRepository{readonly HashSet<string> k=new HashSet<string>();public int Attempts;public bool RegistrarIntento(Gate8EventoRegistro e){Attempts++;return k.Add(e.EventKey);}public void ActualizarResultado(string k,string r,string e){} }
  private sealed class Queue:IEmailQueueService{public int Count;public bool Throw;public EmailQueueItem Last;public Task<int> EncolarAsync(EmailQueueItem i){if(Throw)throw new InvalidOperationException("queue failure");Last=i;Count++;return Task.FromResult(Count);}public Task<int> EncolarConAdjuntosAsync(EmailQueueItem i,IEnumerable<EmailAttachmentItem>a){return EncolarAsync(i);}public Task<bool> ExisteNotificacionAsync(string t,string k,int? s=null){return Task.FromResult(false);}public Task<EmailQueueItem> ObtenerSiguienteAsync(){return Task.FromResult<EmailQueueItem>(null);}public Task ActualizarEstadoAsync(int i,string e,string x=null){return Task.CompletedTask;}public Task MarcarEnviadoAsync(int i,string m){return Task.CompletedTask;}public Task<IEnumerable<EmailQueueItem>> ObtenerPendientesAsync(int l=10){return Task.FromResult<IEnumerable<EmailQueueItem>>(new EmailQueueItem[0]);}public Task ReprogramarReintentoAsync(int i,TimeSpan d){return Task.CompletedTask;}public Task<int> ReactivarEnviandoAbandonadosAsync(TimeSpan a){return Task.FromResult(0);}}
 }
}
