// Reads the car models' own material definitions out of their FBX files, so the lamp
// question ("which materials were authored to glow?") is answered by the models rather than guessed.
const fs = require('fs');
const path = require('path');

function readTree(buf) {
  const version = buf.readUInt32LE(23);
  const wide = version >= 7500;
  let pos = 27;
  const pathStack = [];
  const found = [];
  const seen = {};
  let lastError = null;
  let mismatch = null;
  let debugMaterial = null;

  let budget = 400000;

  const readNode = () => {
    if (--budget < 0) throw new Error('node budget exhausted');
    const start = pos;
    const endOffset = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const numProps = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const propLen = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const nameLen = buf.readUInt8(pos); pos += 1;
    if (endOffset === 0 && numProps === 0 && propLen === 0 && nameLen === 0) return null;
    const name = buf.slice(pos, pos + nameLen).toString('latin1'); pos += nameLen;
    seen[name] = (seen[name] || 0) + 1;
    const props = [];
    for (let i = 0; i < numProps; i++) {
      const t = String.fromCharCode(buf.readUInt8(pos)); pos += 1;
      if (t === 'Y') { props.push(buf.readInt16LE(pos)); pos += 2; }
      else if (t === 'C') { props.push(!!buf.readUInt8(pos)); pos += 1; }
      else if (t === 'I') { props.push(buf.readInt32LE(pos)); pos += 4; }
      else if (t === 'F') { props.push(buf.readFloatLE(pos)); pos += 4; }
      else if (t === 'D') { props.push(buf.readDoubleLE(pos)); pos += 8; }
      else if (t === 'L') { props.push(Number(buf.readBigInt64LE(pos))); pos += 8; }
      else if (t === 'S' || t === 'R') { const l = buf.readUInt32LE(pos); pos += 4; props.push(buf.slice(pos, pos + l).toString('latin1')); pos += l; }
      else if ('fdlbic'.includes(t)) {
        const len = buf.readUInt32LE(pos); const enc = buf.readUInt32LE(pos + 4); const compLen = buf.readUInt32LE(pos + 8); pos += 12;
        const unit = t === 'b' ? 1 : (t === 'd' || t === 'l' ? 8 : 4);
        const bytes = buf.slice(pos, pos + (enc ? compLen : len * unit)); pos += bytes.length;
        const values = [];
        if (!enc) {
          for (let k = 0; k < len; k++) {
            values.push(t === 'b' ? !!bytes[k]
              : t === 'i' ? bytes.readInt32LE(k * 4)
              : t === 'f' ? bytes.readFloatLE(k * 4)
              : t === 'l' ? Number(bytes.readBigInt64LE(k * 8))
              : bytes.readDoubleLE(k * 8));
          }
        }
        props.push({ type: t, len, enc, values });
      } else throw new Error('unknown property type ' + JSON.stringify(t) + ' via ' + pathStack.slice(-6).join('>') + ' at ' + pos);
    }
    pathStack.push(name);
    const children = [];

    // A record with no room left after its properties is a leaf, and a leaf has no end-of-list
    // marker of its own - the marker belongs to the list it sits in. Reading one there eats the
    // parent's marker and starts a cascade, which is what turned this file inside out.
    while (pos < endOffset) {
      const save = pos;
      const childEnd = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos);
      if (childEnd === 0) { pos = save + (wide ? 25 : 13); break; }
      const child = readNode();
      if (child) children.push(child);
    }
    pathStack.pop();

    if (pos !== endOffset && !mismatch && endOffset > 0) {
      mismatch = `record mismatch: ${pathStack.join('>')}>${name} ended at ${pos}, header said ${endOffset}`;
    }

    // The declared end of a record is authoritative: if the walk of its children came up short,
    // trusting the header keeps the rest of the file in step instead of cascading.
    if (endOffset > pos) pos = endOffset;

    // Snapshotted here rather than from the finished tree: the reader cannot follow this file all
    // the way to its tail, and a throw at the end must not lose what was read on the way.
    if (name === 'Material') {
      // An object record is (id, name, type): the name is the string in there, whichever slot it is in.
      const label = props.find(p => typeof p === 'string');
      const p70 = children.find(c => c.name === 'Properties70');
      if (!debugMaterial) {
        debugMaterial = `props=[${props.map(p => typeof p === 'object' ? 'array' : JSON.stringify(p)).join(',')}] children=[${children.map(c => c.name).join(',')}]`;
      }
      if (label && p70) found.push({ name: label, props70: p70, children });
    }

    return { name, props, children, start };
  };

  const roots = [];
  while (pos < buf.length - (wide ? 25 : 13)) {
    const peekEnd = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos);
    if (peekEnd === 0) break;
    let node = null;
    try { node = readNode(); } catch (e) { lastError = e.message; node = null; }
    if (!node) break;
    roots.push(node);
    if (node.start === pos) break;
  }
  return { version, roots, found, lastError, mismatch, seen, debugMaterial };
}

const find = (nodes, name) => nodes.find(n => n.name === name);
const childrenNamed = (node, name) => (node ? node.children.filter(c => c.name === name) : []);

function numbers(props) {
  return props.filter(p => typeof p === 'number');
}

function collect(node, name, out, depth = 0) {
  if (node.name === name) out.push({ node, depth });
  for (const c of node.children) collect(c, name, out, depth + 1);
  return out;
}

for (const file of process.argv.slice(2)) {
  const { version, found, lastError, mismatch, seen, debugMaterial } = readTree(fs.readFileSync(file));
  if (mismatch) console.log('   ' + mismatch);
  if (debugMaterial) console.log('   first Material node: ' + debugMaterial);
  if (process.env.DUMP) console.log(Object.entries(seen).sort((a, b) => b[1] - a[1]).slice(0, 40).map(([k, v]) => `${k}:${v}`).join('  '));
  console.log(`===================== ${path.basename(file)}   (fbx v${version}, ${found.length} materials)`);
  if (lastError) console.log('   stopped: ' + lastError.slice(0, 90));

  for (const m of found) {
    const props70 = m.props70;
    const out = { emissive: null, emissiveFactor: null, diffuse: null, transparency: null };
    for (const p of childrenNamed(props70, 'P')) {
      const key = p.props[0];
      const vals = numbers(p.props);
      if (/EmissiveColor/i.test(key)) out.emissive = vals.slice(0, 3);
      if (/EmissiveFactor/i.test(key)) out.emissiveFactor = vals[0];
      if (/^DiffuseColor/i.test(key)) out.diffuse = vals.slice(0, 3);
      if (/TransparencyFactor|Opacity/i.test(key)) out.transparency = vals[0];
    }
    console.log(`   ${m.name}  emissive=${JSON.stringify(out.emissive)} factor=${out.emissiveFactor} diffuse=${JSON.stringify(out.diffuse)} transparency=${out.transparency}`);
  }
}
