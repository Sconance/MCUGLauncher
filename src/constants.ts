/** 与 origin/src/MCUGLauncher.App/Contracts/LaunchLimits.cs 保持一致。 */
export const LIMITS = {
  ramMin: 512,
  ramMax: 65536,
  windowWidthMin: 640,
  windowWidthMax: 7680,
  windowHeightMin: 480,
  windowHeightMax: 4320,
  displayNameMax: 120,
} as const;
