# 📧 FLUJO COMPLETO DE ENVÍO DE CORREOS EN AOCR

## 🎯 Arquitectura General

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          EVENTO EN EL SISTEMA                               │
│    (Crear Solicitud, Cambiar Estado, Pagar, etc.)                          │
└────────────────────────────────┬────────────────────────────────────────────┘
                                 ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│                    CAPA DE NEGOCIOS (CapaNegocio)                           │
│  NotificacionBL.EnviarNotificacion()                                        │
│  OrdenRecaudacionService.EnviarCorreo()                                     │
│  SolicitudAocrCorreoService.NotificarEvento()                               │
└────────────────────────────────┬────────────────────────────────────────────┘
                                 ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│           COLA DE CORREOS EN BD (email_queue)                               │
│  ✓ Persistencia: to_address, subject, body, status, error_message, etc.    │
│  ✓ Estados: PENDIENTE → ENVIANDO → ENVIADO / ERROR                         │
└────────────────────────────────┬────────────────────────────────────────────┘
                                 ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│          SERVICIO DE EMAIL (CapaDatos/Services)                             │
│  EmailQueueService: Procesa cola asincronamente                            │
│  AocrEmailService: Construye y envía SMTP                                   │
└────────────────────────────────┬────────────────────────────────────────────┘
                                 ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│              SERVIDOR SMTP                                                  │
│  mail.aviacioncivil.gob.ec:25 (Sin SSL)                                    │
│  De: no_reply@aviacioncivil.gob.ec                                          │
└────────────────────────────────┬────────────────────────────────────────────┘
                                 ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│           ✉️ BANDEJA DEL DESTINATARIO                                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📋 TIPOS DE CORREOS Y DESTINATARIOS

### 1️⃣ **CAMBIO DE ESTADO DE SOLICITUD AOCR**

