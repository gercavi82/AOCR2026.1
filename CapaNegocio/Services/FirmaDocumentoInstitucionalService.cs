using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaNegocio.DTOs;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Npgsql;

namespace CapaNegocio.Services
{
    public sealed class FirmaDocumentoInstitucionalService : IFirmaDocumentoInstitucionalService
    {
        private readonly FirmaDocumentoInstitucionalDAO _dao;
        private readonly IPerfilFirmanteService _perfiles;
        private readonly IConfiguracionPosicionFirmaService _posiciones;
        private readonly string _baseAplicacion;

        public FirmaDocumentoInstitucionalService() : this(new FirmaDocumentoInstitucionalDAO(),new PerfilFirmanteService(),new ConfiguracionPosicionFirmaService(),AppDomain.CurrentDomain.BaseDirectory) { }
        public FirmaDocumentoInstitucionalService(FirmaDocumentoInstitucionalDAO dao,IPerfilFirmanteService perfiles,IConfiguracionPosicionFirmaService posiciones,string baseAplicacion)
        { _dao=dao;_perfiles=perfiles;_posiciones=posiciones;_baseAplicacion=Path.GetFullPath(baseAplicacion??AppDomain.CurrentDomain.BaseDirectory); }

        public ResultadoValidacionFirma ValidarFirma(int solicitudId,int inspeccionId,string tipoDocumento,int usuarioId)
        {
            var tipo=NormalizarTipo(tipoDocumento);
            if(solicitudId<=0||inspeccionId<=0||usuarioId<=0||tipo==null)return ErrorValidacion(400,"Datos de firma inválidos.");
            try
            {
                using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction()){var d=_dao.CargarParaFirma(cn,tx,solicitudId,inspeccionId,tipo);var r=ValidarContexto(d,usuarioId,tipo);tx.Rollback();return r;}}
            }
            catch(Exception ex){Trace.TraceError("[SIGNATURE][ERROR] ValidarFirma="+ex);return ErrorValidacion(500,"No se pudo validar la firma institucional.");}
        }

        public ResultadoFirmaDocumento Firmar(FirmarDocumentoInstitucionalRequest request)
        {
            var tipo=NormalizarTipo(request!=null?request.TipoDocumento:null);
            Trace.TraceInformation("[SIGNATURE][IN] SolicitudId="+(request!=null?request.SolicitudId:0)+"; InspeccionId="+(request!=null?request.InspeccionId:0)+"; Tipo="+(tipo??string.Empty)+"; UsuarioId="+(request!=null?request.UsuarioId:0));
            if(request==null||request.SolicitudId<=0||request.InspeccionId<=0||request.UsuarioId<=0||tipo==null)return Rechazar(request,tipo,400,"Datos de firma inválidos.");
            string temporal=null,definitivo=null;
            try
            {
                using(var cn=_dao.CrearConexion())
                {
                    cn.Open();
                    using(var tx=cn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                    {
                        var d=_dao.CargarParaFirma(cn,tx,request.SolicitudId,request.InspeccionId,tipo);
                        if(d==null){tx.Rollback();return Rechazar(request,tipo,404,"No existe el expediente aprobado o sus PDF exactos.");}
                        var clave=ConstruirClave(d,request.UsuarioId,tipo);
                        if(_dao.IdempotenciaExiste(cn,tx,clave)){tx.Rollback();Trace.TraceInformation("[IDEMPOTENCY][HIT] Clave="+clave);return new ResultadoFirmaDocumento{Exitoso=true,YaProcesado=true,CodigoHttp=200,Mensaje="La firma ya fue registrada.",TipoDocumento=tipo,DocumentoId=d.DocumentoId,PdfOrigenId=d.PdfOrigenId,VersionDocumento=d.VersionDocumento};}
                        var validacion=ValidarContexto(d,request.UsuarioId,tipo);
                        if(!validacion.Valido){tx.Rollback();Trace.TraceWarning("[SIGNATURE][VALIDATION_ERROR] "+validacion.Mensaje);return Rechazar(request,tipo,validacion.CodigoHttp,validacion.Mensaje);}
                        if(request.VersionExpediente<=0||request.VersionExpediente!=d.VersionExpediente){tx.Rollback();Trace.TraceWarning("[CONCURRENCY][CONFLICT] Version expediente.");return Rechazar(request,tipo,409,"El expediente cambió mientras se preparaba la firma.");}
                        Trace.TraceInformation("[SIGNATURE][CONTEXT_OK] DocumentoId="+d.DocumentoId+"; PdfOrigenId="+d.PdfOrigenId+"; Version="+d.VersionDocumento);
                        Trace.TraceInformation("[SIGNATURE][DOCUMENT_LOAD_OK] DocumentoId="+d.DocumentoId);
                        if(_dao.ExisteFirma(cn,tx,d.SolicitudId,d.InspeccionId,tipo,d.VersionDocumento)){tx.Rollback();return Rechazar(request,tipo,409,"El documento ya tiene una firma institucional vigente.");}

                        var perfil=validacion.Perfil;var posicion=validacion.Posicion;
                        var rutaOrigen=ResolverRutaPrivada(d.RutaPdfOrigen,"App_Data"+Path.DirectorySeparatorChar+"Uploads");
                        var pdf=File.ReadAllBytes(rutaOrigen);
                        ValidarPdfOrigen(pdf,d);
                        Trace.TraceInformation("[SIGNATURE][SOURCE_PDF_VALIDATION_OK] PdfOrigenId="+d.PdfOrigenId);
                        var rutaImagen=ResolverRutaPrivada(perfil.RutaInternaFirma,"App_Data"+Path.DirectorySeparatorChar+"Signatures");
                        var imagen=File.ReadAllBytes(rutaImagen);
                        ValidarImagen(imagen,perfil);
                        Trace.TraceInformation("[SIGNATURE][IMAGE_VALIDATION_OK] FirmaImagenId="+perfil.FirmaImagenId);
                        var qr="AOCR|Solicitud="+d.SolicitudId+"|Inspeccion="+d.InspeccionId+"|Documento="+d.DocumentoId+"|Version="+d.VersionDocumento+"|Usuario="+request.UsuarioId;
                        var firmado=AplicarFirma(pdf,imagen,perfil,posicion,qr);
                        Trace.TraceInformation("[SIGNATURE][IMAGE_APPLIED]");
                        ValidarPdfFirmado(firmado);
                        var hash=Sha256(firmado);
                        Trace.TraceInformation("[SIGNATURE][HASH_CALCULATED] Sha256="+hash+"; Bytes="+firmado.LongLength);
                        var carpeta=Path.Combine(_baseAplicacion,"App_Data","Uploads","AOCR","FirmadosInstitucionales",d.SolicitudId.ToString(CultureInfo.InvariantCulture),d.InspeccionId.ToString(CultureInfo.InvariantCulture));
                        Directory.CreateDirectory(carpeta);
                        temporal=Path.Combine(carpeta,"."+Guid.NewGuid().ToString("N")+".tmp");
                        File.WriteAllBytes(temporal,firmado);Trace.TraceInformation("[SIGNATURE][TEMP_FILE_CREATED]");
                        var nombre=(tipo==TiposDocumentoFirmaInstitucional.Aocr?"aocr_dgac_":"condiciones_dcav_")+d.DocumentoId+"_v"+d.VersionDocumento+"_"+Guid.NewGuid().ToString("N")+".pdf";
                        definitivo=Path.Combine(carpeta,nombre);File.Move(temporal,definitivo);temporal=null;
                        var rutaRelativa="~/"+definitivo.Substring(_baseAplicacion.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\','/');
                        var estadoDocumento=tipo==TiposDocumentoFirmaInstitucional.Aocr?"FIRMADO_DGAC":"FIRMADO_DCAV";
                        var estadoAocr=tipo==TiposDocumentoFirmaInstitucional.Aocr?estadoDocumento:d.EstadoAocr;
                        var estadoCond=tipo==TiposDocumentoFirmaInstitucional.Condiciones?estadoDocumento:d.EstadoCondiciones;
                        var ambas=string.Equals(estadoAocr,"FIRMADO_DGAC",StringComparison.OrdinalIgnoreCase)&&string.Equals(estadoCond,"FIRMADO_DCAV",StringComparison.OrdinalIgnoreCase);
                        var estadoCentral=ambas?"DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE":"PENDIENTE_FIRMAS_INSTITUCIONALES";
                        var auditoria=ConstruirAuditoria(d,perfil,posicion,hash,firmado.LongLength,estadoDocumento,qr);
                        var firmaId=_dao.RegistrarFirma(cn,tx,d,perfil.UsuarioId,perfil.Rol,perfil.NombreCompleto,perfil.Cargo,rutaRelativa,hash,firmado.LongLength,qr,estadoDocumento,"IMAGE_SHA256="+perfil.HashFirma);
                        auditoria += ";PdfFirmadoId="+firmaId+";EstadoAnterior="+d.EstadoDocumento+";EstadoNuevo="+estadoDocumento+";CorrelationId="+(request.CorrelationId??string.Empty)+";ClaveIdempotente="+clave+";Resultado=OK";
                        _dao.ActualizarEstados(cn,tx,d,estadoDocumento,estadoCentral,perfil.UsuarioId,perfil.Rol,request.Ip,request.CorrelationId,clave,auditoria);
                        _dao.RegistrarIdempotencia(cn,tx,d,clave,estadoCentral,request.CorrelationId);
                        var prefijoEvento=tipo==TiposDocumentoFirmaInstitucional.Aocr?"FIRMA_AOCR":"FIRMA_CONDICIONES";
                        _dao.RegistrarAuditoria(cn,tx,d.SolicitudId,perfil.UsuarioId,request.Ip,prefijoEvento+"_INICIADA",auditoria);
                        _dao.RegistrarAuditoria(cn,tx,d.SolicitudId,perfil.UsuarioId,request.Ip,prefijoEvento+(tipo==TiposDocumentoFirmaInstitucional.Aocr?"_APLICADA_DGAC":"_APLICADA_DCAV"),auditoria);
                        _dao.RegistrarAuditoria(cn,tx,d.SolicitudId,perfil.UsuarioId,request.Ip,estadoDocumento=="FIRMADO_DGAC"?"AOCR_FIRMADO_DGAC":"CONDICIONES_FIRMADAS_DCAV",auditoria);
                        _dao.RegistrarAuditoria(cn,tx,d.SolicitudId,perfil.UsuarioId,request.Ip,ambas?"AMBAS_FIRMAS_COMPLETADAS":"FIRMA_PARCIAL_REGISTRADA",auditoria);
                        if(ambas)_dao.RegistrarAuditoria(cn,tx,d.SolicitudId,perfil.UsuarioId,request.Ip,"DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE",auditoria);
                        tx.Commit();
                        Trace.TraceInformation("[SIGNATURE][DOCUMENT_STATE_UPDATED] Estado="+estadoDocumento);
                        Trace.TraceInformation("[SIGNATURE][METADATA_SAVED] FirmaId="+firmaId);Trace.TraceInformation("[SIGNATURE][BOTH_SIGNATURES_CHECK] Ambas="+ambas);if(ambas)Trace.TraceInformation("[SIGNATURE][ALL_SIGNATURES_COMPLETED] Evento=AMBAS_FIRMAS_COMPLETADAS");Trace.TraceInformation("[SIGNATURE][OK]");
                        return new ResultadoFirmaDocumento{Exitoso=true,CodigoHttp=200,Mensaje=ambas?"Ambas firmas institucionales fueron completadas.":"Firma institucional registrada; el otro documento continúa pendiente.",FirmaId=firmaId,DocumentoId=d.DocumentoId,PdfOrigenId=d.PdfOrigenId,VersionDocumento=d.VersionDocumento,TipoDocumento=tipo,EstadoDocumento=estadoDocumento,EstadoExpediente=estadoCentral,RutaPdfFirmado=rutaRelativa,HashPdfFirmado=hash,TamanioPdfFirmado=firmado.LongLength,FechaFirma=DateTime.Now};
                    }
                }
            }
            catch(PostgresException ex) when(ex.SqlState=="23505"){Compensar(definitivo,temporal,request,tipo);Trace.TraceWarning("[CONCURRENCY][CONFLICT] "+ex.ConstraintName);return Rechazar(request,tipo,409,"La firma ya fue registrada por otra operación.");}
            catch(InvalidOperationException ex) when(ex.Message.StartsWith("CONCURRENCY_CONFLICT",StringComparison.Ordinal)){Compensar(definitivo,temporal,request,tipo);Trace.TraceWarning("[CONCURRENCY][CONFLICT] "+ex.Message);return Rechazar(request,tipo,409,"El expediente cambió durante la firma.");}
            catch(FirmaInstitucionalException ex){Compensar(definitivo,temporal,request,tipo);Trace.TraceWarning("[SIGNATURE][VALIDATION_ERROR] "+ex.Message);return Rechazar(request,tipo,ex.CodigoHttp,ex.Message);}
            catch(Exception ex){Compensar(definitivo,temporal,request,tipo);Trace.TraceError("[SIGNATURE][ERROR] "+ex);return Rechazar(request,tipo,500,"Error interno al aplicar la firma institucional.");}
        }

        public EstadoFirmasExpedienteDto ObtenerEstadoFirmas(int solicitudId,int inspeccionId)
        {
            using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction()){var d=_dao.CargarParaFirma(cn,tx,solicitudId,inspeccionId,TiposDocumentoFirmaInstitucional.Aocr);tx.Rollback();if(d==null)return null;var a=string.Equals(d.EstadoAocr,"FIRMADO_DGAC",StringComparison.OrdinalIgnoreCase);var c=string.Equals(d.EstadoCondiciones,"FIRMADO_DCAV",StringComparison.OrdinalIgnoreCase);return new EstadoFirmasExpedienteDto{SolicitudId=solicitudId,InspeccionId=inspeccionId,EstadoAocr=d.EstadoAocr,EstadoCondiciones=d.EstadoCondiciones,AocrFirmadoDgac=a,CondicionesFirmadasDcav=c,AmbasFirmasCompletas=a&&c,EstadoCentral=d.EstadoCentral};}}
        }

        private ResultadoValidacionFirma ValidarContexto(FirmaDocumentoInstitucionalSnapshot d,int usuarioId,string tipo)
        {
            if(d==null)return ErrorValidacion(404,"No existe el expediente aprobado o sus PDF exactos.");
            if(d.EstadoCentral=="AOCR_FINALIZADO"||d.EstadoCentral=="ANULADO")return ErrorValidacion(409,"El expediente no admite firmas.");
            if(d.EstadoCentral!="PENDIENTE_FIRMA_DIRDAC"&&d.EstadoCentral!="PENDIENTE_FIRMAS_INSTITUCIONALES"&&d.EstadoCentral!="DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE")return ErrorValidacion(409,"El expediente no está en etapa de firmas institucionales.");
            var estadoEsperado=tipo==TiposDocumentoFirmaInstitucional.Aocr?"PENDIENTE_FIRMA_DGAC":"PENDIENTE_FIRMA_DCAV";
            if(d.EstadoDocumento!="APROBADO_DCAV"&&d.EstadoDocumento!=estadoEsperado)return ErrorValidacion(d.EstadoDocumento=="FIRMADO_DGAC"||d.EstadoDocumento=="FIRMADO_DCAV"?409:422,"El documento exacto no está aprobado y pendiente de la firma correspondiente.");
            var perfil=_perfiles.ObtenerPerfil(usuarioId,tipo);if(perfil==null||!perfil.AutorizadoParaDocumento)return ErrorValidacion(403,tipo==TiposDocumentoFirmaInstitucional.Aocr?"Solo DGAC puede firmar el AOCR.":"Solo DCAV puede firmar Condiciones y Limitaciones.");
            if(!perfil.Activo)return ErrorValidacion(403,"El perfil firmante no está activo o vigente.");
            if(string.IsNullOrWhiteSpace(perfil.NombreCompleto)||string.IsNullOrWhiteSpace(perfil.Cargo))return ErrorValidacion(422,"El perfil firmante no tiene nombre y cargo institucional completos.");
            if(perfil.FirmaImagenId<=0||string.IsNullOrWhiteSpace(perfil.RutaInternaFirma)||string.IsNullOrWhiteSpace(perfil.HashFirma))return ErrorValidacion(422,"El perfil firmante no tiene una imagen de firma activa configurada.");
            var posicion=_posiciones.Obtener(tipo,d.VersionDocumento);if(posicion==null||!PosicionValida(posicion))return ErrorValidacion(422,"No existe una posición de firma válida para el documento y plantilla.");
            Trace.TraceInformation("[SIGNATURE][ROLE_VALIDATION_OK] Rol="+perfil.Rol);Trace.TraceInformation("[SIGNATURE][SIGNER_PROFILE_OK] UsuarioId="+perfil.UsuarioId);Trace.TraceInformation("[SIGNATURE][POSITION_VALIDATION_OK] ConfiguracionId="+posicion.ConfiguracionId);
            return new ResultadoValidacionFirma{Valido=true,CodigoHttp=200,Mensaje="Firma válida.",DocumentoId=d.DocumentoId,PdfOrigenId=d.PdfOrigenId,VersionDocumento=d.VersionDocumento,Perfil=perfil,Posicion=posicion};
        }

        private static bool PosicionValida(ConfiguracionPosicionFirmaDto p){return p.Pagina>0&&p.XRatio>=0&&p.YRatio>=0&&p.AnchoRatio>0&&p.AltoRatio>0&&p.XRatio+p.AnchoRatio<=1&&p.YRatio+p.AltoRatio<=1&&p.MargenRatio>=0&&p.MargenRatio<0.2m&&Ratio(p.NombreYRatio)&&Ratio(p.CargoYRatio)&&Ratio(p.FechaYRatio)&&Ratio(p.QrXRatio)&&Ratio(p.QrYRatio)&&p.QrTamanioRatio>0&&p.QrTamanioRatio<0.5m&&(!p.MostrarQr||(p.QrXRatio-p.QrTamanioRatio/2>=0&&p.QrXRatio+p.QrTamanioRatio/2<=1&&p.QrYRatio-p.QrTamanioRatio/2>=0&&p.QrYRatio+p.QrTamanioRatio/2<=1));}
        private static bool Ratio(decimal valor){return valor>=0m&&valor<=1m;}
        private string ResolverRutaPrivada(string ruta,string subraiz)
        {if(string.IsNullOrWhiteSpace(ruta))throw new FirmaInstitucionalException(404,"No existe el archivo requerido.");var limpia=ruta.Trim().Replace('/',Path.DirectorySeparatorChar).TrimStart('~',Path.DirectorySeparatorChar);var path=Path.GetFullPath(Path.Combine(_baseAplicacion,limpia));var root=Path.GetFullPath(Path.Combine(_baseAplicacion,subraiz)).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new FirmaInstitucionalException(404,"No existe el archivo institucional requerido.");return path;}
        private static void ValidarPdfOrigen(byte[] pdf,FirmaDocumentoInstitucionalSnapshot d){if(pdf==null||pdf.Length<100||pdf[0]!=0x25||pdf[1]!=0x50||pdf[2]!=0x44||pdf[3]!=0x46)throw new FirmaInstitucionalException(422,"El PDF aprobado no es válido.");if(d.TamanioPdfOrigen<=0||d.TamanioPdfOrigen!=pdf.LongLength)throw new FirmaInstitucionalException(409,"El tamaño del PDF aprobado no coincide.");var h=Sha256(pdf);if(string.IsNullOrWhiteSpace(d.HashPdfOrigen)||!string.Equals(h,d.HashPdfOrigen,StringComparison.OrdinalIgnoreCase))throw new FirmaInstitucionalException(409,"El hash del PDF aprobado no coincide.");using(var r=new PdfReader(pdf)){if(r.NumberOfPages<=0)throw new FirmaInstitucionalException(422,"El PDF aprobado no contiene páginas.");}}
        private static void ValidarImagen(byte[] bytes,PerfilFirmanteDto p){if(bytes==null||bytes.Length<100||bytes.Length>5*1024*1024)throw new FirmaInstitucionalException(422,"El tamaño de la imagen de firma no es válido.");if(!string.Equals(Sha256(bytes),p.HashFirma,StringComparison.OrdinalIgnoreCase))throw new FirmaInstitucionalException(409,"El hash de la imagen de firma no coincide.");try{using(var ms=new MemoryStream(bytes))using(var img=System.Drawing.Image.FromStream(ms,true,true)){if(img.Width<40||img.Height<20||img.Width>4000||img.Height>4000)throw new FirmaInstitucionalException(422,"Las dimensiones de la imagen de firma no son válidas.");var f=img.RawFormat;if(f.Guid!=ImageFormat.Png.Guid&&f.Guid!=ImageFormat.Jpeg.Guid)throw new FirmaInstitucionalException(422,"La imagen de firma debe ser PNG o JPEG.");}}catch(FirmaInstitucionalException){throw;}catch{throw new FirmaInstitucionalException(422,"La imagen de firma está corrupta.");}}
        private static byte[] AplicarFirma(byte[] pdf,byte[] imagen,PerfilFirmanteDto perfil,ConfiguracionPosicionFirmaDto p,string qr)
        {
            using(var reader=new PdfReader(pdf))
            {
                if(p.Pagina>reader.NumberOfPages)throw new FirmaInstitucionalException(422,"La página configurada para la firma no existe.");
                using(var ms=new MemoryStream())
                {
                    using(var stamper=new PdfStamper(reader,ms))
                    {
                        var page=reader.GetPageSize(p.Pagina);
                        float left=(float)(p.XRatio*(decimal)page.Width),bottom=(float)((1m-p.YRatio-p.AltoRatio)*(decimal)page.Height),width=(float)(p.AnchoRatio*(decimal)page.Width),height=(float)(p.AltoRatio*(decimal)page.Height),margin=(float)(p.MargenRatio*(decimal)Math.Min(width,height));
                        var cb=stamper.GetOverContent(p.Pagina);cb.SaveState();cb.SetColorFill(BaseColor.WHITE);cb.Rectangle(left,bottom,width,height);cb.Fill();
                        var qrWidth=p.MostrarQr?Math.Min(width*(float)p.QrTamanioRatio,height*(float)p.QrTamanioRatio):0f;
                        var imageAreaHeight=height*.58f;var firma=iTextSharp.text.Image.GetInstance(imagen);
                        firma.ScaleToFit(Math.Max(1,width-2*margin-qrWidth),Math.Max(1,imageAreaHeight-2*margin));
                        firma.SetAbsolutePosition(left+margin+(width-2*margin-qrWidth-firma.ScaledWidth)/2,bottom+height-imageAreaHeight+margin+(imageAreaHeight-2*margin-firma.ScaledHeight)/2);cb.AddImage(firma);
                        if(p.MostrarQr){var q=new BarcodeQRCode(qr,100,100,null).GetImage();q.ScaleAbsolute(qrWidth,qrWidth);q.SetAbsolutePosition(left+width*(float)p.QrXRatio-qrWidth/2,bottom+height*(float)p.QrYRatio-qrWidth/2);cb.AddImage(q);}
                        var font=BaseFont.CreateFont(BaseFont.HELVETICA,BaseFont.CP1252,BaseFont.EMBEDDED);var bold=BaseFont.CreateFont(BaseFont.HELVETICA_BOLD,BaseFont.CP1252,BaseFont.EMBEDDED);var textX=left+width/2;
                        cb.BeginText();cb.SetFontAndSize(bold,9);cb.ShowTextAligned(Element.ALIGN_CENTER,perfil.NombreCompleto,textX,bottom+height*(float)p.NombreYRatio,0);cb.SetFontAndSize(font,8);cb.ShowTextAligned(Element.ALIGN_CENTER,perfil.Cargo,textX,bottom+height*(float)p.CargoYRatio,0);cb.ShowTextAligned(Element.ALIGN_CENTER,"Firmado: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm"),textX,bottom+height*(float)p.FechaYRatio,0);cb.EndText();cb.RestoreState();
                    }
                    return ms.ToArray();
                }
            }
        }
        private static void ValidarPdfFirmado(byte[] pdf){if(pdf==null||pdf.Length<100)throw new FirmaInstitucionalException(500,"No se generó el PDF firmado.");using(var r=new PdfReader(pdf)){if(r.NumberOfPages<=0)throw new FirmaInstitucionalException(500,"El PDF firmado no contiene páginas.");}Trace.TraceInformation("[SIGNATURE][SIGNED_PDF_VALIDATED]");}
        private static string Sha256(byte[] b){using(var h=SHA256.Create()){var x=h.ComputeHash(b);var s=new StringBuilder(x.Length*2);foreach(var v in x)s.Append(v.ToString("x2"));return s.ToString();}}
        private static string NormalizarTipo(string tipo){var x=(tipo??string.Empty).Trim().ToUpperInvariant();if(x=="AOCR"||x=="RECONOCIMIENTO")return TiposDocumentoFirmaInstitucional.Aocr;if(x=="CONDICIONES"||x=="CONDICIONES_LIMITACIONES")return TiposDocumentoFirmaInstitucional.Condiciones;return null;}
        private static string ConstruirClave(FirmaDocumentoInstitucionalSnapshot d,int usuario,string tipo){return "FIRMA_INST:"+d.SolicitudId+":"+d.InspeccionId+":"+d.DocumentoId+":"+d.VersionDocumento+":"+d.PdfOrigenId+":"+usuario+":"+(tipo==TiposDocumentoFirmaInstitucional.Aocr?"FIRMAR_AOCR":"FIRMAR_CONDICIONES");}
        private static string ConstruirAuditoria(FirmaDocumentoInstitucionalSnapshot d,PerfilFirmanteDto p,ConfiguracionPosicionFirmaDto pos,string hash,long bytes,string estado,string qr){return "DocumentoId="+d.DocumentoId+";VersionDocumento="+d.VersionDocumento+";PdfOrigenId="+d.PdfOrigenId+";UsuarioFirmanteId="+p.UsuarioId+";RolFirmante="+p.Rol+";NombreFirmante="+p.NombreCompleto+";CargoFirmante="+p.Cargo+";FirmaImagenId="+p.FirmaImagenId+";HashImagen="+p.HashFirma+";HashPdfFirmado="+hash+";Tamanio="+bytes+";EstadoDocumento="+estado+";PosicionId="+pos.ConfiguracionId+";Qr="+qr;}
        private void Compensar(string definitivo,string temporal,FirmarDocumentoInstitucionalRequest r,string tipo){try{if(!string.IsNullOrWhiteSpace(temporal)&&File.Exists(temporal))File.Delete(temporal);if(!string.IsNullOrWhiteSpace(definitivo)&&File.Exists(definitivo))File.Delete(definitivo);Trace.TraceInformation("[SIGNATURE][ROLLBACK] [SIGNATURE][COMPENSATION_OK]");}catch(Exception ex){Trace.TraceError("[SIGNATURE][COMPENSATION_ERROR] "+ex);try{_dao.RegistrarAlertaCompensacion(r!=null?r.SolicitudId:0,r!=null?r.UsuarioId:0,r!=null?r.Ip:null,"TipoDocumento="+(tipo??string.Empty)+";ArchivoTemporal="+(!string.IsNullOrWhiteSpace(temporal)?Path.GetFileName(temporal):string.Empty)+";ArchivoDefinitivo="+(!string.IsNullOrWhiteSpace(definitivo)?Path.GetFileName(definitivo):string.Empty)+";Error="+ex.Message);}catch(Exception alerta){Trace.TraceError("[SIGNATURE][COMPENSATION_ERROR] No se pudo registrar alerta="+alerta.Message);}}}
        private static ResultadoFirmaDocumento Error(int code,string message,string tipo){return new ResultadoFirmaDocumento{Exitoso=false,CodigoHttp=code,Mensaje=message,TipoDocumento=tipo};}
        private ResultadoFirmaDocumento Rechazar(FirmarDocumentoInstitucionalRequest r,string tipo,int code,string message){try{_dao.RegistrarRechazo(r!=null?r.SolicitudId:0,r!=null?r.UsuarioId:0,r!=null?r.Ip:null,"TipoDocumento="+(tipo??string.Empty)+";InspeccionId="+(r!=null?r.InspeccionId:0)+";CodigoHttp="+code+";CorrelationId="+(r!=null?r.CorrelationId:string.Empty)+";Resultado=RECHAZADO;Motivo="+message);}catch(Exception ex){Trace.TraceError("[SIGNATURE][ERROR] Auditoria rechazo="+ex.Message);}return Error(code,message,tipo);}
        private static ResultadoValidacionFirma ErrorValidacion(int code,string message){return new ResultadoValidacionFirma{Valido=false,CodigoHttp=code,Mensaje=message};}
    }

    public sealed class FirmaInstitucionalException:Exception
    {public int CodigoHttp{get;private set;}public FirmaInstitucionalException(int codigo,string mensaje):base(mensaje){CodigoHttp=codigo;}}
}
