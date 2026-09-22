# Script para aplicar migración de usuario_id a aocr_tbauditoria
# Uso: .\ApplyMigration.ps1

$connectionString = "Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=control;Timeout=15;Connection Idle Lifetime=0;"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Migración: Agregar columna usuario_id" -ForegroundColor Cyan
Write-Host "Tabla: aocr_tbauditoria" -ForegroundColor Cyan
Write-Host "Fecha: 2026-09-22" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

try {
    Write-Host "Cargando ensamblados..." -ForegroundColor Yellow
    
    # Usar AppDomain para cargar sin problemas de versión
    $NpgsqlPath = "C:\proyectos\AOCR\Npgsql.dll"
    
    if (-not (Test-Path $NpgsqlPath)) {
        Write-Host "ERROR: No se encontró Npgsql.dll en $NpgsqlPath" -ForegroundColor Red
        Write-Host "Verifica que el archivo DLL esté en el directorio." -ForegroundColor Red
        exit 1
    }
    
    # Usar psql de PostgreSQL si está disponible en PATH
    $psqlPath = (Get-Command psql -ErrorAction SilentlyContinue).Path
    
    if ($psqlPath) {
        Write-Host "✓ Encontrado psql en: $psqlPath" -ForegroundColor Green
        Write-Host "Conectando a PostgreSQL y ejecutando migración..." -ForegroundColor Yellow
        
        $env:PGPASSWORD = "control"
        
        $sqlScript = @"
BEGIN;
ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL;

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción auditada';
COMMIT;
"@
        
        # Ejecutar el SQL usando psql
        $sqlScript | & psql -h 172.20.16.55 -U root -d dgac_des -v ON_ERROR_STOP=1 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✓ MIGRACIÓN COMPLETADA EXITOSAMENTE" -ForegroundColor Green
            Write-Host ""
            
            # Verificar
            Write-Host "Verificando que la columna existe..." -ForegroundColor Yellow
            @"
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'aocr_tbauditoria' AND column_name = 'usuario_id';
"@ | & psql -h 172.20.16.55 -U root -d dgac_des
            
        } else {
            Write-Host "ERROR: La migración falló con código $LASTEXITCODE" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "⚠  psql no se encontró en PATH" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Alternativa: Ejecuta este script SQL en pgAdmin o DBeaver:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "--- INICIO DEL SCRIPT SQL ---" -ForegroundColor Cyan
        Write-Host @"
BEGIN;
ALTER TABLE IF EXISTS public.aocr_tbauditoria
ADD COLUMN IF NOT EXISTS usuario_id INTEGER NULL;

CREATE INDEX IF NOT EXISTS idx_aocr_tbauditoria_usuario_id 
ON public.aocr_tbauditoria (usuario_id);

COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción auditada';
COMMIT;
"@ -ForegroundColor Cyan
        Write-Host "--- FIN DEL SCRIPT SQL ---" -ForegroundColor Cyan
        exit 1
    }
    
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    Write-Host "Stack: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}
