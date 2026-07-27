using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AOCR.Tests.Unit
{
    [TestClass]
    public class PermisosRolManagementTests
    {
        [TestMethod]
        public void PermisosRol_ExponeCargaRealYGuardadoProtegido()
        {
            var controller = Leer("CapaPresentacion\\Controllers\\AdminUsuariosController.cs");

            StringAssert.Contains(controller, "[Authorize(Roles = \"Administrador\")]");
            StringAssert.Contains(controller, "public JsonResult ObtenerPermisosPorRol(int codigoRol)");
            StringAssert.Contains(controller, "public JsonResult GuardarPermisosRolDiferencial(");
            StringAssert.Contains(controller, "[ValidateAntiForgeryToken]");
            StringAssert.Contains(controller, "Response.StatusCode = 400;");
            StringAssert.Contains(controller, "Response.StatusCode = 401;");
            StringAssert.Contains(controller, "Response.StatusCode = 409;");
            StringAssert.Contains(controller, "Response.StatusCode = 500;");
        }

        [TestMethod]
        public void GuardadoPermisos_EsTransaccionalIdempotenteYConcurrente()
        {
            var dao = Leer("CapaDatos\\DAOs\\AdminUsuariosDAO.cs");

            StringAssert.Contains(dao, "using (var tx = cn.BeginTransaction())");
            StringAssert.Contains(dao, "ON CONFLICT (codigorol, id_permiso)");
            StringAssert.Contains(dao, "versionEsperada");
            StringAssert.Contains(dao, "conflictoVersion = true;");
            StringAssert.Contains(dao, "ADM_ROLES_PERMISOS");
            StringAssert.Contains(dao, "Anteriores = permisosAnteriores");
            StringAssert.Contains(dao, "Nuevos = permisosNuevos");
            StringAssert.Contains(dao, "tx.Commit();");
        }

        [TestMethod]
        public void VistaPermisos_ConservaAntiforgeryDiferencialYControlDeCambios()
        {
            var view = Leer("CapaPresentacion\\Views\\AdminUsuarios\\PermisosRol.cshtml");

            StringAssert.Contains(view, "@Html.AntiForgeryToken()");
            StringAssert.Contains(view, "versionEsperada: $('#rp-version-esperada').val()");
            StringAssert.Contains(view, "agregados: agregados");
            StringAssert.Contains(view, "retirados: retirados");
            StringAssert.Contains(view, "beforeunload.rpPermissions");
            StringAssert.Contains(view, "rpToggleAllModule");
            StringAssert.Contains(view, "xhr.status === 409");
        }

        [TestMethod]
        public void Sidebar_UsaLaMismaFuenteDePermisoAdministrativo()
        {
            var sidebar = Leer("CapaPresentacion\\Helpers\\SidebarMenuBuilder.cs");

            StringAssert.Contains(sidebar, "PuedeGestionarRolesPermisos");
            StringAssert.Contains(sidebar, "SeguridadBL.UsuarioTienePermiso(");
            StringAssert.Contains(sidebar, "\"ADM_ROLES_PERMISOS\"");
        }

        private static string Leer(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", ".."));
            var path = Path.Combine(root, relativePath);
            if (!File.Exists(path))
            {
                root = Directory.GetCurrentDirectory();
                while (root != null && !File.Exists(Path.Combine(root, "AOCR.sln")))
                {
                    root = Directory.GetParent(root)?.FullName;
                }
                path = Path.Combine(root ?? string.Empty, relativePath);
            }
            return File.ReadAllText(path);
        }
    }
}
