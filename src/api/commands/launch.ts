import { call } from "../invoke";
import type { LaunchAcceptedInfo } from "../../types/launcher";

/** 启动指定版本或实例。 */
export const launchGame = (instanceId: string) => call<LaunchAcceptedInfo>("launch_game", { instanceId });

/** 使用指定实例直连服务器启动。 */
export const launchToServer = (instanceId: string, serverAddress: string) =>
  call<LaunchAcceptedInfo>("launch_to_server", { instanceId, serverAddress });
