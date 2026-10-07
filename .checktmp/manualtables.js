// The controls tables, measured: how many rows each gearbox setting's table has, and where the bottom of
// each screen's table lands. The two tables have to hold the same number of rows, because the notes are laid
// out underneath them - a manual table one row longer would push the notes off the bottom of the screen.
//
// Run: node .checktmp/manualtables.js
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const source = fs.readFileSync(path.join(root, 'Assets', 'Scripts', 'UI', 'ControlBindings.cs'), 'utf8');

// The two tables are the two arrays of ControlGroup[...], each with a DRIVING and a MENUS group.
function table(name) {
  const start = source.indexOf(`ControlGroup[] ${name} =`);
  const end = source.indexOf('};', start);
  const body = source.slice(start, end);
  const rows = [...body.matchAll(/new ControlBinding\(/g)].length;
  const groups = [...body.matchAll(/new ControlGroup\(/g)].length;
  const empty = [...body.matchAll(/new ControlGroup\("(\w+)"\)/g)].map(m => m[1]);
  return { rows, groups, empty };
}

const auto = table('Automatic');
const manual = table('Manual');

console.log(`AUTOMATIC table: ${auto.rows} rows in ${auto.groups} groups (empty groups: ${auto.empty.join(', ') || 'none'})`);
console.log(`MANUAL    table: ${manual.rows} rows in ${manual.groups} groups (empty groups: ${manual.empty.join(', ') || 'none'})`);
console.log(auto.rows === manual.rows
  ? 'OK: both tables hold the same number of rows, so the notes under them stay put.\n'
  : 'MISMATCH: the notes would move between the two tables.\n');

// Where each screen's table ends.
function mainMenu(rowsByGroup) {
  const contentTopY = -224, captionSize = 22, headerHeight = 32, rowHeight = 44, rowGap = 4, groupGap = 16;
  const noteSize = 22;
  let y = contentTopY - headerHeight;
  for (const rows of rowsByGroup) {
    y -= captionSize * 1.5 + 10 + 4;         // BuildCaption, then its own 4px gap
    y -= rows * (rowHeight + rowGap);
    y -= groupGap;
  }
  const notes = [];
  for (let i = 0; i < 2; i++) notes.push(y - noteSize * 1.6 - 8), y = notes[i];
  return { tableEnd: y + noteSize * 1.6 + 8, notesEnd: notes[notes.length - 1] };
}

function pausePanel(rowsByGroup) {
  const contentTopY = 94, headerHeight = 18, captionHeight = 27, rowHeight = 27, rowGap = 2, groupGap = 8;
  let y = contentTopY + headerHeight;
  for (const rows of rowsByGroup) {
    y += captionHeight;
    y += rows * (rowHeight + rowGap);
    y += groupGap;
  }
  return { tableEnd: y };
}

const groupsOf = (t, driving) => [driving, t.rows - driving];
const autoGroups = groupsOf(auto, 10), manualGroups = groupsOf(manual, 10);

const mainAuto = mainMenu(autoGroups), mainManual = mainMenu(manualGroups);
console.log(`Main menu controls page (1080 tall, 46px bottom margin):`);
console.log(`  automatic: table ends at ${mainAuto.tableEnd.toFixed(0)}, notes end at ${mainAuto.notesEnd.toFixed(0)}`);
console.log(`  manual:    table ends at ${mainManual.tableEnd.toFixed(0)}, notes end at ${mainManual.notesEnd.toFixed(0)}`);
console.log(`  ${mainManual.notesEnd > -1070 ? 'OK: both clear the bottom of the screen.' : 'TOO LOW: the notes run off the screen.'}\n`);

const pauseAuto = pausePanel(autoGroups), pauseManual = pausePanel(manualGroups);
console.log(`Pause panel controls page (safe box 540 tall):`);
console.log(`  automatic: table ends at ${pauseAuto.tableEnd}`);
console.log(`  manual:    table ends at ${pauseManual.tableEnd}`);
console.log(`  ${pauseManual.tableEnd < 540 ? 'OK: both fit the safe box.' : 'TOO TALL: the table runs past the safe box.'}`);
