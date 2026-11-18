import { Input, Textarea, type InputProps, type TextareaProps } from "@chakra-ui/react";
import { forwardRef, type SelectHTMLAttributes } from "react";

/**
 * 统一输入框：Chakra 的 Input，复用项目 .input 的视觉（高度/圆角/边框/聚焦）。
 * 页面里原先的 <input className="input" /> 一律换成 <AppInput />。
 */
export const AppInput = forwardRef<HTMLInputElement, InputProps>(function AppInput(props, ref) {
  const { className, ...rest } = props;
  return <Input {...rest} ref={ref} className={className ? `input ${className}` : "input"} />;
});

/**
 * 统一多行文本：Chakra 的 Textarea，复用项目 textarea.input 的视觉。
 * 原先的 <textarea className="input"> 一律换成 <AppTextarea />。
 */
export const AppTextarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function AppTextarea(props, ref) {
  const { className, ...rest } = props;
  return <Textarea {...rest} ref={ref} className={className ? `input ${className}` : "input"} />;
});

/**
 * 统一下拉框：保留原生 select 语义，视觉复用项目 .select（含自定义双三角箭头）。
 * 原先的 <select className="select"> 一律换成 <AppSelect>，子项仍是原生 <option>。
 */
export function AppSelect({ className, ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={className ? `select ${className}` : "select"} />;
}
