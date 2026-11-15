import { access, mkdir, readFile, readdir, writeFile } from "node:fs/promises";

const sourcePath = "schemas/launcher.schema.json";
try {
  await access(sourcePath);
} catch {
  // Schema files are committed after the initial split; keep shared definitions inline.
  const common = JSON.parse(await readFile("schemas/common.schema.json", "utf8"));
  for (const file of await readdir("schemas")) {
    if (!file.endsWith(".schema.json") || file === "common.schema.json") continue;
    const path = `schemas/${file}`;
    const schema = JSON.parse(await readFile(path, "utf8"));
    schema.$defs = { ...common.$defs, ...schema.$defs };
    await writeFile(path, `${JSON.stringify(schema, null, 2)}\n`, "utf8");
  }
  process.exit(0);
}
const source = JSON.parse(await readFile(sourcePath, "utf8"));
const groups = {
  common: ["ApiError", "ResourceKeyInfo", "SavedInfo", "ResourceSnapshot"],
  instances: ["LauncherOverview", "InstanceInfo", "InstanceCatalogInfo", "ModInfo", "SavedServerInfo", "FavoriteInfo", "DataSourceSet", "SettingsInfo"],
  accounts: ["AccountInfo", "MuaProfileInfo", "LoginSessionInfo", "AvatarInfo"],
  tasks: ["TaskInfo", "GameSessionInfo", "LaunchAcceptedInfo"],
  content: ["CommunityData", "ContentItem", "ContentSearchResult", "ResourceVersionInfo", "ResourceVersionsInfo", "VanillaVersionInfo", "LoaderListInfo", "LaunchSettings", "LaunchOptionsInfo"],
  operations: ["UninstallPreviewInfo"],
};
const shared = Object.fromEntries(groups.common.map((name) => [name, source.$defs[name]]));

await mkdir("schemas", { recursive: true });
for (const [group, names] of Object.entries(groups)) {
  const defs = group === "common"
    ? shared
    : { ...shared, ...Object.fromEntries(names.filter((name) => source.$defs[name]).map((name) => [name, source.$defs[name]])) };
  const schema = {
    $schema: source.$schema,
    $id: `https://mcuglauncher.local/schemas/${group}.schema.json`,
    title: `MCUGLauncher ${group} DTO`,
    description: `MCUGLauncher ${group} 领域 DTO。字段名使用 camelCase。`,
    $defs: defs,
  };
  await writeFile(`schemas/${group}.schema.json`, `${JSON.stringify(schema, null, 2)}\n`, "utf8");
}
