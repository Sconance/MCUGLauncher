import { call } from "../invoke";
import type { GameSessionInfo, ResourceSnapshot, SavedInfo } from "../../types/launcher";

/** 列出游戏会话及其生命周期状态。 */
export const listGameSessions = () => call<ResourceSnapshot<GameSessionInfo[]>>("list_game_sessions");

/** 请求关闭指定游戏进程。 */
export const stopGame = (sessionId: string) => call<SavedInfo>("stop_game", { sessionId });
