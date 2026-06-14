import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { marked } from "marked";
import HTMLtoDOCX from "@turbodocx/html-to-docx";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const docsDir = path.resolve(__dirname, "..");
const exportDir = __dirname;
const editableDir = path.join(exportDir, "editable");

const VERSION = "2026-06-11";

const manuals = [
  {
    id: "MANUAL_USUARIO_AOCR",
    source: path.join(docsDir, "MANUAL_USUARIO_AOCR.md"),
    title: "Manual de usuario",
    subtitle: "Sistema AOCR — implementación actual",
    coverLead:
      "Roles, menús, estados, revisión documental inspector vs coordinador, LV, informe, modificación tipo 3 y checklist solicitud #12.",
    theme: "usuario",
  },
  {
    id: "MANUAL_TECNICO_AOCR",
    source: path.join(docsDir, "MANUAL_TECNICO_AOCR.md"),
    title: "Manual técnico",
    subtitle: "Sistema AOCR — auditoría de implementación",
    coverLead:
      "Arquitectura, servicios de flujo, revisión documental, LV/EAE (§16), informe técnico (§17), modificación tipo 3 (§18), despliegue y checklist QA.",
    theme: "tecnico",
  },
  {
    id: "GUIA_INSPECTOR_SOLICITUD_12",
    source: path.join(docsDir, "GUIA_INSPECTOR_SOLICITUD_12.md"),
    title: "Guía inspector",
    subtitle: "Solicitud #12 — paso a paso",
    coverLead:
      "Procedimiento verificable para DGAC-GOP-2026-AOCR012, inspección #11, inspector id 43.",
    theme: "guia",
  },
  {
    id: "GUIA_VISUAL_POR_ROL",
    source: path.join(docsDir, "GUIA_VISUAL_POR_ROL.md"),
    title: "Guía visual por rol",
    subtitle: "Textos UI y endpoints exactos",
    coverLead:
      "Referencia de pantallas por rol: RT, financiero, coordinación, inspector, DIRDAC.",
    theme: "guia",
  },
  {
    id: "MANUAL_FLUJO_RT_A_AOCR",
    source: path.join(docsDir, "MANUAL_FLUJO_RT_A_AOCR.md"),
    title: "Flujo completo RT → AOCR",
    subtitle: "Desde el RT hasta la emisión final",
    coverLead:
      "16 fases institucionales: RT, financiero, coordinación, inspector, DIRDAC y descarga final en GeneradasFirmadas.",
    theme: "usuario",
  },
  {
    id: "GUIA_VISUAL_FLUJO_RT_AOCR",
    source: path.join(docsDir, "GUIA_VISUAL_FLUJO_RT_AOCR.md"),
    title: "Guía visual flujo RT → AOCR",
    subtitle: "42 capturas · 16 fases · textos UI exactos",
    coverLead:
      "Validación pantalla por pantalla desde formulario RT hasta GeneradasFirmadas y firma DIRDAC.",
    theme: "guia",
  },
  {
    id: "CHECKLIST_DOCUMENTACION_100",
    source: path.join(docsDir, "CHECKLIST_DOCUMENTACION_100.md"),
    title: "Checklist documentación 100%",
    subtitle: "Artefactos · capturas · E2E · export",
    coverLead:
      "Lista maestra para cerrar documentación, PNG, prueba manual #12 y exportaciones PDF/DOCX.",
    theme: "guia",
  },
  {
    id: "HOJA_RUTA_PUBLICACION",
    source: path.join(docsDir, "HOJA_RUTA_PUBLICACION.md"),
    title: "Hoja de ruta — Publicación oficial",
    subtitle: "Gates A–E · infra · go-live",
    coverLead:
      "Todo lo pendiente para pasar de piloto publicacion1 a producción institucional aprobada.",
    theme: "guia",
  },
  {
    id: "PENDIENTES_PUBLICACION_AOCR",
    source: path.join(docsDir, "PENDIENTES_PUBLICACION_AOCR.md"),
    title: "Pendientes publicación AOCR",
    subtitle: "Todo lo que falta — consolidado",
    coverLead:
      "Bloqueantes, Gates A–E, infraestructura, checklist prod, deuda técnica y plan 4 semanas.",
    theme: "guia",
  },
];

