// Mirrors AICarController.IsLampName / Squash and runs it over every object name the four car
// prefabs actually carry, so the rule is checked against the real names rather than a guess at them.
const fs = require('fs');

const TrimWords = ['ring', 'bezel', 'trim'];
const NeverLampNames = ['carcolor', 'material.005', 'body', 'glass', 'window', 'windscreen', 'windshield', 'siren'];

function squash(name) {
  let out = '';
  for (const c of name) {
    if (c === ' ' || c === '_' || c === '-') continue;
    out += c.toLowerCase();
  }
  return out;
}

function isLampName(name) {
  if (!name) return false;
  const s = squash(name);
  for (const t of TrimWords) if (s.includes(t)) return false;
  return s.includes('headlight') || s.startsWith('light') || s.startsWith('lamp');
}

function isNeverLamp(name) {
  if (!name) return true;
  const lower = name.toLowerCase();
  return NeverLampNames.some(n => lower.includes(n));
}

const files = process.argv.slice(2);
for (const f of files) {
  const text = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');
  const names = new Set();

  // Names the GameObject records carry.
  for (const m of text.matchAll(/^  m_Name: (.*)$/gm)) if (m[1]) names.add(m[1]);
  // Names overridden onto the prefab instance's parts.
  for (const m of text.matchAll(/propertyPath: m_Name\n\s+value: (.*)\n/g)) if (m[1]) names.add(m[1]);

  console.log('===== ' + f);
  const lamps = [];
  for (const n of [...names].sort()) {
    const lamp = isLampName(n);
    const never = isNeverLamp(n);
    if (lamp && !never) lamps.push(n);
    console.log(`   ${lamp ? (never ? 'lamp-name-but-never-lamp' : 'LAMP PART') : 'not a lamp'}   ${n}`);
  }
  console.log('   -> lit by name: ' + (lamps.join(', ') || '(none)'));
}
