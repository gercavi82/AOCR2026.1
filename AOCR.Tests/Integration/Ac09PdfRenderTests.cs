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
using CapaPresentacion.Helpers;
using CapaPresentacion.Models.ViewModels;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Integration
{
    // Ejecuta la plantilla Razor y el layout reales, sin arrancar la aplicación ni tocar su BD.
    public abstract class Ac09PdfPage
    {
        public InformeTecnicoPdfViewModel Model { get; set; }
        public string Layout { get; set; }
        public StringBuilder Output = new StringBuilder();
        public Dictionary<string, Action> Sections = new Dictionary<string, Action>();
        public void WriteLiteral(object value) => Output.Append(value);
        public void Write(object value) => Output.Append(HttpUtility.HtmlEncode(value));
        public void DefineSection(string name, Action action) => Sections[name] = action;
        public abstract void Execute();
    }

    [TestClass]
    public class Ac09PdfRenderTests
    {
        [TestMethod]
        public void PlantillaReal_GeneraPdfPreliminarYDefinitivo_ConHistoricoYTextoLargo()
        {
            var root = Ac09InformeTecnicoTests.Root();
            var source = File.ReadAllText(Path.Combine(root, "CapaPresentacion/Views/Inspeccion/InformeTecnicoPdf.cshtml"));
            source = Regex.Replace(source, @"^@inherits [^\r\n]+", "@inherits AOCR.Tests.Integration.Ac09PdfPage");
            var host = new RazorEngineHost(new CSharpRazorCodeLanguage())
            {
                DefaultClassName = "InformeRender", DefaultNamespace = "Ac09Generated",
                GeneratedClassContext = new GeneratedClassContext("Execute", "Write", "WriteLiteral", "WriteTo", "WriteLiteralTo", "HelperResult", "DefineSection")
            };
            var generated = new RazorTemplateEngine(host).GenerateCode(new StringReader(source));
            Assert.IsTrue(generated.Success, string.Join("; ", generated.ParserErrors));
            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters { GenerateInMemory = true };
                parameters.ReferencedAssemblies.AddRange(new[] { "System.dll", "System.Core.dll", "System.Web.dll",
                    typeof(System.Web.Mvc.Controller).Assembly.Location, typeof(Ac09PdfPage).Assembly.Location,
                    typeof(InformeTecnicoPdfViewModel).Assembly.Location, typeof(Inspeccion).Assembly.Location });
                var result = provider.CompileAssemblyFromDom(parameters, generated.GeneratedCode);
                Assert.IsFalse(result.Errors.HasErrors, string.Join("; ", result.Errors.Cast<CompilerError>()));
                var output = Path.Combine(root, "TestResults", "ac09 revisión # &");
                Directory.CreateDirectory(output);
                foreach (var preview in new[] { true, false })
                {
                    var page = (Ac09PdfPage)Activator.CreateInstance(result.CompiledAssembly.GetType("Ac09Generated.InformeRender"));
                    page.Model = new InformeTecnicoPdfViewModel
                    {
                        EsVistaPrevia = preview, MostrarMarcaAguaBorrador = preview, MostrarFirmas = true,
                        Inspeccion = new Inspeccion { CodigoInspeccion = 940, CodigoSolicitud = 700, Lugar = "Base canónica Quito", Tipo = "Renovación", InspectorPrincipalNombre = "Inspector de prueba", FechaProgramada = new DateTime(2026,9,7) },
                        Solicitud = new SolicitudAOCR { NombreOperador = "Operador de prueba", RazonSocial = "Operador canónico de prueba", NumeroSolicitud = "AOCR700-2026" },
                        Informe = new InspeccionInformeTecnico
                        {
                            Antecedentes = "Antecedentes de prueba: áéíóú, ñ, <script>alert(1)</script>.", Resumen = "Objetivo de prueba",
                            BaseLegal = "Referencia técnica de prueba aportada por el inspector.",
                            Alcance = "Alcance histórico conservado", Observaciones = "Observación histórica conservada",
                            Desarrollo = string.Join("\n\n", Enumerable.Range(1,12).Select(i => "Verificación " + i + ": se contrastaron los registros y las evidencias disponibles para documentar el proceso de inspección. Las comprobaciones y sus resultados se conservan en el expediente para su revisión posterior.")),
                            NoConformidades = "Hallazgo de prueba visible también con resultado satisfactorio.", Conclusiones = "Conclusión de prueba", Recomendaciones = "Recomendación de prueba",
                            EstacionesInspeccionManual = "NO IMPRIMIR COBERTURA HISTORICA", Resultado = "SATISFACTORIO"
                        }
                    };
                    page.Execute(); var body = page.Output.ToString(); page.Output.Clear(); page.Sections["PdfHead"]();
                    var layout = File.ReadAllText(Path.Combine(root, "CapaPresentacion/Views/Shared/_PdfLayoutDGACAocr.cshtml"));
                    layout = Regex.Replace(layout, @"\A@\{.*?\}", "", RegexOptions.Singleline)
                        .Replace("@RenderSection(\"PdfHead\", required: false)", page.Output.ToString()).Replace("@RenderBody()", body);
                    foreach (var label in new[] { "1. ANTECEDENTES", "2. OBJETIVO", "3. BASE LEGAL", "4. DESARROLLO DEL PROCESO", "5. HALLAZGOS", "6. CONCLUSIONES", "7. RECOMENDACIONES" }) StringAssert.Contains(layout, label);
                    StringAssert.Contains(HttpUtility.HtmlDecode(layout), "Alcance histórico conservado");
                    StringAssert.Contains(HttpUtility.HtmlDecode(layout), "Observación histórica conservada");
                    StringAssert.Contains(HttpUtility.HtmlDecode(layout), "Base canónica Quito");
                    Assert.IsFalse(layout.Contains("NO IMPRIMIR COBERTURA HISTORICA"));
                    Assert.IsFalse(layout.Contains("<script>alert(1)</script>"));
                    Assert.AreEqual(preview, layout.Contains("BORRADOR / VISTA PREVIA"));
                    var name = preview ? "preview" : "definitivo";
                    var html = Path.Combine(output, name + ".html"); var pdf = Path.Combine(output, name + ".pdf");
                    File.WriteAllText(html, layout, Encoding.UTF8);
                    var header = Path.Combine(output, "encabezado á #.html");
                    var footer = Path.Combine(output, "pie ñ &.html");
                    File.WriteAllText(header, "<html><head><meta charset='utf-8'></head><body style='font:12px Arial'>INSPECCIÓN — Documento de prueba</body></html>", Encoding.UTF8);
                    File.WriteAllText(footer, "<html><head><meta charset='utf-8'></head><body style='font:12px Arial'>Revisión técnica — Prueba AC-09</body></html>", Encoding.UTF8);
                    var start = new ProcessStartInfo(Path.Combine(root, "CapaPresentacion/Rotativa/wkhtmltopdf.exe"),
                        "--encoding utf-8 --enable-local-file-access --disable-smart-shrinking --page-size A4 --margin-top 30mm --margin-bottom 26mm --margin-left 8mm --margin-right 8mm --header-spacing 0 --footer-spacing 0 --header-html \"" + new Uri(header).AbsoluteUri + "\" --footer-html \"" + new Uri(footer).AbsoluteUri + "\" \"" + html + "\" \"" + pdf + "\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                    using (var process = Process.Start(start))
                    {
                        var error = process.StandardError.ReadToEnd();
                        Assert.IsTrue(process.WaitForExit(30000), "wkhtmltopdf excedió el tiempo esperado.");
                        Assert.AreEqual(0, process.ExitCode, error);
                    }
                    Assert.IsTrue(new FileInfo(pdf).Length > 1000);
                }
            }
        }
    }
}
