# AOCR 2026.1

Repositorio para la solución AOCR (DGAC Ecuador). Este documento cubre el
levantamiento local, configuración por entorno (DEV/QA/PROD), base de datos e
implementación en IIS.

## Requisitos

- Visual Studio (2019/2022) con .NET Framework instalado.
- SQL Server (local o remoto) y permisos para crear/restaurar bases.
- IIS (para despliegue on-premise).

## Estructura principal

- `CapaPresentacion/`: Aplicación web (ASP.NET MVC).
- `CapaDatos/`: Acceso a datos (DAOs, servicios).
- `CapaNegocio/`, `CapaModelo/`: lógica y modelos.

## Configuración por entorno (DEV / QA / PROD)

La configuración se define en `CapaPresentacion/Web.config` (y `web.config`
en otros proyectos si aplica). Se recomienda **no** versionar credenciales
reales. Mantener un archivo local con los valores reales y reemplazar al
publicar.

### Variables/keys recomendadas

En `appSettings`:

- `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`, `SmtpEnableSsl`
- `FromEmail`, `FromName`
- `UploadStoragePath` (ruta fuera del webroot; p. ej. `D:\AOCR\Uploads`)
- `MaxUploadSize` (en bytes, p. ej. `10485760` para 10 MB)
- `AdminEmails` (correo(s) financieros, separados por `;` o `,`)

En `connectionStrings`:

- `DefaultConnection` o el nombre definido por el DAO.

### Ejemplo (DEV)

```xml
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="Data Source=localhost;Initial Catalog=AOCR_DEV;User ID=...;Password=...;"
       providerName="System.Data.SqlClient" />
</connectionStrings>
<appSettings>
  <add key="UploadStoragePath" value="D:\AOCR\Uploads\DEV" />
  <add key="MaxUploadSize" value="10485760" />
  <add key="AdminEmails" value="finanzas-dev@dgac.gob.ec" />
</appSettings>
```

### Ejemplo (QA/PROD)

Cambiar la base de datos y rutas de archivos:

- `AOCR_QA` / `AOCR_PROD`
- `D:\AOCR\Uploads\QA` / `D:\AOCR\Uploads\PROD`
- Credenciales SMTP productivas

## Base de datos

1. Crear la base en SQL Server.
2. Ejecutar scripts de inicialización si existen (consultar al equipo).
3. Verificar tablas críticas: `aocr_or_orden`, `aocr_tbpago`, `aocr_solicitud`.

## Despliegue en IIS

1. Crear un *Application Pool* en IIS (recomendado: **.NET CLR v4.0**, modo
   **Integrated**).
2. Publicar el proyecto `CapaPresentacion` a una carpeta (p. ej. `D:\AOCR\Web`).
3. Configurar el sitio en IIS apuntando a la carpeta publicada.
4. Ajustar permisos del pool en la carpeta de uploads (p. ej. `D:\AOCR\Uploads`).
5. Reiniciar el sitio y validar acceso.

## Notas

- Si se habilita correo SMTP, comprobar conectividad y credenciales.
- Para cargas de archivos, asegurar almacenamiento **fuera del webroot**.

