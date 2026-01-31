using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CapaNegocio.Interfaces;
using CapaNegocio.Services;

namespace CapaPresentacion
{
    public static class UnityConfig
    {
        public static void RegisterTypes(IUnityContainer container)
        {
            // ...existing code (registros existentes)...

            // Registrar orquestador
            container.RegisterType<IOrdenRecaudacionOrchestrator, OrdenRecaudacionOrchestrator>(
                new HierarchicalLifetimeManager());

            // Registrar repositorio de pagos
            container.RegisterType<IPagoRepository, PagoRepository>(
                new HierarchicalLifetimeManager());

            // Registrar servicios de infraestructura
            container.RegisterType<IFileStorageService, LocalFileStorageService>(
                new HierarchicalLifetimeManager());

            // ...existing code...
        }
    }
}