using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Unity;
using Unity.Lifetime;
using Unity.Mvc5;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;
using CapaDatos.DAOs;
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
            container.RegisterType<IEmailService, EmailService>(new HierarchicalLifetimeManager());
            container.RegisterType<IFileStorageService, LocalFileStorageService>(
                new HierarchicalLifetimeManager());

            // Registrar orquestador
            container.RegisterType<IOrdenRecaudacionOrchestrator, OrdenRecaudacionOrchestrator>(
                new HierarchicalLifetimeManager());

            // Registrar repositorio de pagos
            container.RegisterType<IPagoRepository, PagoRepository>(
                new HierarchicalLifetimeManager());

            DependencyResolver.SetResolver(new UnityDependencyResolver(container));
        }
    }
}