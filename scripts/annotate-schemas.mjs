import { readdir, readFile, writeFile } from "node:fs/promises";

for (const file of await readdir("schemas")) {
  if (!file.endsWith(".schema.json")) continue;
  const path = `schemas/${file}`;
  const schema = JSON.parse(await readFile(path, "utf8"));
  for (const [name, definition] of Object.entries(schema.$defs ?? {})) {
    definition.description ??= `${name}：通过 Tauri 命令在前后端之间传输的数据对象。`;
    for (const [field, property] of Object.entries(definition.properties ?? {})) {
      property.description ??= `${field} 字段。`;
    }
  }
  await writeFile(path, `${JSON.stringify(schema, null, 2)}\n`, "utf8");
}
