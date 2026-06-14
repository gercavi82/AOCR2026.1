# Exportación — Manuales AOCR

**Fuente canónica:** archivos `.md` en `docs/`.

## Regenerar todo

```powershell
cd docs\export
npm install
npm run build
```

| Salida | Ubicación |
|--------|-----------|
| PDF | `docs/export/*.pdf` |
| HTML | `docs/export/*.html` |
| Word | `docs/export/editable/*.docx` |
| Markdown copia | `docs/export/editable/*.md` |

## Documentos incluidos (8)

| ID | Contenido |
|----|-----------|
| `MANUAL_USUARIO_AOCR` | Manual usuario |
| `MANUAL_FLUJO_RT_A_AOCR` | Flujo 16 fases RT→AOCR |
| `GUIA_VISUAL_FLUJO_RT_AOCR` | 42 capturas + textos UI |
| `CHECKLIST_DOCUMENTACION_100` | Checklist maestro 100% |
| `MANUAL_TECNICO_AOCR` | Técnico §16–§18 |
| `GUIA_INSPECTOR_SOLICITUD_12` | Inspector #12 |
| `GUIA_VISUAL_POR_ROL` | Matrices por rol |
| `HOJA_RUTA_PUBLICACION` | Hoja de ruta go-live |

## Seguimiento publicación (Excel)

| Archivo | Uso |
|---------|-----|
| [editable/SEGUIMIENTO_PUBLICACION_AOCR.csv](editable/SEGUIMIENTO_PUBLICACION_AOCR.csv) | Plantilla seguimiento semanal (~75 tareas) |
| [editable/SEGUIMIENTO_PUBLICACION_README.md](editable/SEGUIMIENTO_PUBLICACION_README.md) | Instrucciones de uso |

## Capturas

Guardar PNG en `docs/images/` según [images/README.md](../images/README.md), luego `npm run build`.
