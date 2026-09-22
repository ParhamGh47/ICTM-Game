// The level list has buttons for levels 1-6 but 5 and 6 were wired to the options
// screen as placeholders. This points them at the new menuBTN methods.
const fs = require('fs');
const path = require('path');

const ASSETS = path.resolve(__dirname, '..', 'Assets');
const file = path.join(ASSETS, 'Scenes', 'Levels.unity');

const MENU_BTN = (() => {
  const meta = fs.readFileSync(path.join(ASSETS, 'Scripts', 'UI', 'Main Menu', 'menuBTN.cs.meta'), 'utf8');
  return /^guid: ([0-9a-f]{32})/m.exec(meta)[1];
})();

const apply = process.argv.includes('--apply');

let text = fs.readFileSync(file, 'utf8');
const nl = text.includes('\r\n') ? '\r\n' : '\n';
const parts = text.split(/^--- /m);
const header = parts.shift();
const docs = parts.map(p => '--- ' + p);

const nameOfGo = id => {
  const doc = docs.find(d => new RegExp('^--- !u!1 &' + id + '( |\\r|\\n)').test(d));
  return doc ? (/(?:^|\r?\n)  m_Name: (.*?)\r?(?:\r?\n|$)/.exec(doc) || [])[1] : '?';
};

let changed = 0;

for (let i = 0; i < docs.length; i++) {
  const d = docs[i];
  if (!/m_OnClick/.test(d)) continue;

  // the click has to be aimed at the scene's menuBTN component - that is what makes it a level button
  const target = /m_Target: \{fileID: (\d+)/.exec(d);
  if (!target) continue;

  const targetDoc = docs.find(x => new RegExp('^--- !u!114 &' + target[1] + '( |\\r|\\n)').test(x));
  const targetScript = targetDoc && /m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32})/.exec(targetDoc);
  if (!targetScript || targetScript[1] !== MENU_BTN) continue;

  const go = /m_GameObject: \{fileID: (\d+)\}/.exec(d);
  const name = go ? nameOfGo(go[1]) : '?';
  const method = /m_MethodName: (.*?)\r?\r?\n/.exec(d);
  const current = method ? method[1].trim() : '?';

  console.log(`button "${name}" -> ${current}`);

  const wanted = name === 'level5' ? 'levelFive' : name === 'level6' ? 'levelSix' : null;
  if (!wanted || current === wanted) continue;

  docs[i] = d.replace(/(m_MethodName: )\S+/, '$1' + wanted);
  changed++;
  console.log(`   -> ${wanted}`);
}

if (apply && changed) {
  fs.writeFileSync(file, header + docs.join(''));
  console.log(`\npatched ${changed} ${changed === 1 ? 'button' : 'buttons'}`);
} else if (changed) {
  console.log('\n(dry run - pass --apply to write)');
} else {
  console.log('\nnothing to do');
}
