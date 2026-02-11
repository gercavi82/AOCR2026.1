const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const includeDirs = ['CapaPresentacion', 'CapaPresentacion/Content', 'CapaPresentacion/Scripts', 'Scripts'];
const exts = ['.js', '.cshtml', '.html'];
// Exclude common vendor/build paths and minified files to avoid scanning third-party libraries
const excludePatterns = ['Content/plugins', 'Content/dist', 'Content/assets', '.min.js', '.map'];
// Regex-based excludes for known vendor/script names (in CapaPresentacion/Scripts)
const excludeRegex = [ /jquery(\.|-)/i, /modernizr/i, /adminlte/i, /bootstrap/i, /datatables/i, /codemirror/i, /summernote/i ];

const patterns = [
  { name: 'template-with-html-and-interp', re: /`[^`]*<[^`]*\${[^`]*`/g },
  { name: 'html-template-concat', re: /\.(html|append)\s*\(\s*`[^`]*\${/g },
  { name: 'string-concat-html-var', re: /\+\s*\${0,1}[a-zA-Z0-9_\.\[\]]+\s*\+\s*['\"]<|<['\"]\s*\+\s*[a-zA-Z0-9_\.\[\]]+/g }
];

let findings = [];

function walkDir(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const e of entries) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (includeDirs.some(d => full.includes(path.normalize(d)))) walkDir(full);
      continue;
    }
    if (!exts.includes(path.extname(e.name))) continue;
    // Skip excluded paths and files (vendor or minified)
    var normalizedFull = full.replace(/\\/g, '/').toLowerCase();
    var excludeNormalized = excludePatterns.map(function(p){ return p.replace(/\\/g, '/').toLowerCase(); });
    if (excludeNormalized.some(function(p){ return normalizedFull.includes(p); })) continue;
    // Skip files that match known vendor names (jquery, modernizr, etc.)
    if (excludeRegex.some(function(r){ return r.test(e.name); })) continue;
    const text = fs.readFileSync(full, 'utf8');
    patterns.forEach(p => {
      const m = text.match(p.re);
      if (m && m.length) {
        findings.push({ file: path.relative(root, full), pattern: p.name, count: m.length });
      }
    });
  }
}

includeDirs.forEach(d => walkDir(path.join(root, d)));

if (findings.length) {
  console.log('Potential unsafe HTML/template usages found:');
  findings.forEach(f => console.log(`- ${f.file} : ${f.pattern} (matches: ${f.count})`));
  process.exitCode = 2;
} else {
  console.log('No obvious risky template/HTML concatenations found.');
}
