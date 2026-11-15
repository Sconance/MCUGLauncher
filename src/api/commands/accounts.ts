import { call } from "../invoke";
import type {
  AuthResultInfo,
  AvatarInfo,
  MuaProfileInfo,
  SavedInfo,
} from "../../types/launcher";

/** 读取当前账号头像（data URL）与展示标签。 */
export const readAvatar = () => call<AvatarInfo>("read_avatar");

/** 执行微软账号登录；成功后需刷新状态与头像。 */
export const loginMicrosoft = () => call<AuthResultInfo>("login_microsoft");

/** 使用 MUA 账号密码登录；成功后需刷新状态与头像。 */
export const loginMua = (account: string, password: string) =>
  call<AuthResultInfo>("login_mua", { account, password });

/** 读取 MUA 登录后的可选角色列表。 */
export const listMuaProfiles = () => call<MuaProfileInfo[]>("list_mua_profiles");

/** 按稳定 ID 切换当前账号。 */
export const selectAccount = (accountId: string) => call<SavedInfo>("select_account", { accountId });

/** 按稳定 ID 移除本机保存的账号。 */
export const forgetAccount = (accountId: string) => call<SavedInfo>("forget_account", { accountId });

/** 设置离线皮肤；`path` 为空字符串表示清除。 */
export const setOfflineSkin = (path: string) => call<AvatarInfo>("set_offline_skin", { path });

/** 选择 MUA 角色并完成登录。 */
export const selectMuaProfile = (token: string, profileId: string) =>
  call<SavedInfo>("select_mua_profile", { token, profileId });
