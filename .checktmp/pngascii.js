// Print a coarse ASCII map of a PNG: one character per sampled block, coloured by a short code.
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

const code = (r, g, b, a) => {
  if (a < 40) return " ";
  const lum = (r * 0.299 + g * 0.587 + b * 0.114) / 255;
  if (Math.abs(r - g) < 26 && Math.abs(g - b) < 26) return lum > 0.8 ? "#" : lum > 0.45 ? "+" : ".";
  if (r > g && g > b) return lum > 0.65 ? "o" : "c";   // warm orange
  if (b > r) return lum > 0.65 ? "B" : "b";            // blue
  if (r > b && g < r) return lum > 0.65 ? "P" : "p";   // pink / red
  return "?";
};

for (const file of process.argv.slice(2)) {
  const img = decodePng(file);
  const cols = 64, rows = 20;
  console.log("\n" + file + "  " + img.width + "x" + img.height);
  for (let ry = 0; ry < rows; ry++) {
    let line = "";
    for (let rx = 0; rx < cols; rx++) {
      const x = Math.floor(((rx + 0.5) / cols) * img.width);
      const y = Math.floor(((ry + 0.5) / rows) * img.height);
      const o = y * img.stride + x * img.bpp;
      const a = img.channels === 4 ? img.data[o + 3] : 255;
      line += code(img.data[o], img.data[o + 1], img.data[o + 2], a);
    }
    console.log("|" + line + "|");
  }
  // corner / centre samples
  for (const [label, x, y] of [["TL", 2, 2], ["C", img.width >> 1, img.height >> 1], ["BR", img.width - 3, img.height - 3], ["T", img.width >> 1, 2]]) {
    const o = y * img.stride + x * img.bpp;
    console.log(`  ${label} rgba=(${img.data[o]},${img.data[o + 1]},${img.data[o + 2]},${img.channels === 4 ? img.data[o + 3] : 255})`);
  }
}
