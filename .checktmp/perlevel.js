// What does a scene carry that is specific to THAT level - the things a copy would silently inherit?
//
// The interesting ones are the values a level author sets in the Inspector:
//   TimeTracker        easy / medium / hard target times (seconds)
//   KillDisplay        easy / medium / hard target kills
//   CheckpointIndicator  the compass's list of checkpoints
//   LevelLoader        the scene names it loads (its own core, and its end scene)
//   GameDifficulty is global, so nothing per-level there.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');

const SCRIPTS = {
  TimeTracker: 'e621e39f07aa1324790ed157b6614ed3',
  KillDisplay: '654b12ee0a6f37942826ae6fa256e0ce',
  CheckpointIndicator: '46c087e1027750343ae4c624aabb94d6',
  LevelLoader: 'c4b1585b316e7a04989302c57ad9e633',
};

function read(f) { return fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n'); }

function slice(file, guid) {
  const text = read(file);
  const at = text.indexOf('guid: ' + guid);
  if (at < 0) return null;
  // The MonoBehaviour's own fields run from the m_Script line to the next document.
  const start = text.lastIndexOf('\n', at);
  const end = text.indexOf('\n--- !u!', at);
  return text.slice(start, end < 0 ? undefined : end);
}

const fields = {
  TimeTracker: [/^\s+(easy|medium|hard)TargetTimeSeconds: (.*)$/gm],
  KillDisplay: [/^\s+(easy|medium|hard)TargetKills: (.*)$/gm],
  CheckpointIndicator: [/^\s+checkpoints:\n((?:\s+-.*\n)*)/m],
  LevelLoader: [/^\s+(gameplaySceneName|endScene): (.*)$/gm],
};

const scenes = [
  'Assets/Scenes/Template.unity',
  ...[1, 2, 3, 4].map((n) => `Assets/Scenes/Levels Scenes/${n}/Core-${n}.unity`),
];

for (const [name, guid] of Object.entries(SCRIPTS)) {
  console.log('=== ' + name);
  for (const scene of scenes) {
    const file = path.join(ROOT, scene);
    const body = slice(file, guid);
    const label = scene.replace('Assets/Scenes/', '');
    if (!body) {
      console.log('  ' + label.padEnd(34) + '(not in this scene)');
      continue;
    }
    const out = [];
    for (const re of fields[name]) {
      re.lastIndex = 0;
      let m;
      while ((m = re.exec(body))) {
        if (m[2] !== undefined) out.push(m[1] + '=' + m[2].trim());
        else {
          const items = (m[1] || '').split('\n').map((l) => l.trim()).filter((l) => l.startsWith('-'));
          out.push('checkpoints[' + items.length + ']');
        }
      }
    }
    console.log('  ' + label.padEnd(34) + (out.join('  ') || '(all empty)'));
  }
}
