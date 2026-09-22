using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Razor;
using System.Web.Razor.Generator;
using CapaModelo;
using CapaPresentacion.Models.ViewModels;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
    public abstract class LugaresPdfPage
    {
        public SolicitudInspeccionPdfViewModel Model { get; set; }
        public string Layout { get; set; }
        public StringBuilder Output = new StringBuilder();
        public Dictionary<string, Action> Sections = new Dictionary<string, Action>();
        public void WriteLiteral(object value) => Output.Append(value);
        public void Write(object value) => Output.Append(HttpUtility.HtmlEncode(value));
        public void DefineSection(string name, Action action) => Sections[name] = action;
        public abstract void Execute();
    }

    [TestClass]
    public class LugaresFechasPdfTests
    {
        [TestMethod]
        public void PlantillaReal_ConservaCadaLugarConSuFechaYEscapaLocalidad()
        {
            var root = Ac09InformeTecnicoTests.Root();
            var source = File.ReadAllText(Path.Combine(root, "CapaPresentacion/Views/OrdenRecaudacion/SolicitudInspeccionesPdf.cshtml"));
            source = Regex.Replace(source, @"^@inherits [^\r\n]+", "@inherits AOCR.Tests.Integration.LugaresPdfPage");
            var host = new RazorEngineHost(new CSharpRazorCodeLanguage())
            {
                DefaultClassName = "LugaresRender", DefaultNamespace = "Generated",
                GeneratedClassContext = new GeneratedClassContext("Execute", "Write", "WriteLiteral", "WriteTo", "WriteLiteralTo", "HelperResult", "DefineSection")
            };
            var generated = new RazorTemplateEngine(host).GenerateCode(new StringReader(source));
            Assert.IsTrue(generated.Success, string.Join("; ", generated.ParserErrors));
            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters { GenerateInMemory = true };
                parameters.ReferencedAssemblies.AddRange(new[] { "System.dll", "System.Core.dll", "System.Web.dll",
                    typeof(System.Web.Mvc.Controller).Assembly.Location, typeof(LugaresPdfPage).Assembly.Location,
                    typeof(SolicitudInspeccionPdfViewModel).Assembly.Location, typeof(SolicitudEstacionInspeccion).Assembly.Location });
                var result = provider.CompileAssemblyFromDom(parameters, generated.GeneratedCode);
                Assert.IsFalse(result.Errors.HasErrors, string.Join("; ", result.Errors.Cast<CompilerError>()));
                var page = (LugaresPdfPage)Activator.CreateInstance(result.CompiledAssembly.GetType("Generated.LugaresRender"));
                page.Model = new SolicitudInspeccionPdfViewModel
                {
                    NombreRT = "Representante de prueba", NombreCompania = "Compania de prueba", NumeroOrden = "PRUEBA",
                    LugarEmision = "Quito", FechaSolicitud = new DateTime(2026, 9, 22), AeropuertosSolicitados = "Quito, Guayaquil",
                    Estaciones = new List<SolicitudEstacionInspeccion>
                    {
                        new SolicitudEstacionInspeccion { EstacionCodigo = "UIO", EstacionNombre = "Quito (UIO)", FechaInicio = new DateTime(2026,9,22), FechaFin = new DateTime(2026,9,22) },
                        new SolicitudEstacionInspeccion { EstacionCodigo = "GYE", EstacionNombre = "Guayaquil (GYE)", FechaInicio = new DateTime(2026,9,26), FechaFin = new DateTime(2026,9,29) },
                        new SolicitudEstacionInspeccion { EstacionCodigo = "OTROS", EstacionNombre = "Localidad <script>prueba</script>", FechaInicio = new DateTime(2026,9,28), FechaFin = new DateTime(2026,9,28) }
                    }
                };
                page.Execute();
                var body = page.Output.ToString();
                StringAssert.Contains(body, "<td>Quito (UIO)</td><td>22/09/2026</td>");
                StringAssert.Contains(body, "<td>Guayaquil (GYE)</td><td>26/09/2026 al 29/09/2026</td>");
                Assert.IsFalse(body.Contains("<script>"));
                page.Output.Clear(); page.Sections["PdfHead"]();
                var output = Path.Combine(root, "TestResults", "lugares-fechas");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "solicitud.html"), "<!doctype html><html><head><meta charset='utf-8'>" + page.Output + "</head><body><div id='pdf-content'>" + body + "</div></body></html>", Encoding.UTF8);
                page.Model.Estaciones.Clear(); page.Model.FechasInspeccion = "Fecha historica conservada";
                page.Output.Clear(); page.Execute();
                StringAssert.Contains(page.Output.ToString(), "Fecha historica conservada");
            }
        }
    }
}
