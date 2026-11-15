import { mkdir, readdir, readFile, writeFile } from "node:fs/promises";
import { compile } from "json-schema-to-typescript";

const schemaFiles = (await readdir("schemas"))
  .filter((file) => file.endsWith(".schema.json"))
  .sort();
const outputPath = "src/types/generated.ts";
const header = `/* eslint-disable */\n/**\n * GENERATED FILE — do not edit by hand.\n * Source: schemas/*.schema.json\n * Run: npm run dto:generate\n */\n\n`;
const definitions = {};
for (const file of schemaFiles) {
  const schema = JSON.parse(await readFile(`schemas/${file}`, "utf8"));
  Object.assign(definitions, schema.$defs ?? {});
}
const output = await compile({
  $schema: "https://json-schema.org/draft/2020-12/schema",
  title: "LauncherDTO",
  type: "object",
  properties: Object.fromEntries(Object.keys(definitions).map((name) => [name, { $ref: `#/$defs/${name}` }])),
  $defs: definitions,
}, "LauncherDTO", { bannerComment: "", style: { singleQuote: false, semi: true }, additionalProperties: false });
await mkdir("src/types", { recursive: true });
await writeFile(outputPath, header + output, "utf8");
console.log(`Generated ${outputPath} from ${schemaFiles.join(", ")}`);
