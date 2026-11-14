import { call } from "../invoke";
import type { SavedInfo, ServerPingInfo, ServerResultInfo } from "../../types/launcher";

export const saveServer = (name: string, address: string, oldAddress = "", instance = "") =>
  call<ServerResultInfo>("save_server", { name, address, oldAddress, instance });

export const deleteServer = (address: string) => call<SavedInfo>("delete_server", { address });

export const pingServer = (address: string) => call<ServerPingInfo>("ping_server", { address });
