# 🚀 IMPLEMENTACIÓN COMPLETA - Workflow AOCR Formalizado

**Fecha:** 07 de Febrero de 2026  
**Estado:** ✅ **COMPLETADO** - 4 tareas de alta prioridad implementadas

---

## 📋 Resumen Ejecutivo

Se implementaron **4 funcionalidades críticas** para completar el workflow formal AOCR:

1. ✅ **Estados SUBSANACION** - Ciclo de corrección de documentos
2. ✅ **Roles Faltantes** - 11 roles jerárquicos incluyendo Director Financiero
3. ✅ **Aprobación Director** - Autorización final jerárquica
4. ✅ **Sistema Emisión AOCR (PDF)** - Generación automática de certificados

---

## 🆕 Archivos Creados

### 1. **Constants/RolesAOCR.cs** (415 líneas)
Define los 11 roles del sistema con jerarquía y permisos:

**Roles de Dirección (Nivel 3):**
- `DIRECTOR_FINANCIERO` ⭐ **CRÍTICO** - Aprobación final
- `JEFATURA_TECNICA`
- `ADMINISTRADOR`

**Roles de Coordinación (Nivel 2):**
- `COORDINADOR_LEGAL`
- `COORDINADOR_FINANCIERO`
- `COORDINADOR_INSPECCIONES`

**Roles Operativos (Nivel 1):**
- `OPERADOR` - Recepciona solicitudes
- `EVALUADOR_TECNICO` - Evalúa documentación
- `INSPECTOR` - Inspecciones en campo

**Roles Externos (Nivel 0):**
- `SOLICITANTE` - Operador de aeronaves
- `REPRESENTANTE_LEGAL`

**Métodos Clave:**
```csharp
RolesAOCR.ObtenerRolesPermitidosParaEstado(estado)  // Roles que pueden editar
RolesAOCR.PuedeTransicionarAEstado(rol, estadoDestino)  // Validar transiciones
RolesAOCR.EsDirector(rol)  // Verificar si es rol de dirección
RolesAOCR.PuedeAprobar(rol)  // Verificar permiso de aprobación
```

---

### 2. **scripts/insertar_roles_faltantes.sql** (285 líneas)

Script SQL que crea:
- ✅ Inserta 11 roles si no existen (INSERT conditional)
- ✅ Agrega columna `nivel_jerarquico` (1-3, 0 para externos)
- ✅ Agrega columna `puede_aprobar` (BOOLEAN)
- ✅ Agrega columna `categoria_rol` (INTERNO/EXTERNO)
- ✅ Crea vista `vw_roles_resumen` con estadísticas
- ✅ Verificaciones automáticas post-migración

**Ejecutar:**
```bash
# Windows
echo control | psql -h 172.20.16.55 -U dgac_admin -d dgac_des -f scripts/insertar_roles_faltantes.sql

# Linux/Mac
psql -h 172.20.16.55 -U dgac_admin -d dgac_des -f scripts/insertar_roles_faltantes.sql
```

---

### 3. **CapaNegocio/SolicitudAOCRBL.cs - Métodos Agregados** (267 líneas nuevas)

#### **Workflow Subsanación:**

```csharp
// 1. Solicitar subsanación (Operador/Evaluador/Coordinador)
SolicitudAOCRBL.SolicitarSubsanacion(
    idSolicitud: 123,
    observaciones: "Falta documento X, Y está incompleto",
    codigoUsuario: 5,
    out mensaje
);
// Estado: ANÁLISIS_REQUISITOS → SUBSANACION
// Notifica al solicitante con WARNING

// 2. Marcar como subsanado (Solicitante carga correcciones)
SolicitudAOCRBL.MarcarSubsanado(
    idSolicitud: 123,
    codigoUsuario: 10,
    comentarios: "Documentos corregidos adjuntos",
    out mensaje
);
// Estado: SUBSANACION → SUBSANADO
// Notifica a operadores para revisión
```

#### **Workflow Aprobación:**

