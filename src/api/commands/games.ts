import { call } from "../invoke";
import type { GameInfo, SavedInfo } from "../../types/launcher";

export const listGames = () => call<GameInfo[]>("list_games");

export const stopGame = (id: string) => call<SavedInfo>("stop_game", { id });
