# 🔒 INSTRUCCIONES CRÍTICAS DE SEGURIDAD - CAMBIO DE PASSWORDS

**URGENCIA**: 🔴 **CRÍTICO** - DEBE EJECUTARSE ANTES DE PRODUCCIÓN  
**Fecha**: 7 de Febrero de 2026  
**Responsable**: Equipo de Infraestructura + DBA

---

## ⚠️ PROBLEMA IDENTIFICADO

Las contraseñas de las bases de datos están **EXPUESTAS EN TEXTO PLANO** en los archivos de configuración:

### 1️⃣ PostgreSQL (dgac_des)
**Ubicación**: `CapaPresentacion\Web.config` líneas 10-11  
**Credenciales actuales**:
```
Host: 172.20.16.55
Port: 5432
Database: dgac_des
Username: root
Password: control  ⚠️ CAMBIAR INMEDIATAMENTE
```

### 2️⃣ AS400/P9 (IBM DB2 iSeries)
**Ubicación**: `CapaPresentacion\Web.config` línea 12  
**Credenciales actuales**:
```
DataSource: 172.20.16.14
UserID: DGAC
Password: DGAC2024  ⚠️ CAMBIAR INMEDIATAMENTE
```

---

## 🛠️ SOLUCIÓN PASO A PASO

### OPCIÓN 1: Encriptación de Web.config (Recomendada)

#### Paso 1: Cambiar las Passwords en las Bases de Datos

**PostgreSQL**:
```sql
-- Conectar como superusuario
psql -h 172.20.16.55 -U postgres -d dgac_des

-- Cambiar password del usuario root
ALTER USER root WITH PASSWORD 'NuevaPasswordSegura2026!@#';

-- Verificar cambio
\du root
```

**AS400/P9**:
```
Coordinar con el equipo de AS400 para cambiar la password de DGAC
Usuario: DGAC
Nueva Password: [Definir con equipo AS400 - mínimo 12 caracteres]
```

#### Paso 2: Actualizar Web.config con Nuevas Passwords

**Editar**: `CapaPresentacion\Web.config`

```xml
<connectionStrings>
  <!-- Actualizar con las nuevas passwords -->
  <add name="AOCRConnection" 
       connectionString="Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=NuevaPasswordSegura2026!@#;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Timeout=15;CommandTimeout=60;" 
       providerName="Npgsql" />
       
  <add name="PostgreSQL" 
       connectionString="Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password=NuevaPasswordSegura2026!@#;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Timeout=15;CommandTimeout=60;" 
       providerName="Npgsql" />
       
  <add name="P9ConnectionString" 
       connectionString="DataSource=172.20.16.14;UserID=DGAC;Password=[NUEVA_PASSWORD_AS400];DefaultCollection=DGACSYS;LibraryList=DGACSYS;Pooling=true;" 
       providerName="IBM.Data.DB2.iSeries" />
</connectionStrings>
```

#### Paso 3: Encriptar la Sección connectionStrings

**Ejecutar en PowerShell como Administrador**:

```powershell
# Navegar al directorio del framework
cd C:\Windows\Microsoft.NET\Framework64\v4.0.30319

# Encriptar connectionStrings
.\aspnet_regiis.exe -pef "connectionStrings" "C:\inetpub\wwwroot\AOCR\CapaPresentacion" -prov "DataProtectionConfigurationProvider"

# Verificar encriptación
.\aspnet_regiis.exe -pdf "connectionStrings" "C:\inetpub\wwwroot\AOCR\CapaPresentacion"
```

**Resultado esperado** en Web.config:
```xml
<connectionStrings configProtectionProvider="DataProtectionConfigurationProvider">
  <EncryptedData>
    <CipherData>
      <CipherValue>AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA...</CipherValue>
    </CipherData>
  </EncryptedData>
</connectionStrings>
```

#### Paso 4: Verificar Funcionalidad

```powershell
# Reiniciar IIS
iisreset

# Probar conexión a la aplicación
curl http://localhost/AOCR/Account/Login
```

---

### OPCIÓN 2: Azure Key Vault (Producción Cloud)

Si la aplicación se desplegará en Azure:

#### Paso 1: Crear Azure Key Vault

```powershell
# Azure CLI
az keyvault create --name aocr-keyvault --resource-group aocr-rg --location eastus

# Agregar secrets
az keyvault secret set --vault-name aocr-keyvault --name "PostgreSQL-Password" --value "NuevaPasswordSegura2026!@#"
az keyvault secret set --vault-name aocr-keyvault --name "AS400-Password" --value "[NUEVA_PASSWORD_AS400]"
```

#### Paso 2: Configurar Managed Identity

