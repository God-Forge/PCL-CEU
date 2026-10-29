# PCL Payload 格式与工具

`.payload` 是带特殊头部的 ZIP，用于打包单个 Minecraft 版本目录。

## 文件结构

```text
+----------------------+ 0
| 128 字节固定头部      |
+----------------------+ 128
| UTF-8 元数据 JSON     |
+----------------------+ zip_offset
| 存储模式 ZIP 数据      |
+----------------------+ zip_offset + zip_size
```

## 头部字段（全部小端）

| 偏移 | 长度 | 字段 | 说明 |
|---:|---:|---|---|
| 0 | 8 | magic | 固定为 `PCLPAY01` |
| 8 | 2 | version | 格式版本，当前为 `1` |
| 10 | 2 | header_size | 固定为 `128` |
| 12 | 4 | flags | bit 0：ZIP 为存储模式；bit 1：UTF-8 元数据 |
| 16 | 8 | zip_offset | ZIP 起始偏移，等于 `128 + metadata_size` |
| 24 | 8 | zip_size | ZIP 字节数 |
| 32 | 4 | metadata_size | 元数据 JSON 字节数 |
| 36 | 4 | metadata_crc32 | 元数据 CRC32 |
| 40 | 4 | zip_crc32 | ZIP 数据 CRC32 |
| 44 | 4 | entry_count | 文件条目数 |
| 48 | 8 | created_unix | 创建时间（Unix 秒） |
| 56 | 72 | reserved | 保留，写 0 |

元数据示例：

```json
{"format":"pcl-payload","version":1,"name":"1.20.1","type":"minecraft-version","created":1793252772,"entries":123}
```

## ZIP 约定

- 使用存储模式（method 0），不压缩。
- 文件名使用 UTF-8，并设置 ZIP 通用标志 bit 11。
- 不生成目录条目，解包时根据文件路径创建目录。
- 不支持加密、数据描述符、Zip64；当前限制为单文件 < 4 GB、条目数 < 65535，足够覆盖普通 Minecraft 版本目录。
- 解包时拒绝绝对路径、盘符和 `..` 路径穿越。

## 工具

```powershell
# 打包
encode.exe <版本文件夹> <输出.payload> [--name <版本名>]

# 查看头部
decode.exe --info <输入.payload>

# 解包
decode.exe <输入.payload> <输出文件夹>
```

构建：

```powershell
.\build.ps1
```

PCL 会把 `encode.exe` / `decode.exe` 嵌入资源，并在首次使用时释放到 `./runtime/`。
