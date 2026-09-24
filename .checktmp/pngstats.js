// Sample the average colour of the pause panel sprites so the new options content can match them.
const fs = require("fs");
const zlib = require("zlib");

function decodePng(file) {
  const buf = fs.readFileSync(file);
  let pos = 8;
  let width = 0, height = 0, bitDepth = 0, colorType = 0;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString("ascii", pos + 4, pos + 8);
    const data = buf.slice(pos + 8, pos + 8 + len);
    if (type === "IHDR") {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
    } else if (type === "IDAT") idat.push(data);
    else if (type === "IEND") break;
    pos += 12 + len;
  }
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const channels = colorType === 6 ? 4 : colorType === 2 ? 3 : colorType === 0 ? 1 : 4;
  const bpp = channels * (bitDepth / 8);
  const stride = width * bpp;
  const out = Buffer.alloc(height * stride);
  let prev = Buffer.alloc(stride);
  let p = 0;
  for (let y = 0; y < height; y++) {
    const filter = raw[p++];
    const line = Buffer.from(raw.slice(p, p + stride));
    p += stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? line[x - bpp] : 0;
      const b = prev[x];
      const c = x >= bpp ? prev[x - bpp] : 0;
      let v = line[x];
      if (filter === 1) v += a;
      else if (filter === 2) v += b;
      else if (filter === 3) v += (a + b) >> 1;
      else if (filter === 4) {
        const pa = Math.abs(b - c), pb = Math.abs(a - c), pc = Math.abs(a + b - 2 * c);
        v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
      }
      line[x] = v & 0xff;
    }
    line.copy(out, y * stride);
    prev = line;
  }
  return { width, height, channels, bpp, data: out, stride };
}

function stats(file) {
  const img = decodePng(file);
  const buckets = new Map();
  let samples = 0;
  let sum = [0, 0, 0];
  let opaque = 0;
  for (let y = 0; y < img.height; y += 2) {
    for (let x = 0; x < img.width; x += 2) {
      const o = y * img.stride + x * img.bpp;
      const r = img.data[o], g = img.data[o + 1], b = img.data[o + 2];
      const a = img.channels === 4 ? img.data[o + 3] : 255;
      if (a > 200) {
        opaque++;
        sum[0] += r; sum[1] += g; sum[2] += b;
        const key = `${r >> 4},${g >> 4},${b >> 4}`;
        buckets.set(key, (buckets.get(key) || 0) + 1);
      }
      samples++;
    }
  }
  const avg = sum.map((s) => Math.round(s / Math.max(1, opaque)));
  const top = [...buckets.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5);
  console.log(file);
  console.log("  size", img.width + "x" + img.height, "channels", img.channels, "opaque%", ((opaque / samples) * 100).toFixed(1));
  console.log("  average", avg, "as 0-1:", avg.map((v) => (v / 255).toFixed(3)).join(", "));
  console.log("  top buckets", top.map(([k, n]) => `${k} -> ${n}`).join("  "));
}

for (const f of process.argv.slice(2)) stats(f);
