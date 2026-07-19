namespace CapaDatos.Constants
{
    public static class AocrEstadosProceso
    {
        public const string LvFirmadaInspector = "LV_FIRMADA_INSPECTOR";
        public const string InformeTecnicoFirmadoInspector = "INFORME_TECNICO_FIRMADO_INSPECTOR";
        public const string PendienteRevisionInformeDirdac = "PENDIENTE_REVISION_INFORME_DIRDAC";
        public const string InformeTecnicoDevueltoInspector = "INFORME_TECNICO_DEVUELTO_INSPECTOR";
        public const string InformeTecnicoAprobadoDirdac = "INFORME_TECNICO_APROBADO_DIRDAC";
        public const string DocumentosFinalesPorGenerar = "DOCUMENTOS_FINALES_POR_GENERAR";
        public const string DocumentosFinalesEnFirma = "DOCUMENTOS_FINALES_EN_FIRMA";

        public const string AocrBorradorInspector = "AOCR_BORRADOR_INSPECTOR";
        public const string AocrListoParaFirma = "AOCR_LISTO_PARA_FIRMA";
        public const string PendienteFirmaAocrDirdac = "PENDIENTE_FIRMA_AOCR_DIRDAC";
        public const string AocrFirmadoDirdac = "AOCR_FIRMADO_DIRDAC";

        public const string CondicionesBorradorInspector = "CONDICIONES_BORRADOR_INSPECTOR";
        public const string CondicionesListasParaFirma = "CONDICIONES_LISTAS_PARA_FIRMA";
        public const string PendienteFirmaCondicionesDcav = "PENDIENTE_FIRMA_CONDICIONES_DCAV";
        public const string CondicionesFirmadasDcav = "CONDICIONES_FIRMADAS_DCAV";

        // Alias de lectura para instalaciones anteriores. Las nuevas transiciones
        // siempre escriben las claves institucionales declaradas arriba.
        public const string PendienteRevisionInformeDcav = "PENDIENTE_REVISION_INFORME_DCAV";
        public const string InformeTecnicoAprobadoDcav = "INFORME_TECNICO_APROBADO_DCAV";
        public const string InformeTecnicoObservadoDcav = "INFORME_TECNICO_OBSERVADO_DCAV";
        public const string PendienteGeneracionAocr = "PENDIENTE_GENERACION_AOCR";
        public const string AocrFirmado = "AOCR_FIRMADO";
        public const string PendienteGeneracionCyl = "PENDIENTE_GENERACION_CYL";
        public const string CylFirmadas = "CYL_FIRMADAS";
        public const string DocumentacionFinalCompleta = "DOCUMENTACION_FINAL_COMPLETA";
        public const string Finalizado = "FINALIZADO";
    }
}
