import LiquidGlass from "simple-liquid-glass";
import type { CSSProperties, ReactNode } from "react";

interface GlassSurfaceProps {
  /** 玻璃面里的内容；留空时只作为一层背景玻璃使用 */
  children?: ReactNode;
  className?: string;
  style?: CSSProperties;
  radius?: number;
  /** 玻璃本体颜色，默认按蓝白主题调的半透明白 */
  glassColor?: string;
  /** 边缘高光颜色 */
  borderColor?: string;
  /** 磨砂强度（0–1），越小越清透 */
  frost?: number;
  /** 玻璃本色不透明度（0–1）：越高越像磨砂白瓷，越低越能透出背后的折射 */
  alpha?: number;
  /** 玻璃明度（0–100） */
  lightness?: number;
  /** 位移贴图的模糊量 */
  displace?: number;
  /** 边缘白线的粗细（0–1），越小越不显眼 */
  border?: number;
  /** 背景模糊半径 */
  blur?: number;
  /** 背景饱和度增益（百分比） */
  saturation?: number;
  /** 液态波动预设；false 表示静止 */
  liquid?: "ripple" | "flow" | "wobble" | false;
  liquidSpeed?: number;
  liquidScale?: number;
  /** 色散强度（0 起）。注意 quality="low" 时库会直接关掉色散 */
  dispersion?: number;
  /** 色散增益，与 dispersion 相乘后才是实际位移量 */
  aberrationIntensity?: number;
  /** 位移强度（梯度模式的倍率）；lens 模式请优先用 displacementScale */
  scale?: number;
  /** lens 模式的位移量（CSS 像素）。不设时沿用库默认（160×强度），小尺寸上会从很远处采样 */
  displacementScale?: number;
  /** 折射方向（度） */
  angle?: number;
  /** 折射模型：classic 用旧梯度贴图，lens 按元素形状生成圆角透镜 */
  refraction?: "classic" | "lens";
  /** lens 的光学预设 */
  lensProfile?: "material" | "loupe" | "player" | "track";
  /** 首次挂载就显示（默认等渲染器就绪，最多 400ms） */
  reveal?: "ready" | "immediate";
  /** 透镜中心（0–1 归一化坐标），凸面模式下决定“水球”位置 */
  lensCenter?: [number, number];
  /** 渲染质量：low 关闭色散，standard/high 才有着色边缘 */
  quality?: "low" | "standard" | "high" | "extreme";
  /** 透镜形态：classic 径向 / convex 凸面圆顶 / shift 整体偏移 / rim 仅边缘折射 */
  lens?: "classic" | "convex" | "shift" | "rim";
  /**
   * 折射渲染器：auto 在 Chromium 上走 SVG 贴图，webgl 走 WebGL 着色器（需要同级背景源，
   * 由外层 `LiquidGlassScene` 提供）。WebGL 复用 lensProfile / lensOptions / displacementScale。
   */
  renderer?: "auto" | "svg" | "webgl";
  /** WebGL 绘制倍率上限（设备像素/ CSS 像素）；不设时按 DPR（上限 3）。 */
  maxScale?: number;
  /**
   * WebGL 的采样源：一块**不是玻璃祖先**的元素（壁纸层、页面容器……）。
   * 不传就只能退回磨砂；传了祖先会让玻璃把自己拍进快照。
   */
  backdropSelector?: string;
  /** 光学模型：native 用自研透镜贴图，liquidgl 用 liquidGL 的 WebGL 着色器（更接近 iOS）。 */
  optics?: "native" | "liquidgl";
  /** 透镜细节（曲率、边缘弯折、高光、内发光等），值见 LENS_OPTION_RANGES */
  lensOptions?: Partial<{
    strength: number;
    depth: number;
    curvature: number;
    bend: number;
    bendWidth: number;
    sheen: number;
    sheenWidth: number;
    sheenFalloff: number;
    sheenAngle: number;
    specular: number;
    glow: number;
    glowSpread: number;
    glowFalloff: number;
    brightness: number;
  }>;
}

/**
 * 液态玻璃面：由 simple-liquid-glass 用 SVG 位移贴图做真实折射
 * （桌面 Chromium 下采样实时页面，非 Chromium 自动降级为磨砂）。
 * 参数按当前蓝白主题调过，与 `--app-glass` 观感接近。
 *
 * 注意：这个组件内部会再包一层 `position: relative; height: 100%` 的 div，
 * 所以不要用它当作页面内容的布局容器——内容会被多套一层、拿不到外层的 flex/内边距。
 * 需要玻璃底板时，把它当作绝对定位的覆盖层放在内容下方。
 */
export default function GlassSurface({
  children,
  className,
  style,
  radius = 18,
  glassColor = "rgba(255, 255, 255, 0.40)",
  borderColor = "rgba(255, 255, 255, 0.55)",
  frost = 0.08,
  alpha,
  lightness,
  displace,
  border,
  blur = 5,
  saturation = 140,
  liquid = false,
  liquidSpeed,
  liquidScale,
  dispersion,
  aberrationIntensity,
  scale,
  displacementScale,
  angle,
  refraction,
  lensProfile,
  reveal,
  lensCenter,
  quality = "low",
  lens,
  lensOptions,
  renderer,
  maxScale,
  backdropSelector,
  optics,
}: GlassSurfaceProps) {
  return (
    <LiquidGlass
      className={className}
      style={style}
      radius={radius}
      blur={blur}
      saturation={saturation}
      frost={frost}
      alpha={alpha}
      lightness={lightness}
      displace={displace}
      dispersion={dispersion}
      aberrationIntensity={aberrationIntensity}
      scale={scale}
      displacementScale={displacementScale}
      angle={angle}
      refraction={refraction}
      lensProfile={lensProfile}
      reveal={reveal}
      glassColor={glassColor}
      borderColor={borderColor}
      border={border}
      liquid={liquid}
      liquidSpeed={liquidSpeed}
      liquidScale={liquidScale}
      lensCenter={lensCenter}
      quality={quality}
      lens={lens}
      lensOptions={lensOptions}
      renderer={renderer}
      maxScale={maxScale}
      backdropSelector={backdropSelector}
      optics={optics}
    >
      {children}
    </LiquidGlass>
  );
}
