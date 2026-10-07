// A small FBX binary reader. Only what is needed: walk the node tree and print the
// properties of every Material object, so we can see which materials are authored to glow.
const fs = require('fs');

function readTree(buf) {
  if (buf.slice(0, 20).toString('latin1') !== 'Kaydara FBX Binary  ') throw new Error('not an FBX');
  const version = buf.readUInt32LE(23);
  const wide = version >= 7500;
  let pos = 27;

  const path = [];

  const readNode = () => {
    const start = pos;
    const endOffset = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const numProps = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const propLen = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const nameLen = buf.readUInt8(pos); pos += 1;
    if (endOffset === 0 && numProps === 0 && propLen === 0 && nameLen === 0) return null;
    const name = buf.slice(pos, pos + nameLen).toString('latin1'); pos += nameLen;
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
        props.push({ array: t, len, enc, bytes });
      } else {
        const around = buf.slice(pos - 60, pos + 20).toString('latin1').replace(/[^\x20-\x7e]/g, '.');
        console.error('trace:', path.join(' > '), '| n=' + numProps + ' i=' + i + ' |', around);
        throw new Error('unknown property type ' + JSON.stringify(t) + ' at ' + pos);
      }
    }
    path.push(name);
    const children = [];
    // Children run until the null record ends this node.
    while (true) {
      const save = pos;
      const childEnd = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos);
      if (childEnd === 0) { pos = save + (wide ? 25 : 13); break; }
      const child = readNode();
      if (child) children.push(child);
    }
    path.pop();
    return { name, props, children, start };
  };

  const roots = [];
  while (pos < buf.length - (wide ? 25 : 13)) {
    const peekEnd = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos);
    if (peekEnd === 0) break;
    const node = readNode();
    if (!node) break;
    roots.push(node);
    if (node.start === pos) break;
  }
  return { version, roots };
}

function walk(node, fn, depth = 0) {
  fn(node, depth);
  for (const c of node.children) walk(c, fn, depth + 1);
}

const file = process.argv[2];
const { version, roots } = readTree(fs.readFileSync(file));
console.log(`${file}  fbx v${version}`);

let mat = null;
walk({ name: '', props: [], children: roots }, node => {
  if (node.name === 'Material') {
    mat = node.props[0];
    console.log(`\nMATERIAL ${mat}`);
  }
  if (node.name === 'P' && mat !== null) {
    const [propName, type] = node.props;
    if (/Emissive|Diffuse|Transparent|Ambient|Opacity/i.test(propName || '')) {
      console.log('   ' + propName + ' = ' + node.props.slice(2).map(p => typeof p === 'object' ? JSON.stringify(p) : p).join(', '));
    }
  }
});
