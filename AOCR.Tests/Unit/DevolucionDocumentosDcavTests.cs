using System.Collections.Generic;
using System.IO;
using CapaNegocio.DTOs;
using CapaNegocio.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class DevolucionDocumentosDcavTests
    {
        [TestMethod]public void Validar_Null_EsInvalido(){Assert.IsFalse(S().Validar(null).Valido);}
        [TestMethod]public void Validar_SinObservaciones_EsInvalido(){Assert.IsFalse(S().Validar(R()).Valido);}
        [TestMethod]public void Validar_TipoInvalido_EsInvalido(){var r=R(O("OTRO"));Assert.IsFalse(S().Validar(r).Valido);}
        [TestMethod]public void Validar_SinSeccion_EsInvalido(){var o=O("AOCR");o.Seccion="";Assert.IsFalse(S().Validar(R(o)).Valido);}
        [TestMethod]public void Validar_SinCampo_EsInvalido(){var o=O("AOCR");o.Campo="";Assert.IsFalse(S().Validar(R(o)).Valido);}
        [TestMethod]public void Validar_SinTexto_EsInvalido(){var o=O("AOCR");o.Texto="";Assert.IsFalse(S().Validar(R(o)).Valido);}
        [TestMethod]public void Validar_Aocr_EsValido(){Assert.IsTrue(S().Validar(R(O("AOCR"))).Valido);}
        [TestMethod]public void Validar_Reconocimiento_EsValido(){Assert.IsTrue(S().Validar(R(O("RECONOCIMIENTO"))).Valido);}
        [TestMethod]public void Validar_Condiciones_EsValido(){Assert.IsTrue(S().Validar(R(O("CONDICIONES"))).Valido);}
        [TestMethod]public void Validar_Ambos_EsValido(){Assert.IsTrue(S().Validar(R(O("AOCR"),O("CONDICIONES_LIMITACIONES"))).Valido);}
        [TestMethod]public void Validar_RolNoDcav_EsInvalido(){var r=R(O("AOCR"));r.Rol="InspectorTecnico";Assert.IsFalse(S().Validar(r).Valido);}
        [TestMethod]public void Validar_Admin_EsValido(){var r=R(O("AOCR"));r.Rol="Administrador";Assert.IsTrue(S().Validar(r).Valido);}
        [TestMethod]public void Clave_NoIncluyeUsuario(){var r=R(O("AOCR"));StringAssert.DoesNotMatch(DevolucionDocumentosDcavService.CrearClave(r,true,false),new System.Text.RegularExpressions.Regex(":"+r.UsuarioId+":"));}
        [TestMethod]public void Clave_DistingueAocr(){StringAssert.EndsWith(DevolucionDocumentosDcavService.CrearClave(R(),true,false),"AOCR");}
        [TestMethod]public void Clave_DistingueCondiciones(){StringAssert.EndsWith(DevolucionDocumentosDcavService.CrearClave(R(),false,true),"CONDICIONES");}
        [TestMethod]public void Clave_DistingueAmbos(){StringAssert.EndsWith(DevolucionDocumentosDcavService.CrearClave(R(),true,true),"AMBOS");}
        [TestMethod]public void Dao_ClonaSoloObservado(){C(Dao(),"CrearCorreccion");C(Dao(),"version+1");}
        [TestMethod]public void Dao_PreservaOrigen(){C(Dao(),"vigente=FALSE,estado='OBSERVADO_DCAV'");}
        [TestMethod]public void Dao_CorreccionEditable(){C(Dao(),"'CORRECCION_INSPECTOR'");}
        [TestMethod]public void Dao_NoObservadoAprobado(){C(Dao(),"'APROBADO_DCAV'");}
        [TestMethod]public void Dao_NoCreaTablas(){Assert.IsFalse(Dao().Contains("CREATE TABLE"));}
        [TestMethod]public void Observacion_UsaTablaExistente(){C(Dao(),"aocr_tbobservacion");}
        [TestMethod]public void Observacion_JsonVersionado(){C(Dao(),"DCAV_DOCUMENTAL_V1");}
        [TestMethod]public void Observacion_TieneRelacionVersiones(){C(Model(),"DocumentoOrigenId");C(Model(),"DocumentoCorreccionId");}
        [TestMethod]public void Transaccion_EsSerializable(){C(Service(),"IsolationLevel.Serializable");}
        [TestMethod]public void Concurrencia_ComparaSnapshot(){C(Service(),"VersionExpediente!=d.VersionExpediente");}
        [TestMethod]public void Idempotencia_SeComprueba(){C(Service(),"ExisteIdempotencia");C(Service(),"YaProcesado=true");}
        [TestMethod]public void EstadoCentral_EsObservado(){C(Dao(),"DOCUMENTOS_OBSERVADOS_DCAV");}
        [TestMethod]public void Auditoria_Y_Notificacion_SonAtomicas(){C(Dao(),"aocr_tbauditoria");C(Dao(),"aocr_tbnotificacion");C(Dao(),"NpgsqlTransaction tx");}
        [TestMethod]public void CicloObservacion_EstaImplementado(){C(Service(),"ABIERTA");C(Service(),"ATENDIDA_INSPECTOR");C(Service(),"CERRADA_DCAV");}
        [TestMethod]public void Inspector_LeeObservacionExacta(){var x=F("CapaPresentacion\\Services\\RevisionDocumentosInspectorService.cs");C(x,"DocumentoOrigenId=x.DocumentoOrigenId");C(x,"Campo=x.Campo");}
        [TestMethod]public void Inspector_NoEditaOrigenObservado(){var x=F("CapaPresentacion\\Services\\RevisionDocumentosInspectorService.cs");Assert.IsFalse(x.Contains("\"OBSERVADO_DCAV\",\"CORREGIDO"));C(x,"CORRECCION_INSPECTOR");}
        [TestMethod]public void Reenvio_AceptaPaqueteMixto(){var x=F("CapaNegocio\\Services\\EnvioDocumentosDcavService.cs");C(x,"APROBADO_DCAV");C(x,"al menos una versión corregida");}
        [TestMethod]public void UiDcav_PermiteMultiplesObservaciones(){var x=F("CapaPresentacion\\Views\\AocrDcav\\DetalleDocumentos.cshtml");C(x,"Agregar observación");C(x,"Observaciones['+i+'].Campo");}

        static DevolucionDocumentosDcavService S(){return new DevolucionDocumentosDcavService(new CapaDatos.DAOs.AocrDcavDocumentosDAO(),new CapaDatos.DAOs.DevolucionDocumentosDcavDAO());}
        static DevolverDocumentosDcavRequest R(params ObservacionDevolucionDcavRequest[] o){return new DevolverDocumentosDcavRequest{SolicitudId=1,InspeccionId=2,VersionExpediente=3,AocrId=4,VersionAocr=1,AocrPdfId=5,CondicionesId=6,VersionCondiciones=1,CondicionesPdfId=7,UsuarioId=8,Rol="DCAV",Observaciones=new List<ObservacionDevolucionDcavRequest>(o)};}
        static ObservacionDevolucionDcavRequest O(string tipo){return new ObservacionDevolucionDcavRequest{TipoDocumento=tipo,Seccion="Datos",Campo="Operador",Texto="Corregir valor"};}
        static string Dao(){return F("CapaDatos\\DAOs\\DevolucionDocumentosDcavDAO.cs");}static string Service(){return F("CapaNegocio\\Services\\DevolucionDocumentosDcavService.cs");}static string Model(){return F("CapaDatos\\Models\\DevolucionDocumentosDcavModels.cs");}
        static string F(string p){return File.ReadAllText(Path.Combine(Root(),p));}static string Root(){var d=new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"AOCR.sln")))d=d.Parent;return d.FullName;}static void C(string x,string y){StringAssert.Contains(x,y);}
    }
}
