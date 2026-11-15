import { call } from "../invoke";
import type { LaunchResultInfo } from "../../types/launcher";

/** 启动指定版本或实例。 */
export const launchGame = (instanceId: string) => call<LaunchResultInfo>("launch_game", { instanceId });

/** 使用指定实例直连服务器启动。 */
export const launchToServer = (instanceId: string, serverAddress: string) =>
  call<LaunchResultInfo>("launch_to_server", { instanceId, serverAddress });
