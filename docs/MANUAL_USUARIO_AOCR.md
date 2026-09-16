# Manual de Usuario — Sistema AOCR (Simplificación y Emisión AOCR)

**Dirección General de Aviación Civil (DGAC) — Ecuador**  
**Proyecto:** DGAC-006-05-02 *Otorgamiento del Reconocimiento del Certificado de Explotador de Servicios Aéreos (AOCR)*  
**Versión:** 2.0 (Septiembre 2026 — Incorporación Acciones Correctivas AC-01 a AC-12)  
**Entorno:** ASP.NET MVC 5 (.NET Framework 4.8) / PostgreSQL

---

## 1. Introducción y Propósito

El **Sistema AOCR** automatiza y simplifica el trámite de emisión, renovación y modificación de autorizaciones y reconocimientos a operadores aéreos extranjeros. Este manual proporciona las directrices operativas paso a paso para cada uno de los roles institucionales y externos que intervienen en el flujo del proceso.

---

## 2. Roles y Actores del Sistema

El acceso al sistema está segmentado estrictamente por roles operativos y de seguridad:

```
 [Operador Aéreo / RT]  ──>  [Coordinación de Inspección]  ──>  [Inspector Técnico Asignado]
          ▲                                                                 │
          │                                                                 ▼
 [Entrega Final AC-12] <── [DIRDAC: Firma AOCR] <── [DIRCAV: Firma CL] <── [Informe & Acta]
```

| Rol Institucional | Perfil / Denominación | Responsabilidad Principal |
| :--- | :--- | :--- |
| **Representante Técnico (RT)** | Solicitante / Operador Extranjero | Carga de solicitud, carga de requisitos documentales, pago de tasas, recepción de documentos legalizados. |
| **Financiero** | Recaudación y Tesorería | Validación de comprobantes de pago y emisión de facturas. |
| **Coordinador** | Jefatura / Coordinación de Inspecciones | Revisión previa de documentos, aceptación formal, emisión de designación y asignación de inspectores. |
| **Inspector Técnico** | Operaciones (OPS) / Aeronavegabilidad (AIR) | Revisión técnica documental, ejecución de listas de verificación por estación, emisión de informe técnico y no conformidades. |
| **DIRCAV** | Director de Certificación Aeronáutica | Revisión final técnica, aprobación y firma electrónica del documento de **Condiciones y Limitaciones (C&L)**. |
| **DIRDAC** | Director General de Aviación Civil | Máxima autoridad: revisión institucional, devolución motivada o firma/legalización final del **Certificado AOCR**. |

---

## 3. Guía Operativa por Rol

### 3.1 Representante Técnico (RT / Operador Extranjero)

#### A. Ingreso y Registro de Solicitud
1. Ingrese al portal con su usuario y contraseña asignados.
2. En el menú lateral izquierdo, seleccione el tipo de trámite:
   * **Solicitud AOCR (Emisión inicial):** Para compañías que operan por primera vez.
   * **Renovación AOCR:** Prórroga de vigencia de reconocimiento existente.
   * **Condiciones y Limitaciones:** Para modificaciones (inclusión de nuevas aeronaves, nuevas rutas/estaciones o cambio de RT).
3. Complete los datos de la aerolínea, flota de aeronaves, tripulaciones y las **estaciones/aeropuertos en Ecuador** donde operará.
4. Presione **"Guardar y Continuar"**. El sistema generará el número oficial de trámite (ej. `DGAC-GOP-2026-AOCR0XX`).

#### B. Carga de Documentos Requeridos
1. Diríjase a la sección **"Documentos y Expediente"**.
2. Cargue en formato PDF cada uno de los requisitos obligatorios:
   * Copia de AOC del país de origen vigente.
   * Especificaciones Operacionales (OpSpecs).
   * Manual de Operaciones / Manual de Vuelo.
   * Certificados de Aeronavegabilidad y Matrícula de aeronaves.
   * Pólizas de seguro vigentes para Ecuador.
   * Permiso de Operación CNAC y Poder legal del Representante en Ecuador.
