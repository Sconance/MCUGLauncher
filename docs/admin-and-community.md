# 管理员认证与社区数据托管

本文对应 0.22.3 的「管理员」和「数据源」两页，说明怎样在本机认证管理员、怎样发布公告，以及怎样把四类社区数据托管到 GitHub 仓库：

<https://github.com/waibibabu1239/MCUGL-storage>

---

## 1. 管理员认证

入口：`设置 → 社区 → 管理员`。

0.22.3 没有内置万能密钥。第一次使用时：

1. 在「管理员密钥」输入框中输入一段只有你知道的文本，建议 16 位以上随机字符。
2. 点击「验证」。
3. 启动器会把这段文本保存到：
   `%LOCALAPPDATA%\MCUGLauncher\config.json` 的 `adminKey` 字段。
4. 页面显示「已认证」，并出现「发布公告」入口。

之后每次认证必须输入同一段文本；输错会显示「管理员密钥不匹配」。

### 重置管理员密钥

关闭启动器，删除或修改：

```
%LOCALAPPDATA%\MCUGLauncher\config.json
```

删除 `adminKey` 字段后重新打开启动器，下一次输入会重新设置密钥。

> 这是**本机功能锁**，不是服务器端权限校验。它只能防止别人随手在你电脑上发公告，不能防止别人直接改 JSON 文件。真正的多人管理员权限必须放在服务端校验。

---

## 2. 公告发布与删除

认证后，`设置 → 社区 → 管理员` 可以：

- 填写标题、正文；
- 选择过期时间；
- 选择一张配图（PNG / JPG / WebP，最长边建议不超过 1920px，文件不超过 4 MB）；
- 保存为草稿，或发布。

没有配置「写入接口」时：

- 公告只写入 `%LOCALAPPDATA%\MCUGLauncher\announcements.local.json`；
- 页面显示「已保存为待发布」；
- 不会假装已经同步给所有用户。

配置了写入接口时：

- 公告和配图（data URL）会 POST 到该接口；
- 接口返回成功后显示「公告已发布」。

删除公告：打开「查看全部」弹窗，点击每条公告右侧的垃圾桶。删除只影响本机缓存；如果数据来自远端 JSON，还需要在仓库里同步删除。

---

## 3. GitHub 仓库结构

建议把四类数据放在仓库根目录或 `data/` 下。当前示例使用根目录：

```
MCUGL-storage/
├─ announcements.json
├─ servers.json
├─ recommendations.json
├─ quotes.json
└─ images/
   ├─ announcement-1.png
   └─ quote-1.png
```

四个 JSON 都使用 UTF-8，数组根节点。示例：

### announcements.json

```json
[
  {
    "id": "2026-10-31-01",
    "title": "0.22.3 社区功能测试开启",
    "body": "群友推荐、服务器状态与数据源切换开始试运行。",
    "tag": "更新",
    "author": "TianXuan",
    "publishedAt": "2026-10-31T10:00:00+08:00",
    "expiresAt": "2026-11-30T23:59:59+08:00",
    "imageUrl": "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/images/announcement-1.png"
  }
]
```

### servers.json

```json
[
  {
    "id": "server-1",
    "name": "暮色森林",
    "address": "play.example.com:25565",
    "description": "轻生存，无经济。",
    "version": "1.20.1",
    "mode": "轻生存",
    "tags": ["公益", "轻生存"],
    "icon": "grass"
  }
]
```

在线人数、延迟和在线状态不写进 JSON，由启动器直接 ping `address` 获取。

### recommendations.json

```json
[
  {
    "id": "rec-1",
    "type": "server",
    "name": "暮色森林 · 轻生存",
    "author": "阿澈",
    "description": "无经济、无地皮，只填好种田。",
    "tags": ["1.20.1", "公益", "轻生存"],
    "version": "1.20.1",
    "size": "",
    "url": "https://example.com/server"
  }
]
```

`type` 只能是 `server`、`material`、`shader`。

### quotes.json

```json
[
  {
    "id": "q-1",
    "type": "text",
    "text": "先把今天的矿挖完，再去想明天的事。",
    "author": "阿澈",
    "source": "暮色森林",
    "created_at": "2026-10-30T23:00:00+08:00"
  },
  {
    "id": "q-2",
    "type": "image",
    "imageUrl": "https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/images/quote-1.png",
    "author": "北岸",
    "source": "红石小镇",
    "created_at": "2026-10-30T23:10:00+08:00"
  }
]
```

---

## 4. 启动器里的数据源配置

打开 `设置 → 社区 → 数据源`，选择「自定义」，分别填入四个 Raw 地址：

```text
https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/announcements.json
https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/servers.json
https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/recommendations.json
https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/quotes.json
```

逐项点「测试」确认 200 响应，再点「保存数据源」。

注意：

- 仓库必须是 **Public**，否则匿名 GET 会 401/404。
- 分支名要写实际分支，默认是 `main`；如果你用 `master` 就改掉。
- 图片同样使用 `raw.githubusercontent.com` 地址；不要使用网页 HTML 地址。
- 四类地址可以分别指向不同仓库或服务器。
- GitHub Raw 有缓存，更新后可能延迟几分钟；启动器自身缓存约 5 分钟。
- 国内网络访问 Raw 可能超时，建议准备镜像或自有域名。

---

## 5. 删除与“同步”的边界

启动器里的删除按钮分两种：

- **本机删除**：服务器、公告、群友推荐、模组等先从本机列表移除，并写入本地 JSON / 配置。
- **远端删除**：如果数据来自 GitHub JSON，本机删除不会修改 GitHub 文件。需要在仓库里删掉对应条目并提交。

这是有意设计：客户端不应该拿着 GitHub Token 去改仓库。否则任何用户都能从客户端提取 Token 并清空你的数据。

如果希望管理员在启动器里删除后对所有人生效，需要配置「写入接口」，由服务端（Cloudflare Worker / Vercel / 自有服务器）持有 GitHub Token，验证管理员密钥后再改仓库。

---

## 6. 建议的 GitHub 社区工作流

1. 在 `MCUGL-storage` 仓库里维护四个 JSON。
2. 图片放 `images/`，用 Raw 链接。
3. 启动器「数据源」填四个 Raw 地址并测试。
4. 发布公告：
   - 单机测试：用本机管理员密钥，发布后只在本机可见；
   - 正式发布：配置写入接口，或直接在 GitHub 网页编辑 JSON 并提交。
5. 修改后在启动器点「刷新」；GitHub 缓存未更新时等几分钟。

---

## 7. 写入接口的最小约定

启动器向 `写入接口` POST JSON，建议服务端实现：

```http
POST /announcements
Authorization: Bearer <admin-key>
Content-Type: application/json

{ "title": "...", "body": "...", "expiresAt": "...", "imageData": "data:image/png;base64,..." }
```

服务端职责：

- 校验管理员密钥；
- 校验图片类型和大小；
- 把图片转存到对象存储或仓库；
- 更新 `announcements.json`；
- 返回 `{ "ok": true }`。

不要把 GitHub PAT 写进启动器，也不要提交到仓库。
