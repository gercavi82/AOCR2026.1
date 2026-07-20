using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    public class Fr3OutboxWorkerService
    {
        private readonly Fr3OutboxWorkerDAO _workerDao;
        private readonly FacturacionAS400Service _as400Service;
        private readonly ILoggingService _logger;
        private readonly string _workerId;

        public Fr3OutboxWorkerService()
        {
            _workerDao = new Fr3OutboxWorkerDAO();
            _as400Service = new FacturacionAS400Service();
            _logger = LoggingServiceFactory.Create();
            _workerId = Environment.MachineName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public string ProcesarPendientes(int limite = 10, int lockMinutes = 5)
        {
            if (!FacturacionAS400Service.IsEnabled())
            {
                return "AS400 integration is disabled globally.";
            }

            var config = new Fr3ConfigurationProvider().GetConfiguration();
            if (config.Mode != CapaModelo.Common.Fr3ProcessingMode.Outbox)
            {
                return "Worker ignorado: FR3_PROCESSING_MODE no es Outbox.";
            }

            int procesados = 0;
            int exitosos = 0;
            int fallidos = 0;

            try
            {
                _logger.LogInfo("Fr3OutboxWorkerService: Reclamando eventos con workerId=" + _workerId);
                var eventos = _workerDao.ReclamarEventos(limite, _workerId, lockMinutes);

                foreach (var ev in eventos)
                {
                    procesados++;
                    try
                    {
                        string adv;
                        // Simulamos enviar vacíos de totales si no lo tenemos en el outbox, 
                        // pero la lógica heredada lee los valores directamente de la DB si es necesario, 
                        // o FacturacionAS400Service se encarga de rehidratar la orden.
                        // La instruccion dice: "Ejecutar FacturacionAS400Service de forma idempotente."
                        
                        // Utilizamos el TryReintentarFr3 heredado pero modificado para que en contexto de worker 
                        // llame realmente al AS400 incluso si el mode es outbox. 
                        // Alternativa: Usar un metodo expuesto explicitamente para el worker.
                        // Wait, if TryReintentarFr3 delegates to Outbox when mode=Outbox, we'll get an infinite loop!
                        // Let's create TryRegistrarDesdeWorker en FacturacionAS400Service.
                        var ok = _as400Service.TryRegistrarDesdeWorker((int)ev.OrdenId, "WORKER_" + _workerId, out adv);

                        if (ok)
                        {
                            _workerDao.CompletarEvento((int)ev.Id, adv);
                            exitosos++;
                            _logger.LogInfo(string.Format("Outbox evento {0} para orden {1} procesado exitosamente.", ev.Id, ev.OrdenId));
                        }
                        else
                        {
                            var nuevoIntento = (int)ev.Intentos + 1;
                            bool definitivo = EsFalloDefinitivo(adv);
                            int backoff = definitivo ? 0 : CalcularBackoff(nuevoIntento);
                            
                            _workerDao.RegistrarFalloEvento((int)ev.Id, adv, nuevoIntento, backoff, definitivo);
                            fallidos++;
                            _logger.LogWarning(string.Format("Outbox evento {0} (orden {1}) falló. Intento: {2}. Definitivo: {3}. Adv: {4}", 
                                ev.Id, ev.OrdenId, nuevoIntento, definitivo, adv));
                        }
                    }
                    catch (Exception itemEx)
                    {
                        var nuevoIntento = (int)ev.Intentos + 1;
                        int backoff = CalcularBackoff(nuevoIntento);
                        _workerDao.RegistrarFalloEvento((int)ev.Id, itemEx.Message, nuevoIntento, backoff, false);
                        fallidos++;
                        _logger.LogError(itemEx, new LogContext { Action = "ProcesarEventoOutbox", AdditionalData = new Dictionary<string, object> { ["OutboxId"] = ev.Id } });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { Action = "Fr3OutboxWorkerService.ProcesarPendientes" });
                return "Error crítico ejecutando worker: " + ex.Message;
            }

            return string.Format("Worker completado. Procesados: {0}, Exitosos: {1}, Fallidos: {2}", procesados, exitosos, fallidos);
        }

        private bool EsFalloDefinitivo(string errorMsg)
        {
            if (string.IsNullOrWhiteSpace(errorMsg)) return false;
            var lower = errorMsg.ToLowerInvariant();
            
            // Reintentables comunes
            if (lower.Contains("timeout") || lower.Contains("sql7008") || lower.Contains("bloqueo") || lower.Contains("connection"))
            {
                return false;
            }
            // Errores logicos: datos invalidos, orden inexistente, null
            if (lower.Contains("invalida") || lower.Contains("no encontrad") || lower.Contains("null") || lower.Contains("requerido"))
            {
                return true;
            }
            
            // Asumimos reintentable por defecto para recuperar ante fallos de red
            return false;
        }

        private int CalcularBackoff(int intentos)
        {
            // Backoffs: 1, 5, 15, 60, 240
            switch (intentos)
            {
                case 1: return 1;
                case 2: return 5;
                case 3: return 15;
                case 4: return 60;
                default: return 240;
            }
        }
    }
}
