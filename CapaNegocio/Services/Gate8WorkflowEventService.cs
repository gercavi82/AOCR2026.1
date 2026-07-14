using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;

namespace CapaNegocio.Services
{
    public static class Gate8Eventos
    {
        public static readonly string[] Todos = { "NC_GENERADA","NC_FIRMADA_INSPECTOR","NC_ENVIADA_COORDINADOR","NC_DEVUELTA_INSPECTOR","NC_CORREGIDA_INSPECTOR","NC_APROBADA_COORDINADOR","NC_FIRMADA_COORDINADOR","NC_NOTIFICADA_RT","SUBSANACION_INICIADA","DOCUMENTO_SUBSANADO_RT","SUBSANACION_ENVIADA_INSPECTOR","DOCUMENTO_SUBSANADO_ACEPTADO","DOCUMENTO_SUBSANADO_RECHAZADO","SUBSANACION_DEVUELTA_RT","SUBSANACION_ACEPTADA","NUEVO_INFORME_REQUERIDO","NUEVA_INSPECCION_SOLICITADA","NUEVA_SOLICITUD_CREADA","NUEVA_ORDEN_PREPARADA","NUEVA_INSPECCION_CREADA","NUEVA_INSPECCION_ASIGNADA","REEVALUACION_INICIADA","REEVALUACION_INSATISFACTORIA","NUEVA_NC_GENERADA","REEVALUACION_SATISFACTORIA","NC_CERRADA","AOCR_GENERADA","CONDICIONES_GENERADAS","DOCUMENTOS_ENVIADOS_COORDINADOR","DOCUMENTOS_ENVIADOS_DIRDAC","DOCUMENTOS_FIRMADOS","DOCUMENTOS_LIBERADOS_RT" };
        public static string Key(string evento, params object[] partes) { return (evento ?? "").Trim().ToUpperInvariant()+":"+string.Join(":",(partes??new object[0]).Select(x=>Convert.ToString(x)??"0")); }
        public static string Correlation(string existente,int ncId) { return !string.IsNullOrWhiteSpace(existente)?existente.Trim():"NC-"+ncId+"-"+Guid.NewGuid().ToString("N").Substring(0,12); }
    }

    public sealed class Gate8EventoRequest
    {
        public Gate8EventoRegistro Registro { get; set; }
        public int? UsuarioNotificacionInterna { get; set; }
        public string Titulo { get; set; }
        public string Mensaje { get; set; }
        public EmailQueueItem Correo { get; set; }
    }
    public sealed class Gate8EventoResultado { public bool EventoNuevo; public bool Duplicado; public bool CorreoEncolado; public string ErrorNotificacion; }

    public sealed class Gate8WorkflowEventService
    {
        private readonly IGate8EventoRepository _eventos; private readonly IEmailQueueService _cola;
        private readonly Action<int,string,string,int?> _interna; private readonly Action<Gate8EventoRegistro> _auditar;
        public Gate8WorkflowEventService() : this(new Gate8EventoDAO(),new EmailQueueService(),
            (u,t,m,s)=>NotificacionBL.EnviarNotificacion(u,t,m,"INFO",s.HasValue?"/SolicitudAOCR/Detalle/"+s.Value:null,"GATE8",s,"SOLICITUD"),null) { }
        public Gate8WorkflowEventService(IGate8EventoRepository eventos,IEmailQueueService cola,Action<int,string,string,int?> interna,Action<Gate8EventoRegistro> auditar)
        { _eventos=eventos;_cola=cola;_interna=interna;_auditar=auditar; }

        public Gate8EventoResultado PublicarPostCommit(Gate8EventoRequest request)
        {
            if(request==null||request.Registro==null)throw new ArgumentNullException("request");
            var r=new Gate8EventoResultado(); var e=request.Registro;
            try { r.EventoNuevo=_eventos.RegistrarIntento(e); r.Duplicado=!r.EventoNuevo; }
            catch(Exception ex){ r.ErrorNotificacion=ex.Message; return r; }
            if(!r.EventoNuevo)return r;
            try { if(request.UsuarioNotificacionInterna.HasValue&&_interna!=null)_interna(request.UsuarioNotificacionInterna.Value,request.Titulo,request.Mensaje,e.SolicitudId); }
            catch(Exception ex){r.ErrorNotificacion=ex.Message;}
            try { if(request.Correo!=null){request.Correo.EventKey=e.EventKey;request.Correo.CorrelationId=e.CorrelationId;request.Correo.TipoNotificacion=e.Evento;request.Correo.SolicitudId=e.SolicitudId;_cola.EncolarAsync(request.Correo).GetAwaiter().GetResult();r.CorreoEncolado=true;} }
            catch(Exception ex){r.ErrorNotificacion=(r.ErrorNotificacion+" | "+ex.Message).Trim(' ','|');}
            e.Resultado=string.IsNullOrWhiteSpace(r.ErrorNotificacion)?"OK":"PENDIENTE_REINTENTO";e.DetalleError=r.ErrorNotificacion;
            try{_eventos.ActualizarResultado(e.EventKey,e.Resultado,e.DetalleError);}catch{}
            try{if(_auditar!=null)_auditar(e);}catch{}
            return r;
        }
    }
}
