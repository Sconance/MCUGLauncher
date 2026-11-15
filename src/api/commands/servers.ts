import { call } from "../invoke";
import type { SavedInfo, ServerPingInfo } from "../../types/launcher";

/** 按稳定 ID 新增或更新服务器收藏；`id` 为空表示新增。 */
export const saveServer = (name: string, address: string, id = "", instanceId: string | null = null) =>
  call<SavedInfo>("save_server", { name, address, id, instanceId });

/** 删除服务器收藏。 */
export const deleteServer = (id: string) => call<SavedInfo>("delete_server", { id });

/** 查询服务器在线人数与延迟。 */
export const pingServer = (address: string) => call<ServerPingInfo>("ping_server", { address });
