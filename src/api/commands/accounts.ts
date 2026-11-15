import { call } from "../invoke";
import type {
  AvatarInfo,
  LoginSessionInfo,
  SavedInfo,
} from "../../types/launcher";

/** 读取当前账号头像（data URL）与展示标签。 */
export const readAvatar = () => call<AvatarInfo>("read_avatar");

/** 创建登录会话；凭据仅传给后端认证适配器。 */
export const startLogin = (provider: "microsoft" | "mua", account?: string, password?: string) =>
  call<LoginSessionInfo>("start_login", { provider, account, password });

/** 查询登录会话的当前阶段。 */
export const getLoginSession = (authSessionId: string) =>
  call<LoginSessionInfo>("get_login_session", { authSessionId });

/** 为等待角色选择的登录会话选择角色。 */
export const selectLoginProfile = (authSessionId: string, profileId: string) =>
  call<LoginSessionInfo>("select_login_profile", { authSessionId, profileId });

/** 取消登录会话。 */
export const cancelLogin = (authSessionId: string) =>
  call<LoginSessionInfo>("cancel_login", { authSessionId });

/** 按稳定 ID 切换当前账号。 */
export const selectAccount = (accountId: string) => call<SavedInfo>("select_account", { accountId });

/** 按稳定 ID 移除本机保存的账号。 */
export const forgetAccount = (accountId: string) => call<SavedInfo>("forget_account", { accountId });

/** 设置离线皮肤；`path` 为空字符串表示清除。 */
export const setOfflineSkin = (path: string) => call<AvatarInfo>("set_offline_skin", { path });
