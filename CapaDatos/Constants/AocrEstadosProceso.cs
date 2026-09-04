namespace CapaDatos.Constants
{
    /// <summary>
    /// Catálogo de estados institucionales segregados para el flujo AOCR.
    /// Define claramente la secuencia:
    /// RT -> FINANCIERO -> COORDINADOR -> INSPECTOR -> COORDINADOR -> DIRCAV -> INSPECTOR -> COORDINADOR -> DIRCAV -> DIRDAC.
    /// </summary>
    public static class AocrEstadosProceso
    {
        // 1. Etapa Documental Inicial y Designación
        public const string PendienteCoordinador = "PENDIENTE_COORDINADOR";
        public const string PendienteDircav = "PENDIENTE_DIRCAV";
        public const string PendienteRevisionCoordinador = "PENDIENTE_REVISION_COORDINADOR";
        public const string DevueltoInspector = "DEVUELTO_INSPECTOR";
        public const string PendienteAceptacionDircav = "PENDIENTE_ACEPTACION_DIRCAV";
        public const string DevueltoCoordinadorPorDircav = "DEVUELTO_COORDINADOR_POR_DIRCAV";
        public const string DevueltoCoordinador = "DEVUELTO_COORDINADOR";
        public const string DocumentacionAceptadaDircav = "DOCUMENTACION_ACEPTADA_DIRCAV";
        public const string PendienteDesignacionDircav = "PENDIENTE_DESIGNACION_DIRCAV";
        public const string DesignacionPendienteFirmaDircav = "DESIGNACION_PENDIENTE_FIRMA_DIRCAV";
        public const string DesignacionFirmadaDircav = "DESIGNACION_FIRMADA_DIRCAV";

        // 2. Etapa de Inspección y LV
        public const string InspeccionEnEjecucion = "INSPECCION_EN_EJECUCION";
        public const string LvFirmadaInspector = "LV_FIRMADA_INSPECTOR";
        public const string PendienteInformeInspector = "PENDIENTE_INFORME_INSPECTOR";
        public const string InformeTecnicoFirmadoInspector = "INFORME_TECNICO_FIRMADO_INSPECTOR";
        public const string InformeTecnicoDevueltoInspector = "INFORME_TECNICO_DEVUELTO_INSPECTOR";

        // 3. Etapa de Revisión Final Documental
        public const string PendienteRevisionFinalCoordinador = "PENDIENTE_REVISION_FINAL_COORDINADOR";
        public const string PaqueteBorradorInspector = "PAQUETE_BORRADOR_INSPECTOR";
        public const string DevueltoInspectorFinal = "DEVUELTO_INSPECTOR_FINAL";
        public const string PendienteRevisionFinalDircav = "PENDIENTE_REVISION_FINAL_DIRCAV";
        public const string DevueltoCoordinadorFinal = "DEVUELTO_COORDINADOR_FINAL";
        public const string DevueltoCoordinadorFinalDircav = "DEVUELTO_COORDINADOR_FINAL_DIRCAV";

        // 4. Etapa de Condiciones y Limitaciones (Firma Exclusiva DIRCAV)
        public const string CondicionesBorradorInspector = "CONDICIONES_BORRADOR_INSPECTOR";
        public const string CondicionesListasParaFirma = "CONDICIONES_LISTAS_PARA_FIRMA";
        public const string ClPendienteDircav = "CL_PENDIENTE_DIRCAV";
        public const string ClPendienteFirmaDircav = "CL_PENDIENTE_FIRMA_DIRCAV";
        public const string ClFirmadaDircav = "CL_FIRMADA_DIRCAV";
        public const string PendienteFirmaCondicionesDcav = "PENDIENTE_FIRMA_CONDICIONES_DCAV";
        public const string CondicionesFirmadasDcav = "CONDICIONES_FIRMADAS_DCAV";

        // 5. Etapa AOCR y Legalización (Firma Exclusiva DIRDAC)
        public const string AocrBorradorInspector = "AOCR_BORRADOR_INSPECTOR";
        public const string AocrListoParaFirma = "AOCR_LISTO_PARA_FIRMA";
        public const string AocrPendienteDirdac = "AOCR_PENDIENTE_DIRDAC";
        public const string PendienteFirmaAocrDirdac = "PENDIENTE_FIRMA_AOCR_DIRDAC";
        public const string DevueltoDircavPorDirdac = "DEVUELTO_DIRCAV_POR_DIRDAC";
        public const string DevueltoDircav = "DEVUELTO_DIRCAV";
        public const string AocrFirmadaDirdac = "AOCR_FIRMADA_DIRDAC";
        public const string AocrFirmadoDirdac = "AOCR_FIRMADO_DIRDAC";

        // 6. Cierre Institucional
        public const string FirmasCompletas = "FIRMAS_COMPLETAS";
        public const string ListoParaEntrega = "LISTO_PARA_ENTREGA";
        public const string Entregado = "ENTREGADO";
        public const string DocumentosEntregados = "DOCUMENTOS_ENTREGADOS";
        public const string DocumentacionFinalCompleta = "DOCUMENTACION_FINAL_COMPLETA";
        public const string Finalizado = "FINALIZADO";
        public const string Cerrado = "CERRADO";

        // Compatibilidad histórica
        public const string PendienteRevisionInformeDirdac = "PENDIENTE_REVISION_INFORME_DIRDAC";
        public const string InformeTecnicoAprobadoDirdac = "INFORME_TECNICO_APROBADO_DIRDAC";
        public const string DocumentosFinalesPorGenerar = "DOCUMENTOS_FINALES_POR_GENERAR";
        public const string DocumentosFinalesEnFirma = "DOCUMENTOS_FINALES_EN_FIRMA";
        public const string PendienteRevisionInformeDcav = "PENDIENTE_REVISION_INFORME_DCAV";
        public const string InformeTecnicoAprobadoDcav = "INFORME_TECNICO_APROBADO_DCAV";
        public const string InformeTecnicoObservadoDcav = "INFORME_TECNICO_OBSERVADO_DCAV";
        public const string PendienteGeneracionAocr = "PENDIENTE_GENERACION_AOCR";
        public const string AocrFirmado = "AOCR_FIRMADO";
        public const string PendienteGeneracionCyl = "PENDIENTE_GENERACION_CYL";
        public const string CylFirmadas = "CYL_FIRMADAS";
    }
}
