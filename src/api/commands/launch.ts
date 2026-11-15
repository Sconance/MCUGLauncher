import { call } from "../invoke";
import type { LaunchAcceptedInfo } from "../../types/launcher";

/** 启动指定版本或实例。 */
export const launchGame = (instanceId: string, accountId: string) =>
  call<LaunchAcceptedInfo>("launch_game", { instanceId, accountId });

/** 使用指定实例直连服务器启动。 */
export const launchToServer = (instanceId: string, accountId: string, serverAddress: string) =>
  call<LaunchAcceptedInfo>("launch_to_server", { instanceId, accountId, serverAddress });
