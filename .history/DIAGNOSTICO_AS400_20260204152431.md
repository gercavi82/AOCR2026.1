# Diagnóstico de Conexión AS400/P9

## Problema Reportado
Los campos "Método de Pago" y "Banco donde se realiza el depósito" están fallando la conexión AS400.

## Configuración Actual
**Servidor AS400:** 190.152.8.185
**Base de datos:** S10a1a05  
**Usuario:** DGACCONEXI
**Contraseña:** DGACTIC20@
**Librería:** DGACDATPRO

## Herramientas de Diagnóstico Implementadas

### 1. Endpoint de Prueba AS400
**URL:** `/OrdenRecaudacion/ProbarAS400`
- Solo accesible para administradores
- Prueba la conexión directa a AS400
- Ejecuta una consulta de prueba para bancos
- Muestra resultados detallados en TempData

### 2. Logging Mejorado
Se agregó logging detallado en:
- `CD_ListaValor.ListaValores()` - Logs de conexión y consultas
- `BancoP9DAO.ProbarConexionAS400()` - Diagnóstico completo

### 3. Manejo de Errores Específicos
Identificación de errores comunes:
- **SQL30082N**: Problema de conectividad de red
- **SQL30061N**: Timeout de conexión  
- **SQL30020N**: Credenciales inválidas

## Pasos para Diagnosticar

### Paso 1: Probar Conexión
1. Iniciar sesión como Administrador
2. Navegar a: `/OrdenRecaudacion/ProbarAS400`
3. Revisar el mensaje resultado

### Paso 2: Revisar Logs de Debug
Los logs aparecerán en la consola de Visual Studio o en los archivos de log:
```
=== PROBANDO CONEXIÓN AS400 ===
Connection String: [string de conexión sin password]
Intentando conectar...
✅ Conexión establecida correctamente
✅ Consulta de prueba exitosa: X bancos encontrados
```

### Paso 3: Verificar Driver ODBC
El sistema usa: `IBM i Access ODBC Driver`

Verificar que esté instalado en el servidor:
- Panel de Control > Herramientas Administrativas > Orígenes de datos ODBC
- Buscar "IBM i Access ODBC Driver"

## Problemas Comunes y Soluciones

### 1. Driver ODBC No Instalado
**Error:** "The specified DSN contains an architecture mismatch"
**Solución:** Instalar IBM i Access Client Solutions

### 2. Firewall/Red
**Error:** "Communication link failure"  
**Solución:** Verificar conectividad de red al servidor 190.152.8.185

### 3. Credenciales
**Error:** "Authorization failure"
**Solución:** Verificar usuario DGACCONEXI y password DGACTIC20@

### 4. Tabla No Existe
**Error:** "Table DGACSYS.TXDGAC not found"
**Solución:** Verificar que la librería DGACDATPRO está correcta

## Sistema de Fallback
Si AS400 falla, el sistema usa valores por defecto:

**Bancos:**
- 001 - BANCO PICHINCHA
- 002 - BANCO GUAYAQUIL  
- 003 - BANCO PACIFICO
- 004 - BANCO INTERNACIONAL
- 005 - PRODUBANCO

**Métodos de Pago:**
- DEPOSITO - DEPÓSITO BANCARIO
- TRANSFERENCIA - TRANSFERENCIA BANCARIA
- CHEQUE - CHEQUE

## Próximos Pasos

1. **Ejecutar diagnóstico** usando `/OrdenRecaudacion/ProbarAS400`
2. **Revisar logs** para identificar el error específico
3. **Verificar conectividad** de red al servidor AS400
4. **Validar driver ODBC** instalado en el servidor
5. **Confirmar credenciales** con el administrador de AS400

## Contactos de Soporte
- **Equipo AS400/P9:** Verificar estado del servidor 190.152.8.185
- **Equipo Red:** Verificar conectividad desde el servidor web
- **Equipo Sistemas:** Verificar driver ODBC instalado