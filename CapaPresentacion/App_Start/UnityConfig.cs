using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Unity;
using Unity.Lifetime;
using Unity.AspNet.Mvc;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;
using CapaDatos.DAOs;
using CapaDatos.Interfaces;
using CapaDatos.Services;

namespace CapaPresentacion
{
    public static class UnityConfig
    {
        public static void RegisterComponents()
        {
            var container = new UnityContainer();

            // Registrar DAOs
            container.RegisterType<OrdenRecaudacionDAO>(new HierarchicalLifetimeManager());
            container.RegisterType<ConceptoDAO>(new HierarchicalLifetimeManager());
            container.RegisterType<SolicitudAOCRDAO>(new HierarchicalLifetimeManager());
            container.RegisterType<BancoP9DAO>(new HierarchicalLifetimeManager());
            container.RegisterType<PagoDAO>(new HierarchicalLifetimeManager());
            container.RegisterType<ParametroDAO>(new HierarchicalLifetimeManager());

            // Registrar servicios
            container.RegisterType<CapaDatos.Services.IEmailService, CapaDatos.Services.EmailService>(new HierarchicalLifetimeManager());
            container.RegisterType<CapaDatos.Services.ISecureConfigurationService, CapaDatos.Services.SecureConfigurationService>(new HierarchicalLifetimeManager());
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
                new HierarchicalLifetimeManager());

            // Registrar repositorio de pagos
            container.RegisterType<IPagoRepository, PagoDAO>(
                new HierarchicalLifetimeManager());

            // Registrar orquestador con dependencias mínimas (pdf/file opcionales)
            container.RegisterFactory<IOrdenRecaudacionOrchestrator>(c =>
                new OrdenRecaudacionOrchestrator(
                    c.Resolve<IOrdenRecaudacionRepository>(),
                    c.Resolve<IPagoRepository>(),
                    null,
                    null,
                    c.Resolve<CapaDatos.Services.IEmailService>(),
                    null
                ));

            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
        }
    }
}
