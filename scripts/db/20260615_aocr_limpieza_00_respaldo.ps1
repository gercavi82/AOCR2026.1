param(
    [string]$HostName = "172.20.16.55",
    [string]$Port = "5432",
    [string]$Database = "dgac_des",
    [string]$UserName = "root",
    [string]$OutputDir = ".\backups"
)

$ErrorActionPreference = "Stop"

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$dbBackup = Join-Path $OutputDir "backup_aocr_completo_antes_limpieza_${Database}_${stamp}.dump"
$txBackup = Join-Path $OutputDir "backup_aocr_transaccional_antes_limpieza_${Database}_${stamp}.dump"
$manifest = Join-Path $OutputDir "manifest_limpieza_aocr_${Database}_${stamp}.txt"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

@"
Fecha: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Base: $Database
Host: $HostName
Puerto: $Port
Usuario: $UserName
Ambiente: VALIDAR_MANUALMENTE_ANTES_DE_EJECUTAR
Backup completo: $dbBackup
Backup transaccional: $txBackup
"@ | Set-Content -Path $manifest -Encoding UTF8

pg_dump -h $HostName -p $Port -U $UserName -d $Database -Fc -f $dbBackup

pg_dump -h $HostName -p $Port -U $UserName -d $Database -Fc -f $txBackup `
  -t public.email_attachment `
  -t public.email_queue `
  -t public.aocr_tbnotificacion `
  -t public.aocr_tbhistorial_documental `
  -t public.aocr_tbhistorial_estado_inspeccion `
  -t public.aocr_tbhistorial_estado `
  -t public.aocr_audit_trail `
  -t public.aocr_tbauditoria `
  -t public.aocr_tblog `
  -t public.aocr_declaracion_historial `
  -t public.aocr_declaracion_tmp `
  -t public.aocr_idempotency_key `
  -t public.aocr_sync_log `
  -t public.aocr_tb_sync_log `
  -t public.sync_log `
  -t public.aocr_tbcorreo_institucional_historial `
  -t public.aocr_tbfirma_documento `
  -t public.aocr_tbdocumento_subsanacion `
  -t public.aocr_tbsubsanacion `
  -t public.aocr_tbrevision_documental `
  -t public.aocr_tbdocumento_inspeccion `
  -t public.aocr_tbdocumento `
  -t public.aocr_tbchecklist_solicitud `
  -t public.aocr_tbchecklist_item `
  -t public.aocr_tbchecklist `
  -t public.aocr_tbhallazgo `
  -t public.aocr_tbinforme_inspeccion `
  -t public.aocr_tbinforme `
  -t public.aocr_tblv_operacional_eae `
  -t public.aocr_tbobservacion `
  -t public.aocr_tbcertificado `
  -t public.aocr_tbaeronave_solicitud `
  -t public.aocr_tbaeronave `
  -t public.aocr_tb_factura_pago `
  -t public.aocr_tbpago `
  -t public.aocr_or_orden_detalle `
  -t public.aocr_or_orden `
  -t public.aocr_orden_recaudacion `
  -t public.detalles_orden `
  -t public.historial_estados_orden `
  -t public.pagos `
  -t public.ordenes_recaudacion `
  -t public.fr3_detalle_pg `
  -t public.fr3_pg `
  -t public.fr3_detalle `
  -t public.fr3 `
  -t public.aocr_tbviatico `
  -t public.aocr_tbinspeccion `
  -t public.aocr_tbsolicitud

Write-Host "Backups creados:"
Write-Host "  $dbBackup"
Write-Host "  $txBackup"
Write-Host "  $manifest"
