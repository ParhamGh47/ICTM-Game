// Typechecks the gameplay assembly (Assembly-CSharp) exactly as Unity builds it, plus any extra file
// given on the command line that Unity's generated csproj does not list yet (a file added this session).
// Usage: node .checktmp/checkcs.js Assets/Scripts/Player/TireSmokeController.cs
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const root = path.resolve(__dirname, '..');
const args = process.argv.slice(2);
const projectArg = args.find((a) => a.endsWith('.csproj'));
const csproj = projectArg ? path.join(root, projectArg) : path.join(root, 'Assembly-CSharp.csproj');
const xml = fs.readFileSync(csproj, 'utf8');

const decode = (s) => s.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"');

const refs = [];
for (const m of xml.matchAll(/<HintPath>([^<]+)<\/HintPath>/g)) {
  refs.push(decode(m[1]));
}
for (const m of xml.matchAll(/<Reference Include="([^"]+)"\s*\/>/g)) {
  const name = decode(m[1]);
  if (!name.includes(',')) continue;
}

const sources = [];
for (const m of xml.matchAll(/<Compile Include="([^"]+)"\s*\/>/g)) {
  sources.push(decode(m[1]));
}
for (const extra of args) {
  if (extra.endsWith('.csproj')) continue;
  if (!sources.includes(extra)) sources.push(extra);
}

// Sibling asmdef assemblies (RoadArchitect and friends) are referenced as already-built DLLs: only the
// gameplay assembly plus the file being added is under test here.
const scriptAssemblies = path.join(root, 'Library', 'ScriptAssemblies');
if (fs.existsSync(scriptAssemblies)) {
  for (const f of fs.readdirSync(scriptAssemblies)) {
    if (!f.endsWith('.dll')) continue;
    if (/^Assembly-CSharp/.test(f)) continue;
    refs.push(path.join(scriptAssemblies, f));
  }
}

const missing = refs.filter((r) => !fs.existsSync(r));
if (missing.length) {
  console.log('MISSING REFS:\n  ' + missing.join('\n  '));
}

const csc = fs
  .readdirSync('C:/Program Files/dotnet/sdk')
  .map((v) => 'C:/Program Files/dotnet/sdk/' + v + '/Roslyn/bincore/csc.dll')
  .filter((p) => fs.existsSync(p))
  .sort()
  .pop();

const rsp = path.join(__dirname, 'checkcs.rsp');
const lines = [
  '-nologo',
  '-target:library',
  '-out:"' + path.join(__dirname, 'out', 'CheckGameplay.dll').replace(/\\/g, '/') + '"',
  '-define:UNITY_2022_1_OR_NEWER;UNITY_EDITOR;UNITY_2022_1',
  '-langversion:9.0',
  '-nowarn:0169,0649,0414,0067,0436',
];
for (const r of refs) if (fs.existsSync(r)) lines.push('-r:"' + r.replace(/\\/g, '/') + '"');
for (const s of sources) lines.push('"' + s.replace(/\\/g, '/') + '"');

fs.mkdirSync(path.join(__dirname, 'out'), { recursive: true });
fs.writeFileSync(rsp, lines.join('\n'));

console.log('sources: ' + sources.length + '  refs: ' + refs.length);

try {
  const out = execFileSync('dotnet', [csc, '@' + rsp], { cwd: root, encoding: 'utf8', stdio: 'pipe' });
  console.log(out);
  console.log('RESULT: 0 errors');
} catch (err) {
  const text = (err.stdout || '') + (err.stderr || '');
  const lines2 = text.split(/\r?\n/).filter((l) => /error CS/.test(l));
  console.log(text.split(/\r?\n/).filter((l) => /TireSmoke|error CS/.test(l)).slice(0, 60).join('\n'));
  console.log('RESULT: ' + lines2.length + ' errors');
  process.exitCode = 1;
}
