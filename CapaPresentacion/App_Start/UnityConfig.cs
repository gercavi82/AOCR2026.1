using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Unity;
using Unity.Lifetime;
using Unity.AspNet.Mvc;
using Unity.Injection;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;
using CapaDatos.DAOs;
using CapaDatos.Interfaces;
using CapaDatos.Services;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Services;

namespace CapaPresentacion
{
    public static class UnityConfig
    {
        public static void RegisterComponents()
        {
            var container = new UnityContainer();

            // Registrar DAOs (force parameterless ctor to avoid ambiguity)
            container.RegisterType<OrdenRecaudacionDAO>(new HierarchicalLifetimeManager(), new InjectionConstructor());
            container.RegisterType<ConceptoDAO>(new HierarchicalLifetimeManager());
            container.RegisterType<SolicitudAOCRDAO>(new HierarchicalLifetimeManager());
            container.RegisterType<PagoDAO>(new HierarchicalLifetimeManager(), new InjectionConstructor());
            container.RegisterType<ParametroDAO>(new HierarchicalLifetimeManager());

            // Registrar servicios
            container.RegisterType<CapaDatos.Services.SecureConfigurationService>(new HierarchicalLifetimeManager());
            container.RegisterType<CapaDatos.Services.ISecureConfigurationService, CapaDatos.Services.SecureConfigurationService>(new HierarchicalLifetimeManager());
            container.RegisterType<IUserContextAccessor, UserContextAccessor>(new HierarchicalLifetimeManager());
            container.RegisterType<IUsuarioContextoService, UsuarioContextoService>(new PerRequestLifetimeManager());
            container.RegisterFactory<BancoP9DAO>(c =>
            {
                var cfg = c.Resolve<CapaDatos.Services.ISecureConfigurationService>();
                return new BancoP9DAO(cfg);
            }, new HierarchicalLifetimeManager());
            container.RegisterFactory<CapaDatos.Services.IEmailService>(c =>
            {
                try
                {
                    var cfg = c.Resolve<CapaDatos.Services.ISecureConfigurationService>();
                    return new CapaDatos.Services.EmailService(cfg);
                }
                catch
                {
                    return new CapaDatos.Services.NoOpEmailService();
                }
            }, new HierarchicalLifetimeManager());
            container.RegisterFactory<CapaDatos.Services.IAuditService>(c =>
            {
                var cfg = c.Resolve<CapaDatos.Services.ISecureConfigurationService>();
                var cs = cfg.GetConnectionString("PostgreSQL")
                         ?? cfg.GetConnectionString("AOCRConnection")
                         ?? string.Empty;
                return new CapaDatos.Services.AuditService(cs);
            }, new HierarchicalLifetimeManager());

            // Registrar repositorio de órdenes
            container.RegisterType<IOrdenRecaudacionRepository, OrdenRecaudacionDAO>(
                new HierarchicalLifetimeManager(), new InjectionConstructor());

            // Registrar repositorio de pagos
            container.RegisterType<IPagoRepository, PagoDAO>(
                new HierarchicalLifetimeManager(), new InjectionConstructor());

            // Registrar orquestador con dependencias mínimas (pdf/file opcionales)
            container.RegisterFactory<IOrdenRecaudacionOrchestrator>(c =>
                new OrdenRecaudacionOrchestrator(
                    c.Resolve<IOrdenRecaudacionRepository>(),
                    c.Resolve<IPagoRepository>(),
                    null,
                    null,
                    c.Resolve<CapaDatos.Services.IEmailService>(),
                    null
                ), new HierarchicalLifetimeManager());

            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
        }
    }
}