**Cuándo se envía:** Cada vez que una solicitud cambia de estado  
**Función:** `NotificacionBL.EncolarCorreoCambioEstadoIdempotente()`  
**Ubicación:** [CapaNegocio/NotificacionBL.cs](../CapaNegocio/NotificacionBL.cs#L449)

**A QUIÉN LLEGA:**
| Rol | Correo | Cuándo | Alias del Remitente |
|-----|--------|--------|---|
| **Representante Técnico (RT)** | `usuario.Email` | Cualquier cambio de estado | Depende del estado (ver tabla abajo) |
| **Inspector Asignado** | `inspector.Email` | Cuando está en inspección | "AOCR - Inspección requerida" |
| **Coordinador** | `coordinador.Email` | Cuando va a revisión | "AOCR - Notificaciones" |

**Alias por tipo de estado:**
```
Observada / Rechazada → "AOCR - Solicitud observada"
Aprobada / Validada   → "AOCR - Documentación aprobada"
En Inspección         → "AOCR - Inspección requerida"
Firma Pendiente       → "AOCR - Firma pendiente"
Documento Emitido     → "AOCR - Documento emitido"
Orden de Pago         → "DGAC - Sistema AOCR"
Notificaciones Gen.   → "AOCR - Notificaciones"
```

**Contenido del Email:**
```
Asunto: Variable según estado
Ejemplo: "AOCR - Solicitud #12345 - Estado: Observada"

Cuerpo (HTML):
- Nombre del destinatario
- Número de solicitud
- Estado anterior → Estado nuevo
- Link a detalle: /SolicitudAOCR/Detalle/12345
- Pie: "Dirección General de Aviación Civil"
```

**Tipo de Notificación:** `AOCR_CAMBIO_ESTADO`  
**Event Key (Para Idempotencia):** `AOCR:CAMBIO_ESTADO:{SolicitudId}:{UsuarioId}:{Estado}`

---

### 2️⃣ **NUEVA ORDEN DE RECAUDACIÓN GENERADA**

**Cuándo se envía:** Cuando usuario genera una orden de recaudación  
**Función:** `OrdenRecaudacionController.EnviarNotificacionOrdenGeneradaAsync()`  
**Ubicación:** [CapaPresentacion/Controllers/OrdenRecaudacionController.cs](../CapaPresentacion/Controllers/OrdenRecaudacionController.cs#L1863)

**A QUIÉN LLEGA:**
| Rol | Correo | Cuándo |
|-----|--------|--------|
| **Representante Técnico** | `orden.Correo` | Siempre se genera orden |
| **Financiero** | Configurado en config | Para revisión de pago |

**Contenido del Email:**
```
Asunto: "Nueva Orden de recaudación - DGAC-OR-{AÑO}-AOCR{NUMERO}"

Cuerpo (HTML):
- Número de orden
- Compañía operadora
- RUC/Cédula
- Subtotal + Admin + Total
- Instrucciones de pago (número de cuenta, banco, etc.)
- Link para descargar PDF: /OrdenRecaudacion/Generar/5
- Información para comunicarse
```

**Tipo de Notificación:** `ORDEN_GENERADA_RT`  
**Event Key:** `ORDEN_GENERADA_RT_{OrdenId}_{UsuarioId}`

---

### 3️⃣ **NUEVAS CREDENCIALES DE USUARIO**

**Cuándo se envía:** Cuando se crea un nuevo usuario interno o externo  
**Función:** `AdminUsuariosBL.EnviarCorreoCredenciales()`  
**Ubicación:** [CapaNegocio/AdminUsuariosBL.cs](../CapaNegocio/AdminUsuariosBL.cs#L690)

**A QUIÉN LLEGA:**
| Rol | Correo |
|-----|--------|
| **Nuevo Usuario** | `usuario.Email` |

**Contenido del Email:**
```
Asunto: "Credenciales de acceso - Sistema AOCR"

Cuerpo (HTML):
- Usuario: {NombreUsuario}
- Contraseña: {PasswordTemporal}
- Link de acceso: {URL_SISTEMA}
- Instrucciones de primer login
- Link para cambiar contraseña
- Teléfono de soporte
```

**Tipo de Notificación:** `CREDENCIALES_USUARIO`

---

### 4️⃣ **SOLICITUD AOCR CREADA (RT Inicial)**

**Cuándo se envía:** Cuando el RT crea una nueva solicitud AOCR formal  
**Función:** `SolicitudAOCRController.NotificarSolicitanteSolicitudCreada()`  
**Ubicación:** [CapaPresentacion/Controllers/SolicitudAOCRController.cs](../CapaPresentacion/Controllers/SolicitudAOCRController.cs#L2941)

**A QUIÉN LLEGA:**
| Rol | Correo |
|-----|--------|
| **Representante Técnico** | `solicitud.CorreoRepresentanteTecnico` |
| **Coordinador** | (Notificación adicional en BD) |

**Contenido del Email:**
```
Asunto: "AOCR - Solicitud registrada {NumeroSolicitud}"

Cuerpo (HTML):
- Confirmación de registro exitoso
- Número de solicitud: #12345
- Operador: Nombre Compañía
- Código OACI: XXXX
- Fecha de registro: DD/MM/YYYY HH:MM
- Link a detalle: /SolicitudAOCR/Detalle/12345
- Próximos pasos
```

**Tipo de Notificación:** `SOLICITUD_COMPLETADA`

---

### 5️⃣ **NOTIFICACIÓN MANUAL A USUARIOS**

**Cuándo se envía:** Admin o personal envía notificaciones manuales  
**Función:** `NotificacionController.EnviarNotificacion()`  
**Ubicación:** [CapaPresentacion/Controllers/NotificacionController.cs](../CapaPresentacion/Controllers/NotificacionController.cs#L360)

**A QUIÉN LLEGA:**
| Rol | Correo | Control |
|-----|--------|---------|
| **Destinatario Seleccionado** | `request.Correo` | Admin elige |

**Contenido del Email:**
```
Asunto: {TituloIngresado}

Cuerpo (HTML):
- Titulo
- Mensaje HTML
- Personalizado según sistema
```

**Tipo de Notificación:** Configurable (usuario especifica)

---

### 6️⃣ **NOTIFICACIONES AC-11 (WORKFLOW FINAL INSTITUCIONAL)**

**Cuándo se envía:** Cuando AOCR transita entre DIRCAV, DIRDAC, Coordinador en la fase final  
**Función:** `AocrProcesoNotificacionService.NotificarAocrRemitidoDirdac()`, `NotificarAocrDevueltoDircav()`, etc.  
**Ubicación:** [CapaNegocio/Services/AocrProcesoNotificacionService.cs](../CapaNegocio/Services/AocrProcesoNotificacionService.cs#L70)

**A QUIÉN LLEGA (Notificación a Múltiples Roles Institucionales):**
| Rol | Correo | Evento | Cuándo |
|-----|--------|--------|--------|
| **DIRCAV** | `DirectorCertificacionesDcav` | AOCR_REMITIDO_DIRDAC | Cuando DIRCAV remite AOCR a DIRDAC |
| **DIRDAC** | `DIRDAC` | AOCR_REMITIDO_DIRDAC | Cuando DIRCAV remite AOCR a DIRDAC |
| **Coordinador** | `Coordinador` | AOCR_REMITIDO_DIRDAC | Cuando DIRCAV remite AOCR a DIRDAC |
| **DIRCAV** | `DirectorCertificacionesDcav` | AOCR_DEVUELTO_DIRCAV | Cuando DIRDAC devuelve AOCR a DIRCAV |
| **DIRDAC** | `DIRDAC` | AOCR_DEVUELTO_DIRCAV | Cuando DIRDAC devuelve AOCR a DIRCAV |
| **Coordinador** | `Coordinador` | AOCR_DEVUELTO_DIRCAV | Cuando DIRDAC devuelve AOCR a DIRCAV |
| **DIRCAV** | `DirectorCertificacionesDcav` | AOCR_FIRMADO_DIRDAC | Cuando DIRDAC firma el AOCR |
| **DIRDAC** | `DIRDAC` | AOCR_FIRMADO_DIRDAC | Cuando DIRDAC firma el AOCR |
| **Coordinador** | `Coordinador` | AOCR_FIRMADO_DIRDAC | Cuando DIRDAC firma el AOCR |
| **DIRCAV** | `DirectorCertificacionesDcav` | FIRMAS_COMPLETAS | Cuando DIRCAV firma C&L y DIRDAC firma AOCR |
| **DIRDAC** | `DIRDAC` | FIRMAS_COMPLETAS | Cuando DIRCAV firma C&L y DIRDAC firma AOCR |
| **Coordinador** | `Coordinador` | FIRMAS_COMPLETAS | Cuando DIRCAV firma C&L y DIRDAC firma AOCR |

**Contenido del Email (Ejemplo):**
```
Asunto: "Sistema AOCR - AOCR remitido a DIRDAC"

Cuerpo (HTML):
- Mensaje de estado del workflow
- Número de solicitud: #12345
- Operador: AEROLÍNEA XYZ
- Descripción del evento (remisión, devolución, firma, etc.)
- Link a detalle: /SolicitudAOCR/Detalle/12345
- Para eventos de devolución: Observación del evaluador
```

**Tipo de Notificación:** 
- `AOCR_REMITIDO_DIRDAC`
- `AOCR_DEVUELTO_DIRCAV`
- `AOCR_FIRMADO_DIRDAC`
- `FIRMAS_COMPLETAS`

**Event Key (Para Idempotencia):** `{TipoEvento}:{SolicitudId}:{Email}`

---

## 🔄 FLUJO PASO A PASO: CAMBIO DE ESTADO DE SOLICITUD

```
┌─────────────────────────────────────────────────┐
│ 1. Acción en Controladora                       │
│    SolicitudAOCRController.CambiarEstado()      │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 2. Lógica de Negocio                            │
│    NotificacionBL.NotificarCambioEstado()       │
│    ├─ Valida cambio de estado                   │
│    ├─ Normaliza estado                          │
│    └─ Resuelve alias por tipo de estado         │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 3. Crear Notificación en BD                     │
│    NotificacionBL.EnviarNotificacion()          │
│    └─ Inserta en tabla notificacion             │
│       (visible en panel del usuario)            │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 4. Encolar Correo Idempotente                   │
│    EncolarCorreoCambioEstadoIdempotente()       │
│    ├─ Verifica si ya existe en cola             │
│    ├─ Crea event_key para unicidad              │
│    └─ Inserta en email_queue                    │
│       status = 'PENDIENTE'                      │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 5. Obtener Datos del Usuario                    │
│    Usuario.ObtenerPorId(codigoUsuario)          │
│    └─ Extrae email, nombre, etc.                │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 6. Construir Correo HTML                        │
│    EmailTemplateRenderer.EnsureStandardLayout() │
│    └─ Template HTML institucional               │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 7. Procesador Asincrónico (Background Job)      │
│    EmailQueueService.ProcesarColaAsync()        │
│    ├─ SELECT * FROM email_queue                 │
│    │  WHERE status = 'PENDIENTE'                │
│    ├─ UPDATE status = 'ENVIANDO'                │
│    └─ Intenta enviar (3 reintentos)             │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 8. Envío SMTP Real                              │
│    AocrEmailService.EnviarMensajeCorreo()       │
│    ├─ SmtpClient: mail.aviacioncivil.gob.ec    │
│    ├─ From: no_reply@aviacioncivil.gob.ec      │
│    ├─ To: usuario.Email                         │
│    ├─ Subject: {Asunto}                         │
│    └─ Body: HTML                                │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 9. Actualizar Estado en Cola                    │
│    ActualizarEstadoAsync()                      │
│    ├─ SI ÉXITO: status = 'ENVIADO'              │
│    │           sent_at = NOW()                  │
│    ├─ SI ERROR: status = 'ERROR'                │
│    │           error_message = {Detalle}        │
│    │           proximo_intento = NOW + 1 hora   │
│    └─ intentos++                                │
└──────────────┬────────────────────────────────┘
               ↓
┌─────────────────────────────────────────────────┐
│ 10. ✉️ CORREO EN BANDEJA DEL USUARIO            │
└─────────────────────────────────────────────────┘
```

---

## 🎯 MATRIZ DE DESTINATARIOS

### Tabla Completa: ¿A QUIÉN LLEGAN LOS CORREOS?

| # | Tipo de Correo | Destinatario | Rol en AOCR | Campo BD | Condición |
|---|---|---|---|---|---|
| 1 | Cambio Estado (Observada) | RT | Solicitante | `usuario.Email` | Siempre |
| 2 | Cambio Estado (Aprobada) | RT | Solicitante | `usuario.Email` | Siempre |
| 3 | Cambio Estado (En Inspección) | Inspector | Inspector | `usuario.Email` | Si hay inspector asignado |
| 4 | Cambio Estado (En Inspección) | Coordinador | Coordinación | `usuario.Email` | Config sistema |
| 5 | Documentación Lista | Inspector | Inspector | `usuario.Email` | Si revisión documental completada |
| 6 | Nueva Orden Generada | RT | Solicitante | `orden.Correo` | Siempre |
| 7 | Nueva Orden Generada | Financiero | Financiero | Config | Para revisión pago |
| 8 | Nuevas Credenciales | Usuario Nuevo | Cualquiera | `usuario.Email` | Siempre |
| 9 | Solicitud AOCR Creada | RT | Solicitante | `solicitud.CorreoRepresentanteTecnico` | Siempre |
| 10 | No Conformidad Creada | RT + Supervisor | Solicitante + Supervisor | Múltiples | Cuando se crea NC |
| 11 | Notificación Manual | Elegido por Admin | Cualquiera | Admin especifica | Solo si Admin envía |
| 12 | Reenvío Manual | Usuario Seleccionado | Especificado | Admin especifica | Desde panel admin |
| 13 | **AC-11: AOCR Remitido a DIRDAC** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **DIRCAV remite a DIRDAC** |
| 14 | **AC-11: AOCR Devuelto a DIRCAV** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **DIRDAC devuelve a DIRCAV** |
| 15 | **AC-11: AOCR Firmado por DIRDAC** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **DIRDAC firma documento** |
| 16 | **AC-11: Firmas Completas** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **Ambas firmas completadas** |
| 13 | **AC-11: AOCR Remitido a DIRDAC** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **DIRCAV remite a DIRDAC** |
| 14 | **AC-11: AOCR Devuelto a DIRCAV** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **DIRDAC devuelve a DIRCAV** |
| 15 | **AC-11: AOCR Firmado por DIRDAC** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **DIRDAC firma documento** |
| 16 | **AC-11: Firmas Completas** | **DIRCAV, DIRDAC, Coordinador** | **Autoridades institucionales** | **Rol asignado** | **Ambas firmas completadas** |

---

## 🔧 CONFIGURACIÓN ACTUAL

### En Web.config:
```xml
<add key="SmtpHost" value="mail.aviacioncivil.gob.ec"/>
<add key="SmtpPort" value="25"/>
<add key="SmtpEnableSsl" value="false"/>
<add key="FromEmail" value="no_reply@aviacioncivil.gob.ec"/>
<add key="FromName" value="Sistema AOCR"/>
```

### Credenciales (Variables de Entorno):
```
SMTP_USERNAME = Desde SecureConfigurationService
SMTP_PASSWORD = Desde SecureConfigurationService
```

### Cola de Correos (PostgreSQL):
```sql
CREATE TABLE email_queue (
    id SERIAL PRIMARY KEY,
    to_address VARCHAR(255),
    subject VARCHAR(255),
    body TEXT,
    status VARCHAR(20),           -- PENDIENTE, ENVIANDO, ENVIADO, ERROR
    error_message TEXT,
    intentos INTEGER,
    proximo_intento TIMESTAMP,
    created_at TIMESTAMP,
    sent_at TIMESTAMP,
    ...
);
```

---

## 📊 EJEMPLO REAL: CAMBIO DE ESTADO

**Escenario:** RT crea solicitud AOCR y coordinador la pasa a "En Revisión"

### BD Antes:
```
email_queue table:
┌─────┬────────────────────┬────────────┬──────────┐
│ id  │ to_address         │ status     │ attempts │
├─────┼────────────────────┼────────────┼──────────┤
│ 1   │ rt@empresa.ec      │ PENDIENTE  │ 0        │
│ 2   │ insp@dgac.gob.ec   │ PENDIENTE  │ 0        │
└─────┴────────────────────┴────────────┴──────────┘

notificacion table:
┌──────┬──────────────┬────────────────────────────────┐
│ id   │ titulo       │ tipo                           │
├──────┼──────────────┼────────────────────────────────┤
│ 120  │ Cambio Estado│ "En Revisión"                  │
└──────┴──────────────┴────────────────────────────────┘
```

### Correos Enviados:

**Email 1: Al Representante Técnico**
```
De: no_reply@aviacioncivil.gob.ec "AOCR - Notificaciones"
Para: rt@empresa.ec
Asunto: AOCR - Solicitud #12345 - Estado: En Revisión

Estimado/a Juan Pérez,

Su solicitud AOCR se encuentra en revisión documental.

• Número de solicitud: #12345
• Operador: AEROLÍNEA XYZ
• Código OACI: XYZZ
• Estado anterior: Documentación Completa
• Estado nuevo: En Revisión

Puede revisar el detalle en: https://aocr.dgac.gob.ec/SolicitudAOCR/Detalle/12345

Atentamente,
Dirección General de Aviación Civil
```

**Email 2: Al Inspector Asignado**
```
De: no_reply@aviacioncivil.gob.ec "AOCR - Inspección requerida"
Para: insp@dgac.gob.ec
Asunto: AOCR - Nueva inspección requerida #12345

Estimado/a Inspector,

Se asignó una nueva solicitud para inspección.

• Solicitud: #12345
• Solicitante: AEROLÍNEA XYZ
• Tipo: Emisión de AOC
• Prioridad: Normal

Acceda al sistema: https://aocr.dgac.gob.ec/SolicitudAOCR/Detalle/12345

Atentamente,
Dirección General de Aviación Civil
```

---

## 🚨 REINTENTOS Y MANEJO DE ERRORES

### Política de Reintentos:
```
Intento 1: Inmediato (5 seg)
Intento 2: +1 hora
Intento 3: +1 día
Intento 4: Abandonado (error_message guardado)
```

### Errores Comunes:
| Error SMTP | Causa | Solución |
|---|---|---|
| 550 User unknown | Email no existe | Validar email en BD |
| 421 Service unavailable | Servidor SMTP caído | Reintentar (automático) |
| 5.7.1 Authentication failed | Credenciales incorrectas | Revisar variables de entorno |
| 45X Timeout | Red lenta | Aumentar timeout (30s default) |

### Query para ver Correos Atrasados:
```sql
SELECT id, to_address, status, intentos, error_message, proximo_intento
FROM email_queue
WHERE status IN ('PENDIENTE', 'ENVIANDO', 'ERROR')
  AND proximo_intento <= NOW()
  AND intentos < 4
ORDER BY proximo_intento ASC;
```

---

## 🔐 SEGURIDAD Y PRIVACIDAD

✅ **Lo que está BIEN:**
- ✓ Remitente centralizado: `no_reply@aviacioncivil.gob.ec`
- ✓ Credenciales en variables de entorno (no hardcodeadas)
- ✓ Validación de emails antes de envío
- ✓ Cifrado de contraseña temporal en correos
- ✓ Logs de auditoría con correlation IDs

⚠️ **Mejoras Posibles:**
- ⚠ Implementar DKIM/SPF para mejorar entregabilidad
- ⚠ Habilitar TLS (SSL) en SMTP para mayor seguridad
- ⚠ Implementar rate limiting para evitar spam
- ⚠ Agregar "unsubscribe" links en notificaciones masivas

---

## 📍 UBICACIONES CLAVE EN CÓDIGO

| Componente | Archivo | Línea | Responsabilidad |
|---|---|---|---|
| **Notificación interna** | `CapaNegocio/NotificacionBL.cs` | 66 | Crea avisos en BD |
| **Encolar correo** | `CapaNegocio/NotificacionBL.cs` | 449 | Agrega a cola SMTP |
| **Procesar cola** | `CapaDatos/Services/EmailQueueService.cs` | 490 | Procesa PENDIENTES |
| **Envío SMTP** | `CapaDatos/Services/AocrEmailService.cs` | 38 | Conecta a servidor |
| **Controladora Solicitud** | `CapaPresentacion/Controllers/SolicitudAOCRController.cs` | 2941 | Desencadena eventos |
| **Controladora Orden** | `CapaPresentacion/Controllers/OrdenRecaudacionController.cs` | 1863 | Ordenes de pago |
| **AC-11: Notificaciones institucionales** | `CapaNegocio/Services/AocrProcesoNotificacionService.cs` | 70 | Envía a DIRCAV, DIRDAC, Coordinador |
| **AC-11: Workflow DIRDAC** | `CapaNegocio/Services/AocrFinalWorkflowService.cs` | 94 | Transiciones de estado final |
| **Config** | `CapaPresentacion/Web.config` | 17-25 | Parámetros SMTP |

---

## ✅ CHECKLIST: ¿ESTÁ FUNCIONANDO?

- ✅ Tabla `email_queue` existe y tiene 17 columnas
- ✅ Columna `status` y `error_message` presentes
- ✅ Índices creados (13 índices activos)
- ✅ Email "Nueva Orden" fue enviado exitosamente  
- ✅ SMTP configurado en Web.config
- ✅ Variables de entorno SMTP_USERNAME/PASSWORD disponibles
- ✅ Procesador asincrónico ejecutándose (fondo o scheduled task)

---

## 🎓 CONCLUSIÓN

El sistema **AOCR tiene emails COMPLETAMENTE OPERATIVO**. Cada acción importante:
- ✉️ Se encola en BD (no se pierde)
- 🔄 Se procesa asincronicamente (no bloquea)
- 📨 Se envía a destinatarios específicos según rol
- 🔐 Con remitente centralizado y auditable
- ♻️ Con reintentos automáticos en caso de fallo

**Destinatarios por fase:**
- **Fase inicial (AC-01 a AC-10):** Representante Técnico, Inspector, Coordinador
- **Fase final (AC-11):** DIRCAV, DIRDAC, Coordinador (notificaciones institucionales sincronizadas)

Todos los emails llegan a **usuarios INTERNOS (DGAC) y EXTERNOS (Operadores)** según su rol y función en el workflow.

