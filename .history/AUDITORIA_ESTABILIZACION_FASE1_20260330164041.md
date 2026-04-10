# Auditoria de Estabilizacion AOCR - Fase 1

## Estado de esta pasada

- Se confirmo que `CapaPresentacion` es el proyecto web operativo principal.
- Se confirmo que `AOCR/AOCR.csproj` es un proyecto web minimo/legacy y hoy introduce un blocker de carga de targets en algunos entornos.
- Se reemplazaron constructores obsoletos de DAOs AS400/P9 en controladores activos por constructores basados en configuracion segura.

## Correcciones aplicadas

- Reemplazo de constructores obsoletos en controladores activos:
  - `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
  - `CapaPresentacion/Controllers/UsuarioController.cs`
  - `CapaPresentacion/Controllers/EmpresaController.cs`
  - `CapaPresentacion/Controllers/AdminUsuariosController.cs`
  - `CapaPresentacion/Controllers/AccountController.cs`
- Normalizacion de `CapaDatos/DAOs/ObservacionDAO.cs` al esquema real `snake_case` de PostgreSQL para dejar de consultar `codigoobservacion` y `codigousuario` inexistentes.
- Resultado:
  - sin errores en los archivos tocados;
  - menor dependencia del patron legacy `new ...DAO()` sin configuracion segura.
  - el DAO de observaciones ya consulta `codigo_observacion`, `codigo_inspeccion`, `fecha_observacion`, `fecha_resolucion` y `codigo_usuario`.

## Hallazgos priorizados

### Critico

1. Secretos productivos o de integracion siguen hardcodeados en configuracion web.
   - Evidencia principal:
     - `CapaPresentacion/Web.config`
   - Impacto:
     - exposicion de credenciales de PostgreSQL y AS400/P9;
     - alto riesgo operacional y de seguridad.
   - Recomendacion:
     - migrar secretos a variables de entorno o store seguro;
     - dejar en `Web.config` solo placeholders no sensibles.

2. El modulo de observaciones/no conformidades mantiene riesgo residual hasta validar el fix en runtime.
   - Evidencia principal:
     - `CapaDatos/DAOs/ObservacionDAO.cs`
   - Estado actual:
     - el DAO ya fue normalizado a `snake_case` para usar `codigo_observacion`, `codigo_inspeccion`, `fecha_observacion`, `fecha_resolucion` y `codigo_usuario`.
   - Riesgo residual:
     - aun no se ha confirmado contra la base real que desaparecio el error `42703` que hacia fallar el dashboard.
   - Impacto:
     - el dashboard requiere degradacion controlada para no caer completo.
   - Recomendacion:
     - validar en runtime que la normalizacion del DAO elimino el error del dashboard y, si queda estable, retirar la degradacion temporal.

### Alta

3. Existen reglas de privilegio o bypass con correos personales/institucionales hardcodeados.
   - Evidencia principal:
     - `CapaDatos/DAOs/AdminUsuariosDAO.cs`
     - `CapaPresentacion/Controllers/AccountController.cs`
     - `CapaNegocio/UsuarioBL.cs`
   - Impacto:
     - reglas de acceso y proteccion de usuarios especiales no auditables desde configuracion.
   - Recomendacion:
     - mover estas excepciones a configuracion administrable o tabla dedicada.

4. La configuracion de correo esta duplicada entre claves legacy y claves nuevas.
   - Evidencia principal:
     - `CapaPresentacion/Web.config`
     - `CapaDatos/Services/SecureConfigurationService.cs`
     - `CapaNegocio/Helpers/EmailHelper.cs`
     - `CapaNegocio/Services/EmailService.cs`
   - Impacto:
     - deriva de configuracion, fallos intermitentes y dificultad para despliegue seguro.
   - Recomendacion:
     - converger a una sola fuente de verdad basada en `ISecureConfigurationService`.

### Media

5. Dependencias frontend siguen mezclando assets locales con CDNs y fallbacks heterogeneos.
   - Evidencia principal:
     - `CapaPresentacion/Views/Shared/_LayoutAOCR.cshtml`
     - `CapaPresentacion/Views/Direccion/EmitirAOCR.cshtml`
     - `CapaPresentacion/Views/Shared/_ModalCrearUsuario.cshtml`
     - `CapaPresentacion/Views/SolicitudAOCR/_FormularioEmisionAOCR.cshtml`
   - Impacto:
     - mayor superficie de fallo por red/CSP y comportamiento inconsistente entre vistas.
   - Recomendacion:
     - unificar carga de librerias en layout o bundles locales.

6. El proyecto `AOCR/AOCR.csproj` parece residual y no sigue la misma estrategia de imports robustos que `CapaPresentacion`.
   - Evidencia principal:
     - `AOCR/AOCR.csproj`
     - `CapaPresentacion/CapaPresentacion.csproj`
   - Impacto:
     - ruido de build y confusion sobre el entrypoint real.
   - Recomendacion:
     - excluirlo del pipeline o documentar explicitamente su caracter legacy antes de eliminarlo.

## Riesgos que no se tocaron en esta fase

- Migracion de secretos fuera de `Web.config`.
- Validacion runtime de la correccion estructural del DAO de observaciones.
- Eliminacion de bypasses por correo hardcodeado.
- Limpieza agresiva de vistas/controladores potencialmente huerfanos.

## Proximo lote seguro recomendado

1. Validar `ObservacionDAO` ya corregido contra el esquema real y eliminar la degradacion temporal del dashboard si no reaparecen errores.
2. Centralizar lectura de correo en `ISecureConfigurationService` y retirar claves duplicadas.
3. Externalizar listas de superadmin/usuarios criticos hoy hardcodeadas.
4. Definir si `AOCR/AOCR.csproj` sale del build operativo o se mantiene solo como legado documentado.
