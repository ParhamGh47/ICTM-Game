const fs = require("fs");

// guid -> name, level-1 environment trees (the ones the terrains are likely painted with)
const guids = {
  "59cf3ffcebaf8484286e2a4b913917d3": "Grass",
  "ab4f38509d1217447a2db0b4db15144b": "L1 Bush 1",
  "2249043e3903ea647bbaa9494e95ad78": "L1 Bush",
  "8245a9a613e24ab4099494003f80b6bd": "L1 Tree-1 1",
  "8655f6382c61a854aaa3cf5df3bf5edf": "L1 Tree-1",
  "5627eef9c52de2546916b85280dd1952": "L1 Tree-2 1",
  "97de121d95f79144f8fbd44efd6541f0": "L1 Tree-2",
  "26b4016cc7a86474f858b1129550e5d6": "L1 Tree-3 1",
  "f9a0ef6f12533b34ba718fbe64f747d4": "L1 Tree-3",
  // level 1 DetailsTerrain set, in case those are the painted ones
  "eef05caaf52338541b8a242ddcf4438d": "L1D Bush",
  "f7d39b04802ab5b48a79c6b23d493bae": "L1D Tree-1",
  "e60658a7ea0b4ce489daf80802816f36": "L1D Tree-2",
  "2b42d15d8a612e74592d4dd9ed8e2e15": "L1D Tree-3",
};

const files = [
  "Assets/Terrains/New Terrain 5.asset",   // Core-1
  "Assets/New Terrain 2.asset",            // Core-3
  "Assets/New Terrain 1.asset",            // Core-4
];

function swap4(hex) {
  // helper: bytes in 4-byte-word-reversed order
  let out = "";
  for (let i = 0; i < 16; i += 8) out += hex.substr(i + 6, 2) + hex.substr(i + 4, 2) + hex.substr(i + 2, 2) + hex.substr(i, 2);
  return out;
}
function swapAll(hex) {
  let out = "";
  for (let i = 0; i < 16; i += 2) out += hex.substr(i, 2);
  // reverse the 16 bytes
  return Buffer.from(hex, "hex").reverse().toString("hex");
}

for (const f of files) {
  const b = fs.readFileSync(f);
  console.log("\n===", f, b.length, "bytes");
  for (const [g, name] of Object.entries(guids)) {
    const variants = { "as-is": Buffer.from(g, "hex"), "reversed": Buffer.from(g, "hex").reverse(), "word-swapped": Buffer.from(swap4(g), "hex") };
    const found = [];
    for (const [vn, bytes] of Object.entries(variants)) {
      const at = b.indexOf(bytes);
      if (at !== -1) found.push(vn + "@0x" + at.toString(16));
    }
    if (found.length) console.log("  " + name + " (" + g + "): " + found.join(", "));
  }
}
