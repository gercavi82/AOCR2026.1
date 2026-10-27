using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Razor;
using System.Web.Razor.Generator;
using CapaModelo;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using CapaPresentacion.Services;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
    public sealed class Datos19Server { public string MapPath(string value) { return Path.Combine(Ac09InformeTecnicoTests.Root(),"CapaPresentacion",value.TrimStart('~','/').Replace('/',Path.DirectorySeparatorChar)); } }
    public sealed class Datos19Url { public string Content(string value) { return new Uri(new Datos19Server().MapPath(value)).AbsoluteUri; } }
    public abstract class Datos19Page
    {
        public AocrDocumentoPdfViewModel Model { get; set; }
        public string Layout { get; set; }
        public Datos19Server Server = new Datos19Server();
        public Datos19Url Url = new Datos19Url();
        public StringBuilder Output = new StringBuilder();
        public Dictionary<string,Action> Sections = new Dictionary<string,Action>();
        public void WriteLiteral(object value) { Output.Append(value); }
        public void Write(object value) { Output.Append(HttpUtility.HtmlEncode(value)); }
        public void DefineSection(string name,Action action) { Sections[name]=action; }
        public void WriteAttribute(string name,Tuple<string,int> prefix,Tuple<string,int> suffix,params Tuple<Tuple<string,int>,Tuple<object,int>,bool>[] values)
        {Output.Append(prefix.Item1);foreach(var value in values){Output.Append(value.Item1.Item1);Write(value.Item2.Item1);}Output.Append(suffix.Item1);}
        public abstract void Execute();
    }

    [TestClass]
    public class Observaciones19PdfTests
    {
        private static ContactoDocumentalAocr Contacto(string nombre) {return new ContactoDocumentalAocr{Nombre=nombre,Direccion="Base "+nombre,Telefono="123456",Correo=nombre.ToLowerInvariant()+"@example.test"};}
        public static DatosDocumentalesAocr Datos()
        {
            return new DatosDocumentalesAocr{PermisoCnacNumero="CNAC-PRUEBA-026",PermisoCnacEmision=new DateTime(2026,1,2),PermisoCnacVencimiento=new DateTime(2028,3,4),AocVigenteHasta=new DateTime(2029,5,6),PaisBandera="QATAR",
                Explotador=Contacto("EXPLOTADOR"),GerenteOperaciones=Contacto("GERENTE"),RepresentanteTecnico=Contacto("RTEAE"),RepresentanteLegalEcuador=Contacto("LEGALECUADOR"),
                Estaciones=new List<OperacionEstacionDocumentalAocr>{new OperacionEstacionDocumentalAocr{EstacionId=36,Codigo="R"},new OperacionEstacionDocumentalAocr{EstacionId=37,Codigo="ET"}}};
        }
        private static List<SolicitudEstacionInspeccion> Estaciones() {return new List<SolicitudEstacionInspeccion>{new SolicitudEstacionInspeccion{Id=36,EstacionCodigo="UIO",Activo=true},new SolicitudEstacionInspeccion{Id=37,EstacionCodigo="GYE",Activo=true}};}

        [TestMethod]
        public void FormularioMvc_EnlazaContactosFechasYEstacionesIndependientes()
        {
            var valores=new System.Collections.Specialized.NameValueCollection {
                {"SolicitudId","901"},{"DatosDocumentales.PermisoCnacNumero","CNAC-026"},{"DatosDocumentales.PermisoCnacEmision","2026-01-02"},
                {"DatosDocumentales.GerenteOperaciones.Nombre","GERENTE"},{"DatosDocumentales.RepresentanteTecnico.Nombre","RT EAE"},{"DatosDocumentales.RepresentanteLegalEcuador.Nombre","LEGAL"},
                {"DatosDocumentales.Estaciones[0].EstacionId","36"},{"DatosDocumentales.Estaciones[0].Codigo","R"},{"DatosDocumentales.Estaciones[1].EstacionId","37"},{"DatosDocumentales.Estaciones[1].Codigo","ET"}
            };
            var contexto=new System.Web.Mvc.ModelBindingContext {ModelMetadata=System.Web.Mvc.ModelMetadataProviders.Current.GetMetadataForType(null,typeof(GuardarDatosFirmaAocrRequest)),ValueProvider=new System.Web.Mvc.NameValueCollectionValueProvider(valores,System.Globalization.CultureInfo.InvariantCulture)};
            var request=(GuardarDatosFirmaAocrRequest)new System.Web.Mvc.DefaultModelBinder().BindModel(new System.Web.Mvc.ControllerContext(),contexto);
            Assert.IsTrue(contexto.ModelState.IsValid);Assert.AreEqual(new DateTime(2026,1,2),request.DatosDocumentales.PermisoCnacEmision);
            Assert.AreEqual("GERENTE",request.DatosDocumentales.GerenteOperaciones.Nombre);Assert.AreEqual("RT EAE",request.DatosDocumentales.RepresentanteTecnico.Nombre);Assert.AreEqual("LEGAL",request.DatosDocumentales.RepresentanteLegalEcuador.Nombre);
            Assert.AreEqual("ET",request.DatosDocumentales.Estaciones.Single(e=>e.EstacionId==37).Codigo);
            Assert.AreEqual("AOCR-2026-0007",GeneracionAOCRService.GenerarNumeroAOCR(7,new DateTime(2026,1,1)),"El helper de numeración anterior mantiene su contrato; no se usa para asignar el nuevo AOCR.");
        }

        [TestMethod]
        public void Validaciones_RechazanDatosCruzadosCodigosDesconocidosYFechasInvertidas()
        {
            var datos=Datos();Assert.AreEqual(0,DatosDocumentalesAocrReglas.Validar(datos,Estaciones(),true).Count);
            datos.Estaciones[1].Codigo="AUTORIZADO";Assert.IsTrue(DatosDocumentalesAocrReglas.Validar(datos,Estaciones(),true).Any(e=>e.Contains("Código")));
            datos.Estaciones[1].Codigo=null;Assert.IsTrue(DatosDocumentalesAocrReglas.Validar(datos,Estaciones(),true).Any(e=>e.Contains("GYE")));
            datos.Estaciones[1].EstacionId=999;Assert.IsTrue(DatosDocumentalesAocrReglas.Validar(datos,Estaciones(),true).Contains("Estación ajena al trámite"));
            datos=Datos();datos.PermisoCnacVencimiento=datos.PermisoCnacEmision.Value.AddDays(-1);Assert.IsTrue(DatosDocumentalesAocrReglas.Validar(datos,Estaciones(),true).Any(e=>e.Contains("vencimiento CNAC")));
            datos=Datos();datos.RepresentanteTecnico.Correo="no-correo";Assert.IsTrue(DatosDocumentalesAocrReglas.Validar(datos,Estaciones(),true).Any(e=>e.Contains("correo inválido")));
        }

        [DataTestMethod]
        [DataRow(1)] [DataRow(2)] [DataRow(3)]
        public void PlantillasReales_RenderizanAmbosDocumentosConDatosIndependientes(int tipo)
        {
            var root=Ac09InformeTecnicoTests.Root();var datos=Datos();
            var model=new AocrDocumentoPdfViewModel{NumeroAocr="DGAC-129-TST-001",AocOriginalNumero="AOC-BASE-PRUEBA",FechaEmisionDocumento=new DateTime(2026,10,26),FechaExpedicion=new DateTime(2026,10,26),FechaVencimiento=new DateTime(2031,7,8),
                Solicitud=new SolicitudAOCR{CodigoSolicitud=901,TipoSolicitud=tipo,Pais="QATAR",RazonSocial="EXPLOTADOR DE PRUEBA",TecnicoResponsableNombre="INSPECTOR_NO_ES_RT",Direccion="DIRECCION_ECUADOR_NO_ES_BASE"},
                RazonSocial="EXPLOTADOR DE PRUEBA",EstadoExplotador="QATAR",NombreDirectorGeneral="DIRECTOR GENERAL DE PRUEBA",NombreDirectorCertificacion="DIRECTOR DCAV DE PRUEBA",
                Aeronaves=new List<AeronaveSolicitud>{new AeronaveSolicitud{Modelo="B767",Matricula="HC-NOIMPRIMIR"},new AeronaveSolicitud{Modelo="B767",Matricula="HC-TAMPOCO"},new AeronaveSolicitud{Modelo="A320",Matricula="HC-NUNCA"}}};
            FirmaAocrWorkflowService.AplicarDatosDocumentales(model,datos,Estaciones());
            Assert.AreEqual("RTEAE",model.RepresentanteTecnico);Assert.AreEqual("LEGALECUADOR",model.PuntoContactoEcuador);
            Assert.AreEqual("Base EXPLOTADOR",model.DireccionExplotador);Assert.AreEqual(2,model.AeronavesCondiciones.Count);
            foreach(var vista in new[]{"AocrReconocimientoPdf","AocrCondicionesLimitacionesPdf"}) {
                var source=File.ReadAllText(Path.Combine(root,"CapaPresentacion/Views/CoordinacionJefatura/"+vista+".cshtml"));
                source=Regex.Replace(source,@"^@inherits [^\r\n]+","@inherits AOCR.Tests.Integration.Datos19Page");
                var host=new RazorEngineHost(new CSharpRazorCodeLanguage()){DefaultClassName="DatosRender",DefaultNamespace="Datos19Generated",GeneratedClassContext=new GeneratedClassContext("Execute","Write","WriteLiteral","WriteTo","WriteLiteralTo","HelperResult","DefineSection")};
                var generado=new RazorTemplateEngine(host).GenerateCode(new StringReader(source));Assert.IsTrue(generado.Success,string.Join("; ",generado.ParserErrors));
                using(var provider=new CSharpCodeProvider()) {
                    var p=new CompilerParameters{GenerateInMemory=true};p.ReferencedAssemblies.AddRange(new[]{"System.dll","System.Core.dll","System.Web.dll",typeof(System.Web.Mvc.Controller).Assembly.Location,typeof(Datos19Page).Assembly.Location,typeof(AocrDocumentoPdfViewModel).Assembly.Location,typeof(SolicitudAOCR).Assembly.Location,typeof(DatosDocumentalesAocrReglas).Assembly.Location});
                    var compilado=provider.CompileAssemblyFromDom(p,generado.GeneratedCode);Assert.IsFalse(compilado.Errors.HasErrors,string.Join("; ",compilado.Errors.Cast<CompilerError>()));
                    var pagina=(Datos19Page)Activator.CreateInstance(compilado.CompiledAssembly.GetType("Datos19Generated.DatosRender"));pagina.Model=model;pagina.Execute();var body=pagina.Output.ToString();pagina.Output.Clear();pagina.Sections["PdfHead"]();
                    Assert.IsFalse(body.Contains("INSPECTOR_NO_ES_RT"));Assert.IsFalse(body.Contains("DIRECCION_ECUADOR_NO_ES_BASE"));Assert.IsFalse(body.Contains("HC-NOIMPRIMIR"));Assert.IsFalse(body.Contains(">Autorizado<"));
                    Assert.AreEqual(1,Regex.Matches(HttpUtility.HtmlDecode(body),Regex.Escape("[X]")).Count,"Se marca exactamente un tipo de trámite.");
                    StringAssert.Contains(body,"RTEAE");
                    if(vista=="AocrReconocimientoPdf") {StringAssert.Contains(body,"CNAC-PRUEBA-026");StringAssert.Contains(body,"02/01/2026");StringAssert.Contains(body,"04/03/2028");Assert.IsTrue(Regex.Matches(body,"06/05/2029").Count>=2);StringAssert.Contains(body,"LEGALECUADOR");StringAssert.Contains(body,"GERENTE");}
                    else {StringAssert.Contains(body,"B767");StringAssert.Contains(body,"A320");StringAssert.Contains(body,">R<");StringAssert.Contains(body,">ET<");}
                    var layout=File.ReadAllText(Path.Combine(root,"CapaPresentacion/Views/Shared/_PdfLayoutDGACAocr.cshtml"));layout=Regex.Replace(layout,@"\A@\{.*?\}","",RegexOptions.Singleline).Replace("@RenderSection(\"PdfHead\", required: false)",pagina.Output.ToString()).Replace("@RenderBody()",body);
                    var output=Path.Combine(root,"output/pdf/observaciones-19");Directory.CreateDirectory(output);var nombre=vista+"_"+tipo;var html=Path.Combine(output,nombre+".html");var pdf=Path.Combine(output,nombre+".pdf");File.WriteAllText(html,layout,Encoding.UTF8);
                    using(var proc=Process.Start(new ProcessStartInfo(Path.Combine(root,"CapaPresentacion/Rotativa/wkhtmltopdf.exe"),"--encoding utf-8 --enable-local-file-access --disable-smart-shrinking --page-size A4 --margin-top 0 --margin-bottom 0 --margin-left 0 --margin-right 0 \""+html+"\" \""+pdf+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true})) {var error=proc.StandardError.ReadToEnd();Assert.IsTrue(proc.WaitForExit(30000));Assert.AreEqual(0,proc.ExitCode,error);}
                    Assert.IsTrue(new FileInfo(pdf).Length>1000);
                }
            }
        }
    }
}
