// Prints the axis mappings from ProjectSettings/InputManager.asset, grouped by axis name.
const fs = require('fs');
const raw = fs.readFileSync('ProjectSettings/InputManager.asset', 'utf8').replace(/\r\n/g, '\n');

// Each axis entry starts with "  - serializedVersion: 3"
const blocks = raw.split(/\n  - serializedVersion: 3\n/).slice(1);
for (const b of blocks) {
  const name = (/m_Name: (.*)$/m.exec(b) || [])[1];
  if (!name) continue;
  const type = (/type: (\d)/.exec(b) || [])[1];
  const axis = (/axis: (\d+)/.exec(b) || [])[1];
  const pos = (/positive Button: (.*)$/m.exec(b) || [])[1];
  const neg = (/negative Button: (.*)$/m.exec(b) || [])[1];
  const altPos = (/altPositiveButton: (.*)$/m.exec(b) || [])[1];
  const altNeg = (/altNegativeButton: (.*)$/m.exec(b) || [])[1];
  const jAxis = (/joyNum|axis: \d+/.test(b) ? '' : '');
  const keys = [];
  if (pos && pos.trim()) keys.push('+' + pos.trim());
  if (altPos && altPos.trim()) keys.push('+' + altPos.trim());
  if (neg && neg.trim()) keys.push('-' + neg.trim());
  if (altNeg && altNeg.trim()) keys.push('-' + altNeg.trim());
  console.log(
    (name + '                          ').slice(0, 26),
    'type=' + (type || '?'),
    type === '2' ? '(mouse/joystick axis ' + axis + ')' : '',
    keys.join(' ')
  );
}
