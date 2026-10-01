// Compiles the AICars assembly strictly on its own - only its own sources and the engine references its
// csproj declares - so a reference to a type from another assembly shows up here rather than in Unity.
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const root = path.resolve(__dirname, '..');
const csproj = path.join(root, 'AICars.csproj');
const xml = fs.readFileSync(csproj, 'utf8');
const decode = (s) => s.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"');

const refs = [];
for (const m of xml.matchAll(/<HintPath>([^<]+)<\/HintPath>/g)) refs.push(path.resolve(root, decode(m[1])));

const sources = [];
for (const m of xml.matchAll(/<Compile Include="([^"]+)"\s*\/>/g)) {
  const p = decode(m[1]);
  if (p.startsWith('obj')) continue;
  const full = path.resolve(root, p);
  if (!fs.existsSync(full)) continue;
  sources.push(p);
}
// a file added this session is not in the stale csproj yet
for (const extra of process.argv.slice(2)) if (extra.endsWith('.cs')) sources.push(extra);

// Anything else in ScriptAssemblies is another asmdef's output: referencing one would defeat the point.
const extraRefs = [];
const scriptAssemblies = path.join(root, 'Library', 'ScriptAssemblies');
if (fs.existsSync(scriptAssemblies)) {
  for (const f of fs.readdirSync(scriptAssemblies)) {
    if (!f.endsWith('.dll')) continue;
    if (/^Assembly-CSharp/.test(f) || f === 'AICars.dll') continue;
    extraRefs.push(path.join(scriptAssemblies, f));
  }
}

const csc = fs.readdirSync('C:/Program Files/dotnet/sdk')
  .map((v) => 'C:/Program Files/dotnet/sdk/' + v + '/Roslyn/bincore/csc.dll')
  .filter((p) => fs.existsSync(p)).sort().pop();

const lines = ['-nologo', '-target:library',
  '-out:"' + path.join(__dirname, 'out', 'CheckAICars.dll').replace(/\\/g, '/') + '"',
  '-define:UNITY_2022_1_OR_NEWER;UNITY_EDITOR;UNITY_2022_1', '-langversion:9.0',
  '-nowarn:0169,0649,0414,0067,0436'];
for (const r of refs.concat(extraRefs)) if (fs.existsSync(r)) lines.push('-r:"' + r.replace(/\\/g, '/') + '"');
for (const s of sources) lines.push('"' + s.replace(/\\/g, '/') + '"');

const rsp = path.join(__dirname, 'checkaicars.rsp');
fs.mkdirSync(path.join(__dirname, 'out'), { recursive: true });
fs.writeFileSync(rsp, lines.join('\n'));
console.log('AICars sources: ' + sources.length + '  refs: ' + refs.length + '  other-assembly refs: ' + extraRefs.length);

try {
  const out = execFileSync('dotnet', [csc, '@' + rsp], { cwd: root, encoding: 'utf8', stdio: 'pipe' });
  if (out.trim()) console.log(out);
  console.log('RESULT: 0 errors');
} catch (err) {
  const text = (err.stdout || '') + (err.stderr || '');
  const errors = text.split(/\r?\n/).filter((l) => /error CS/.test(l));
  console.log(errors.slice(0, 40).join('\n'));
  console.log('RESULT: ' + errors.length + ' errors');
  process.exitCode = 1;
}
