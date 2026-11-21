import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const tauriCli = fileURLToPath(new URL("../node_modules/@tauri-apps/cli/tauri.js", import.meta.url));

if (!existsSync(tauriCli)) {
  console.error("Tauri CLI is missing. Run the platform setup script or npm ci from the repository root.");
  process.exit(1);
}

const requestedOptions = process.argv.slice(2);
const options = requestedOptions.length ? requestedOptions : ["--debug", "--no-bundle"];
const result = spawnSync(process.execPath, [tauriCli, "build", ...options], {
  cwd: root,
  stdio: "inherit",
});

if (result.error) console.error(result.error.message);
process.exitCode = result.status ?? 1;
