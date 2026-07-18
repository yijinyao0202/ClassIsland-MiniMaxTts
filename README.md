# MiniMax Speech TTS

将 MiniMax Speech T2A v2 注册为 ClassIsland 语音提供方，用于上下课、提醒和自动化通知播报。

## 安装

从 [Releases](https://github.com/yijinyao0202/ClassIsland-MiniMaxTts/releases/latest) 下载 `ClassIsland.MiniMaxTts.cipx`，在 ClassIsland 的「应用设置 → 插件」中安装，然后重启 ClassIsland。

## 配置

1. 打开「应用设置 → 提醒 → 语音引擎」。
2. 选择 `MiniMax Speech TTS`。
3. 填写 MiniMax API Key。
4. 中国平台保持 `https://api.minimaxi.com`；国际平台改为 `https://api.minimax.io`。
5. 点击「读取账户音色」，选择音色后使用 ClassIsland 自带的「测试语音」。

也可以直接填写 MiniMax 复刻音色、生成音色或其它自定义 `voice_id`，并在插件中为其命名后保存。自定义音色会与账户音色一起显示在音色列表中。

模型 ID 同样支持手动输入、保存和删除，因此无需等待插件更新即可使用 MiniMax 后续发布的兼容语音模型。

## 短语缓存

插件会自动按中文间空格和常用中英文标点拆分播报文本，并将生成的短语音频持久化缓存。后续播报即使没有空格，也会优先匹配已经缓存的最长短语，只向 MiniMax 请求生成缺失部分。

例如首次播报 `今天值日生 张三` 时会分别缓存 `今天值日生` 和 `张三`；再次播报 `今天擦黑板张三` 时只需生成 `今天擦黑板`，随后复用 `张三` 并按原顺序播放。模型、音色、语速、音量、音调、情绪、语言增强或 API 地址变化后会自动使用另一组缓存，重启 ClassIsland 不会丢失短语索引。

API Key 仅保存在当前 ClassIsland 用户配置目录的插件设置文件中，不会写入插件包。

MiniMax Speech API 文档：https://platform.minimaxi.com/docs/api-reference/speech-t2a-http

## 开发声明

该代码由AI生成。
