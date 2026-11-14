import { call } from "../invoke";
import type {
  AccountSwitchResultInfo,
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

/** 切换账号模式（microsoft / mua / offline）。 */
export const switchAccountMode = (mode: string) =>
  call<AccountSwitchResultInfo>("switch_account_mode", { mode });

/** 移除本机保存的指定账号（microsoft / mua）。 */
export const forgetAccount = (mode: string) => call<AuthResultInfo>("forget_account", { mode });

/** 设置离线皮肤；`path` 为空字符串表示清除。 */
export const setOfflineSkin = (path: string) => call<AvatarInfo>("set_offline_skin", { path });

/** 选择 MUA 角色并完成登录。 */
export const selectMuaProfile = (token: string, profileId: string) =>
  call<SavedInfo>("select_mua_profile", { token, profileId });
