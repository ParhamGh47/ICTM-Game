// The preview only serves the one registered file, so the project's own faces are embedded as data URIs. This
// keeps the mock honest: what it shows is the font the card will really be drawn in.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const html = path.join(__dirname, 'tutorialpreview.html');

const faces = [
  { file: 'Assets/Fonts/Saad.ttf', marker: "url('../Assets/Fonts/Saad.ttf')" },
  { file: 'Assets/Fonts/Potk.ttf', marker: "url('../Assets/Fonts/Potk.ttf')" },
];

let text = fs.readFileSync(html, 'utf8');

for (const face of faces) {
  const bytes = fs.readFileSync(path.join(root, face.file));
  const uri = 'url(data:font/ttf;base64,' + bytes.toString('base64') + ')';

  if (!text.includes(face.marker)) {
    console.log('marker not found (already inlined?):', face.marker);
    continue;
  }

  text = text.replace(face.marker, uri);
  console.log('inlined', face.file, bytes.length, 'bytes');
}

fs.writeFileSync(html, text);
console.log('written', path.relative(root, html), text.length, 'bytes');
