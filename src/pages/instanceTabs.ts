/**
 * 实例管理器的标签页状态。
 *
 * 实例管理器是页面级组件，离开页面就会卸载；把已打开的标签放在模块级
 * store 里，重新进入时才能恢复。这里只保存标签 id，不保存当前选中项：
 * 从「实例设置」进入时仍然直达目标实例，从实例卡进入则回到「全部实例」。
 */
let pinnedTabs: string[] = [];

export function getPinnedInstanceTabs(): string[] {
  return pinnedTabs;
}

export function setPinnedInstanceTabs(ids: string[]): void {
  pinnedTabs = ids;
}
