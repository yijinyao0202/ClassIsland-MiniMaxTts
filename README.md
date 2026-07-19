# MiniMax Speech TTS

把 MiniMax 的语音合成接入 ClassIsland，用来播放上下课提醒、日程提醒和自动化播报。

## 你需要准备

- 一个 MiniMax 账号
- MiniMax API Key
- 账号里可用的语音额度

如果账号余额不足，插件能正常连接，但播报时 MiniMax 会拒绝生成语音。

## 安装

1. 打开 [最新 Release](https://github.com/yijinyao0202/ClassIsland-MiniMaxTts/releases/latest)。
2. 下载 `ClassIsland.MiniMaxTts.cipx`。
3. 在 ClassIsland 里打开「应用设置 → 插件」。
4. 安装这个 `.cipx` 文件。
5. 重启 ClassIsland。

## 基本配置

1. 打开「应用设置 → 提醒 → 语音引擎」。
2. 选择 `MiniMax Speech TTS`。
3. 填写 `API Key`。
4. 选择或填写模型 ID。
5. 选择或填写音色 ID。
6. 用 ClassIsland 自带的「测试语音」试播。

API 地址一般不用改：

- 中国大陆平台：`https://api.minimaxi.com`
- 国际平台：`https://api.minimax.io`

## 音色怎么填

你可以用两种方式配置音色。

第一种：点「读取账户音色」，插件会从 MiniMax 账号读取可用音色，然后在列表里选择。

第二种：直接填 `voice_id`。这适合复刻音色、生成音色，或 MiniMax 后台里已有但列表没有显示的音色。填好后可以点「保存自定义音色」，下次就能直接在列表里选。

## 如何复刻音色

插件本身负责使用音色播报，不负责上传音频或创建复刻音色。复刻音色需要先在 MiniMax 平台或 MiniMax API 里完成，拿到 `voice_id` 后再填回插件。

### 方式一：在 MiniMax 页面操作

1. 登录 MiniMax 开放平台。
2. 进入语音相关的音色/复刻功能。
3. 按平台要求上传声音样本。
4. 生成或复制最终的 `voice_id`。
5. 回到 ClassIsland，打开 `MiniMax Speech TTS` 配置。
6. 把 `voice_id` 填到「音色 ID」。
7. 点「保存自定义音色」，给它取一个容易认的名字。
8. 用「测试语音」确认能正常播报。

### 方式二：用 MiniMax API 复刻

API 复刻的大致流程是：

1. 上传待复刻音频，得到 `file_id`。
2. 可选：上传一段很短的示例音频，增强相似度和稳定性。
3. 调用音色复刻接口，传入自定义 `voice_id`。
4. 在插件里填写这个 `voice_id`。

MiniMax 对复刻音频有要求：

- 格式：`mp3`、`m4a` 或 `wav`
- 待复刻音频：不少于 10 秒，不超过 5 分钟
- 文件大小：不超过 20 MB
- 自定义 `voice_id`：8 到 256 个字符，首字符必须是英文字母，可包含数字、字母、`-`、`_`，末尾不能是 `-` 或 `_`

示例 `voice_id`：

```text
YjyVoice001
```

复刻完成后，插件里只需要填这个 `voice_id`，不需要上传音频文件。

## 模型怎么填

插件内置了常用模型，也支持手动输入模型 ID。

如果 MiniMax 后续发布了新的兼容语音模型，不需要等插件更新。直接把模型 ID 填进去，点「保存自定义模型」即可。

## 短语缓存是什么

插件会自动缓存已经生成过的短语，减少重复请求 MiniMax。

例如第一次播报：

```text
今天值日生 张三
```

插件会拆成：

```text
今天值日生
张三
```

这两个音频都会被缓存。

以后再播报：

```text
今天擦黑板张三
```

插件会发现 `张三` 已经缓存过，只需要重新生成：

```text
今天擦黑板
```

然后按顺序播放 `今天擦黑板` 和 `张三`。

## 怎样让缓存更好用

第一次播报新内容时，建议在容易复用的地方加空格或标点。

推荐：

```text
今天值日生 张三
```

不推荐第一次就写成：

```text
今天值日生张三
```

原因是插件无法第一次就准确知道 `张三` 是一个需要单独复用的名字。只要第一次用空格或标点建立过缓存，后面没有空格也能自动匹配。

## 缓存什么时候不会复用

下面这些语音参数变了以后，插件会使用另一组缓存，不会错误复用旧音频：

- API 地址
- 模型
- 音色
- 语速
- 音量
- 音调
- 情绪
- 语言增强

API Key 不会写入短语缓存索引。

## 钱包和余额怎么查

插件目前不内置钱包余额查询。

原因是 MiniMax 文档里没有给普通开放平台 API Key 提供一个稳定的“查询钱包余额”接口。插件不能用非公开接口去抓控制台页面，否则容易失效，也不适合保存更多账户权限。

你可以这样查：

### 按量付费余额

登录 MiniMax 开放平台，在「账户管理 → 余额」里查看和充值。

如果余额不足，播报时通常会失败，并返回类似 `insufficient balance` 或错误码 `1008`。

### Token Plan 额度

如果你用的是 MiniMax CLI / Token Plan，可以在终端里查：

```powershell
mmx quota
```

注意：Token Plan 的订阅 Key 和普通开放平台 API Key 不是一回事。插件当前使用的是普通 MiniMax Speech API Key。

## 常见问题

### 点「读取账户音色」失败

检查 API Key 是否正确，以及 API 地址是否对应你的 MiniMax 账号区域。

### 测试语音失败，提示余额不足

这是 MiniMax 账号额度问题，不是插件安装问题。需要在 MiniMax 账号里确认余额或套餐。

### 修改设置后重启会不会丢

不会。API 地址、模型、音色、自定义模型、自定义音色和语音参数都会自动保存。

### 缓存重启后还在吗

还在。短语缓存会保存在 ClassIsland 的缓存目录里。音频文件丢失时，插件会自动清理对应索引。

## 相关链接

- MiniMax Speech API 文档：https://platform.minimaxi.com/docs/api-reference/speech-t2a-http
- 插件 Release：https://github.com/yijinyao0202/ClassIsland-MiniMaxTts/releases/latest

## 开发声明

该代码由AI生成。