3. Una vez cargados todos los archivos, presione **"Enviar a Revisión Institucional"**.

#### C. Subsanación de Documentos (En caso de devolución)
* Si un documento es observado o devuelto por el Coordinador o el Inspector, recibirá una notificación automática por correo.
* Ingrese a **"Mis Trámites"**, abra la solicitud y en la pestaña de documentos identifique los marcados como **"DEVUELTO"**.
* Cargue la versión corregida y presione **"Reenviar Documentos Subsanados"**.

#### D. Descarga de Documentos Finales Legalizados (AC-12)
* Cuando el trámite culmine con las firmas del Director General y Director de Certificación, recibirá una notificación de disponibilidad.
* En su bandeja de **"Mis Trámites"**, aparecerá la sección **"Documentos Legalizados Disponibles"**.
* Podrá descargar con verificación de autenticidad:
  1. **Certificado AOCR Oficial** (firmado por DIRDAC).
  2. **Hoja de Condiciones y Limitaciones** (firmada por DIRCAV).

---

### 3.2 Rol Financiero (Recaudación)
1. Ingrese a **"Financiero" > "Órdenes de Recaudación"**.
2. Localice el trámite por número de solicitud o nombre del operador.
3. Verifique el comprobante de depósito/transferencia bancaria cargado por el RT.
4. Ingrese el número de comprobante/factura institucional y presione **"Aprobar Pago"**.
5. El sistema habilitará inmediatamente el expediente para que Coordinación proceda con la asignación.

---

### 3.3 Coordinador de Inspecciones (`GEN_COORDINACION`)

#### A. Aceptación Formal de Documentos y Designación (AC-05 / AC-06)
1. Ingrese a **"Coordinación" > "Bandeja Integral"** o **"Revisión y Verificación"**.
2. Seleccione la solicitud pendiente.
3. Verifique el cumplimiento preliminar de la documentación base.
4. Presione **"Asignar Inspector"** (`/Tecnico/AsignarInspector`):
   * Seleccione el inspector técnico responsable (ej. Operaciones o Aeronavegabilidad).
   * Defina las **fechas individuales y cronograma programado para cada estación declarada** (AC-02).
5. Al confirmar, el sistema genera de forma automática el **Acta de Designación en PDF con firma institucional** y notifica al inspector designado y al operador.

---

### 3.4 Inspector Técnico Asignado

#### A. Revisión Técnica Documental Especializada
1. Ingrese a **"Dashboard Técnico" > "Revisión Documental"**.
2. Abra la solicitud asignada. Se mostrará la lista de documentos con estado **"PENDIENTE"**.
3. Revise cada archivo y seleccione para cada uno:
   * **ACEPTAR:** Si cumple cabalmente con las regulaciones aeronáuticas (RDAC).
   * **DEVOLVER / OBSERVAR:** Si está incompleto o desactualizado. Ingrese de forma obligatoria el motivo detallado de la observación.
4. Una vez calificados todos los documentos, presione **"Confirmar Cierre Documental"**. Esto desbloqueará la fase operativa de inspección.

#### B. Listas de Verificación Operacional (LV / EAE por Estación — AC-07 / AC-08)
1. En el menú de la inspección, ingrese a **"Lista de Verificación LV/EAE"**.
2. **Seleccione la estación a inspeccionar:** Cada estación tiene su propia lista de chequeo independiente.
3. Califique cada ítem de la lista:
   * `SATISFACTORIO`
   * `NO SATISFACTORIO`
   * `NO APLICA`
4. **Regla de integridad:** El sistema no permitirá guardar ni firmar la lista hasta que el **100% de los elementos** hayan sido calificados y los no satisfactorios cuenten con su respectivo hallazgo redactado.
5. Firme electrónicamente la lista de chequeo de la estación.