```csharp
// Instalar paquete
Install-Package Azure.Identity
Install-Package Azure.Security.KeyVault.Secrets

// Modificar App_Start/UnityConfig.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

public static class SecureConfiguration
{
    private static SecretClient _secretClient;
    
    static SecureConfiguration()
    {
        var keyVaultUrl = "https://aocr-keyvault.vault.azure.net/";
        _secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
    }
    
    public static string GetConnectionString(string name)
    {
        if (name == "PostgreSQL")
        {
            var password = _secretClient.GetSecret("PostgreSQL-Password").Value.Value;
            return $"Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password={password};...";
        }
        // Similar para AS400
    }
}
```

---

### OPCIÓN 3: Variables de Entorno (Desarrollo/Testing)

#### Paso 1: Configurar Variables de Entorno

**Windows PowerShell (Sistema)**:
```powershell
[System.Environment]::SetEnvironmentVariable("AOCR_DB_PASSWORD", "NuevaPasswordSegura2026!@#", "Machine")
[System.Environment]::SetEnvironmentVariable("AOCR_AS400_PASSWORD", "[NUEVA_PASSWORD_AS400]", "Machine")
```

#### Paso 2: Modificar Código para Leer Variables

```csharp
// En ConexionDAO.cs o SecureConfigurationService.cs
public string GetConnectionString(string name)
{
    if (name == "PostgreSQL")
    {
        var password = Environment.GetEnvironmentVariable("AOCR_DB_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            // Fallback a Web.config si no está en variable de entorno
            password = ConfigurationManager.ConnectionStrings["PostgreSQL"].ConnectionString;
        }
        return $"Host=172.20.16.55;Port=5432;Database=dgac_des;Username=root;Password={password};...";
    }
    // ...
}
```

---

## 📋 CHECKLIST DE SEGURIDAD

### Antes de Producción
- [ ] **CRÍTICO**: Cambiar password de PostgreSQL (usuario `root`)
- [ ] **CRÍTICO**: Cambiar password de AS400 (usuario `DGAC`)
- [ ] **CRÍTICO**: Actualizar Web.config con nuevas passwords
- [ ] **CRÍTICO**: Encriptar sección connectionStrings con aspnet_regiis
- [ ] Verificar que customErrors está en modo "RemoteOnly"
- [ ] Probar conexión a PostgreSQL después del cambio
- [ ] Probar conexión a AS400 después del cambio
- [ ] Reiniciar IIS y validar que la aplicación funciona
- [ ] Documentar las nuevas passwords en bóveda segura (LastPass/1Password)
- [ ] Implementar rotación de passwords (cada 90 días)

### Validaciones Post-Deploy
- [ ] Login exitoso en aplicación
- [ ] Consultas a PostgreSQL funcionando
- [ ] Consultas a AS400 funcionando
- [ ] Web.config no muestra passwords en texto plano
- [ ] Logs no muestran passwords
- [ ] Error pages no exponen información sensible

---

## 🚨 POLÍTICA DE PASSWORDS

### Requisitos Mínimos
- **Longitud**: Mínimo 12 caracteres
- **Complejidad**: Mayúsculas + minúsculas + números + símbolos
- **No usar**: Palabras del diccionario, información personal, passwords anteriores
- **Ejemplos válidos**:
  - `Dgac#Prod2026!Secure`
  - `P0stgr3$QL_S3cur3_2026`
  - `A$400_Dgac#Pr0d2026`

### Rotación
- **Frecuencia**: Cada 90 días
- **No repetir**: Últimas 5 passwords
- **Notificación**: 15 días antes del vencimiento

### Almacenamiento
- **Bóveda**: LastPass Enterprise / 1Password Teams
- **Acceso**: Solo DBA + DevOps Lead
- **Backup**: Encriptado en bóveda secundaria

---

## 📞 CONTACTOS

**DBA PostgreSQL**: [nombre@dgac.gob.ec]  
**Administrador AS400**: [nombre@dgac.gob.ec]  
**DevOps Lead**: [nombre@dgac.gob.ec]  
**Seguridad IT**: [nombre@dgac.gob.ec]

---

## 📝 REGISTRO DE CAMBIOS

| Fecha | Usuario | Acción | Sistema |
|-------|---------|--------|---------|
| YYYY-MM-DD | | Password cambiada | PostgreSQL |
| YYYY-MM-DD | | Password cambiada | AS400 |
| YYYY-MM-DD | | connectionStrings encriptada | Web.config |
| YYYY-MM-DD | | Validación OK | Producción |

---

**IMPORTANTE**: Este archivo contiene información sensible sobre la infraestructura. 
**NO COMMITEAR** a repositorios públicos o sistemas de control de versiones públicos.

Agregar a `.gitignore`:
```
INSTRUCCIONES_SEGURIDAD_PASSWORDS.md
```
