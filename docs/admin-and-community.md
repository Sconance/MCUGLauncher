# 管理员认证与社区数据托管

本文对应 0.22.3 的「管理员」和「数据源」两页，说明怎样在本机认证管理员、管理推荐，以及怎样把四类社区数据托管到 GitHub 仓库：

<https://github.com/waibibabu1239/MCUGL-storage>

---

## 1. 管理员认证

入口：`设置 → 社区 → 管理员`。

0.22.3 没有内置万能密钥。第一次使用时：

1. 在「管理员密钥」输入框中输入一段只有你知道的文本，建议 16 位以上随机字符。
2. 点击「验证」。
3. 启动器会把这段文本保存到：
   `%LOCALAPPDATA%\MCUGLauncher\config.json` 的 `adminKey` 字段。
4. 页面显示「已认证」，可管理本机推荐列表。

之后每次认证必须输入同一段文本；输错会显示「管理员密钥不匹配」。

### 重置管理员密钥

关闭启动器，删除或修改：

```
%LOCALAPPDATA%\MCUGLauncher\config.json
```

删除 `adminKey` 字段后重新打开启动器，下一次输入会重新设置密钥。

> 这是**本机功能锁**，不是服务器端权限校验。它只限制启动器内的推荐管理操作，不能防止别人直接改 JSON 文件。

---

## 2. 公告与推荐管理

公告为只读内容：启动器从「数据源」配置的地址读取单篇 Markdown 文件，并按约定字段生成预览；点击「查看完整公告」后，完整正文由 Markdown.Avalonia 渲染。不提供客户端公告发布或删除功能。需要新增、修改或删除公告时，编辑托管的 Markdown 文件并提交，然后在启动器刷新社区内容。

公告 Markdown 文件的预览字段和完整正文格式见[公告 Markdown 编写规范](./ANNOUNCEMENT_MARKDOWN.md)。

管理员认证仅用于推荐管理。删除推荐会重写本机的 `recommendations.local.json`；远端推荐数据需在托管仓库中单独修改。

---

## 3. GitHub 仓库结构

建议把四类数据放在仓库根目录或 `data/` 下。当前示例使用根目录：

```
MCUGL-storage/
├─ announcements.md
├─ servers.json
├─ recommendations.json
├─ quotes.json
└─ images/
   ├─ announcement-1.png
   └─ quote-1.png
```

公告使用 UTF-8 Markdown 单文件，格式见[公告 Markdown 编写规范](./ANNOUNCEMENT_MARKDOWN.md)。旧版默认 JSON 地址会自动迁移到 Markdown 示例；自定义地址不会覆盖，需要手动改为 `.md` 地址。
服务器、推荐和语录 JSON 使用 UTF-8，数组根节点。示例：

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
https://raw.githubusercontent.com/Sconance/MCUGLauncher/main/community/announcements.md
https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/servers.json
https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/recommendations.json
https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/quotes.json
```

逐项点「测试」确认公告 Markdown 格式有效、其他数据源可访问，再点「保存数据源」。

注意：

- 仓库必须是 **Public**，否则匿名 GET 会 401/404。
- 分支名要写实际分支，默认是 `main`；如果你用 `master` 就改掉。
- 图片同样使用 `raw.githubusercontent.com` 地址；不要使用网页 HTML 地址。
- 四类地址可以分别指向不同仓库或服务器。
- GitHub Raw 有缓存，更新后可能延迟几分钟。
- 国内网络访问 Raw 可能超时，建议准备镜像或自有域名。

---

## 5. 删除与“同步”的边界

启动器里的删除操作只影响本机：群友推荐在本机删除后写入本地 JSON；公告只读，不提供启动器内的删除入口。公告源不接受 JSON 列表，单个 URL 只对应一篇 Markdown。远端数据不会被修改，需要在托管仓库中编辑 Markdown 文件并提交。

这是有意设计：客户端不应该拿着 GitHub Token 去改仓库。否则任何用户都能从客户端提取 Token 并清空你的数据。

---

## 6. 建议的 GitHub 社区工作流

1. 在托管仓库里维护一篇 `announcements.md` 和三个 JSON。
2. 图片放 `images/`，用 Raw 链接。
3. 启动器「数据源」填四个 Raw 地址并测试。
4. 在 GitHub 网页编辑公告 Markdown 并提交。
5. 修改后在启动器点「刷新」；GitHub 缓存未更新时等几分钟。

---

## 7. 公告发布边界

公告服务仅执行 HTTP(S) 只读加载和 Markdown 解析。启动器没有远端公告写入接口，也不会保存 GitHub Token；公告发布、修改和删除必须在托管 Markdown 文件的仓库中完成。

旧配置中的 `WriteEndpoint` 字段仅为兼容读取而保留，不参与运行。公告数据源不再接受 JSON 数组。

以下请求格式仅为旧版本记录，当前启动器不会调用：

```http
POST /announcements
Authorization: Bearer <admin-key>
Content-Type: application/json

{ "title": "...", "body": "...", "expiresAt": "...", "imageData": "data:image/png;base64,..." }
```
