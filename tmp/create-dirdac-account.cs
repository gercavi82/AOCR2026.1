using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
class CreateDirdacAccount
{
    static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var path = Path.Combine(@"C:\proyectos\AOCR\CapaPresentacion\bin", new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message); return 1; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        const string login = "GEN_DIRDAC";
        const string email = "juan.franco@aviacioncivil.gob.ec";
        var dao = new CapaDatos.DAOs.AdminUsuariosDAO();
        if (dao.ExisteCodigoUsuario(login) || dao.ExisteCorreo(email))
            throw new InvalidOperationException("Ya existe el usuario o el correo; no se ha modificado ninguna cuenta.");
        var rol = dao.ObtenerRolesActivos().Single(r => string.Equals(r.Descripcion, "DIRDAC", StringComparison.OrdinalIgnoreCase));
        var temporal = CapaNegocio.Helpers.PasswordHelper.GenerarPasswordAleatoria(16);
        var hash = CapaNegocio.Helpers.PasswordHelper.HashPassword(temporal);
        var usuario = new CapaModelo.Seguridad.SeguridadUsuarioDTO {
            CodigoUsuario = login, NombreUsuario = "Juan", ApellidoUsuario = "Franco",
            Correo = email, Activo = true
        };
        var id = dao.CrearUsuarioConRoles(usuario, hash, new[] { rol.CodigoRol }, null, "CODEX", null);
        if (id <= 0) throw new InvalidOperationException("No se obtuvo el identificador del usuario.");
        var creado = CapaDatos.DAOs.UsuarioDAO.ObtenerPorId(id);
        if (creado == null || !creado.Activo || !creado.MustChangePassword
            || !CapaNegocio.Helpers.PasswordHelper.VerifyPassword(temporal, creado.Contrasena)
            || !dao.ObtenerRolesUsuario(id).SequenceEqual(new[] { rol.CodigoRol }))
            throw new InvalidOperationException("La cuenta fue creada, pero requiere revisar su verificacion antes de usarla. Id=" + id);
        Console.WriteLine("CREADO_Y_VERIFICADO Id=" + id + "; Usuario=" + login + "; Correo=" + email + "; Rol=DIRDAC; CambioObligatorio=True");
        Console.WriteLine("ClaveTemporal=" + temporal);
    }
}

