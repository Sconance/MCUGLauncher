import { call } from "../invoke";
import type { SavedInfo, ServerPingInfo, ServerResultInfo } from "../../types/launcher";

/** 新增或更新服务器收藏；`oldAddress` 为空表示新增。 */
export const saveServer = (name: string, address: string, oldAddress = "", instance = "") =>
  call<ServerResultInfo>("save_server", { name, address, oldAddress, instance });

/** 删除服务器收藏。 */
export const deleteServer = (address: string) => call<SavedInfo>("delete_server", { address });

/** 查询服务器在线人数与延迟。 */
export const pingServer = (address: string) => call<ServerPingInfo>("ping_server", { address });