const styles = {
  usuario: {
    brand: "#0f617a",
    brand2: "#1296b8",
    light: "#eef8fb",
  },
  tecnico: {
    brand: "#1e3a5f",
    brand2: "#2d5a87",
    light: "#eef2f7",
  },
  guia: {
    brand: "#2c5282",
    brand2: "#4299e1",
    light: "#ebf4ff",
  },
};

marked.setOptions({
  gfm: true,
  breaks: false,
});

function extractTitle(md) {
  const m = md.match(/^#\s+(.+)$/m);
  return m ? m[1].trim() : "Documento AOCR";
}

function stripLeadingH1(md) {
  return md.replace(/^#\s+.+\n+/, "");
}

function buildHtml(manual, bodyHtml) {
  const t = styles[manual.theme];
  const docTitle = `${manual.title} — Sistema AOCR`;
  return `<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${docTitle}</title>
  <style>
    :root { --brand:${t.brand}; --brand2:${t.brand2}; --light:${t.light}; --border:#cfd8e3; --text:#1a2332; --muted:#5c6b7a; }
    * { box-sizing:border-box; }
    body { margin:0; font-family:"Segoe UI",Tahoma,sans-serif; color:var(--text); line-height:1.55; font-size:10.5pt; background:#eef1f5; }
    .page { max-width:940px; margin:0 auto; background:#fff; box-shadow:0 0 20px rgba(0,0,0,.07); }
    .cover { padding:56px 64px; background:linear-gradient(135deg,var(--brand),var(--brand2) 50%,var(--light)); color:#fff; page-break-after:always; min-height:88vh; display:flex; flex-direction:column; justify-content:center; }
    .cover h1 { font-size:2.15rem; margin:12px 0 8px; line-height:1.2; }
    .cover .subtitle { font-size:1.05rem; opacity:.95; max-width:620px; }
    .cover .lead { margin-top:20px; max-width:640px; opacity:.92; font-size:.98rem; }
    .cover .meta { margin-top:36px; font-size:.9rem; opacity:.88; }
    .content { padding:40px 52px 56px; }
    .no-print { background:#fff3cd; border:1px solid #ffc107; padding:12px 14px; border-radius:6px; margin-bottom:22px; font-size:10pt; }
    .md-body h1 { color:var(--brand); font-size:1.45rem; border-bottom:2px solid var(--brand); padding-bottom:6px; margin-top:2rem; page-break-after:avoid; }
    .md-body h1:first-child { margin-top:0; }
    .md-body h2 { color:var(--brand2); font-size:1.12rem; margin-top:1.5rem; page-break-after:avoid; }
    .md-body h3 { font-size:1rem; color:#334; margin-top:1.1rem; page-break-after:avoid; }
    .md-body h4 { font-size:.95rem; margin-top:.9rem; }
    .md-body p, .md-body li { margin:.45rem 0; }
    .md-body ul, .md-body ol { padding-left:1.35rem; }
    .md-body table { width:100%; border-collapse:collapse; margin:12px 0; font-size:9.5pt; page-break-inside:avoid; }
    .md-body th, .md-body td { border:1px solid var(--border); padding:7px 9px; text-align:left; vertical-align:top; }
    .md-body th { background:var(--light); }
    .md-body code { font-family:Consolas,monospace; font-size:9pt; background:#f4f6f8; padding:1px 4px; border-radius:3px; }
    .md-body pre { font-family:Consolas,monospace; font-size:9pt; background:#f4f6f8; padding:12px; border-radius:6px; overflow-x:auto; white-space:pre-wrap; page-break-inside:avoid; }
    .md-body pre code { background:transparent; padding:0; }
    .md-body blockquote { border-left:4px solid var(--brand); background:var(--light); margin:12px 0; padding:8px 14px; border-radius:0 6px 6px 0; }
    .md-body hr { border:none; border-top:1px solid var(--border); margin:24px 0; }
    .md-body a { color:var(--brand2); }
    .md-body img { max-width:100%; height:auto; border:1px solid var(--border); border-radius:6px; margin:10px 0; page-break-inside:avoid; }
    .footer { margin-top:36px; padding-top:14px; border-top:1px solid var(--border); font-size:9pt; color:var(--muted); }
    @media print {
      body { background:#fff; }
      .page { box-shadow:none; max-width:none; }
      .cover { min-height:auto; padding:40px 48px; }
      .no-print { display:none; }
      .md-body h1 { page-break-before:always; }
      .md-body h1:first-child { page-break-before:avoid; }
    }
    @page { margin:16mm 14mm; }
  </style>
</head>
<body>
<div class="page">
  <section class="cover">
    <div>Documentación DGAC — AOCR</div>
    <h1>${manual.title}<br><span style="font-weight:400;font-size:1.35rem">${manual.subtitle}</span></h1>
    <p class="lead">${manual.coverLead}</p>
    <p class="meta">Versión ${VERSION} · Fuente: docs/${path.basename(manual.source)}</p>
  </section>
  <div class="content">
    <div class="no-print"><strong>Exportación:</strong> PDF → <code>docs/export/${manual.id}.pdf</code> · Editable Word → <code>docs/export/editable/${manual.id}.docx</code> · Markdown → <code>docs/export/editable/${manual.id}.md</code></div>
    <article class="md-body">
${bodyHtml}
    </article>
    <div class="footer">${docTitle} v${VERSION} · Generado desde Markdown canónico · ${new Date().toISOString().slice(0, 10)}</div>
  </div>
</div>
</body>
</html>`;
}

function findEdge() {
  const candidates = [
    path.join(process.env["ProgramFiles(x86)"] || "", "Microsoft", "Edge", "Application", "msedge.exe"),
    path.join(process.env.ProgramFiles || "", "Microsoft", "Edge", "Application", "msedge.exe"),
  ];
  return candidates.find((p) => p && fs.existsSync(p));
}

async function printPdf(edgePath, htmlPath, pdfPath) {
  const { spawnSync } = await import("child_process");
  const fileUrl = "file:///" + htmlPath.replace(/\\/g, "/");
  const result = spawnSync(
    edgePath,
    ["--headless", "--disable-gpu", "--no-pdf-header-footer", `--print-to-pdf=${pdfPath}`, fileUrl],
    { encoding: "utf8", timeout: 120000 }
  );
  if (result.status !== 0) {
    throw new Error(result.stderr || result.stdout || `Edge exit ${result.status}`);
  }
  if (!fs.existsSync(pdfPath)) {
    throw new Error(`PDF no generado: ${pdfPath}`);
  }
}

async function main() {
  fs.mkdirSync(editableDir, { recursive: true });

  const only = process.argv.find((a) => a.startsWith("--only="))?.slice(7);
  const list = only
    ? manuals.filter((m) => m.id === only || m.id.toLowerCase().includes(only.toLowerCase()))
    : manuals;

  if (only && list.length === 0) {
    console.error(`No se encontró manual: ${only}`);
    process.exit(1);
  }

  const edgePath = findEdge();
  if (!edgePath) {
    console.warn("Advertencia: Microsoft Edge no encontrado; se omitirán PDFs.");
  }

  for (const manual of list) {
    if (!fs.existsSync(manual.source)) {
      console.warn(`Omitido (no existe): ${manual.source}`);
      continue;
    }

    const md = fs.readFileSync(manual.source, "utf8");
    const bodyMd = stripLeadingH1(md);
    const bodyHtml = await marked.parse(bodyMd);

    const htmlPath = path.join(exportDir, `${manual.id}.html`);
    const pdfPath = path.join(exportDir, `${manual.id}.pdf`);
    const docxPath = path.join(editableDir, `${manual.id}.docx`);
    const mdCopyPath = path.join(editableDir, `${manual.id}.md`);

    const html = buildHtml(manual, bodyHtml);
    fs.writeFileSync(htmlPath, html, "utf8");
    fs.copyFileSync(manual.source, mdCopyPath);

    console.log(`HTML  → ${htmlPath}`);

    const docxBuffer = await HTMLtoDOCX(html, null, {
      table: { row: { cantSplit: true } },
      footer: true,
      pageNumber: true,
    });
    try {
      fs.writeFileSync(docxPath, docxBuffer);
      console.log(`DOCX  → ${docxPath}`);
    } catch (err) {
      if (err.code === "EBUSY") {
        console.warn(`DOCX  → omitido (archivo abierto): ${docxPath}`);
      } else {
        throw err;
      }
    }
    console.log(`MD    → ${mdCopyPath}`);

    if (edgePath) {
      await printPdf(edgePath, htmlPath, pdfPath);
      const kb = Math.round(fs.statSync(pdfPath).size / 1024);
      console.log(`PDF   → ${pdfPath} (${kb} KB)`);
    }
  }

  console.log("\nListo. Editables en docs/export/editable/ (.md + .docx)");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
