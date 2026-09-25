// Lists every AudioSource in a scene/prefab: owning GameObject, clip asset, loop/spatial/volume settings.
// Usage: node .checktmp/audiosources.js <file...>
const fs = require('fs');
const path = require('path');

// guid -> asset path for audio files
const audioByGuid = new Map();
function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = dir + '/' + e.name;
    if (e.isDirectory()) walk(p);
    else if (/\.(mp3|wav|ogg|aiff?|m4a)\.meta$/.test(e.name)) {
      const g = /guid: (\w+)/.exec(fs.readFileSync(p, 'utf8'));
      if (g) audioByGuid.set(g[1], p.replace(/\.meta$/, ''));
    }
  }
}
walk('Assets/Audio');

for (const file of process.argv.slice(2)) {
  const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
  const blocks = [];
  const re = /^--- !u!(\d+) &(\d+)(?: stripped)?\n([\s\S]*?)(?=^--- |\Z)/gm;
  let m;
  while ((m = re.exec(text)) !== null) blocks.push({ c: m[1], id: m[2], body: m[3] });

  const goName = new Map();
  const goComps = new Map();
  for (const b of blocks) {
    if (b.c !== '1') continue;
    const n = /^  m_Name: (.*)$/m.exec(b.body);
    goName.set(b.id, n ? n[1] : '?');
  }
  for (const b of blocks) {
    if (b.c !== '1') continue;
    const list = /^  m_Component:\n((?:  - component: \{fileID: \d+\}\n)*)/m.exec(b.body);
    const ids = [...(list?.[1] || '').matchAll(/fileID: (\d+)/g)].map((x) => x[1]);
    goComps.set(b.id, ids);
  }

  console.log('=== ' + file + ' ===');
  let found = 0;
  for (const b of blocks) {
    if (b.c !== '82') continue;
    found++;
    const go = /^  m_GameObject: \{fileID: (\d+)\}/m.exec(b.body);
    const clip = /^  m_audioClip: \{fileID: \d+, guid: (\w+)/m.exec(b.body) || /^  m_audioClip: \{fileID: (\d+)\}/m.exec(b.body);
    const vol = /^  m_Volume: (.*)$/m.exec(b.body);
    const loop = /^  m_Loop: (\d)/m.exec(b.body);
    const blend = /^  m_SpatialBlend: (.*)$/m.exec(b.body);
    const playAwake = /^  m_PlayOnAwake: (\d)/m.exec(b.body);

    let clipName = clip ? (audioByGuid.get(clip[1]) || 'guid:' + clip[1]) : 'none';
    // the AudioSource may sit on a child; find the owner up the chain is enough by name of its own GO
    const owner = go ? goName.get(go[1]) : '?';
    console.log(
      '  ' + (owner || '?') + '  #' + (go ? go[1] : '?') +
      '  clip=' + clipName +
      '  vol=' + (vol ? vol[1] : '?') +
      '  loop=' + (loop ? loop[1] : '?') +
      '  blend=' + (blend ? blend[1] : '?') +
      '  playOnAwake=' + (playAwake ? playAwake[1] : '?')
    );
  }
  if (found === 0) console.log('  (no AudioSources)');
}
