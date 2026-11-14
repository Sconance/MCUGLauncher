import { call } from "../invoke";
import type {
  AccountSwitchResultInfo,
  AuthResultInfo,
  AvatarInfo,
  MuaProfileInfo,
  SavedInfo,
} from "../../types/launcher";

export const readAvatar = () => call<AvatarInfo>("read_avatar");

export const loginMicrosoft = () => call<AuthResultInfo>("login_microsoft");

export const loginMua = (account: string, password: string) =>
  call<AuthResultInfo>("login_mua", { account, password });

export const listMuaProfiles = () => call<MuaProfileInfo[]>("list_mua_profiles");

export const switchAccountMode = (mode: string) =>
  call<AccountSwitchResultInfo>("switch_account_mode", { mode });

export const forgetAccount = (mode: string) => call<AuthResultInfo>("forget_account", { mode });

export const setOfflineSkin = (path: string) => call<AvatarInfo>("set_offline_skin", { path });

export const selectMuaProfile = (token: string, profileId: string) =>
  call<SavedInfo>("select_mua_profile", { token, profileId });
