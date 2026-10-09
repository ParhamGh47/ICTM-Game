// The exact colours the HUD's own art uses, so a new icon can be drawn in the same hand rather than next to
// it. The note is hudPaperStyle.png (its outline and its fill), the compass is the one element with an accent
// colour in it beyond the blues.
const fs = require('fs');
const zlib = require('zlib');

function decodePng(file) {
  const buf = fs.readFileSync(file);
  let pos = 8;
  let width = 0, height = 0, bitDepth = 0, colorType = 0;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    const data = buf.slice(pos + 8, pos + 8 + len);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
    } else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const channels = colorType === 6 ? 4 : colorType === 2 ? 3 : 1;
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

for (const file of process.argv.slice(2)) {
  const img = decodePng(file);
  const counts = new Map();

  for (let y = 0; y < img.height; y++) {
    for (let x = 0; x < img.width; x++) {
      const o = y * img.stride + x * img.bpp;
      const a = img.channels === 4 ? img.data[o + 3] : 255;
      if (a < 200) continue;
      // Quantise a little: the art is anti-aliased and shaded, and the question is which colour family it
      // is drawn in, not the exact value of one pixel.
      const key =
        ((img.data[o] >> 4) << 8) | ((img.data[o + 1] >> 4) << 4) | (img.data[o + 2] >> 4);
      const c = counts.get(key) || { n: 0, r: 0, g: 0, b: 0 };
      c.n++;
      c.r += img.data[o];
      c.g += img.data[o + 1];
      c.b += img.data[o + 2];
      counts.set(key, c);
    }
  }

  const top = [...counts.values()].sort((a, b) => b.n - a.n).slice(0, 8);
  const total = img.width * img.height;
  console.log('\n' + file + '  ' + img.width + 'x' + img.height);
  for (const c of top) {
    const r = Math.round(c.r / c.n), g = Math.round(c.g / c.n), b = Math.round(c.b / c.n);
    console.log(
      '  ' + ((100 * c.n) / total).toFixed(1).padStart(5) + '%  rgb(' + r + ',' + g + ',' + b + ')' +
        '  #' + [r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('')
    );
  }
}
