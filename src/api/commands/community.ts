import { call } from "../invoke";
import type { CommunityData, RecommendationInfo, ResourceSnapshot, SavedInfo } from "../../types/launcher";

/** 读取社区公告、推荐与语录。 */
export const getCommunity = () => call<ResourceSnapshot<CommunityData>>("get_community");

/** 重新拉取社区内容并返回最新数据。 */
export const refreshCommunity = () => call<ResourceSnapshot<CommunityData>>("refresh_community");

/** 覆写本机推荐列表；传入删除后的完整列表。 */
export const saveRecommendations = (items: RecommendationInfo[]) =>
  call<SavedInfo>("save_recommendations", { items });
