# 0.22.3-codex 代码结构复查

本轮按“窗口不堆业务、优先复用 Core、组件按职责划分”的方向整理，没有通过 partial MainWindow 隐藏复杂度。

## 已落实

- MainWindow 251 行，新增的内容只有原生保存对话框和确认框；业务不进窗口。
- LauncherApplication 负责消息解析/分发，状态快照由只读 LauncherStateBuilder 生成。
- 克隆/导出、卸载、更新分别在独立 UseCases；执行在 InstanceOperationExecutor 与 Core，不重复实现文件复制/依赖解析/删除算法。
- 实例快照集中读取根目录、数据目录、准确加载器版本与有效启动参数；队列不依赖页面当前选择。
- 卸载和更新使用有上限、有有效期的预览令牌；客户端不能直接给删除路径。
- 前端入口 app.js 231 行；导航、主页、资源、设置、实例页面、弹窗、图形与演示数据分别放置。host.js 829 行。
- 原生模式的 state.js 从空数据开始，bootstrap 仅在普通浏览器加载 preview-data。真实状态和演示数据不再依赖先后覆盖。
- C# 统一格式与导入顺序；JS 使用 Prettier（2 空格、单引号、100 列），Git 和编辑器统一 LF。两种格式检查与边界检查均接入 CI。

## 保留的改进项

host.js 的旧事件绑定还可按功能继续迁移；LauncherContext 仍是较宽的共享依赖，不能据此声称所有依赖都已彻底隔离。Core 内部也还没有拆成独立 Domain/Infrastructure 项目。

行数只是辅助检查，不能替代职责审查。新增维护操作已补真实文件夹的隔离测试；真实游戏、账号与原生窗口仍需另外验收。

后台维护经任务执行器运行；启动准备登记占用并固定身份/目录/参数，状态刷新通过注入的窗口调度执行，避免直接在工作线程枚举可变界面配置。相应检查包含测试进程，仍不代替真实 Minecraft 验收。

## 页面遗漏修订

- 目录与实例交互放在 directory.js；设置保存事件放在 settings-actions.js；推荐跳转和刷新放在 community-actions.js，不向 MainWindow 增加业务。
- 设置按当前控件发送字段，Java 保存不携带内存和窗口默认值。目录验证在修改配置之前完成，目录历史随配置保存。
- 模组操作使用 data-mod-action，不通过中文按钮文字匹配；未接通的固定更新、皮肤操作明确禁用。
- 浏览器检查增加目录选择/添加/导入/刷新、折叠/搜索状态、12 项模组、设置字段和社区刷新；仍不等同于真实游戏验收。

## 登录与进度依赖整理

- MicrosoftAccountService 不创建登录窗口，不探测 WebView2；MainWindow 组装入口提供 IWebUI 工厂，UseCases 仅传递接口。静默认证不需要原生窗口。
- MicrosoftLoginWebUi 只做线程调度；MicrosoftLoginWindow 按布局、初始化、导航、重载和释放组织方法，关闭窗口会取消初始化等待。
- 缓存权限、磁盘、运行库、超时与组件异常分别转换成安全提示，不输出原始 OAuth URL 或响应。
- InstallationStages 集中命名安装阶段权重，保留原来的阶段比例和提交后 100% 语义。
- 架构检查增加认证服务不得创建具体 UI 或探测 WebView2 的规则。

## 账号切换

AccountModeCommands 负责恢复/选择/移除流程，AccountCommands 负责交互登录；均复用现有认证服务，不引用 WPF。accounts.js 负责账号控件，host.js 不继续堆账号事件。模式切换使用请求序号拒绝过期结果，离线玩家名独立保存。回归覆盖两类在线模式、网络与失效区别、晚到恢复结果及前端标签不直接弹登录。
