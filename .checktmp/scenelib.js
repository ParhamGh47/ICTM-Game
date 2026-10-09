// Minimal Unity scene YAML reader: split into documents, index them, follow references.
const fs = require('fs');

function read(file) { return fs.readFileSync(file, 'utf8').split('\r\n').join('\n'); }
function write(file, text) { fs.writeFileSync(file, text.split('\n').join('\r\n'), 'utf8'); }

// A scene is a list of documents, each starting with "--- !u!<class> &<id>[ stripped]".
function splitDocs(text) {
  const parts = text.split(/^--- !u!/m);
  const head = parts.shift(); // "%YAML 1.1\n%TAG ..." preamble
  const docs = [];
  for (const p of parts) {
    const m = p.match(/^(\d+) &(\d+)( stripped)?\n([\s\S]*)$/);
    if (!m) continue;
    docs.push({
      cls: m[1],
      id: m[2],
      stripped: !!m[3],
      body: m[4],
      get text() { return '--- !u!' + this.cls + ' &' + this.id + (this.stripped ? ' stripped' : '') + '\n' + this.body; },
    });
  }
  return { head, docs };
}

function index(docs) {
  const byId = new Map();
  for (const d of docs) byId.set(d.id, d);
  return byId;
}

function refsIn(text) {
  const out = [];
  for (const m of text.matchAll(/\{fileID: (\d+)(?:, guid: ([0-9a-f]+))?/g)) {
    if (m[1] !== '0' && !m[2]) out.push(m[1]); // internal references only
  }
  return out;
}

function nameOf(doc) {
  if (!doc) return '(missing)';
  const n = (doc.body.match(/m_Name: (.*)/) || [])[1];
  return n !== undefined ? n : '';
}

module.exports = { read, write, splitDocs, index, refsIn, nameOf };
