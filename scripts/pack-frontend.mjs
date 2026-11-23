#!/usr/bin/env node
// 把一个前端目录打包成 `.mcuglf`（本质是 zip），供启动器「设置 → 开发者选项 → 前端」导入。
//
// 用法：
//   node scripts/pack-frontend.mjs examples/third-party-frontend
//   node scripts/pack-frontend.mjs my-frontend --out dist-frontends/my.mcuglf
//
// 不依赖任何 npm 包：Node 自带 zlib 做 deflate，CRC32 在这里实现。
// 包内约定见 docs/FRONTEND_PACKAGE.md：根目录必须有 frontend.json。

import { createHash } from "node:crypto";
import { readdirSync, readFileSync, statSync, writeFileSync, mkdirSync } from "node:fs";
import { deflateRawSync } from "node:zlib";
import { basename, dirname, join, relative, resolve, sep } from "node:path";

const SKIP_DIRS = new Set([".git", "node_modules", "dist-frontends", ".staging"]);
const MAX_ENTRY_BYTES = 128 * 1024 * 1024;
const MAX_TOTAL_BYTES = 256 * 1024 * 1024;
const MAX_ENTRIES = 2048;

function fail(message) {
  console.error(`[frontend:pack] ${message}`);
  process.exit(1);
}

function parseArgs(argv) {
  const positional = [];
  let output = null;
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--out" || arg === "-o") {
      output = argv[++index];
      if (!output) fail("--out 后面需要一个输出路径");
    } else if (arg === "--help" || arg === "-h") {
      printHelp();
      process.exit(0);
    } else {
      positional.push(arg);
    }
  }
  if (positional.length !== 1) {
    printHelp();
    fail("需要一个前端目录。");
  }
  return { dir: positional[0], output };
}

function printHelp() {
  console.log(`用法：node scripts/pack-frontend.mjs <前端目录> [--out <文件.mcuglf>]

目录根部必须包含 frontend.json：
  {
    "identifier": "com.example.my-frontend",
    "name": "我的前端",
    "version": "1.0.0",
    "kernelApi": "^1.0.0",
    "entry": "index.html"
  }

输出默认写到 dist-frontends/<identifier>-<version>.mcuglf。`);
}

