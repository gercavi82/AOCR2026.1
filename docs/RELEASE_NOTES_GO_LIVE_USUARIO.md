# Release Notes — Go-Live Sistema AOCR

**Versión:** 1.0.0.4  
**Fecha objetivo go-live:** _pendiente_  
**Audiencia:** Usuarios RT, Inspector, Coordinación, Financiero, DIRDAC

---

## Novedades principales

- Flujo integral emisión AOCR: orden de recaudación → documentación → inspección → informe → firma DIRDAC → certificado PDF.
- Bandejas por rol con contadores en sidebar (RT, Inspector, Coordinación, Financiero, Dirección).
- Revisión documental del inspector con modos `revision` / `ver` y cierre documental obligatorio antes de LV.
- Lista de Verificación (LV) e Informe Técnico con firma electrónica (.p12).
- Modificación tipo 3 (nuevo aeropuerto) y ramas Condiciones/Limitaciones.
- Notificaciones por correo institucional desde `no_reply@aviacioncivil.gob.ec`.
- Endurecimiento de seguridad: acceso por rol, URL directa bloqueada, auditoría de intentos.

---

## Mejoras de estabilidad

- Correos idempotentes (sin duplicados por reintentos).
- Normalización de estados AOCR.
- Health checks: `/Health/Live`, `/Health/Ready`, `/Health/Details`.
- Autorización unificada en acciones críticas de inspección y documentos.

---

## Requisitos de acceso

| Rol | Función principal |
|-----|-------------------|
| RT / Solicitante | Crear solicitud, cargar documentos, subsanar observaciones |
| Financiero | Validar pagos y órdenes de recaudación |
| Coordinación | Revisar documentación, asignar inspector, validar AOCR |
| Inspector | Revisión documental, LV, informe técnico, NC |
| DIRDAC | Aprobar informe y firmar AOCR final |

Seleccione el **rol activo** en la barra superior antes de operar.

---

## Soporte

- Manual de usuario: contactar mesa de ayuda DGAC / equipo AOCR.
- Incidencias post go-live: registrar ticket con captura, rol, número de solicitud y hora.

---

*Documento borrador — completar fecha y URL producción el día del go-live.*
