import { Input, Textarea, type InputProps, type TextareaProps } from "@chakra-ui/react";
import {
  Children,
  Fragment,
  forwardRef,
  isValidElement,
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type CSSProperties,
  type KeyboardEvent,
  type ReactNode,
  type SelectHTMLAttributes,
} from "react";
import { createPortal } from "react-dom";
import checkIcon from "../assets/icons/ui/check.svg";
import chevronDownIcon from "../assets/icons/ui/chevron-down.svg";
import { usePresence } from "../shell/usePresence";
import SvgIcon from "./SvgIcon";
import "./AppSelect.css";

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

interface SelectOptionItem {
  value: string;
  label: string;
  disabled: boolean;
}

function flattenText(node: ReactNode): string {
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(flattenText).join("");
  if (isValidElement<{ children?: ReactNode }>(node)) return flattenText(node.props.children);
  return "";
}

function extractOptions(children: ReactNode): SelectOptionItem[] {
  const result: SelectOptionItem[] = [];
  const walk = (nodes: ReactNode) => {
    Children.forEach(nodes, (child) => {
      if (!isValidElement<{ value?: string | number; disabled?: boolean; children?: ReactNode }>(child)) {
        return;
      }
      if (child.type === Fragment || child.type === "optgroup") {
        walk(child.props.children);
        return;
      }
      if (child.type === "option") {
        const label = flattenText(child.props.children);
        const value =
          child.props.value !== undefined && child.props.value !== null
            ? String(child.props.value)
            : label;
        result.push({
          value,
          label,
          disabled: Boolean(child.props.disabled),
        });
      }
    });
  };
  walk(children);
  return result;
}

/**
 * 统一下拉选择器：保持与原生 <select> 一致的 props 和 <option> 子项写法，
 * 但使用圆角矩形浮动菜单替代原生直角下拉弹窗。
 */
