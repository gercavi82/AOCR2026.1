const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const includeDirs = ['CapaPresentacion', 'CapaPresentacion/Content', 'CapaPresentacion/Scripts', 'Scripts'];
const exts = ['.js', '.cshtml', '.html'];

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