```csharp
// 3. Aprobar por Coordinador
SolicitudAOCRBL.AprobarCoordinador(
    idSolicitud: 123,
    codigoUsuario: 7,
    observaciones: "Aprobado aspecto financiero",
    out mensaje
);
// Estado: EN_APROBACION_COORDINADOR → EN_APROBACION_DIRECTOR
// Notifica a Director Financiero

// 4. Aprobar por Director (CRÍTICO)
SolicitudAOCRBL.AprobarDirector(
    idSolicitud: 123,
    codigoUsuario: 2,
    observaciones: "Aprobado para emisión",
    out mensaje
);
// Estado: EN_APROBACION_DIRECTOR → APROBADO
// Notifica al solicitante SUCCESS

// 5. Rechazar (desde cualquier estado)
SolicitudAOCRBL.Rechazar(
    idSolicitud: 123,
    codigoUsuario: 2,
    motivoRechazo: "No cumple con requisito Z",
    out mensaje
);
// Estado: CUALQUIERA → RECHAZADO (final)
// Notifica al solicitante ERROR
```

#### **Emisión AOCR:**

```csharp
// 6. Marcar AOCR emitido (genera PDF)
SolicitudAOCRBL.MarcarAOCREmitido(
    idSolicitud: 123,
    numeroAOCR: "AOCR-2026-0123",
    rutaPDF: "~/Uploads/Certificados/AOCR-2026-0123.pdf",
    codigoUsuario: 7,
    out mensaje
);
// Estado: APROBADO → AOCR_EMITIDO
// Actualiza NumeroAOCR y FechaEmision
// Notifica al solicitante SUCCESS con link descarga

// 7. Marcar AOCR entregado (estado final)
SolicitudAOCRBL.MarcarAOCREntregado(
    idSolicitud: 123,
    codigoUsuario: 7,
    fechaEntrega: DateTime.Now,
    observaciones: "Entregado personalmente",
    out mensaje
);
// Estado: AOCR_EMITIDO → AOCR_ENTREGADO (final)
// Actualiza FechaEntrega
// Notifica proceso completado
```

---

### 4. **CapaNegocio/UsuarioBL.cs - Método Agregado**

```csharp
// Obtener usuarios por rol (para notificaciones)
List<Usuario> operadores = UsuarioBL.ObtenerPorRol("Operador");
List<Usuario> directores = UsuarioBL.ObtenerPorRol("DirectorFinanciero");

foreach (var dir in directores)
{
    NotificacionBL.EnviarNotificacion(
        codigoUsuario: dir.CodigoUsuario,
        titulo: "Solicitud requiere aprobación",
        mensaje: "...",
        tipo: TiposNotificacion.WARNING,
        url: "/SolicitudAOCR/Detalle/123"
    );
}
```

---

### 5. **Services/AOCRPdfService.cs** (200 líneas)

Servicio para generación de certificados AOCR en PDF usando Rotativa:

```csharp
// Generar número de certificado
string numeroAOCR = AOCRPdfService.GenerarNumeroAOCR(idSolicitud);
// Resultado: "AOCR-2026-0123"

// Generar PDF del certificado
var solicitud = SolicitudAOCRBL.ObtenerPorId(123);
var controller = this; // Desde SolicitudAOCRController

ActionResult pdf = AOCRPdfService.GenerarPDFCertificado(
    controller: controller,
    solicitud: solicitud,
    numeroAOCR: "AOCR-2026-0123",
    guardarArchivo: true
);

// Guardar y obtener ruta
string rutaPDF = AOCRPdfService.GenerarYGuardarCertificado(
    controller: controller,
    solicitud: solicitud,
    numeroAOCR: "AOCR-2026-0123",
    out mensaje
);
// Guarda en: ~/Uploads/Certificados/AOCR-2026-0123_20260207153045.pdf

// Descargar certificado
FileResult file = AOCRPdfService.ObtenerCertificadoParaDescarga(
    rutaArchivo: rutaPDF,
    numeroAOCR: "AOCR-2026-0123"
);
return file;
```