function dosDateTime(date) {
  const year = Math.max(1980, date.getFullYear());
  const time = (date.getHours() << 11) | (date.getMinutes() << 5) | (date.getSeconds() >> 1);
  const day = ((year - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate();
  return { time, date: day };
}

const CRC_TABLE = (() => {
  const table = new Uint32Array(256);
  for (let index = 0; index < 256; index += 1) {
    let value = index;
    for (let bit = 0; bit < 8; bit += 1) {
      value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
    }
    table[index] = value >>> 0;
  }
  return table;
})();

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc = CRC_TABLE[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function walk(dir, root, files) {
  const entries = readdirSync(dir, { withFileTypes: true });
  // 排序让产物可复现，便于比较两次打包的 sha256。
  entries.sort((left, right) => left.name.localeCompare(right.name));
  for (const entry of entries) {
    if (entry.isSymbolicLink()) fail(`不允许符号链接：${join(relative(root, dir), entry.name)}`);
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (SKIP_DIRS.has(entry.name)) continue;
      walk(full, root, files);
    } else if (entry.isFile()) {
      const name = relative(root, full).split(sep).join("/");
      if (name.includes("..")) fail(`非法路径：${name}`);
      files.push({ name, file: full });
    }
  }
}

function buildZip(files) {
  const localParts = [];
  const centralParts = [];
  let offset = 0;
  let total = 0;

  for (const { name, file } of files) {
    const data = readFileSync(file);
    if (data.length > MAX_ENTRY_BYTES) fail(`文件过大：${name}`);
    total += data.length;
    if (total > MAX_TOTAL_BYTES) fail("前端目录总大小超过 256 MiB");

    const nameBytes = Buffer.from(name, "utf8");
    const crc = crc32(data);
    const deflated = deflateRawSync(data, { level: 9 });
    const useDeflate = deflated.length < data.length;
    const payload = useDeflate ? deflated : data;
    const method = useDeflate ? 8 : 0;
    const { time, date } = dosDateTime(statSync(file).mtime);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4); // version needed
    local.writeUInt16LE(0x0800, 6); // flags: UTF-8 文件名
    local.writeUInt16LE(method, 8);
    local.writeUInt16LE(time, 10);
    local.writeUInt16LE(date, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(payload.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBytes.length, 26);
    local.writeUInt16LE(0, 28);
    localParts.push(local, nameBytes, payload);

    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4); // version made by
    central.writeUInt16LE(20, 6); // version needed
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt16LE(method, 10);
    central.writeUInt16LE(time, 12);
    central.writeUInt16LE(date, 14);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(payload.length, 20);
    central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(nameBytes.length, 28);
    central.writeUInt16LE(0, 30); // extra
    central.writeUInt16LE(0, 32); // comment
    central.writeUInt16LE(0, 34); // disk
    central.writeUInt16LE(0, 36); // internal attrs
    central.writeUInt32LE(0, 38); // external attrs
    central.writeUInt32LE(offset, 42);
    centralParts.push(central, nameBytes);

    offset += local.length + nameBytes.length + payload.length;
  }

  const centralSize = centralParts.reduce((sum, part) => sum + part.length, 0);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(0, 4);
  end.writeUInt16LE(0, 6);
  end.writeUInt16LE(files.length, 8);
  end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(centralSize, 12);
  end.writeUInt32LE(offset, 16);
  end.writeUInt16LE(0, 20);

  return Buffer.concat([...localParts, ...centralParts, end]);
}

const { dir: input, output } = parseArgs(process.argv.slice(2));
const root = resolve(input);
const manifestPath = join(root, "frontend.json");

let manifest;
try {
  manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
} catch (error) {
  fail(`读取 ${manifestPath} 失败：${error.message}`);
}

for (const key of ["identifier", "name", "version", "kernelApi"]) {
  if (typeof manifest[key] !== "string" || manifest[key].trim() === "") {
    fail(`frontend.json 缺少字符串字段 ${key}（打包时必须提供 identifier）`);
  }
}
if (!/^[A-Za-z0-9_-][A-Za-z0-9._-]{2,127}$/.test(manifest.identifier)) {
  fail(`identifier 需要是 3–128 个字母数字与 . - _，且不能以点开头：${manifest.identifier}`);
}
if (!/^\d+\.\d+\.\d+/.test(manifest.version)) {
  fail(`version 必须是 semver（如 1.0.0）：${manifest.version}`);
}
const entry = manifest.entry ?? "index.html";
if (entry.startsWith("/") || entry.includes("..") || entry.includes("\\")) {
  fail(`entry 必须是包内相对路径：${entry}`);
}
try {
  if (!statSync(join(root, entry)).isFile()) fail(`入口文件不存在：${entry}`);
} catch {
  fail(`入口文件不存在：${entry}`);
}

const files = [];
walk(root, root, files);
if (files.length === 0) fail("目录是空的");
if (files.length > MAX_ENTRIES) fail(`文件数超过 ${MAX_ENTRIES}`);
if (!files.some((file) => file.name === "frontend.json")) fail("打包后缺少 frontend.json");

const zip = buildZip(files);
const target = output
  ? resolve(output)
  : resolve(`dist-frontends/${manifest.identifier}-${manifest.version}.mcuglf`);
mkdirSync(dirname(target), { recursive: true });
writeFileSync(target, zip);

const sha256 = createHash("sha256").update(zip).digest("hex");
const kb = (zip.length / 1024).toFixed(1);
console.log(`[frontend:pack] ${basename(target)}`);
console.log(`  前端：${manifest.name} ${manifest.version}（${manifest.identifier}）`);
console.log(`  文件：${files.length} 个，原始 ${(files.reduce((sum, f) => sum + statSync(f.file).size, 0) / 1024).toFixed(1)} KiB`);
console.log(`  产物：${target}（${kb} KiB）`);
console.log(`  sha256：${sha256}`);
