using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using CapaDatos.DAOs;
using CapaModelo;
using CapaPresentacion;
using CapaPresentacion.Controllers;
using CapaPresentacion.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AOCR.Tests.Integration
{
    [TestClass]
    public class Ac09InformeTecnicoTests
    {
        private static readonly BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
        private static InspeccionController Controller() =>
            (InspeccionController)FormatterServices.GetUninitializedObject(typeof(InspeccionController));

        internal static string Root()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AOCR.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repositorio no encontrado");
        }

        private static InspeccionInformeTecnico Mapear(NameValueCollection form, InspeccionInformeTecnico actual) =>
            (InspeccionInformeTecnico)typeof(InspeccionController).GetMethod("ConstruirInformeTecnicoDesdeFormulario", Privado)
                .Invoke(Controller(), new object[] { 940, form, actual, false });

        private static InspeccionInformeTecnico Completo() => new InspeccionInformeTecnico
        {
            CodigoInspeccion = 940, Antecedentes = "Antecedentes", Resumen = "Objetivo",
            BaseLegal = "Referencia técnica aportada por el inspector", Desarrollo = "Desarrollo",
            NoConformidades = "No se identificaron hallazgos adversos.", Conclusiones = "Conclusiones",
            Recomendaciones = "Recomendaciones", FechasInspeccionManual = "07/09/2026", Resultado = "SATISFACTORIO"
        };

        private static bool Valido(InspeccionInformeTecnico informe)
        {
            object[] args = { informe, null };
            return (bool)typeof(InspeccionController).GetMethod("ValidarInformeTecnicoParaFinalizar", Privado).Invoke(Controller(), args);
        }

        [TestMethod]
        public void PostManipulado_ConservaCamposRetiradosYHallazgos()
        {
            var actual = Completo();
            actual.Alcance = "Alcance histórico";
            actual.Observaciones = "Observación histórica";
            actual.EstacionesInspeccionManual = "Texto antiguo";
            var mapped = Mapear(new NameValueCollection {
                { "alcance", "BORRAR" }, { "observaciones", "BORRAR" },
                { "estacionesInspeccionManual", "ESTACION AJENA" }, { "resultado", "SATISFACTORIO" },
                { "baseLegal", "Nueva referencia" } }, actual);
            Assert.AreEqual(actual.Alcance, mapped.Alcance);
            Assert.AreEqual(actual.Observaciones, mapped.Observaciones);
            Assert.AreEqual(actual.EstacionesInspeccionManual, mapped.EstacionesInspeccionManual);
            Assert.AreEqual(actual.NoConformidades, mapped.NoConformidades);
            Assert.AreEqual("Nueva referencia", mapped.BaseLegal);
        }

        [TestMethod]
        public void CambioResultado_NoBorraNarrativas()
        {
            var actual = Completo(); actual.Observaciones = "Historia";
            var mapped = Mapear(new NameValueCollection { { "resultado", "INSATISFACTORIO" }, { "tipoResultadoInsatisfactorio", "SIN_INSPECCION" } }, actual);
            Assert.AreEqual("Historia", mapped.Observaciones);
            Assert.AreEqual(actual.NoConformidades, mapped.NoConformidades);
            Assert.IsTrue(Valido(mapped));
        }

        [TestMethod]
        public void PreviewSoloLectura_IgnoraPostYNoMutaInformeFirmado()
        {
            var actual = Completo(); actual.FirmadoInspector = true; actual.Finalizado = true;
            actual.EstadoInforme = "FIRMADO_INSPECTOR";
            var preview = (InspeccionInformeTecnico)typeof(InspeccionController).GetMethod("ConstruirContenidoPreviewInforme", Privado)
                .Invoke(Controller(), new object[] { 940, new NameValueCollection { { "desarrollo", "Manipulado" } }, actual, false });
            Assert.AreEqual(actual.Desarrollo, preview.Desarrollo);
            Assert.IsTrue(preview.FirmadoInspector);
            preview.EstadoInforme = "EN_PREVISUALIZACION";
            Assert.AreEqual("FIRMADO_INSPECTOR", actual.EstadoInforme);
            Assert.IsTrue(actual.Finalizado);
        }

        [TestMethod]
        public void Finalizar_NoExigeCamposRetirados_PeroSiBaseLegalYHallazgos()
        {
            var informe = Completo();
            Assert.IsTrue(Valido(informe));
            informe.BaseLegal = " "; Assert.IsFalse(Valido(informe));
            informe.BaseLegal = "Referencia"; informe.NoConformidades = " "; Assert.IsFalse(Valido(informe));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("MANIPULADO")]
        [DataRow("OBSERVADO")]
        [DataRow("NO_APLICA")]
        public void Finalizar_RechazaResultadoFueraDelCatalogo(string resultado)
        {
            var informe = Completo(); informe.Resultado = resultado;
            Assert.IsFalse(Valido(informe));
        }

        [TestMethod]
        public void TextoHistorico_SePresentaSinDuplicarloNiMutarlo()
        {
            Assert.AreEqual("Histórico" + Environment.NewLine + Environment.NewLine + "Nuevo", InformeTecnicoTemplateHelper.ConsolidarTexto("Histórico", "Nuevo"));
            Assert.AreEqual("Histórico y nuevo", InformeTecnicoTemplateHelper.ConsolidarTexto("Histórico", "Histórico y nuevo"));
            Assert.AreEqual("Histórico", InformeTecnicoTemplateHelper.ConsolidarTexto("Histórico", null));
        }

        private sealed class Request : HttpRequestBase
        {
            private readonly string _path;
            public Request(string path) { _path = path; }
            public override string AppRelativeCurrentExecutionFilePath => "~/" + _path;
            public override string PathInfo => "";
            public override string HttpMethod => "GET";
        }
        private sealed class Context : HttpContextBase
        {
            private readonly HttpRequestBase _request;
            private readonly IPrincipal _user;
            public Context(string path, string role = "Coordinador") { _request = new Request(path); _user = new GenericPrincipal(new GenericIdentity("prueba"), new[] { role }); }
            public override HttpRequestBase Request => _request;
            public override IPrincipal User { get => _user; set => throw new NotSupportedException(); }
        }

        [TestMethod]
        public void AliasModal_EnlazaParametroReal_YRutasPreviewNoSon404()
        {
            var routes = new RouteCollection(); RouteConfig.RegisterRoutes(routes);
            var modal = routes.GetRouteData(new Context("InformeTecnico/ModalInformeTecnico/940"));
            Assert.AreEqual("Inspeccion", modal.Values["controller"]);
            Assert.AreEqual("940", modal.Values["codigoInspeccion"]);
            foreach (var action in new[] { "PrevisualizarInformeTecnico", "VerPreviewInformeTecnico", "GuardarInformeTecnico", "DescargarInformeTecnicoPdf" })
            {
                var route = routes.GetRouteData(new Context("InformeTecnico/" + action));
                Assert.AreEqual("Inspeccion", route.Values["controller"]);
                Assert.AreEqual(action, route.Values["action"]);
            }
        }

        [TestMethod]
        public void Preview_CoordinadorPuedeConsultar_PeroNoEditarNiSolicitantePrevisualizar()
        {
            var controller = Controller();
            controller.ControllerContext = new ControllerContext(new Context(""), new RouteData(), controller);
            var inspeccion = new Inspeccion { CodigoInspeccion = 940 };
            Assert.IsTrue((bool)typeof(InspeccionController).GetMethod("PuedeAccederInspeccion", Privado).Invoke(controller, new object[] { inspeccion }));
            Assert.IsFalse((bool)typeof(InspeccionController).GetMethod("PuedeEditarInformeTecnicoModal", Privado).Invoke(controller, new object[] { inspeccion }));
            foreach (var action in new[] { "PrevisualizarInformeTecnico", "VerPreviewInformeTecnico" })
            {
                var roles = typeof(InspeccionController).GetMethod(action).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().SelectMany(x => x.Roles.Split(',')).ToArray();
                CollectionAssert.Contains(roles, "Coordinador");
                CollectionAssert.DoesNotContain(roles, "Solicitante");
            }
            Assert.IsNotNull(typeof(InspeccionController).GetMethod("PrevisualizarInformeTecnico").GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }

        [DataTestMethod]
        [DataRow("../InformeTecnico_Preview_1_2_20260907_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        [DataRow("InformeTecnico_Preview_1_2_20260907_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf")]
        [DataRow("InformeTecnico_Preview_1_2_20260907_adivinado")]
        public void Preview_RechazaTokenInvalidoAntesDeAccederADatos(string token)
        {
            var result = (HttpStatusCodeResult)Controller().VerPreviewInformeTecnico(token);
            Assert.AreEqual(400, result.StatusCode);
        }

        [TestMethod]
        public void RutaArchivo_CodificaEspaciosAcentosYCaracteresEspeciales()
        {
            var path = Path.Combine(Path.GetTempPath(), "revisión # &", "pie de página.html");
            var uri = (string)typeof(InspeccionController).GetMethod("ConvertirRutaFisicaAUrlArchivo", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { path });
            Assert.AreEqual(path, new Uri(uri).LocalPath);
            StringAssert.Contains(uri, "%23");
            StringAssert.Contains(uri, "%20");
        }

        [TestMethod]
        public void PostgreSql_GuardarRecargar_NoPierdeTextoHistoricoYBaseLegal()
        {
            var cs = Environment.GetEnvironmentVariable("AOCR_AC07_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(cs)) Assert.Inconclusive("Requiere scripts/test_ac07.py y base desechable.");
            Assert.IsTrue(new NpgsqlConnectionStringBuilder(cs).Database.StartsWith("aocr_ac07_test_", StringComparison.Ordinal));
            var dao = new InspeccionInformeDAO(cs);
            var informe = Completo(); informe.Alcance = "Histórico de alcance"; informe.Observaciones = "Histórico de observaciones";
            var guardado = dao.GuardarBorrador(informe, 900);
            var recargado = new InspeccionInformeDAO(cs).ObtenerPorId(guardado.CodigoInforme);
            var editado = Mapear(new NameValueCollection { { "desarrollo", "Desarrollo actualizado" }, { "baseLegal", "Referencia actualizada" }, { "observaciones", "borrar" } }, recargado);
            dao.GuardarBorrador(editado, 900);
            var final = new InspeccionInformeDAO(cs).ObtenerUltimoPorInspeccion(940);
            Assert.AreEqual("Desarrollo actualizado", final.Desarrollo);
            Assert.AreEqual("Referencia actualizada", final.BaseLegal);
            Assert.AreEqual(informe.Alcance, final.Alcance);
            Assert.AreEqual(informe.Observaciones, final.Observaciones);
            Assert.AreEqual(informe.NoConformidades, final.NoConformidades);
        }
    }
}