#### C. Informe Técnico y Conclusiones (AC-09)
1. Ingrese a **"Informe Técnico"**.
2. Redacte el análisis técnico de las inspecciones realizadas.
3. En caso de existir **No Conformidades**, regístrelas con su plazo máximo de subsanación.
4. Seleccione el dictamen: **FAVORABLE** o **DESFAVORABLE**.
5. Presione **"Firmar y Remitir a Dirección"**. El expediente avanzará a la bandeja de DIRCAV.

---

### 3.5 Director de Certificación Aeronáutica y Vigilancia Continua (DIRCAV — AC-11)

1. Ingrese con el perfil **DIRCAV** a la bandeja **"Validar AOCR / Condiciones y Limitaciones"**.
2. Revise el expediente completo: Informe Técnico del Inspector, Listas de Verificación firmadas y documentos habilitantes.
3. **Generación y Firma de Condiciones y Limitaciones (C&L):**
   * El sistema genera el documento oficial de Condiciones y Limitaciones con las especificaciones de aeronaves, rutas y bases operacionales aprobadas.
   * Realice la firma electrónica del documento C&L.
4. **Remisión al Director General:**
   * Una vez firmadas las Condiciones y Limitaciones, presione el botón **"Remitir AOCR a DIRDAC"**.
   * El estado del expediente pasará a `AOCR_PENDIENTE_DIRDAC`.
   * *(En caso de identificar errores del inspector, puede presionar "Devolver a Coordinador" con la debida justificación).*

---

### 3.6 Director General de Aviación Civil (DIRDAC — AC-11)

1. Ingrese con el perfil **DIRDAC** a la bandeja **"Bandeja AOCR Dirección General"** (`/Dirdac/BandejaAocr`).
2. En la lista de trámites pendientes, seleccione el expediente a legalizar.
3. En la pantalla de detalle (`/Dirdac/Detalle`), podrá constatar:
   * El Certificado AOCR generado en borrador institucional.
   * El documento de Condiciones y Limitaciones debidamente **firmado por DIRCAV**.
   * El informe técnico consolidado y evidencias.
4. **Acciones disponibles:**
   * **Devolver a DIRCAV:** Si requiere aclaración o corrección técnica de fondo. El expediente regresa a DIRCAV con la observación formal.
   * **Firmar y Legalizar AOCR:** Aplica la firma electrónica de la Máxima Autoridad sobre el Certificado AOCR.
5. **Cierre y Entrega Automática:**
   * Al registrarse la firma del DIRDAC, el sistema consolida las firmas completas, activa la entrega digital segura (AC-12) y notifica automáticamente al operador aéreo y a los inspectores.

---

## 4. Preguntas Frecuentes y Solución de Problemas

| Situación / Pregunta | Causa Común | Solución Operativa |
| :--- | :--- | :--- |
| **No puedo ver el botón para evaluar la Lista de Verificación (LV)** | El inspector no ha realizado el cierre formal de la revisión documental. | Ingrese a la revisión documental de la solicitud y presione *"Confirmar Cierre Documental"*. |
| **El sistema me indica "Error 403 / No autorizado" al firmar el AOCR** | Se intenta firmar el Certificado AOCR con un rol distinto a DIRDAC (ej. Administrador o Inspector). | Ingrese estrictamente con el usuario y rol institucional asignado a la Dirección General. |
| **No se permite guardar la Lista de Verificación** | Existen preguntas o ítems sin calificar en la lista de chequeo de la estación. | Revise que todos los ítems tengan seleccionada una opción (Satisfactorio / No Satisfactorio / No Aplica). |
| **El operador no recibe el correo de finalización** | La cola de correos realiza reintentos automáticos si el servidor receptor presenta demoras. | El operador puede ingresar directamente al sistema con sus credenciales y descargar los documentos en la sección *"Mis Trámites"*. |

---
*Manual actualizado por la Dirección de Tecnologías de la Información y Comunicación (DTIC) — DGAC Ecuador.*
