import { call } from "../invoke";
import type { LaunchResultInfo } from "../../types/launcher";

export const launchGame = (version: string) => call<LaunchResultInfo>("launch_game", { version });

export const launchToServer = (instance: string, serverAddress: string) =>
  call<LaunchResultInfo>("launch_to_server", { instance, serverAddress });
