import { readFile } from "node:fs/promises";
import { KERNEL_METHODS } from "@mcugl/frontpack-sdk";

/**
 * Fail when the installed `@mcugl/frontpack-sdk` method table drifts from
 * `METHODS` in `src-tauri/src/commands/kernel.rs`.
 */

const kernelPath = "src-tauri/src/commands/kernel.rs";

function quotedStrings(text, start, end) {
  const block = text.slice(start, end);
  return [...block.matchAll(/"([^"\\]+)"/g)].map((match) => match[1]);
}

const kernelSource = await readFile(kernelPath, "utf8");
const methodsStart = kernelSource.indexOf("METHODS");
const arrayStart = kernelSource.indexOf("&[", methodsStart);
const arrayEnd = kernelSource.indexOf("];", arrayStart);
if (methodsStart < 0 || arrayStart < 0 || arrayEnd < 0) {
  console.error(`无法解析 ${kernelPath} 里的 METHODS 表。`);
  process.exit(1);
}
const kernelMethods = quotedStrings(kernelSource, arrayStart, arrayEnd);
const sdkMethods = [...KERNEL_METHODS];

const problems = [];
if (kernelMethods.length !== sdkMethods.length) {
  problems.push(`数量不一致：内核 ${kernelMethods.length} 项，SDK ${sdkMethods.length} 项。`);
}
for (let index = 0; index < Math.max(kernelMethods.length, sdkMethods.length); index += 1) {
  if (kernelMethods[index] !== sdkMethods[index]) {
    problems.push(
      `第 ${index + 1} 项不一致：内核 ${kernelMethods[index] ?? "（无）"}，SDK ${sdkMethods[index] ?? "（无）"}。`,
    );
  }
}

if (problems.length > 0) {
  console.error("@mcugl/frontpack-sdk 与内核方法表不一致：");
  for (const problem of problems) console.error(`  - ${problem}`);
  console.error("修改命令后请同步更新并发布 @mcugl/frontpack-sdk。");
  process.exit(1);
}

console.log(`@mcugl/frontpack-sdk 与内核方法表一致：${kernelMethods.length} 项`);
