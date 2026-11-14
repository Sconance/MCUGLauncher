import { call } from "../invoke";
import type { CommunityData, RecommendationInfo, SavedInfo } from "../../types/launcher";

export const getCommunity = () => call<CommunityData>("get_community");

export const refreshCommunity = () => call<CommunityData>("refresh_community");

export const saveRecommendations = (items: RecommendationInfo[]) =>
  call<SavedInfo>("save_recommendations", { items });