export function AppSelect({
  className,
  style,
  children,
  value,
  defaultValue,
  onChange,
  disabled,
  id,
  name,
  title,
  "aria-label": ariaLabel,
  "aria-labelledby": ariaLabelledBy,
}: SelectHTMLAttributes<HTMLSelectElement>) {
  const options = useMemo(() => extractOptions(children), [children]);
  const isControlled = value !== undefined;
  const [innerValue, setInnerValue] = useState<string>(() => {
    if (defaultValue !== undefined && defaultValue !== null) return String(defaultValue);
    return options[0]?.value ?? "";
  });
  const currentValue = isControlled ? String(value ?? "") : innerValue;

  const [open, setOpen] = useState(false);
  const { mounted: menuMounted, closing: menuClosing } = usePresence(open, 140);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [menuStyle, setMenuStyle] = useState<CSSProperties>({});
  const [openUp, setOpenUp] = useState(false);

  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const listboxId = useId();

  const selectedIndex = options.findIndex((item) => item.value === currentValue);
  const selectedOption = selectedIndex >= 0 ? options[selectedIndex] : options[0];

  const updateMenuPosition = useCallback(() => {
    const trigger = triggerRef.current;
    if (!trigger) return;
    const rect = trigger.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const viewportWidth = window.innerWidth;
    const estimatedHeight = Math.min(260, Math.max(44, options.length * 34 + 12));
    const spaceBelow = viewportHeight - rect.bottom - 8;
    const spaceAbove = rect.top - 8;
    const shouldOpenUp = spaceBelow < estimatedHeight && spaceAbove > spaceBelow;
    const minWidth = Math.max(rect.width, 112);
    const maxWidth = Math.min(360, viewportWidth - 16);
    const width = Math.min(minWidth, maxWidth);
    const left = Math.min(Math.max(8, rect.left), viewportWidth - width - 8);

    setOpenUp(shouldOpenUp);
    setMenuStyle(
      shouldOpenUp
        ? { left, bottom: viewportHeight - rect.top + 5, minWidth: width, maxWidth }
        : { left, top: rect.bottom + 5, minWidth: width, maxWidth },
    );
  }, [options.length]);

  useLayoutEffect(() => {
    if (!open) return;
    updateMenuPosition();
  }, [open, updateMenuPosition]);

  useEffect(() => {
    if (!open) return;

    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as Node | null;
      if (!target) return;
      if (triggerRef.current?.contains(target) || menuRef.current?.contains(target)) return;
      setOpen(false);
    };

    const handleScrollOrResize = (event: Event) => {
      if (event.type === "scroll" && menuRef.current?.contains(event.target as Node)) return;
      updateMenuPosition();
    };

    window.addEventListener("mousedown", handlePointerDown);
    window.addEventListener("resize", handleScrollOrResize);
    window.addEventListener("scroll", handleScrollOrResize, true);
    return () => {
      window.removeEventListener("mousedown", handlePointerDown);
      window.removeEventListener("resize", handleScrollOrResize);
      window.removeEventListener("scroll", handleScrollOrResize, true);
    };
  }, [open, updateMenuPosition]);

  const commitValue = (nextValue: string) => {
    if (!isControlled) setInnerValue(nextValue);
    setOpen(false);
    triggerRef.current?.focus();
    if (nextValue !== currentValue && onChange) {
      const synthetic = {
        target: { value: nextValue, name: name ?? "" },
        currentTarget: { value: nextValue, name: name ?? "" },
      } as ChangeEvent<HTMLSelectElement>;
      onChange(synthetic);
    }
  };

  const findNextEnabled = (start: number, step: 1 | -1): number => {
    if (options.length === 0) return -1;
    let index = start;
    for (let count = 0; count < options.length; count += 1) {
      index = (index + step + options.length) % options.length;
      if (!options[index].disabled) return index;
    }
    return -1;
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (disabled) return;
    if (event.key === "Escape") {
      if (open) {
        event.preventDefault();
        setOpen(false);
      }
      return;
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        setActiveIndex(selectedIndex >= 0 ? selectedIndex : findNextEnabled(-1, 1));
      } else {
        setActiveIndex((prev) => findNextEnabled(prev < 0 ? selectedIndex : prev, 1));
      }
      return;
    }
    if (event.key === "ArrowUp") {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        setActiveIndex(selectedIndex >= 0 ? selectedIndex : findNextEnabled(0, -1));
      } else {
        setActiveIndex((prev) => findNextEnabled(prev < 0 ? selectedIndex : prev, -1));
      }
      return;
    }
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        setActiveIndex(selectedIndex >= 0 ? selectedIndex : findNextEnabled(-1, 1));
      } else if (activeIndex >= 0 && options[activeIndex] && !options[activeIndex].disabled) {
        commitValue(options[activeIndex].value);
      } else {
        setOpen(false);
      }
      return;
    }
    if (event.key === "Tab" && open) {
      setOpen(false);
    }
  };

  const toggleOpen = () => {
    if (disabled) return;
    setOpen((prev) => {
      const next = !prev;
      if (next) setActiveIndex(selectedIndex >= 0 ? selectedIndex : findNextEnabled(-1, 1));
      return next;
    });
  };

  return (
    <>
      <button
        ref={triggerRef}
        id={id}
        type="button"
        role="combobox"
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-controls={open ? listboxId : undefined}
        aria-label={ariaLabel}
        aria-labelledby={ariaLabelledBy}
        title={title}
        disabled={disabled}
        style={style}
        className={`app-select-trigger${open ? " is-open" : ""}${className ? ` ${className}` : ""}`}
        onClick={toggleOpen}
        onKeyDown={handleKeyDown}
      >
        <span className="app-select-label">{selectedOption?.label ?? ""}</span>
        <SvgIcon src={chevronDownIcon} className="app-select-caret" />
      </button>

      {menuMounted && typeof document !== "undefined"
        ? createPortal(
            <div
              ref={menuRef}
              id={listboxId}
              role="listbox"
              className={`app-select-menu${openUp ? " is-up" : ""}${menuClosing ? " is-closing" : ""}`}
              style={menuStyle}
            >
              {options.map((option, index) => {
                const selected = option.value === currentValue;
                const active = index === activeIndex;
                return (
                  <button
                    key={`${option.value}-${index}`}
                    type="button"
                    role="option"
                    aria-selected={selected}
                    disabled={option.disabled}
                    className={`app-select-option${selected ? " is-selected" : ""}${active ? " is-active" : ""}`}
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => commitValue(option.value)}
                  >
                    <span className="app-select-option-text">{option.label}</span>
                    {selected ? <SvgIcon src={checkIcon} className="app-select-check" /> : null}
                  </button>
                );
              })}
            </div>,
            document.body,
          )
        : null}
    </>
  );
}
