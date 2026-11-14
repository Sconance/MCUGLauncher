import { call } from "../invoke";
import type { GameInfo, SavedInfo } from "../../types/launcher";

/** 列出由本次启动器启动、仍在运行的游戏。 */
export const listGames = () => call<GameInfo[]>("list_games");

/** 请求关闭指定游戏进程。 */
export const stopGame = (id: string) => call<SavedInfo>("stop_game", { id });