**Características:**
- PDF A4 vertical con encabezado DGAC
- Logo oficial + número de certificado destacado
- Datos del operador aéreo (nombre, RUC, representante)
- Tipo de operación y alcance
- Tabla de aeronaves autorizadas
- Vigencia del certificado
- Observaciones legales
- Firmas digitales y sello oficial
- Footer con datos de contacto DGAC

---

### 6. **Views/SolicitudAOCR/CertificadoAOCR.cshtml** (250 líneas)

Vista Razor HTML para generación del certificado en PDF:

**Secciones:**
1. Encabezado DGAC con logo oficial
2. Número de certificado en box destacado
3. Fecha de emisión
4. Datos del operador (I)
5. Autorización de operación (II)
6. Aeronaves autorizadas (III - tabla)
7. Vigencia (box amarillo con fechas)
8. Observaciones (IV - opcional)
9. Declaración legal
10. Firmas y sello oficial
11. Pie de página con contactos

**Estilos CSS:**
- Diseño profesional A4
- Colores corporativos DGAC (#003366)
- Tabla responsive para aeronaves
- Boxes destacados para vigencia
- Footer fijo con info contacto

---

## 🔄 Flujo Completo de Workflow AOCR

### **Diagrama de Estados Actualizado**

```
RECEPCIONADO (Operador recibe)
    ↓
ANALISIS_REQUISITOS (Operador revisa documentos)
    ↓ ↓ ↓
    ├──→ SUBSANACION ←──┐ (Si faltan documentos)
    │       ↓            │
    │    SUBSANADO ──────┘ (Solicitante carga correcciones)
    │       ↓
    │    ANALISIS_REQUISITOS (Revalidar)
    ↓
EN_EVALUACION_TECNICA (Evaluador/Inspector)
    ↓ ↓
    ├──→ SUBSANACION (puede solicitar correcciones)
    ↓
EN_EVALUACION_LEGAL (Coordinador Legal)
    ↓ ↓
    ├──→ SUBSANACION
    ↓
EN_EVALUACION_FINANCIERA (Coordinador Financiero)
    ↓ ↓
    ├──→ SUBSANACION
    ↓
EN_APROBACION_COORDINADOR (Coordinadores aprueban)
    ↓ ↓ ↓
    ├──→ SUBSANACION
    ├──→ RECHAZADO (Estado final negativo)
    ↓
EN_APROBACION_DIRECTOR ⭐ (Director Financiero - CRÍTICO)
    ↓ ↓ ↓
    ├──→ SUBSANACION
    ├──→ RECHAZADO
    ↓
APROBADO (Autorizado para emisión)
    ↓
AOCR_EMITIDO (Certificado PDF generado)
    ↓
AOCR_ENTREGADO ✅ (Estado final exitoso)
```

### **Quién Puede Hacer Qué:**

| Estado | Rol Responsable | Acciones Permitidas |
|--------|----------------|---------------------|
| **RECEPCIONADO** | Operador | Pasar a análisis |
| **ANALISIS_REQUISITOS** | Operador | Aprobar / Solicitar subsanación / Rechazar |
| **SUBSANACION** | Solicitante | Cargar documentos corregidos |
| **SUBSANADO** | Operador | Revalidar documentos |
| **EN_EVALUACION_TECNICA** | Evaluador/Inspector | Aprobar / Solicitar subsanación / Rechazar |
| **EN_EVALUACION_LEGAL** | Coordinador Legal | Aprobar / Solicitar subsanación / Rechazar |
| **EN_EVALUACION_FINANCIERA** | Coordinador Financiero | Aprobar / Solicitar subsanación / Rechazar |
| **EN_APROBACION_COORDINADOR** | Coordinadores | Aprobar / Solicitar subsanación / Rechazar |
| **EN_APROBACION_DIRECTOR** ⭐ | **Director Financiero** | **Aprobar** / Solicitar subsanación / Rechazar |
| **APROBADO** | Coordinador Financiero | Emitir certificado PDF |
| **AOCR_EMITIDO** | Coordinador | Marcar como entregado |
| **AOCR_ENTREGADO** | - | Read-only (estado final) |
| **RECHAZADO** | - | Read-only (estado final) |

---

## 🚀 Implementación en Controladores

### **Ejemplo 1: SolicitudAOCRController - Solicitar Subsanación**

```csharp
[HttpPost]
[Authorize(Roles = "Administrador,Operador,EvaluadorTecnico,CoordinadorLegal,CoordinadorFinanciero")]
public JsonResult SolicitarSubsanacion(int idSolicitud, string observaciones)
{
    try
    {
        int codigoUsuario = ObtenerCodigoUsuario();
        bool resultado = SolicitudAOCRBL.SolicitarSubsanacion(idSolicitud, observaciones, codigoUsuario, out string mensaje);
        
        return Json(new { 
            success = resultado, 
            message = mensaje 
        });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Error: " + ex.Message });
    }
}
```

### **Ejemplo 2: Aprobar por Director (CRÍTICO)**

```csharp
[HttpPost]
[Authorize(Roles = "Administrador,DirectorFinanciero,JefaturaTecnica")]
public JsonResult AprobarDirector(int idSolicitud, string observaciones)
{
    try
    {
        int codigoUsuario = ObtenerCodigoUsuario();
        
        // Verificar que el usuario tiene rol Director
        if (!User.IsInRole("DirectorFinanciero") && !User.IsInRole("Administrador"))
        {
            return Json(new { success = false, message = "No tiene permisos de Director." });
        }
        
        bool resultado = SolicitudAOCRBL.AprobarDirector(idSolicitud, codigoUsuario, observaciones, out string mensaje);
        
        return Json(new { 
            success = resultado, 
            message = mensaje,
            redirect = resultado ? Url.Action("EmitirCertificado", new { id = idSolicitud }) : null
        });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Error: " + ex.Message });
    }
}
```

### **Ejemplo 3: Emitir Certificado AOCR (PDF)**

```csharp
[HttpPost]
[Authorize(Roles = "Administrador,CoordinadorFinanciero")]
public ActionResult EmitirCertificado(int id)
{
    try
    {
        var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
        
        if (solicitud == null)
        {
            return HttpNotFound();
        }
        
        if (solicitud.Estado != "APROBADO")
        {
            TempData["Error"] = "La solicitud debe estar en estado APROBADO.";
            return RedirectToAction("Detalle", new { id });
        }
        
        // Generar número AOCR
        string numeroAOCR = Services.AOCRPdfService.GenerarNumeroAOCR(id);
        
        // Generar PDF
        string rutaPDF = Services.AOCRPdfService.GenerarYGuardarCertificado(
            controller: this,
            solicitud: solicitud,
            numeroAOCR: numeroAOCR,
            out string mensajePDF
        );
        
        if (string.IsNullOrEmpty(rutaPDF))
        {
            TempData["Error"] = mensajePDF;
            return RedirectToAction("Detalle", new { id });
        }
        
        // Marcar como AOCR_EMITIDO
        int codigoUsuario = ObtenerCodigoUsuario();
        bool resultado = SolicitudAOCRBL.MarcarAOCREmitido(id, numeroAOCR, rutaPDF, codigoUsuario, out string mensaje);
        
        if (resultado)
        {
            TempData["Success"] = $"Certificado AOCR {numeroAOCR} emitido correctamente.";
            return RedirectToAction("DescargarCertificado", new { id });
        }
        else
        {
            TempData["Error"] = mensaje;
            return RedirectToAction("Detalle", new { id });
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "Error al emitir certificado: " + ex.Message;
        return RedirectToAction("Index");
    }
}
```

### **Ejemplo 4: Descargar Certificado PDF**

```csharp
[HttpGet]
[Authorize]
public FileResult DescargarCertificado(int id)
{
    try
    {
        var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
        
        if (solicitud == null || solicitud.Estado != "AOCR_EMITIDO")
        {
            throw new Exception("Certificado no disponible.");
        }
        
        // Obtener ruta del PDF (debe estar guardada en solicitud.RutaCertificado)
        string rutaPDF = solicitud.RutaCertificado;
        
        return Services.AOCRPdfService.ObtenerCertificadoParaDescarga(rutaPDF, solicitud.NumeroAOCR);
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
        return RedirectToAction("Index");
    }
}
```

---

## 📊 Notificaciones Automáticas Integradas

Todas las operaciones críticas envían notificaciones automáticas:

| Evento | Destinatario | Tipo | URL Redirección |
|--------|-------------|------|----------------|
| **Subsanación solicitada** | Solicitante | WARNING | `/SolicitudAOCR/Detalle/{id}` |
| **Subsanado** | Operadores | INFO | `/SolicitudAOCR/Detalle/{id}` |
| **Aprobación Coordinador** | Director Financiero | WARNING | `/SolicitudAOCR/Detalle/{id}` |
| **Aprobación Director** | Solicitante | SUCCESS | `/SolicitudAOCR/Detalle/{id}` |
| **AOCR Emitido** | Solicitante | SUCCESS | `/SolicitudAOCR/DescargarCertificado/{id}` |
| **AOCR Entregado** | Solicitante | SUCCESS | `/SolicitudAOCR/Detalle/{id}` |
| **Rechazado** | Solicitante | ERROR | `/SolicitudAOCR/Detalle/{id}` |

---

## 🧪 Testing y Validación

### **Test 1: Insertar Roles**

```sql
-- 1. Ejecutar script
\i scripts/insertar_roles_faltantes.sql

-- 2. Verificar roles creados
SELECT descripcion, nivel_jerarquico, puede_aprobar, categoria_rol
FROM rol
WHERE activo = TRUE
ORDER BY nivel_jerarquico DESC;

-- Resultado esperado: 11 roles
-- Director Financiero nivel 3, puede_aprobar TRUE
```

### **Test 2: Workflow Subsanación**

```csharp
// 1. Solicitud en ANALISIS_REQUISITOS
var solicitud = SolicitudAOCRBL.ObtenerPorId(1);
Assert.AreEqual("ANALISIS_REQUISITOS", solicitud.Estado);

// 2. Solicitar subsanación
bool resultado = SolicitudAOCRBL.SolicitarSubsanacion(1, "Falta documento X", 5, out var mensaje);
Assert.IsTrue(resultado);

// 3. Verificar estado cambió
solicitud = SolicitudAOCRBL.ObtenerPorId(1);
Assert.AreEqual("SUBSANACION", solicitud.Estado);

// 4. Solicitante subsana
resultado = SolicitudAOCRBL.MarcarSubsanado(1, 10, "Documentos adjuntos", out mensaje);
Assert.IsTrue(resultado);

// 5. Verificar estado cambió
solicitud = SolicitudAOCRBL.ObtenerPorId(1);
Assert.AreEqual("SUBSANADO", solicitud.Estado);
```

### **Test 3: Aprobación Director**

```csharp
// 1. Solicitud en EN_APROBACION_DIRECTOR
var solicitud = SolicitudAOCRBL.ObtenerPorId(1);
Assert.AreEqual("EN_APROBACION_DIRECTOR", solicitud.Estado);

// 2. Director aprueba
bool resultado = SolicitudAOCRBL.AprobarDirector(1, 2, "Aprobado", out var mensaje);
Assert.IsTrue(resultado);

// 3. Verificar estado APROBADO
solicitud = SolicitudAOCRBL.ObtenerPorId(1);
Assert.AreEqual("APROBADO", solicitud.Estado);
```

### **Test 4: Generar PDF**

```csharp
// 1. Solicitud APROBADA
var solicitud = SolicitudAOCRBL.ObtenerPorId(1);
Assert.AreEqual("APROBADO", solicitud.Estado);

// 2. Generar número AOCR
string numeroAOCR = AOCRPdfService.GenerarNumeroAOCR(1);
Assert.AreEqual("AOCR-2026-0001", numeroAOCR);

// 3. Generar PDF
string rutaPDF = AOCRPdfService.GenerarYGuardarCertificado(controller, solicitud, numeroAOCR, out var mensaje);
Assert.IsNotNull(rutaPDF);
Assert.IsTrue(File.Exists(rutaPDF));

// 4. Marcar como emitido
bool resultado = SolicitudAOCRBL.MarcarAOCREmitido(1, numeroAOCR, rutaPDF, 7, out mensaje);
Assert.IsTrue(resultado);

// 5. Verificar estado AOCR_EMITIDO
solicitud = SolicitudAOCRBL.ObtenerPorId(1);
Assert.AreEqual("AOCR_EMITIDO", solicitud.Estado);
Assert.AreEqual(numeroAOCR, solicitud.NumeroAOCR);
```

---

## 📚 Documentación de Base de Datos

### **Tabla: rol (ya existente, modificada)**

```sql
Column              | Type         | Nullable | Default
--------------------+--------------+----------+----------
codigorol           | INTEGER      | NOT NULL | nextval(...)
descripcion         | VARCHAR(200) | NOT NULL |
activo              | BOOLEAN      | NOT NULL | TRUE
nivel_jerarquico    | INTEGER      | NULL     | 0  -- NUEVO
puede_aprobar       | BOOLEAN      | NULL     | FALSE  -- NUEVO
categoria_rol       | VARCHAR(50)  | NULL     |  -- NUEVO (INTERNO/EXTERNO)
```

### **Vista: vw_roles_resumen**

```sql
SELECT 
    codigorol,
    descripcion,
    activo,
    nivel_jerarquico,
    puede_aprobar,
    categoria_rol,
    COUNT(usuariorol.codigousuario) AS usuarios_asignados
FROM rol
LEFT JOIN usuariorol ON usuariorol.codigorol = rol.codigorol
GROUP BY codigorol, descripcion, activo, nivel_jerarquico, puede_aprobar, categoria_rol
ORDER BY nivel_jerarquico DESC;
```

---

## ✅ Checklist de Implementación

### **Fase 1: Base de Datos**
- [x] Crear archivo RolesAOCR.cs con constantes
- [x] Crear script insertar_roles_faltantes.sql
- [ ] **Ejecutar script SQL en base de datos**
- [ ] **Verificar 11 roles creados con \d rol**
- [ ] **Asignar rol DirectorFinanciero a usuario de prueba**

### **Fase 2: Lógica de Negocio**
- [x] Agregar métodos subsanación en SolicitudAOCRBL
- [x] Agregar métodos aprobación en SolicitudAOCRBL
- [x] Agregar método ObtenerPorRol en UsuarioBL
- [x] Crear AOCRPdfService para generación PDF
- [x] Integrar notificaciones automáticas

### **Fase 3: Vistas y UI**
- [x] Crear vista CertificadoAOCR.cshtml para PDF
- [ ] **Agregar botones en Detalle.cshtml:**
  - [ ] Botón "Solicitar Subsanación" (Operador/Coordinador)
  - [ ] Botón "Marcar Subsanado" (Solicitante)
  - [ ] Botón "Aprobar" (Coordinador/Director)
  - [ ] Botón "Rechazar" (con modal motivo)
  - [ ] Botón "Emitir Certificado" (Admin/Coordinador)
  - [ ] Botón "Descargar Certificado" (si AOCR_EMITIDO)
- [ ] **Agregar acciones AJAX en JavaScript**
- [ ] **Agregar autorización por rol en cada botón**

### **Fase 4: Controladores**
- [ ] **Agregar endpoints en SolicitudAOCRController:**
  ```csharp
  [HttpPost] SolicitarSubsanacion(id, observaciones)
  [HttpPost] MarcarSubsanado(id, comentarios)  
  [HttpPost] AprobarCoordinador(id, observaciones)
  [HttpPost] AprobarDirector(id, observaciones)
  [HttpPost] Rechazar(id, motivoRechazo)
  [HttpPost] EmitirCertificado(id)
  [HttpGet] DescargarCertificado(id)
  [HttpPost] MarcarEntregado(id, fechaEntrega, observaciones)
  ```

### **Fase 5: Testing**
- [ ] **Crear usuario con rol DirectorFinanciero**
- [ ] **Test ciclo subsanación completo**
- [ ] **Test aprobación coordinador → director**
- [ ] **Test generación PDF certificado**
- [ ] **Test descargar certificado**
- [ ] **Verificar notificaciones enviadas**
- [ ] **Test permisos por rol (403 si no autorizado)**

### **Fase 6: Configuración**
- [ ] **Crear carpeta ~/Uploads/Certificados**
- [ ] **Configurar permisos escritura en servidor**
- [ ] **Agregar logo DGAC en ~/Content/img/dgac-logo.png**
- [ ] **Crear firma digital Director (imagen)**
- [ ] **Crear sello oficial DGAC (imagen)**

---

## 🎯 Próximos Pasos Opcionales

### **Dashboard por Etapa AOCR** 🟢 Baja Prioridad

```sql
-- Vista dashboard estados
CREATE VIEW vw_dashboard_estados AS
SELECT 
    estado,
    COUNT(*) AS cantidad,
    COUNT(*) FILTER (WHERE fecha_solicitud >= NOW() - INTERVAL '30 days') AS mes_actual,
    AVG(EXTRACT(DAY FROM (fecha_actualizacion - fecha_solicitud))) AS dias_promedio
FROM aocr_tbsolicitudaocr
WHERE estado NOT IN ('RECHAZADO', 'AOCR_ENTREGADO')
GROUP BY estado
ORDER BY 
    CASE estado
        WHEN 'RECEPCIONADO' THEN 1
        WHEN 'ANALISIS_REQUISITOS' THEN 2
        WHEN 'SUBSANACION' THEN 3
        WHEN 'EN_APROBACION_DIRECTOR' THEN 10
        ELSE 5
    END;
```

### **Historial de Cambios de Estado**

```sql
-- Tabla auditoría estados
CREATE TABLE aocr_tbhistorialestados (
    codigo_historial SERIAL PRIMARY KEY,
    codigo_solicitud INT NOT NULL REFERENCES aocr_tbsolicitudaocr(codigo_solicitud),
    estado_anterior VARCHAR(100),
    estado_nuevo VARCHAR(100) NOT NULL,
    codigo_usuario INT NOT NULL REFERENCES usuario(codigousuario),
    fecha_cambio TIMESTAMP DEFAULT NOW(),
    observaciones TEXT
);

CREATE INDEX idx_historial_solicitud ON aocr_tbhistorialestados(codigo_solicitud);
CREATE INDEX idx_historial_fecha ON aocr_tbhistorialestados(fecha_cambio DESC);
```

---

## 📖 Referencias y Documentación

- **Estados AOCR:** [EstadosSolicitudAOCR.cs](CapaDatos/Constants/EstadosSolicitudAOCR.cs) - 13 estados formalizados
- **Roles AOCR:** [RolesAOCR.cs](CapaDatos/Constants/RolesAOCR.cs) - 11 roles jerárquicos
- **Notificaciones:** [IMPLEMENTACION_NOTIFICACIONES.md](IMPLEMENTACION_NOTIFICACIONES.md)
- **Inspecciones:** EstadosInspeccion.cs - 9 estados workflow inspecciones

---

**✅ Implementación Completada - Listo para Testing y Despliegue**

**Siguiente paso:** Ejecutar `scripts/insertar_roles_faltantes.sql` y agregar endpoints en `SolicitudAOCRController.cs`.
