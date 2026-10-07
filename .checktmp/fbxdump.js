const fs = require('fs');
const buf = fs.readFileSync(process.argv[2]);
const version = buf.readUInt32LE(23);
console.log('version', version, 'size', buf.length);

let pos = 27;
for (let n = 0; n < 12 && pos < buf.length; n++) {
  const start = pos;
  const end = buf.readUInt32LE(pos); pos += 4;
  const numProps = buf.readUInt32LE(pos); pos += 4;
  const propLen = buf.readUInt32LE(pos); pos += 4;
  const nameLen = buf.readUInt8(pos); pos += 1;
  const name = buf.slice(pos, pos + nameLen).toString('latin1');
  console.log(`@${start}: end=${end} props=${numProps} propLen=${propLen} nameLen=${nameLen} name="${name}"`);
  if (nameLen === 0) break;
  pos += nameLen;
  // Step to the declared end so the next iteration reads what should be the next node.
  pos = end;
}
