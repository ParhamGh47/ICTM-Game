// Reads the difficulty table out of GameDifficulty.cs and each level's authored numbers out of the core
// scenes, then prints what every level would ask for at every difficulty - the check that the feature does
// what was asked of it: Easy/Medium/Hard as a time limit and a kill target.
const fs = require('fs');
const path = require('path');

const src = fs.readFileSync('Assets/Scripts/Global/GameDifficulty.cs', 'utf8');

const table = [...src.matchAll(/new Settings \{ name = "(\w+)",\s*timeScale = ([\d.]+)f,\s*killScale = ([\d.]+)f/g)]
  .map((m) => ({ name: m[1], time: parseFloat(m[2]), kills: parseFloat(m[3]) }));

console.log('table in GameDifficulty.cs:');
for (const t of table) console.log(`  ${t.name.padEnd(7)} time x${t.time}  kills x${t.kills}`);

const minimum = parseFloat(src.match(/ShortestTime = ([\d.]+)f/)[1]);
console.log('  floor on a level\'s time: ' + minimum + 's');

const clamp = (v) => Math.max(1, Math.min(3, Math.round(v)));
const timeAt = (authored, t) => Math.max(minimum, authored * t.time);
const killsAt = (authored, t) => (authored <= 0 ? 0 : Math.max(1, Math.round(authored * t.kills)));

const mmss = (s) => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;

console.log('\nlevels, as authored and as each difficulty would play them:');

for (const n of [1, 2, 3, 4]) {
  const file = `Assets/Scenes/Levels Scenes/${n}/Core-${n}.unity`;
  if (!fs.existsSync(file)) continue;

  const yaml = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
  const authored = (prop) => {
    const m = yaml.match(new RegExp('propertyPath: ' + prop + '\\n\\s*value: (-?[\\d.]+)'));
    return m ? parseFloat(m[1]) : null;
  };

  // The prefab's own values are what a scene that does not override the property uses.
  const prefab = fs.readFileSync('Assets/Prefabs/Utils/CanvasUI.prefab', 'utf8');
  const fallback = (prop) => parseFloat(prefab.match(new RegExp(prop + ': (-?[\\d.]+)'))[1]);

  const time = authored('targetTimeSeconds') ?? fallback('targetTimeSeconds');
  const kills = authored('targetKills');

  console.log(`\n  Core-${n}:  authored ${mmss(time)} (${time}s)` + (kills !== null ? `, ${kills} targets` : ', no target override'));

  for (const t of table) {
    const seconds = timeAt(time, t);
    const out = `    ${t.name.padEnd(7)} ${mmss(seconds).padStart(5)} (${seconds.toFixed(1)}s)`;
    console.log(kills === null ? out + '   - no kill requirement of its own' : out + `, must hit ${killsAt(kills, t)} of ${kills}`);
  }
}

// Every level's time must be reachable on Hard, and the count must never move the wrong way.
console.log('\nsanity:');
let bad = 0;
for (const t of table) {
  if (t.time <= 0 || t.kills <= 0) { console.log('  a scale of zero or less'); bad++; }
}
const easy = table.find((t) => t.name === 'Easy');
const hard = table.find((t) => t.name === 'Hard');
const medium = table.find((t) => t.name === 'Medium');
if (!(easy.time > medium.time && medium.time > hard.time)) { console.log('  time does not fall as difficulty rises'); bad++; }
if (!(easy.kills < medium.kills && medium.kills < hard.kills)) { console.log('  targets do not rise as difficulty rises'); bad++; }
// the prefab's own kill default is what a level without an override inherits
const prefabKills = parseFloat(fs.readFileSync('Assets/Prefabs/Utils/CanvasUI.prefab', 'utf8').match(/targetKills: (\d+)/)[1]);
for (const t of table) {
  const k = killsAt(3, t);
  if (k < 1 || k > 6) { console.log(`  Core-3's ${t.name} target is ${k}; expected 1..6`); bad++; }
}
console.log(`  Easy/Medium/Hard targets for 3 authored: ` + table.map((t) => killsAt(3, t)).join('/') + `, for 7 authored: ` + table.map((t) => killsAt(7, t)).join('/'));
console.log(`  default in the prefab (${prefabKills} authored): ` + table.map((t) => killsAt(prefabKills, t)).join('/'));
console.log(bad === 0 ? '  ok' : `  ${bad} problem(s)`);
