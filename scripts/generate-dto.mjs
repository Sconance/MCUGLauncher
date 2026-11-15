import { mkdir, readFile, writeFile } from "node:fs/promises";
import { compile } from "json-schema-to-typescript";

const schemaPath = "schemas/launcher.schema.json";
const outputPath = "src/types/generated.ts";

const header = `/* eslint-disable */
/**
 * GENERATED FILE — do not edit by hand.
 * Source: schemas/launcher.schema.json
 * Run: npm run dto:generate
 */

`;

const schema = JSON.parse(await readFile(schemaPath, "utf8"));
const definitions = schema.$defs ?? {};
const output = await compile({
  $schema: schema.$schema,
  title: "LauncherDTO",
  type: "object",
  properties: Object.fromEntries(Object.keys(definitions).map((name) => [name, { $ref: `#/$defs/${name}` }])),
  $defs: definitions,
}, "LauncherDTO", {
  bannerComment: "",
  style: { singleQuote: false, semi: true },
  additionalProperties: false,
  $refOptions: { resolve: { file: false } },
});

await mkdir("src/types", { recursive: true });
await writeFile(outputPath, header + output, "utf8");
console.log(`Generated ${outputPath} from ${schemaPath}`);
