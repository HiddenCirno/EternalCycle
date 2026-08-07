# EternalCycle（永恒时序）JSON 编写规范指导文档

> 版本：1.3.0  |  目标引擎：SPTarkov Server `~4.1.0`
> 依据源码：本仓库 `EternalCycle/` 目录下的 `Classes/*.cs`（反序列化定义）与 `Utils/*.cs`（加载逻辑）
> 本文档以**代码为唯一真理**（Source of Truth），所有字段、多态判别、默认值均直接来源于源码。

---

## 目录

1. [总览：工作原理](#一总览工作原理)
2. [通用约定（必读）](#二通用约定必读)
3. [物品 `CustomItemTemplate`](#三物品-customitemtemplate)
4. [任务 `CustomQuest`](#四任务-customquest)
5. [任务逻辑树 `QuestLogicTree`](#五任务逻辑树-questlogictree)
6. [任务区域 `QuestZone`](#六任务区域-questzone)
7. [商人 `TraderBaseWithDesc`](#七商人-traderbasewithdesc)
8. [报价单 `CustomAssortData`](#八报价单-customassortdata)
9. [配方 `CustomRecipeData`](#九配方-customrecipedata)
10. [成就 `CustomAchievementData`](#十成就-customachievementdata)
11. [套装 `CustomSuit`](#十一套装-customsuit)
12. [自定义外观 `CustomCustomizationItem`](#十二自定义外观-customcustomizationitem)
13. [武器预设 `CustomPresetData`](#十三武器预设-custompresetdata)
14. [礼物码 `CustomGiftCodeData`](#十四礼物码-customgiftcodedata)
15. [抽奖池 `DrawPoolClass`](#十五抽奖池-drawpoolclass)
16. [机器人修改 `CustomAlterBot`](#十六机器人修改-customalterbot)
17. [物品标签 `ItemTagDictionary`](#十七物品标签-itemtagdictionary)
18. [资源同步（客户端 API）](#十八资源同步客户端-api)
19. [枚举参考](#十九枚举参考)
20. [配置文件 `config.jsonc`](#二十配置文件-configjsonc)
21. [`Register*` 注册函数速查表](#二十一-register-注册函数速查表)
22. [完整 JSON 示例](#二十二完整-json-示例)
23. [常见问题（FAQ）](#二十三常见问题faq)

---

## 一、总览：工作原理

EternalCycle 是一个 SPTarkov 服务端 Mod。它采用 **“反序列化定义 + 事件驱动加载”** 的架构：

```
                    ┌─────────────────────────────────────────────┐
  JSON 数据文件      │  加载管线（事件驱动，严格顺序）                 │
  (物品/任务/商人…)  │                                            │
     │              │  ① SaveServer.LoadAsync 被 Patch 钩住        │
     ▼              │  ② 触发 30 个 DataLoadEvent 阶段事件          │
  ① 反序列化         │  ③ 各 Utils.InitXxx 把数据写入原版数据库      │
  JsonNode 预处理    │                                            │
  → 自定义转换器      │                                            │
  → CustomXxx 类     │─────────────────────────────────────────────│
                    │  数据库: Items / Quests / Traders / Handbook │
                    │  / Prices / Locales / Hideout / Globals 等   │
                    └─────────────────────────────────────────────┘
```

- **反序列化层**：所有 JSON 通过 `System.Text.Json`（`JsonSerializer`）+ 自定义 `JsonConverter` 解析进 `Classes/*.cs` 定义的 `CustomXxx` 类。
- **注册层**：每个 `Register*` 函数（如 `RegisterItem`）把加载逻辑挂到 `EventManager.DataLoadEvent` 对应阶段事件上；事件由 `ProfileHelperPatch` 在 `SaveServer.LoadAsync` 时按顺序触发。
- **写入层**：`Init*` 函数把 `CustomXxx` 对象克隆/覆盖到原版数据库对象，最终对客户端生效。

> 重要：这些 `Register*` 是**公开 API**，供本 Mod 或其它 Mod 在代码中调用。若你只编写 JSON 数据文件而不编写代码，需要由 Mod 作者（或你自己写一个入口类）调用对应 `Register*` 将你的数据目录注册进加载管线。**JSON 本身不会自动被发现。**

---

## 二、通用约定（必读）

### 2.1 ID 与哈希规则（最重要）

Tarkov 的 MongoId 是 **24 位十六进制**字符串。EternalCycle 允许你在 JSON 中写**任意字符串**（包括中文），加载时通过 `ConvertHashID()` 自动转换：

| 输入 | 处理 | 示例 |
|---|---|---|
| 已是 24 位十六进制 | **原样保留** | `"5b47574386f77428ca22b33c"` → 不变 |
| 其它任意字符串 | SHA1 哈希取前 24 位（小写 hex） | `"永恒之环"` → `"ff1a8b8c…"`（24位） |

- 该规则对所有标注“ID”的字段生效（`MongoIdConverter` / `StringHashConverter` 自动执行）。
- 同一字符串每次哈希结果**一致**，所以同名字段（如物品 ID）在多个文件中写同一字符串即可稳定关联。
- 物品 `_props` 内部嵌套的所有 ID 引用（`Slots._parent`、`Grids.Filter`、`defAmmo` 等）会被 `ResolveJsonNode` 递归处理，你不需要手动算哈希。

### 2.2 多态判别字段 `$type`

凡是有 `[JsonDerivedType]` 的类，JSON 对象内**必须**提供 `"$type": "xxx"` 来选择具体子类型。`$type` 的值是固定类型串（见各章节）。例如：

```jsonc
"_customprops": {
    "$type": "lootable",   // 选择 LootableItemProps 子类
    "Name": "我的物资",
    ...
}
```

### 2.3 JSONC 注释支持

所有可编辑 JSON **支持 `//` 行注释**（服务端用 `JsonCommentHandling.Skip` 解析）。你可以放心写注释。

### 2.4 文件组织：文件夹 与 单文件 两种模式

每个 `Register*` 都接受“文件夹路径”或“单文件路径”：

| 模式 | 判定 | 解析方式 | 适用容器 |
|---|---|---|---|
| 文件夹 | `Directory.Exists` | 遍历文件夹内**每个文件**独立反序列化 | 单对象 或 对象 List |
| 单文件 | `File.Exists` | 整个文件反序列化为一个容器 | `Dictionary` 或 `List` |

- **文件夹模式**下文件命名任意，但每个文件的结构必须与对应 `Register*` 的解析类型一致（例如注册任务时，每个文件是单个 `CustomQuest` 对象；注册成就时每个文件是单个 `CustomAchievementData`；注册套装时每个文件是 `List<CustomSuit>`）。
- 例外：`RegisterQuestZones` 文件夹模式只扫描 `*.json*` 扩展名文件。
- 单文件模式的具体容器类型请对照 [21. `Register*` 速查表](#二十一-register-注册函数速查表)。

### 2.5 加载顺序（阶段事件）

`ProfileHelperPatch` 按以下**严格顺序**触发（ProfileHelperPatch.cs:114-142）。若你的数据间有依赖，注意安排注册阶段：

```
PreDataLoad
 → LoadItem              (物品)
 → LoadTraderBase        (商人基础)
 → LoadQuest             (任务骨架)
 → LoadAchievement       (成就)
 → LoadRecipe            (藏身处配方)
 → LoadScavCaseRecipe    (SCAV宝箱配方)
 → LoadCultistCircleRecipe (邪教圈配方)
 → LoadGiftCode          (礼物码)
 → LoadAlterBot          (机器人修改)
 → LoadItemTag           (物品标签)
 → LoadDrawPool          (抽奖池)
 → LoadTraderAssort      (商人报价单)
 → LoadQuestData         (任务条件，延迟执行)
 → LoadQuestReward       (任务奖励，延迟执行)
 → LoadLockedTraderAssort (锁定报价单奖励)
 → LoadLockedRecipe      (锁定配方奖励)
 → LoadQuestLogic        (任务逻辑树)
 → LoadQuestLocale       (任务本地化)
 → LoadLocale            (通用本地化)
 → LoadPreset            (武器预设)
 → LoadCustomization     (外观)
 → LoadSuit              (套装)
 → LoadHideoutCustomization (藏身处外观)
 → LoadQuestZone         (任务区域)
 → LoadResource          (资源)
PostDataLoad
 → FixItemCompatible     (物品兼容修复)
 → AfterModLoaded
 → PreRagfairLoad        (跳蚤市场初始化前)
```

> 注意：任务的条件（`QuestData`）与奖励（`QuestReward`）虽然定义在任务 JSON 里，但实际在 `LoadQuestDataEvent` / `LoadQuestRewardEvent` 阶段（较晚）才真正写入，以保证任务与奖励依赖的物品已就绪。

### 2.6 本地化机制（多语言）

- 支持 `ch`（简中）、`en`（英文）、`jp`（日文）。
- 物品：中文取 `Name / ShortName / Description`；英文取 `EName / EShortName / EDescription`（缺失回退中文）；日文取 `JName / JShortName / JDescription`（缺失回退中文）。
- 物品描述会自动追加来源水印：`Created By: <creator> / Added By: <modname> / ModAPI: EternalCycle / Item Id: {id}`。
- 任务、商人、成就、外观的本地化由各自的 `Init*` 通过 `AddTransformer` 注入，无需你单独配置语言文件。

### 2.7 物品实例 `CustomItem`（原版 `Item` 的派生）

很多 JSON（报价单 `Item`、预设 `Preset`、礼物数据 `item`、任务奖励 `Items`）都用 `List<CustomItem>` 描述**一组物品实例树**。`CustomItem` 继承自 SPTarkov 原版 `Item`，其 JSON 字段（Tarkov 物品实例的标准格式）：

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `_id` | string | ✅ | 该物品实例的 ID（哈希） |
| `_tpl` | string | ✅ | 物品模板 ID（`_id`，即 `CustomItemTemplate` 的 `_id`） |
| `parentId` | string | 可选 | 父物品实例 ID（根节点为 `"hideout"` 或省略） |
| `slotId` | string | 可选 | 所在槽位（如 `mod_muzzle`） |
| `location` | object | 可选 | 容器内的网格位置 |
| `upd` | object | 可选 | 物品动态属性（`StackObjectsCount` 堆叠数、`UnlimitedCount` 无限、`SpawnedInSession` 等） |

- **根节点**（第 1 个元素）的 `parentId` 通常写 `"hideout"`。
- 物品实例内的 ID 同样会被 `ConvertHashID` 处理；`RegenerateItemListData` 还会对所有实例 ID 加“盐”重新哈希，避免与你复制的原版物品 ID 冲突。
- 若主物品是**弹药包**（手册分类为“弹药包”），加载时会自动补充其内装弹药子对象。

### 2.8 常用手册分类（`RagfairType` / `ParentId`）

`CustomProps.RagfairType` 与任务/商人中的分类字段可填以下 ID 或其对应中文（会被哈希）。完整列表见 [19. 枚举参考](#十九枚举参考)。

常用分类示例：`"其他"`、`"武器零件或配件"`、`"弹药包"`、`"次元博物"`（本 Mod 自定义）等。

---

## 三、物品 `CustomItemTemplate`

- **源码**：`EternalCycle/Classes/ItemClasses.cs:20-42`
- **注册**：`ItemUtils.RegisterItem(modPath, path, creator, modname)` → `LoadItemEvent`
- **文件模式**：文件夹 = 每文件一个对象；单文件 = `{"key": CustomItemTemplate, ...}` 字典（Key 会被忽略，以对象内 `_id` 为准）

### 3.1 顶层结构

```jsonc
{
  "_id": "物品ID（字符串，自动哈希）",
  "_targetid": "复制的原版物品模板ID（如 5449016a4bdc2d6f028b456f）",
  "_parent": "（可选）覆盖父类模板ID",
  "_name": "（可选）模板内部名",
  "_proto": "（可选）原型模板ID",
  "_type": "（可选）模板类型，如 Item/Node",
  "_props": { ... },          // （可选）覆盖原版物品属性（键名与 SPT TemplateItemProperties 一致）
  "_customprops": { "$type": "...", ... }  // ✅ 必填，自定义属性（见 3.2）
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `_id` | string | ✅ | 新物品的模板 ID（自动哈希）。**同名 `_id` 的已有物品会被覆盖更新（更新模式）** |
| `_targetid` | string | ✅ | 复制的**原版物品模板 ID**。加载时克隆该物品作为基底，再叠加你的属性。若 `_id` 已存在则直接以现有物品为基底 |
| `_parent` | string | 可选 | 覆盖物品的 `_parent`（继承关系） |
| `_name` | string | 可选 | 模板内部名 |
| `_proto` | string | 可选 | 覆盖 `_proto` |
| `_type` | string | 可选 | 覆盖 `_type`（如 `"Item"`） |
| `_props` | object | 可选 | **非空属性覆盖**到原版物品属性（`CopyNonNullProperties`：源属性为 null 时保留原值） |
| `_customprops` | object | ✅ | 自定义属性，`$type` 多态（见 3.2） |

> **工作原理（CreateAndAddItem）**：克隆 `_targetid` 的原版物品 → `_props` 非空覆盖 → 设置 `_id/_parent/_proto/_type` → 依次执行自定义处理（Buff / 黑名单 / 局内限数 / 狗牌 / 价格 / 武器专精 / 任务刷点 / 容器尺寸 / 礼物箱 / 战利品 / 燃料 / 兼容修复）→ 写入多语言 → 写入物品表。

### 3.2 自定义属性 `_customprops`

基类 `CustomProps` 的公共字段（ItemClasses.cs:53-127）：

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `Name` | string | ✅ | 中文名 |
| `ShortName` | string | ✅ | 中文短名 |
| `Description` | string | ✅ | 中文描述 |
| `EName` / `EShortName` / `EDescription` | string | 可选 | 英文（缺失回退中文） |
| `JName` / `JShortName` / `JDescription` | string | 可选 | 日文（缺失回退中文） |
| `DefaultPrice` | int | ✅ | 基础价格。手册价与跳蚤价的后备值 |
| `RagfairPrice` | int | 可选 | 跳蚤市场价覆盖（优先于 `DefaultPrice`） |
| `RagfairType` | string | 可选 | 手册分类（中文或 ID）。为空时沿用目标物品分类，再回退“其他” |
| `CopyPrice` | bool | 可选 | `true` 时价格从 `_targetid` 复制（忽略上面两个价格字段） |
| `isMoney` | bool | 可选 | `true` 时注册为**自定义货币**（加入 `CustomMoneyTpls`） |
| `addToKappa` | bool | 可选 | `true` 时加入 Kappa 容器可收纳列表 |
| `BlackListType` | int | 可选 | **位掩码**黑名单（见下方），如 1=空投 2=PMC拾取 4=SCAV箱子… |
| `SafeMode` | bool | 可选 | 预留字段（当前加载逻辑未使用） |
| `InRaidLimit` | int | 可选 | 局内最大同时存在数量（`RestrictionsInRaid`） |
| `InLobbyLimit` | int | 可选 | 大厅最大持有数量（默认 -1 = 不限制） |
| `ApplyAsPMCDogTag` | bool | 可选 | `true` 时将该物品作为 PMC 狗牌加入战利品池 |
| `ApplyToBEAR` / `ApplyToUSEC` | bool | 可选 | 应用到哪个阵营的狗牌池 |
| `ApplyToStandard` / `ApplyToEOD` / `ApplyToUnheard` | bool | 可选 | 应用到哪个游戏版本的狗牌池 |
| `FuelLevel` | int | 可选 | 发电机燃料等级（加入对应等级发电机的可消耗燃料） |

**`BlackListType` 位掩码取值**（可相加组合）：

| 值 | 含义 |
|---|---|
| 1 | 空投黑名单 |
| 2 | PMC 拾取战利品黑名单 |
| 4 | SCAV 宝箱战利品黑名单 |
| 8 | 跳蚤黑市商人（Fence）黑名单 |
| 16 | 邪教圈黑名单 |
| 32 | 每日任务奖励黑名单 |
| 64 | 全局黑名单 |

### 3.3 `$type` 子类型一览

| `$type` | C# 类型 | 附加能力 |
|---|---|---|
| `base` | `CustomProps` | 普通物品（默认） |
| `fixed` | `CustomFixedItemProps` | **兼容修复**：将新物品加入/移除其它物品的兼容筛选器 |
| `weapon` | `WeaponItemProps` | **武器**：专精（Mastering）处理 |
| `lootable` | `LootableItemProps` | **战利品**：可被搜刮、静态/动态战利品刷点 |
| `container` | `CustomSizeContainerProps` | **容器**：自定义格子尺寸 |
| `giftbox` | `GiftBoxProps` | **礼盒**：4 种礼物箱机制 |
| `buff` | `BuffItemProps` | **BUFF**：定义全局 Stimulator 增益（配合 `_props.StimulatorBuffs`） |
| `quest` | `QuestItemProps` | **任务刷点**：在地图定义强制刷新点 |

#### `fixed` —— 兼容修复（CustomFixedItemProps）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `CustomFixID` | string | 修复目标物品 ID（缺省用 `_targetid`） |
| `FixType` | string[] | 修复类型集合（如 `["Slots","Grids"]`），把新物品加进目标的对应兼容筛选器 |

> `FixType` 的合法字符串由 `FixItems` 内部逻辑决定（如 `"Slots"`、`"Grids"`、`"Quests"`、`"InRaidLimit"` 等）。最终在 `LoadFixItemCompatibleEvent` 阶段批量执行。

#### `weapon` —— 武器专精（WeaponItemProps）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `FixMastering` | bool | `true` 时将新武器加入目标武器的专精数据 |
| `AddMastering` | bool | `true` 时为新武器添加自定义专精 |
| `Mastering` | object | 专精数据（`CustomMastering`，字段同 SPT `Mastering`） |
| `CustomMasteringTarget` | string | 自定义专精应用目标 |

#### `lootable` —— 战利品（LootableItemProps）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `CanFindInRaid` | bool | ✅ 主开关，`false` 则本物品不参与任何战利品生成 |
| `CustomLoot`（`UseCustomData`） | bool | `true` 时用自定义除数控制概率（见下） |
| `MapLoot` | bool | `false` 关闭**动态战利品**（地图散落物） |
| `CustomMapLootTarget` | string / string[] | 动态战利品的参考物品 ID（`ListOrT`：单值或数组）；缺省用 `_targetid` |
| `MapLootDivisor` | int | 动态战利品概率除数（`CustomLoot=true` 时生效，默认 4） |
| `StaticLoot` | bool | `false` 关闭**静态战利品**（容器内固定刷新） |
| `CustomStaticLootTarget` | string / string[] | 静态战利品的参考物品 ID；缺省用 `_targetid` |
| `StaticLootDivisor` | int | 静态战利品概率除数（`CustomLoot=true` 时生效，默认 2） |

> 概率逻辑：新物品的相对概率 = 参考物品相对概率 ÷ 除数。即 `MapLootDivisor`/`StaticLootDivisor` **越大，刷得越少**。

#### `container` —— 自定义容器（CustomSizeContainerProps）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `ContainerSizeWidth` | int | 容器格子宽（写入首个 Grid 的 `CellsH`） |
| `ContainerSizeHeight` | int | 容器格子高（写入首个 Grid 的 `CellsV`） |

#### `giftbox` —— 礼盒（GiftBoxProps）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `isGiftBox` | bool | 原版随机礼盒：`BoxData` 定义卡池与开盒次数 |
| `BoxData` | object | `{ "Count": 次数, "Rewards": { "物品ID": 权重, ... } }` |
| `isStaticBox` | bool | 固定内容礼盒：`StaticBoxData` |
| `StaticBoxData` | object | `{ "forcefindinraid": bool, "giftdata": [ GiftData... ] }` |
| `isSpecialBox` | bool | 特殊礼盒：`SpecialBoxData` |
| `SpecialBoxData` | object | `{ "giftdata": [ GiftData... ] }` |
| `isAdvGiftBox` | bool | 高级礼盒（引用抽奖池）：`AdvancedBoxData` |
| `AdvBoxData` | object | `{ "count": 次数, "forcefindinraid": bool, "giftdata": 抽奖池名称 }` |

**`GiftData`（礼物条目）的 `$type` 多态**：

| `$type` | C# 类型 | JSON Key | 说明 |
|---|---|---|---|
| `CustomPreset` | `GiftCustomPresetData` | `item`: `CustomItem[]` | 自定义预设（物品树） |
| `VanillaPreset` | `GiftVanillaPresetData` | `item`: 预设ID | 原版武器预设 |
| `Item` | `GiftItemData` | `itemid`: 物品ID, `stackcount`: int | 直接给指定物品 N 个 |
| `Container` | `GiftContainerData` | `item`: `CustomItem[]` | 一个容器（含其内物品） |
| `Skill` | `GiftDataSkillData` | `skill`, `count`, `itemid`, `stackcount`, `forcefir` | 技能经验 |
| `Experience` | `GiftDataExperienceData` | `count`, `itemid`, `stackcount`, `forcefir` | 玩家经验 |
| `Standing` | `GiftDataTraderStandingData` | `trader`, `count`, `itemid`, `stackcount`, `forcefir` | 商人好感 |

#### `buff` —— 增益（BuffItemProps）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `BuffValue` | object[] | 全局增益列表（配合顶层 `_props.StimulatorBuffs` 指定刺激剂 ID） |

#### `quest` —— 任务刷点（QuestItemProps）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `QuestItemData` | object | 刷点配置（见下） |

`CustomSpawnPointData`：

| JSON Key | 类型 | 说明 |
|---|---|---|
| `locationId` | string | 刷点位置 ID |
| `probability` | double | 刷新概率 |
| `template` | object | 刷新点模板：`Id`, `IsContainer`, `useGravity`, `randomRotation`, `Position`(x,y,z), `Rotation`, `IsAlwaysSpawn`, `IsGroupPosition`, `GroupPositions`, `Root`, `Items`(物品实例) |
| `location` | string | **目标地图 ID**（如 `"factory4_day"`） |

> 该物品会作为**强制刷新点**注入目标地图的 `SpawnpointsForced` 列表（`AddQuestItemGenerate`）。

---

## 四、任务 `CustomQuest`

- **源码**：`EternalCycle/Classes/QuestClasses.cs:8-36`
- **注册**：`QuestUtils.RegisterQuest(modPath, path, respath)` → `LoadQuestEvent`（resp 目录用于任务图片路由）
- **文件模式**：文件夹 = 每文件一个 `CustomQuest`；单文件 = `{"key": CustomQuest, ...}` 字典

### 4.1 顶层结构

```jsonc
{
  "ID": "任务ID",
  "Type": 0,
  "ImagePath": "quest_image.png",
  "TraderID": "商人ID",
  "Restartable": false,
  "Location": "任意地点名",
  "QuestData": { "Finish": [...], "Failed": [...] },
  "QuestReward": [ ... ]
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `ID` | string | ✅ | 任务 ID（自动哈希），同时作为模板 ID |
| `Type` | int | ✅ | 任务类型，对应 SPT `QuestTypeEnum` 整数值（0=Elimination, 1=Pickup, 2=Discover, 3=Completion, 4=Exploration…，以原版枚举为准） |
| `ImagePath` | string | ✅ | 任务图标文件名，会注册到 `/{respath}/{ImagePath}` 图片路由 |
| `TraderID` | string | ✅ | 发布任务的商人 ID |
| `Restartable` | bool | 可选 | 任务可否重复完成 |
| `Location` | string | 可选 | 任务地点名（展示用） |
| `QuestData` | object | ✅ | `{ "Finish": [...], "Failed": [...] }`，两个数组均用 **`CustomQuestData` 条件**（见 4.2） |
| `QuestReward` | object[] | ✅ | 奖励数组，用 **`CustomQuestRewardData`**（见 4.3） |

> 任务基于原版“匮乏”（SHORTAGE）任务克隆，本地化键自动生成：`{任务ID} name`、`{任务ID} description` 等，无需单独配置。

### 4.2 任务条件 `CustomQuestData`（`$type` 多态）

`QuestData.Finish` / `QuestData.Failed` 数组的每个元素按 `$type` 分派为不同条件。**公共基类字段**（所有条件共有）：

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | ✅ | 条件 ID（自动哈希），同一条件可被 `parent` 引用 |
| `locale` | string | 可选 | 本地化键（用于生成条件文案） |
| `parent` | string | 可选 | 父条件 ID（构造条件树） |
| `visible` | string[] | 可选 | 可见性控制列表 |

#### 条件类型总表

| `$type` | C# 类型 | 描述 | 特有字段 |
|---|---|---|---|
| `base` | `CustomQuestData` | 空条件 | 无 |
| `find` | `FindItemData` | 在战局内**找到**物品 | `inraid`, `itemid`, `count`, `dogtaglevel`, `autolocale` |
| `findgroup` | `FindItemGroupData` | 找到**一组**物品 | `inraid`, `itemgroup`, `count`, `dogtaglevel`, `tags` |
| `hand` | `HandoverItemData` | **上交**物品 | 同 `find` |
| `handgroup` | `HandoverItemGroupData` | 上交一组物品 | 同 `findgroup` |
| `kill` | `KillTargetData` | **击杀**目标 | 见下方详表 |
| `level` | `ReachLevelData` | 达到**玩家等级** | `count` |
| `visit` | `VisitPlaceData` | **到达**指定区域 | `oneraid`, `zoneid` |
| `place` | `PlaceItemData` | **放置**物品到区域 | `time`, `itemid`, `zoneid`, `count` |
| `placegroup` | `PlaceItemGroupData` | 放置一组物品 | `time`, `itemgroup`, `zoneid`, `count`, `tags` |
| `exit` | `ExitLocationData` | **撤离** | 见下方详表 |
| `skill` | `ReachSkillLevelData` | 达到**技能等级** | `skill`, `level` |
| `trust` | `ReachTraderTrustLevelData` | 达到商人**信任等级** | `traderid`, `level` |
| `standing` | `ReachTraderStandingData` | 达到商人**好感度** | `traderid`, `standing` |
| `quest` | `CompleteQuestData` | **完成前置任务** | `questid`, `status`, `cdtimemin`, `cdtimemax` |
| `block` | `CustomizationBlockData` | 外观屏蔽（空条件） | 无 |
| `prestige` | `ReachPrestigeLevelData` | 达到**威望等级** | `type`, `level` |

#### `find` / `hand` 字段

| JSON Key | 类型 | 说明 |
|---|---|---|
| `inraid` | bool | 是否必须战局内获得 |
| `itemid` | string | 物品 ID |
| `count` | int | 数量 |
| `dogtaglevel` | int | 狗牌最低等级（可选） |
| `autolocale` | bool | 自动生成条件文案 |

#### `findgroup` / `handgroup` 字段

| JSON Key | 类型 | 说明 |
|---|---|---|
| `inraid` | bool | 是否必须战局内获得 |
| `itemgroup` | string[] | 物品 ID 组（满足其一即可） |
| `count` | int | 数量 |
| `dogtaglevel` | int | 狗牌最低等级（可选） |
| `tags` | string[] | 物品标签（引用物品标签系统） |

#### `kill` 字段

| JSON Key | 类型 | 说明 |
|---|---|---|
| `oneraid` | bool | 是否单局内完成 |
| `count` | int | 击杀数 |
| `bot` | string | 目标 bot 的 SavageRole（如 `"assault"`） |
| `role` | string[] | 目标 bot 类型列表 |
| `bodyPart` | int | **位掩码**（`EBodyPartType`）：1头 2胸 4胃 8左臂 16右臂 32左腿 64右腿 |
| `daytime` | int[2] | 时间窗 `[From, To]`（小时 0-23） |
| `distance` | int | 击杀距离 |
| `distancetype` | int | 比较方式（`ECompareType`：0== 1!= 2> 3>= 4< 5<=） |
| `weapon` | string[] | 使用的武器 ID 列表（可含标签名） |
| `mod` | string[][] | 武器配件要求（每组内为或关系） |
| `location` | int | **位掩码**（`ELocationType`）限制地图 |
| `zone` | string[] | 击杀发生区域（QuestZone 的 `zoneId`） |
| `equip` | string[][] | 目标装备要求（二维或关系） |
| `enemyequip` | string[][] | 目标**敌方**装备要求 |
| `tags` | string[] | 物品标签引用 |

#### `exit` 字段

| JSON Key | 类型 | 说明 |
|---|---|---|
| `oneraid` | bool | 是否单局内完成 |
| `count` | int | 撤离次数 |
| `status` | int | **位掩码**（`EExitStatusType`）：1生还 2跑刀者 4阵亡 8失踪 16撤离 32转场 |
| `location` | int | **位掩码**（`ELocationType`）限制地图 |
| `chooseexitpoint` | bool | 是否指定具体撤离点 |
| `exitpoint` | string | 撤离点名称（`chooseexitpoint=true` 时生效） |

#### `quest` 字段

| JSON Key | 类型 | 说明 |
|---|---|---|
| `questid` | string | 前置任务 ID |
| `status` | int | 所需任务状态（`EQuestStatusType` 位掩码） |
| `cdtimemin` | int | 完成后的最短冷却（分钟） |
| `cdtimemax` | int | 完成后的最长冷却（分钟） |

### 4.3 任务奖励 `CustomQuestRewardData`（`$type` 多态）

**公共基类字段**（所有奖励共有）：

| JSON Key | 类型 | 说明 |
|---|---|---|
| `ID` | string | 奖励 ID（自动哈希） |
| `Quest` | string | 所属任务 ID |
| `Stage` | int | 奖励阶段（`EQuestStageType`：0=Start 1=Finish 2=Failed） |
| `Unknown` | bool | 是否未知奖励 |
| `Hidden` | bool | 是否隐藏奖励 |
| `IsAchievement` | bool | 是否成就奖励 |
| `AvailableGameEdition` | int | 可用的游戏版本位掩码（`EGameVersionType`） |

#### 奖励类型总表

| `$type` | C# 类型 | 描述 | 特有字段 |
|---|---|---|---|
| `base` | `CustomQuestRewardData` | 空奖励 | 无 |
| `item` | `CustomItemRewardData` | **给物品** | `Items`(`CustomItem[]`), `Count`, `FindInRaid` |
| `assort` | `CustomAssortUnlockRewardData` | **解锁报价单** | `AssortData`（内嵌一个 `CustomLockedAssortData`） |
| `recipe` | `CustomRecipeUnlockRewardData` | **解锁配方** | `RecipeData`（内嵌一个 `CustomLockedRecipeData`） |
| `experience` | `CustomExperienceRewardData` | **玩家经验** | `Count` |
| `skillexperience` | `CustomSkillExperienceRewardData` | **技能经验** | `Skill`, `Count` |
| `standing` | `CustomTraderStandingRewardData` | **商人好感** | `TraderID`, `Count`(double) |
| `trader` | `CustomTraderUnlockRewardData` | **解锁商人** | `Trader` |
| `customization` | `CustomCustomizationRewardData` | **解锁外观** | `Target` |
| `achievement` | `CustomAchievementRewardData` | **解锁成就** | `Target` |
| `pocket` | `CustomPocketRewardData` | **解锁口袋** | `Target` |

> 提示：`assort` 奖励内的 `AssortData` 可以直接内嵌一个带 `"$type":"locked"` 的 `CustomAssortData` 对象；`recipe` 同理（`"$type":"locked"`）。

---

## 五、任务逻辑树 `QuestLogicTree`

- **源码**：`EternalCycle/Classes/QuestClasses.cs:381-415`
- **注册**：`QuestUtils.RegisterQuestLogicTree(modPath, path)` → `LoadQuestLogicEvent`
- **文件模式**：文件夹 = 每文件一个 `QuestLogicTree`；单文件 = `{"key": QuestLogicTree, ...}` 字典
- **作用**：为已有任务（含原版任务）的 `AvailableForStart` 条件动态追加**前置条件**（前置任务 / 商人好感 / 商人等级 / 玩家等级 / 威望）。

```jsonc
{
  "id": "目标任务ID",
  "prequestdata": { "前置任务ID": { "state": 16, "cdtime": 60, "extracdtime": 30 } },
  "pretraderstanding": { "商人ID": 0.2 },
  "pretraderlevel": { "商人ID": 3 },
  "prelevel": 15,
  "prestigelevel": 1,
  "prestigetype": 3
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | ✅ | 目标任务 ID |
| `prequestdata` | object | 可选 | `{ 前置任务ID: {...} }`，为每个前置任务生成“完成该任务”条件 |
| `prequestdata.*.state` | int | ✅ | 所需任务状态（`EQuestStatusType` 位掩码，如 16=Success） |
| `prequestdata.*.cdtime` | int | 可选 | 完成后最短冷却（分钟） |
| `prequestdata.*.extracdtime` | int | 可选 | 额外随机冷却上限（实际冷却 = `cdtime + [0, extra]`） |
| `pretraderstanding` | object | 可选 | `{ 商人ID: 好感度 }` |
| `pretraderlevel` | object | 可选 | `{ 商人ID: 信任等级 }` |
| `prelevel` | int | 可选 | 玩家等级要求（`> 0` 时生效） |
| `prestigelevel` | int | 可选 | 威望等级要求 |
| `prestigetype` | int | 可选 | 比较方式（`ECompareType`），默认 3（>=） |

---

## 六、任务区域 `QuestZone`

- **源码**：`EternalCycle/Classes/QuestZoneClasses.cs:5-60`
- **注册**：`QuestZoneUtils.RegisterQuestZones(modPath, path)` → `LoadQuestZoneEvent`
- **文件模式**：文件夹（只扫描 `*.json*`）或单文件，均为 `List<QuestZone>` 结构

```jsonc
{
  "zoneId": "我的区域",
  "zoneName": "演示区",
  "zoneLocation": "factory4_day",
  "zoneType": "visit",
  "flareType": "Airdrop",
  "position": { "x": 100, "y": 1, "z": -50, "w": 0 },
  "rotation": { "x": 0, "y": 45, "z": 0, "w": 0 },
  "scale": { "x": 1, "y": 1, "z": 1, "w": 0 },
  "groupPosition": [
    { "position": { "x": 0, "y": 0, "z": 0 }, "rotation": { "x": 0, "y": 0, "z": 0 }, "scale": { "x": 1, "y": 1, "z": 1 } }
  ]
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `zoneId` | string | ✅ | 区域唯一 ID（供任务 `zoneid` / `zone` 引用） |
| `zoneName` | string | 可选 | 显示名 |
| `zoneLocation` | string | ✅ | 地图 ID（如 `"factory4_day"`）。支持虚拟地点映射：`FactoryCommon`→`factory4_day`/`factory4_night`，`SandboxCommon`→`sandbox`/`sandbox_high` |
| `zoneType` | string | 可选 | 区域类型：`"visit"` / `"placeitem"` / `"killbot"` / `"flarezone"` |
| `flareType` | string | 可选 | 信号弹类型：`"Light"` / `"Airdrop"` / `"ExitActivate"` / `"Quest"` / `"AIFollowEvent"` |
| `position` | object | ✅ | 位置 `{x,y,z,w}`（w 默认 0） |
| `rotation` | object | 可选 | 旋转 `{x,y,z,w}` |
| `scale` | object | 可选 | 缩放 `{x,y,z,w}` |
| `groupPosition` | object[] | 可选 | 成组位置列表，每项含 `position`/`rotation`/`scale` |

---

## 七、商人 `TraderBaseWithDesc`

- **源码**：`EternalCycle/Classes/TraderClasses.cs:18-35`
- **注册**：`TraderUtils.RegisterTrader(modPath, path, imagePath, creator, modname)` → `LoadTraderBaseEvent`
- **文件模式**：文件夹或单文件，均为单个 `TraderBaseWithDesc` 对象
- **说明**：继承 SPT 原版 `TraderBase`，克隆原版“普拉波尔”商人作为基底，再覆盖你提供的字段。

### 7.1 原版 `TraderBase` 常见字段

| JSON Key | 类型 | 说明 |
|---|---|---|
| `_id` | string | ✅ 商人 ID（自动哈希），写入各配置表 |
| `avatar` | string | 头像文件名，自动注册图片路由 |
| `name` / `nickname` / `surname` | string | 商人名 |
| `currency` | string | 结算货币 |
| `balance` | object | 资金 |
| `loyalty` | object | `{ currentLevel, currentStanding, currentSalesSum }` |
| `locations` | object | 商人所在位置（地图刷点） |
| `discount` | int | 折扣 |
| `repair` | object | 维修配置 |
| `insurance` | object | 保险配置 |
| `customization_seller` | bool | 是否外观商人（为 true 时启用 Suits 列表） |
| `services` | object | 提供的服务 |
| `medic` | bool | 是否医疗商人 |

### 7.2 本 Mod 扩展字段

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `description` | string | 可选 | 商人介绍 |
| `insurance_locale` | object | 可选 | `{ "insuranceStart": [..], "insuranceFound": [..], "insuranceComplete": [..], ... }` 保险对话 |
| `insuranceChance` | int | 可选 | 保险返还概率（`>0` 时写入 `ReturnChancePercent`） |
| `minReflashTime` | int | 可选 | 刷新间隔下限（秒），默认 1800 |
| `maxReflashTime` | int | 可选 | 刷新间隔上限（秒），默认 3600 |
| `showInRagfair` | bool | 可选 | 是否在跳蚤市场显示该商人 |

> 商人的报价单/图纸/对话由独立的 `RegisterAssort` 等模块提供，此处只定义商人本体。

---

## 八、报价单 `CustomAssortData`

- **源码**：`EternalCycle/Classes/AssortClasses.cs:16-62`
- **注册**：`AssortUtils.RegisterAssort(modPath, path)` → `LoadTraderAssortEvent`
- **文件模式**：文件夹 = 每文件一个 `List<CustomAssortData>`；单文件 = `List<CustomAssortData>`

### 8.1 顶层结构

```jsonc
{
  "$type": "normal",        // normal 或 locked
  "ID": "报价单ID",
  "Trader": "商人ID",
  "Item": [ { "_id": "...", "_tpl": "..." } ],
  "Barter": { "物品ID": 数量 },
  "DogTag": { "物品ID": { "count": 1, "level": 0, "side": 1 } },
  "TrustLevel": 3,
  "isWeapon": false
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `$type` | string | ✅ | `"normal"` 或 `"locked"` |
| `ID` | string | ✅ | 报价单 ID（自动哈希），作为 `BarterScheme`/`LoyalLevelItems` 的键 |
| `Trader` | string | ✅ | 所属商人 ID |
| `Item` | object[] | ✅ | 物品实例树（`CustomItem[]`），**第 1 项为主物品**；主物品为弹药包时自动补弹 |
| `DogTag` | object | 可选 | 狗牌交换需求：`{ 狗牌ID: { "count": 数量, "level": 等级, "side": 阵营 } }`，`side` 用 `DogtagExchangeSide` 枚举（参考 SPT，通常 1=Bear 2=Usec 0=All） |
| `Barter` | object | 可选 | 交换需求：`{ 物品ID: 数量 }` |
| `TrustLevel` | int | 可选 | 需要的信任等级（`LoyalLevelItems`） |
| `isWeapon` | bool | 可选 | **预留字段**（当前加载逻辑未使用） |

### 8.2 `locked` 子类型额外字段（`$type: "locked"`）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `Locked` | bool | 是否锁定 |
| `Quest` | string | 解锁所需任务 ID |
| `QuestStage` | int | 解锁所需任务阶段（`EQuestStageType`） |
| `Unknown` | bool | 是否未知奖励 |

> `locked` 报价单会被转化为任务的“解锁报价单”奖励（`assort`），在对应任务完成时自动开放。

---

## 九、配方 `CustomRecipeData`

- **源码**：`EternalCycle/Classes/RecipeClasses.cs:9-94`
- **注册**：`RecipeUtils.RegisterRecipe(modPath, path)` → `LoadRecipeEvent`（藏身处制造配方）
- **文件模式**：文件夹 = 每文件一个 `CustomRecipeData`；单文件 = `{"key": CustomRecipeData, ...}` 字典

### 9.1 顶层结构

```jsonc
{
  "$type": "normal",       // normal 或 locked
  "ID": "配方ID",
  "Area": 1,               // HideoutAreas
  "AreaLevel": 2,
  "Output": "产出物品ID",
  "IsEncoded": false,
  "OutputCount": 5,
  "Time": 3600,            // 秒
  "NeedFuel": true,
  "Require": {
    "Tool": { "工具物品ID": 1 },
    "Item": { "材料物品ID": 3 }
  }
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `$type` | string | ✅ | `"normal"` 或 `"locked"` |
| `ID` | string | ✅ | 配方 ID（自动哈希） |
| `Area` | int | ✅ | 藏身处区域（`HideoutAreas`，参考 SPT：1=发电机, 2=水收集器…） |
| `AreaLevel` | int | ✅ | 区域等级（1 起） |
| `Output` | string | ✅ | 产出物品 ID |
| `IsEncoded` | bool | 可选 | 是否加密产物（升级蓝图） |
| `OutputCount` | int | 可选 | 产出数量 |
| `Time` | int | ✅ | 制造耗时（秒） |
| `NeedFuel` | bool | 可选 | 是否需要燃料 |
| `Require` | object | 可选 | `{ "Tool": {物品ID:数量}, "Item": {物品ID:数量} }` |

### 9.2 `locked` 子类型额外字段（`$type: "locked"`）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `Locked` | bool | 是否锁定 |
| `Quest` | string | 解锁所需任务 ID |
| `QuestStage` | int | 解锁所需任务阶段（`EQuestStageType`） |
| `Unknown` | bool | 是否未知奖励 |

### 9.3 SCAV 宝箱配方 `CustomScavCaseRecipeData`

- 注册于 `LoadScavCaseRecipeEvent`（`RecipeUtils` 内部处理）

| JSON Key | 类型 | 说明 |
|---|---|---|
| `id` | string | 配方 ID |
| `time` | int | 耗时（秒） |
| `requires` | object | `{ 物品ID: 数量 }` 投入需求 |
| `rewards` | object | `{ "common": [数量...], "rare": [数量...], "superrare": [数量...] }` 各品质产出数量数组 |

### 9.4 邪教圈配方 `CustomCultistCircleRecipe`

- 注册于 `LoadCultistCircleRecipeEvent`

| JSON Key | 类型 | 说明 |
|---|---|---|
| `requires` | string[] | 投入物品 ID 列表 |
| `rewards` | string[] | 产出物品 ID 列表 |
| `time` | int | 耗时（秒） |
| `repeatable` | bool | 是否可重复 |

---

## 十、成就 `CustomAchievementData`

- **源码**：`EternalCycle/Classes/AchievementClasses.cs:7-44`
- **注册**：`AchievementUtils.RegisterAchievement(modPath, path, respath)` → `LoadAchievementEvent`
- **文件模式**：文件夹 = 每文件一个 `CustomAchievementData`；单文件 = `List<CustomAchievementData>`

```jsonc
{
  "id": "成就ID",
  "img": "achievement.png",
  "name": "成就名",
  "description": "成就描述",
  "rarity": "common",
  "side": "Pmc",
  "instantComplete": false,
  "showNotificationsInGame": true,
  "showProgress": true,
  "hidden": false,
  "showConditions": true,
  "progressBarEnabled": true,
  "conditions": { "finish": [...], "failed": [...] },
  "rewards": [ ... ]
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | ✅ | 成就 ID |
| `img` | string | ✅ | 图标文件名（注册图片路由） |
| `name` / `description` | string | ✅ | 成就名 / 描述（本地化键自动为 `{id} name` / `{id} description`） |
| `rarity` | string | 可选 | 稀有度（如 `"common"`） |
| `side` | string | 可选 | 阵营（如 `"Pmc"`） |
| `instantComplete` | bool | 可选 | 是否注册即完成 |
| `showNotificationsInGame` | bool | 可选 | 游戏中是否弹通知 |
| `showProgress` | bool | 可选 | 是否显示进度 |
| `hidden` | bool | 可选 | 是否隐藏 |
| `showConditions` | bool | 可选 | 是否显示条件 |
| `progressBarEnabled` | bool | 可选 | 是否启用进度条 |
| `conditions` | object | ✅ | `{ "finish": [CustomQuestData...], "failed": [CustomQuestData...] }`，复用任务条件（见 4.2） |
| `rewards` | object[] | ✅ | 成就奖励，复用 `CustomQuestRewardData`（见 4.3） |

---

## 十一、套装 `CustomSuit`

- **源码**：`EternalCycle/Classes/SuitClasses.cs:15-72`
- **注册**：`SuitUtils.RegisterSuit(modPath, path, traderId = null)` → `LoadSuitEvent`
- **文件模式**：文件夹 = 每文件一个 `List<CustomSuit>`；单文件 = `List<CustomSuit>`
- **说明**：`traderId` 为可选参数。传入时套装挂到指定商人；不传时挂到 `Tid` 指定的商人（找不到回退服装商人 RAGMAN）。

```jsonc
{
  "_id": "套装ID",
  "suiteId": "服装套件ID",
  "tid": "商人ID",
  "externalObtain": true,
  "internalObtain": false,
  "isHiddenInPVE": false,
  "isActive": true,
  "relatedBattlePassSeason": 0,
  "requirements": {
    "LoyaltyLevel": 0,
    "PrestigeLevel": 0,
    "ProfileLevel": 0,
    "Standing": 0,
    "RequiredTid": "商人ID",
    "SkillRequirements": [],
    "AchievementRequirements": [],
    "ItemRequirements": [ { "count": 1, "itemId": "物品ID" } ],
    "QuestRequirements": []
  }
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `_id` | string | ✅ | 套装 ID |
| `suiteId` | string | ✅ | 关联的服装套件 ID |
| `tid` | string | ✅ | 商人 ID（不传 `traderId` 参数时用此字段找商人） |
| `externalObtain` | bool | 可选 | 外部获取 |
| `internalObtain` | bool | 可选 | 内部获取 |
| `isHiddenInPVE` | bool | 可选 | PVE 中隐藏 |
| `isActive` | bool | 可选 | 是否激活 |
| `relatedBattlePassSeason` | int | 可选 | 相关战斗通行证赛季 |
| `requirements` | object | 可选 | 解锁需求（默认全部为 0/空） |

`requirements`（继承 SPT `SuitRequirements`）：

| JSON Key | 类型 | 说明 |
|---|---|---|
| `LoyaltyLevel` | int | 商人信任等级（默认 0） |
| `PrestigeLevel` | int | 威望等级（默认 0） |
| `ProfileLevel` | int | 玩家等级（默认 0） |
| `Standing` | int | 好感度（默认 0） |
| `RequiredTid` | string | 需要的商人（默认 RAGMAN） |
| `SkillRequirements` | object[] | 技能需求（参考 SPT `SkillRequirement`） |
| `AchievementRequirements` | string[] | 成就 ID 列表（自动哈希） |
| `ItemRequirements` | object[] | 物品需求（参考 SPT `ItemRequirement`：`count`+`itemId`） |
| `QuestRequirements` | string[] | 任务 ID 列表（自动哈希） |

---

## 十二、自定义外观 `CustomCustomizationItem`

- **源码**：`EternalCycle/Classes/CustomizationClasses.cs:16-66`
- **注册**：`CustomizationUtils.RegisterCustomization(modPath, path, respath)` → `LoadCustomizationEvent`
- **文件模式**：文件夹 = 每文件一个 `Dictionary<string, CustomCustomizationItem>`；单文件 = `Dictionary<string, CustomCustomizationItem>`（字典 Key 为物品 ID，会被 `_id` 覆盖）

```jsonc
{
  "外部唯一键": {
    "_id": "外观ID",
    "_name": "内部名",
    "_parent": "父模板ID",
    "_type": "BodyPart",
    "_proto": "原型ID",
    "_props": {
      "Name": "显示名",
      "ShortName": "短名",
      "Description": "描述",
      "Body": "身体外观ID",
      "Feet": "脚外观ID",
      "Hands": "手外观ID",
      "IsVoice": false,
      "IsDeco": false,
      "IsTarget": false
    }
  }
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `_id` | string | ✅ | 外观 ID（自动哈希） |
| `_name` | string | 可选 | 内部名 |
| `_parent` | string | 可选 | 父模板 |
| `_type` | string | 可选 | 类型（如 `"BodyPart"`） |
| `_proto` | string | 可选 | 原型 |
| `_props` | object | ✅ | 外观属性（继承原版 `CustomizationProperties`） |

`_props` 关键字段（继承 + 扩展）：

| JSON Key | 类型 | 说明 |
|---|---|---|
| `Name` / `ShortName` / `Description` | string | 本地化名称（键自动为 `{外观ID} Name/ShortName/Description`） |
| `Body` / `Feet` / `Hands` | string | 身体/脚/手外观 ID |
| `BearTemplateId` / `UsecTemplateId` | string | 阵营模板 ID |
| `Prefab` | object | 预制体 `{ path, rcid }`（`IsVoice=true` 时用于语音注册） |
| `AssetPath` | object | 资源路径 `{ rcid }`（`IsTarget=true` 时图标按 `rcid.png` 注册） |
| `IsVoice` | bool | `true` 时注册为语音外观（`CustomisationStorage` + `VoicePath`） |
| `VoicePath` | string | 语音资源路径 |
| `IsDeco` | bool | `true` 时注册装饰图标（`{respath}/{Name}.png`） |
| `IsTarget` | bool | `true` 时注册目标图标（`{respath}/{AssetPath.Rcid}.png`） |

### 藏身处外观 `CustomHideoutCustomization`

- 注册于 `LoadHideoutCustomizationEvent`

```jsonc
{
  "id": "外观ID",
  "type": "Object",
  "name": "显示名",
  "shortname": "短名",
  "description": "描述",
  "enbale": true,
  "target": "目标ID",
  "conditions": [ { "$type": "level", "id": "...", "count": 10 } ]
}
```

| JSON Key | 类型 | 说明 |
|---|---|---|
| `id` | string | 外观 ID |
| `type` / `name` / `shortname` / `description` | string | 类型与显示信息 |
| `enbale` | bool | 是否启用 |
| `target` | string | 目标 ID |
| `conditions` | object[] | 解锁条件（复用 `CustomQuestData`，见 4.2） |

---

## 十三、武器预设 `CustomPresetData`

- **源码**：`EternalCycle/Classes/PresetClasses.cs:15-31`
- **注册**：`PresetUtils.RegisterPreset(modPath, path)` → `LoadPresetEvent`
- **文件模式**：文件夹 = 每文件一个 `CustomPresetData`；单文件 = `List<CustomPresetData>`

```jsonc
{
  "Name": "预设显示名（会哈希为预设ID）",
  "PresetName": "预设内部名",
  "IsBasePreset": true,
  "ChangePresetName": true,
  "SpawnInRaid": false,
  "SpawnTarget": "战利品目标物品ID",
  "Preset": [
    { "_id": "...", "_tpl": "武器模板ID", "parentId": "hideout", "upd": { "StackObjectsCount": 1 } },
    { "_id": "...", "_tpl": "配件模板ID", "parentId": "主物品实例ID", "slotId": "mod_muzzle" }
  ]
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `Name` | string | ✅ | 预设名，**自动哈希为预设 ID** |
| `PresetName` | string | ✅ | 预设内部名 |
| `IsBasePreset` | bool | 可选 | `true` 时注册为基础预设（写入 `Encyclopedia`） |
| `ChangePresetName` | bool | 可选 | 是否替换武器显示名 |
| `SpawnInRaid` | bool | 可选 | `true` 时把整套预设作为**静态战利品**生成 |
| `SpawnTarget` | string | 可选 | 战利品参考目标物品 ID（`SpawnInRaid=true` 时生效） |
| `Preset` | object[] | ✅ | 物品实例树（`CustomItem[]`，第 1 项为武器本体） |

---

## 十四、礼物码 `CustomGiftCodeData`

- **源码**：`EternalCycle/Classes/ItemClasses.cs:472-493`
- **注册**：`GiftCodeUtils.RegisterGiftCode(modPath, path)` → `LoadGiftCodeEvent`
- **文件模式**：文件夹 = 每文件一个 `CustomGiftCodeData`；单文件 = `Dictionary<string, CustomGiftCodeData>`

```jsonc
{
  "id": "礼物码组ID",
  "code": "兑换码字符串",
  "item": { "任意分组名": [ { "_id": "...", "_tpl": "..." } ] },
  "message": "兑换提示语",
  "storagetime": 48,
  "maxcount": 3
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | ✅ | 礼物组 ID（自动哈希，作为物品树的父节点 ID） |
| `code` | string | ✅ | 玩家输入的兑换码（注册为 `GiftsConfig.Gifts` 的键） |
| `item` | object | ✅ | `{ 分组名: CustomItem[] }`，每组物品树会被独立加盐重哈希，并挂到 `{id}` 父节点下 |
| `message` | string | 可选 | 兑换系统消息 |
| `storagetime` | int | 可选 | 领取后邮件保留时长（小时，`CollectionTimeHours`） |
| `maxcount` | int | 可选 | 每人最大使用次数（`MaxToSendPlayer`） |

---

## 十五、抽奖池 `DrawPoolClass`

- **源码**：`EternalCycle/Classes/ItemClasses.cs:302-371`
- **注册**：`ItemUtils.RegisterDrawPool(modPath, path)` → `LoadDrawPoolEvent`
- **文件模式**：文件夹或单文件，均为单个 `DrawPoolClass` 对象
- **作用**：作为高级礼盒（`giftbox` + `isAdvGiftBox`）的抽奖卡池；随次数累进提高稀有度概率。

```jsonc
{
  "name": "抽奖池名",
  "basereward": {
    "superrare": { "havebasereward": true, "chance": 0.05, "upchance": 0.002, "upaddchance": 0.001, "chancegrowcount": 10, "chancegrowpercount": 0.01 },
    "rare":     { "havebasereward": true, "chance": 0.2,  "upchance": 0.005, "upaddchance": 0.002, "chancegrowcount": 5,  "chancegrowpercount": 0.02 },
    "normal":   { "upchance": 0.8 }
  },
  "itempool": {
    "superrare": { "chanceup": [ /* GiftData 数组 */ ], "normal": [ /* GiftData 数组 */ ] },
    "rare":     { "chanceup": [ /* GiftData 数组 */ ], "normal": [ /* GiftData 数组 */ ] },
    "normal":   { "chanceup": [ /* GiftData 数组 */ ], "normal": [ /* GiftData 数组 */ ] }
  }
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | string | ✅ | 抽奖池名（被 `AdvBoxData.giftdata` 引用） |
| `basereward` | object | ✅ | 基础奖励概率配置 |
| `basereward.superrare` / `.rare` | object | ✅ | `havebasereward`(bool), `chance`(double 基础概率), `upchance`(double 抽中后提升), `upaddchance`(double 额外累加), `chancegrowcount`(int 增长阈值), `chancegrowpercount`(double 每次增长) |
| `basereward.normal` | object | ✅ | 仅 `upchance` |
| `itempool` | object | ✅ | 物品卡池 |
| `itempool.superrare` / `.rare` / `.normal` | object | ✅ | `chanceup`(`GiftData[]`，中奖后进入的池), `normal`(`GiftData[]`，常规池) |

> `GiftData` 的写法见 [3.3 giftbox](#giftbox--礼盒giftboxprops)。

---

## 十六、机器人修改 `CustomAlterBot`

- **源码**：`EternalCycle/Classes/BotGeneratorClasses.cs:16-39`
- **注册**：`BotGeneratorUtils.RegisterAlterBotData(modPath, path)` → `LoadAlterBotEvent`
- **文件模式**：文件夹 = 每文件一个 `CustomAlterBot`；单文件 = 单个 `CustomAlterBot`

```jsonc
{
  "role": "assault",
  "type": 1,
  "forceloot": { "物品ID": 3 },
  "typeloot": { "物品ID": 2 },
  "location": 1,
  "chance": 50,
  "cleanweapon": true
}
```

| JSON Key | 类型 | 必填 | 说明 |
|---|---|---|---|
| `role` | string | ✅ | 目标 bot 角色名（如 `"assault"`），注册到该角色字典 |
| `type` | int | 可选 | `BotType` 枚举（参考 SPT：1=Default…） |
| `forceloot` | object | 可选 | 强制携带物品：`{ 物品ID: 数量 }` |
| `typeloot` | object | 可选 | 按类型携带物品：`{ 物品ID: 数量 }` |
| `location` | int | 可选 | **位掩码**（`ELocationType`）限定出现地图 |
| `chance` | int | 可选 | 生效概率（0-100） |
| `cleanweapon` | bool | 可选 | 是否清空武器耐久 |

---

## 十七、物品标签 `ItemTagDictionary`

- **源码**：`EternalCycle/Classes/ItemClasses.cs:495-503`、`EternalCycle/Utils/ItemTagUtils.cs`
- **注册**：`ItemTagUtils.RegisterItemTag(modPath, path)` → `LoadItemTagEvent`（**仅支持单文件**）
- **作用**：为物品组定义命名标签，供任务条件（`tags`）、击杀武器（`weapon`）等引用。

```jsonc
{
  "我的标签": [ "物品ID1", "物品ID2", "另一个物品ID" ]
}
```

- JSON 根对象即字典：**键 = 标签名**，值 = 该标签包含的物品 ID 数组（自动哈希）。
- 引用了同一标签的任务/条件会自动展开为该标签内的全部物品。

---

## 十八、资源同步（客户端 API）

- **源码**：`EternalCycle/Classes/RouterClasses.cs`、`EternalCycle/Utils/ResourceUtils.cs`、`Core.cs`
- **作用**：客户端通过以下 HTTP 端点从服务端同步**二进制资源**（Bundle / 槽位图标 / 装饰图标 / 目标图标 / 语音）。

| 端点 | 说明 |
|---|---|
| `/eternalcycle/loadriglayout` | 同步 Bundle（`RegisterRigLayoutResource`） |
| `/eternalcycle/loadsloticon` | 同步槽位图标（`RegisterSlotIconResource`） |
| `/eternalcycle/loaddecoicon` | 同步装饰图标（`RegisterDecoIconResource`） |
| `/eternalcycle/loadtarget` | 同步目标图标（`RegisterTargetResource`） |
| `/eternalcycle/loadvoice` | 返回语音路径字典 |
| `/eternalcycle/loadquestzone` | 返回全部 QuestZone（含虚拟地点展开） |
| `/eternalcycle/callprofilebackup` | 触发服务端存档备份 |

请求体 `SyncResourceRequest`：

```jsonc
{ "clientHashes": { "文件名": "客户端已有MD5", ... } }
```

响应 `SyncResourceResponse`：

```jsonc
{ "validFiles": [ "已匹配的文件名" ], "filesToUpdate": { "文件名": "Base64内容" } }
```

- 客户端提交自己已拥有的文件 MD5，服务端对比后把差异文件以 Base64 回传（`filesToUpdate`）。
- 资源注册函数（`RegisterRigLayoutResource` 等）与其它 `Register*` 一样挂在 `LoadResourceEvent` 阶段。

---

## 十九、枚举参考

以下枚举由 `EternalCycle/Enums/*.cs` 定义，直接用于 JSON 中的整数字段。

### 19.1 `EBlackListType`（黑名单位掩码）

| 值 | 常量 | 含义 |
|---|---|---|
| 1 | AirDrop | 空投黑名单 |
| 2 | PMCLoot | PMC 拾取战利品黑名单 |
| 4 | ScavCaseLoot | SCAV 宝箱战利品黑名单 |
| 8 | Fence | 跳蚤黑市（Fence）黑名单 |
| 16 | Circle | 邪教圈黑名单 |
| 32 | DailyReward | 每日任务奖励黑名单 |
| 64 | Global | 全局黑名单 |

### 19.2 `EBodyPartType`（身体部位位掩码）

| 值 | 常量 | 部位 |
|---|---|---|
| 1 | Head | 头 |
| 2 | Chest | 胸 |
| 4 | Stomach | 胃 |
| 8 | LeftArm | 左臂 |
| 16 | RightArm | 右臂 |
| 32 | LeftLeg | 左腿 |
| 64 | RightLeg | 右腿 |

### 19.3 `ECompareType`（比较方式）

| 值 | 常量 | 符号 |
|---|---|---|
| 0 | Equal | == |
| 1 | NotEqual | != |
| 2 | Greater | > |
| 3 | GreaterOrEqual | >=（默认） |
| 4 | Less | < |
| 5 | LessOrEqual | <= |

### 19.4 `EExitStatusType`（撤离状态位掩码）

| 值 | 常量 | 含义 |
|---|---|---|
| 1 | Survived | 生还 |
| 2 | Runner | 跑刀者 |
| 4 | Killed | 阵亡 |
| 8 | MissingInAction | 失踪 |
| 16 | Left | 撤离 |
| 32 | Transit | 转场 |

### 19.5 `EGameVersionType`（游戏版本位掩码）

| 值 | 常量 |
|---|---|
| 1 | standard |
| 2 | left_behind |
| 4 | prepare_for_escape |
| 8 | edge_of_darkness |
| 16 | unheard_edition |
| 32 | develop |
| 64 | tournament |
| 128 | tournament_live |
| 256 | press_edition |
| 512 | exhibition |

### 19.6 `ELocationType`（地图位掩码）

| 值 | 常量 | 地图 |
|---|---|---|
| 1 | Custom | 森林?（Custom=海关） |
| 2 | Woods | 森林 |
| 4 | Factory_Day | 工厂（昼） |
| 8 | Factory_Night | 工厂（夜） |
| 16 | Laboratory | 实验室 |
| 32 | Shoreline | 海岸线 |
| 64 | ReserveBase | 储备站 |
| 128 | Interchange | 立交桥 |
| 256 | Lighthouse | 灯塔 |
| 512 | TarkovStreets | 街区 |
| 1024 | GroundZero | 起点（Ground Zero） |
| 2048 | GroundZero_High | 起点（高等级） |
| 4096 | Labyrinth | 迷宫 |

> 注：常量名以源码为准（`Custom=1` 对应海关地图的枚举常量）。

### 19.7 `EQuestStageType`（任务阶段）

| 值 | 常量 | 含义 |
|---|---|---|
| 0 | Start | 开始 |
| 1 | Finish | 完成 |
| 2 | Failed | 失败 |

### 19.8 `EQuestStatusType`（任务状态位掩码）

| 值 | 常量 | 含义 |
|---|---|---|
| 1 | Locked | 锁定 |
| 2 | AvailableForStart | 可开始 |
| 4 | Started | 进行中 |
| 8 | AvailableForFinish | 可完成 |
| 16 | Success | 成功 |
| 32 | Fail | 失败 |
| 64 | FailRestartable | 失败可重试 |
| 128 | MarkedAsFailed | 标记失败 |
| 256 | Expired | 过期 |
| 512 | AvailableAfter | 稍后可用 |

### 19.9 `ERagfairTagsType`（手册分类 / `RagfairType`）

`RagfairType` 可填下表**任一中文**或直接填 ID（代码里 `props.RagfairType.ConvertHashID()` 统一转哈希）。本 Mod 还定义了自定义分类：**次元博物**、**特殊物品**、**调试物品**、**任务物品**。

| 中文名 | ID |
|---|---|
| 能源物品 | `5b47574386f77428ca22b2ed` |
| 建筑材料 | `5b47574386f77428ca22b2ee` |
| 电子产品 | `5b47574386f77428ca22b2ef` |
| 日常用品 | `5b47574386f77428ca22b2f0` |
| 贵重物品 | `5b47574386f77428ca22b2f1` |
| 易燃物品 | `5b47574386f77428ca22b2f2` |
| 医疗用品 | `5b47574386f77428ca22b2f3` |
| 其他 | `5b47574386f77428ca22b2f4` |
| 工具 | `5b47574386f77428ca22b2f6` |
| 面部装备 | `5b47574386f77428ca22b32f` |
| 头部装备 | `5b47574386f77428ca22b330` |
| 眼部装备 | `5b47574386f77428ca22b331` |
| 饮品 | `5b47574386f77428ca22b335` |
| 食物 | `5b47574386f77428ca22b336` |
| 药品 | `5b47574386f77428ca22b337` |
| 急救包 | `5b47574386f77428ca22b338` |
| 创伤处理 | `5b47574386f77428ca22b339` |
| 注射器 | `5b47574386f77428ca22b33a` |
| 子弹 | `5b47574386f77428ca22b33b` |
| 弹药包 | `5b47574386f77428ca22b33c` |
| 交换用物品 | `5b47574386f77428ca22b33e` |
| 装备 | `5b47574386f77428ca22b33f` |
| 给养 | `5b47574386f77428ca22b340` |
| 情报物品 | `5b47574386f77428ca22b341` |
| 钥匙 | `5b47574386f77428ca22b342` |
| 地图 | `5b47574386f77428ca22b343` |
| 医疗物品 | `5b47574386f77428ca22b344` |
| 特殊装备 | `5b47574386f77428ca22b345` |
| 弹药 | `5b47574386f77428ca22b346` |
| 机械钥匙 | `5c518ec986f7743b68682ce2` |
| 电子钥匙 | `5c518ed586f774119a772aee` |
| 耳机 | `5b5f6f3c86f774094242ef87` |
| 背包 | `5b5f6f6c86f774093f2ecf0b` |
| 战术胸挂 | `5b5f6f8786f77447ed563642` |
| 容器 | `5b5f6fa186f77409407a7eb7` |
| 安全箱 | `5b5f6fd286f774093f2ecf0d` |
| 防弹衣 | `5b5f701386f774093f2ecf0f` |
| 装备组件 | `5b5f704686f77447ec5d76d7` |
| 武器零件或配件 | `5b5f71a686f77447ed5636ab` |
| 功能模块 | `5b5f71b386f774093f2ecf11` |
| 脚架 | `5b5f71c186f77409407a7ec0` |
| 前握把 | `5b5f71de86f774093f2ecf13` |
| 枪口装置 | `5b5f724186f77447ed5636ad` |
| 消焰器或制退器 | `5b5f724c86f774093f2ecf15` |
| 膛口转接器 | `5b5f72f786f77447ec5d7702` |
| 消音器 | `5b5f731a86f774093e6cb4f9` |
| 照明或激光装置 | `5b5f736886f774094242f193` |
| 多功能战术设备 | `5b5f737886f774093e6cb4fb` |
| 手电 | `5b5f73ab86f774094242f195` |
| 激光指示器 | `5b5f73c486f77447ec5d7704` |
| 瞄具 | `5b5f73ec86f774093e6cb4fd` |
| 突击瞄准镜 | `5b5f740a86f77447ec5d7706` |
| 反射式瞄具 | `5b5f742686f774093e6cb4ff` |
| 紧凑型反射式瞄具 | `5b5f744786f774094242f197` |
| 机械瞄具 | `5b5f746686f77447ec5d7708` |
| 光学瞄准镜 | `5b5f748386f774093e6cb501` |
| 手电枪口 | `5b5f749986f774094242f199` |
| 组合枪口 | `5b5f74cc86f77447ec5d770a` |
| 装备套装 | `5b5f750686f774093e6cb503` |
| 机匣 | `5b5f751486f77447ec5d770c` |
| 弹药机匣 | `5b5f752e86f774093e6cb505` |
| 弹匣 | `5b5f754a86f774094242f19b` |
| 导轨 | `5b5f755f86f77447ec5d770e` |
| 枪械护木 | `5b5f757486f774093e6cb507` |
| 弹匣井 | `5b5f759686f774094242f19d` |
| 冲锋枪握把 | `5b5f75b986f77447ec5d7710` |
| 枪托 | `5b5f75c686f774094242f19f` |
| 手枪握把 | `5b5f75e486f77447ec5d7712` |
| 握把组件 | `5b5f760586f774093e6cb509` |
| 无枪托式枪托 | `5b5f761f86f774094242f1a1` |
| 折叠式枪托 | `5b5f764186f77447ec5d7714` |
| 消音器 | `5b5f78b786f77447ed5636af` |
| 手枪 | `5b5f78dc86f77409407a7f8e` |
| 突击步枪 | `5b5f78e986f77447ed5636b1` |
| 冲锋枪 | `5b5f78fc86f77409407a7f90` |
| 精确射手步枪 | `5b5f791486f774093f2ed3be` |
| 霰弹枪 | `5b5f792486f77447ed5636b3` |
| 轻机枪 | `5b5f794b86f77409407a7f92` |
| 狙击步枪 | `5b5f796a86f774093f2ed3c0` |
| 栓动式步枪 | `5b5f798886f77447ed5636b5` |
| 步枪 | `5b5f79a486f77409407a7f94` |
| 发射器 | `5b5f79d186f774093f2ed3c2` |
| 冲锋手枪 | `5b5f79eb86f77447ed5636b7` |
| 近战武器 | `5b5f7a0886f77409407a7f96` |
| 投掷物 | `5b5f7a2386f774093f2ed3c4` |
| 次元博物 | `66f1d60097d24f49a043bbd1` |

---

## 二十、配置文件 `config.jsonc`

`EternalCycleServer/config.jsonc`（Mod 根目录），服务启动时读取：

```jsonc
{
  // 是否使用旧版跳蚤市场价格
  "UseOldRagfairPrice": false
}
```

| Key | 类型 | 说明 |
|---|---|---|
| `UseOldRagfairPrice` | bool | 目前为**预留开关**（`Core.cs` 中对应的启用代码被注释） |

---

## 二十一、`Register*` 注册函数速查表

所有函数都放在 `EternalCycle/Utils/*.cs`。参数含义：`modPath` = Mod 根目录（代码内 `modPath`），`path` = 相对路径，`respath` = 资源（图片/语音）相对路径。

| 注册函数 | 挂载阶段事件 | 文件夹模式解析 | 单文件模式解析 |
|---|---|---|---|
| `ItemUtils.RegisterItem(modPath, path, creator, modname)` | `LoadItemEvent` | 每文件单个 `CustomItemTemplate` | `Dictionary<string, CustomItemTemplate>` |
| `QuestUtils.RegisterQuest(modPath, path, respath)` | `LoadQuestEvent` | 每文件单个 `CustomQuest` | `Dictionary<string, CustomQuest>` |
| `QuestUtils.RegisterQuestRewards(modPath, path)` | `LoadQuestRewardEvent` | 每文件 `List<CustomQuestRewardData>` | `List<CustomQuestRewardData>` |
| `QuestUtils.RegisterQuestLogicTree(modPath, path)` | `LoadQuestLogicEvent` | 每文件单个 `QuestLogicTree` | `Dictionary<string, QuestLogicTree>` |
| `TraderUtils.RegisterTrader(modPath, path, imagePath, creator, modname)` | `LoadTraderBaseEvent` | 每文件单个 `TraderBaseWithDesc` | 单个 `TraderBaseWithDesc` |
| `AssortUtils.RegisterAssort(modPath, path)` | `LoadTraderAssortEvent` | 每文件 `List<CustomAssortData>` | `List<CustomAssortData>` |
| `RecipeUtils.RegisterRecipe(modPath, path)` | `LoadRecipeEvent` | 每文件单个 `CustomRecipeData` | `Dictionary<string, CustomRecipeData>` |
| `AchievementUtils.RegisterAchievement(modPath, path, respath)` | `LoadAchievementEvent` | 每文件单个 `CustomAchievementData` | `List<CustomAchievementData>` |
| `SuitUtils.RegisterSuit(modPath, path, traderId=null)` | `LoadSuitEvent` | 每文件 `List<CustomSuit>` | `List<CustomSuit>` |
| `CustomizationUtils.RegisterCustomization(modPath, path, respath)` | `LoadCustomizationEvent` | 每文件 `Dictionary<string, CustomCustomizationItem>` | `Dictionary<string, CustomCustomizationItem>` |
| `PresetUtils.RegisterPreset(modPath, path)` | `LoadPresetEvent` | 每文件单个 `CustomPresetData` | `List<CustomPresetData>` |
| `GiftCodeUtils.RegisterGiftCode(modPath, path)` | `LoadGiftCodeEvent` | 每文件单个 `CustomGiftCodeData` | `Dictionary<string, CustomGiftCodeData>` |
| `ItemUtils.RegisterDrawPool(modPath, path)` | `LoadDrawPoolEvent` | 每文件单个 `DrawPoolClass` | `Dictionary<string, DrawPoolClass>` |
| `BotGeneratorUtils.RegisterAlterBotData(modPath, path)` | `LoadAlterBotEvent` | 每文件单个 `CustomAlterBot` | 单个 `CustomAlterBot` |
| `ItemTagUtils.RegisterItemTag(modPath, path)` | `LoadItemTagEvent` | 不支持（仅单文件） | `ItemTagDictionary` |
| `QuestZoneUtils.RegisterQuestZones(modPath, path)` | `LoadQuestZoneEvent` | 每文件 `List<QuestZone>`（仅 `*.json*`） | `List<QuestZone>` |
| `ResourceUtils.RegisterRigLayoutResource(modPath, path)` | `LoadResourceEvent` | 文件夹内全部文件（MD5+Base64） | 单个文件 |
| `ResourceUtils.RegisterSlotIconResource(modPath, path)` | `LoadResourceEvent` | 同上 | 单个文件 |
| `ResourceUtils.RegisterDecoIconResource(modPath, path)` | `LoadResourceEvent` | 同上 | 单个文件 |
| `ResourceUtils.RegisterTargetResource(modPath, path)` | `LoadResourceEvent` | 同上 | 单个文件 |

---

## 二十二、完整 JSON 示例

以下示例均为**完整可参考**写法（含 `//` 注释，文件模式为“文件夹内单个对象”）。

### 示例 A：普通物品（`$type: base`）

```jsonc
{
  "_id": "我的测试物品",
  "_targetid": "5449016a4bdc2d6f028b456f",   // 复制原版“卢布”，这样自带货币属性可再叠加
  "_name": "test_item",
  "_type": "Item",
  "_props": {
    "Width": 1,
    "Height": 1
  },
  "_customprops": {
    "$type": "base",
    "Name": "测试物品",
    "ShortName": "测试",
    "Description": "这是永恒时序的一个测试物品。",
    "EName": "Test Item",
    "DefaultPrice": 5000,
    "RagfairPrice": 8000,
    "RagfairType": "贵重物品",
    "isMoney": false,
    "addToKappa": true,
    "BlackListType": 2,      // 仅 PMC 拾取黑名单
    "InRaidLimit": 3,
    "InLobbyLimit": 5
  }
}
```

### 示例 B：战利品物品（`$type: lootable`）

```jsonc
{
  "_id": "战利品物资",
  "_targetid": "5448bc234bdc2d3c308b4569",   // 原版“补给物资”
  "_customprops": {
    "$type": "lootable",
    "Name": "豪华补给箱",
    "ShortName": "补给箱",
    "Description": "可以在战局里找到的补给箱。",
    "DefaultPrice": 30000,
    "RagfairType": "容器",
    "CanFindInRaid": true,
    "MapLoot": true,
    "MapLootDivisor": 4,         // 动态刷新概率 = 参考物品 ÷ 4
    "CustomMapLootTarget": "5448bc234bdc2d3c308b4569",
    "StaticLoot": true,
    "StaticLootDivisor": 2,
    "CustomStaticLootTarget": [ "5448bc234bdc2d3c308b4569", "5d6d2b5486f774785c2ba8ea" ] // 或单值
  }
}
```

### 示例 C：礼盒物品（`$type: giftbox`，普通随机盒）

```jsonc
{
  "_id": "神秘礼盒",
  "_targetid": "5a14495786f7747a751c0dc3",
  "_customprops": {
    "$type": "giftbox",
    "Name": "神秘礼盒",
    "ShortName": "礼盒",
    "Description": "打开可获得随机奖励。",
    "DefaultPrice": 20000,
    "isGiftBox": true,
    "BoxData": {
      "Count": 2,                      // 可开 2 次
      "Rewards": {
        "5449016a4bdc2d6f028b456f": 50,   // 卢布 权重50
        "5672cb724bdc2dc2088b456b": 10    // 医疗绷带 权重10
      }
    }
  }
}
```

### 示例 D：任务（`CustomQuest`，含条件与奖励）

```jsonc
{
  "ID": "演示任务",
  "Type": 1,                          // Pickup
  "ImagePath": "quest_demo.png",
  "TraderID": "普拉波尔",
  "Restartable": true,
  "Location": "工厂",
  "QuestData": {
    "Finish": [
      {
        "$type": "find",
        "id": "cond1",
        "inraid": true,
        "itemid": "我的测试物品",
        "count": 2
      },
      {
        "$type": "kill",
        "id": "cond2",
        "oneraid": true,
        "count": 3,
        "bot": "assault",
        "bodyPart": 2,                 // 胸部
        "location": 4,                 // 工厂昼
        "distancetype": 3,             // >=
        "distance": 50,
        "weapon": [ "步枪" ]           // 引用物品标签
      }
    ],
    "Failed": []
  },
  "QuestReward": [
    {
      "$type": "item",
      "ID": "reward1",
      "Quest": "演示任务",
      "Stage": 1,                     // Finish
      "Items": [ { "_id": "主实例", "_tpl": "我的测试物品", "parentId": "hideout", "upd": { "StackObjectsCount": 5 } } ],
      "Count": 1,
      "FindInRaid": false
    },
    {
      "$type": "experience",
      "ID": "reward2",
      "Quest": "演示任务",
      "Stage": 1,
      "Count": 3000
    }
  ]
}
```

### 示例 E：报价单（`CustomAssortData`，正常报价）

```jsonc
{
  "$type": "normal",
  "ID": "普拉波尔卖测试物品",
  "Trader": "普拉波尔",
  "Item": [ { "_id": "出售实例", "_tpl": "我的测试物品", "parentId": "hideout" } ],
  "Barter": { "5449016a4bdc2d6f028b456f": 10000 },   // 卖 1 万卢布
  "DogTag": {
    "59f32c3b86f77472a31742f0": { "count": 1, "level": 5, "side": 1 }
  },
  "TrustLevel": 2
}
```

### 示例 F：配方（`CustomRecipeData`，制造配方）

```jsonc
{
  "$type": "normal",
  "ID": "制造测试物品",
  "Area": 1,                 // 发电机
  "AreaLevel": 2,
  "Output": "我的测试物品",
  "OutputCount": 1,
  "Time": 3600,
  "NeedFuel": true,
  "Require": {
    "Tool": { "5448fee04bdc2d84298b456a": 1 },   // 工具
    "Item": { "57347ca924597745ee10d352": 5 }    // 材料
  }
}
```

### 示例 G：商人（`TraderBaseWithDesc`）

```jsonc
{
  "_id": "演示商人",
  "name": "演示商人",
  "nickname": "演示",
  "surname": "永恒",
  "currency": "RUB",
  "avatar": "trader_demo.png",       // 放在 imagePath 目录
  "customization_seller": true,
  "description": "一个演示商人。",
  "insuranceChance": 40,
  "minReflashTime": 1800,
  "maxReflashTime": 3600,
  "showInRagfair": true
}
```

### 示例 H：套装（`CustomSuit`）

```jsonc
{
  "_id": "演示套装",
  "suiteId": "5cd946231388ce000d572fe3",
  "tid": "演示商人",
  "isActive": true,
  "requirements": {
    "ProfileLevel": 20,
    "LoyaltyLevel": 3,
    "ItemRequirements": [ { "count": 2, "itemId": "我的测试物品" } ],
    "QuestRequirements": [ "演示任务" ]
  }
}
```

### 示例 I：礼物码（`CustomGiftCodeData`）

```jsonc
{
  "id": "迎新礼包",
  "code": "EC-WELCOME-2026",
  "item": {
    "主奖励": [ { "_id": "主物品", "_tpl": "我的测试物品", "parentId": "hideout", "upd": { "StackObjectsCount": 10 } } ]
  },
  "message": "欢迎来到永恒时序！",
  "storagetime": 72,
  "maxcount": 1
}
```

---

## 二十三、常见问题（FAQ）

**Q1：我把 JSON 文件放进文件夹，为什么游戏里没生效？**
A：JSON 不会自动被发现。必须由代码调用对应的 `Register*` 函数把目录/文件挂到加载事件上（见 [21. 速查表](#二十一-register-注册函数速查表)）。EternalCycle 自带的加密物品包在 `Core.cs` 中已直接挂载；自定义数据需你在 Mod 入口（如 `IOnLoad`）里调用 `RegisterItem`、`RegisterQuest` 等。

**Q2：ID 我随便写中文可以吗？**
A：可以。所有 ID 字段加载时会自动 `ConvertHashID()`（24 位 hex 保留，否则 SHA1 哈希取前 24 位）。同一字符串结果稳定，跨文件关联不会断。

**Q3：为什么我的物品没出现在跳蚤市场 / 手册里？**
A：检查 `_customprops` 是否填写 `RagfairType`（手册分类）与 `DefaultPrice`。空分类时会回退“其他”。注意 `Core.cs` 会把所有未分类且非任务物品自动归入“调试物品”并加全局黑名单。

**Q4：为什么我的物品在战局里刷不出来？**
A：确认 `$type` 为 `lootable`、`CanFindInRaid=true`，并正确配置 `MapLoot` / `StaticLoot`。这两个默认仅在你显式设了 `MapLootDivisor`/`StaticLootDivisor` 且 `CustomLoot=true` 时采用自定义概率；否则用目标物品默认除数。

**Q5：`$type` 不写会怎样？**
A：多态字段（`_customprops`、任务条件、任务奖励、`GiftData`、报价单、配方）缺 `$type` 会反序列化失败或退回基类，多数情况该对象被忽略。**务必每个多态对象都带 `$type`**。

**Q6：我的 JSON 里可以写 `//` 注释吗？**
A：可以。所有经 `JsonSerializer` 解析的文件都开启了注释跳过（`JsonCommentHandling.Skip`），加密物品包除外（解密后是纯 JSON）。

**Q7：如何更新/覆盖一个已有的原版物品？**
A：物品的 `_id` 设为该物品 ID，并在 `_props` 里填要覆盖的属性；`_targetid` 可填该物品自身。`CreateAndAddItem` 检测到 `_id` 已存在时会直接基于现有物品叠加属性（更新模式）。

**Q8：任务条件里的 `dogtaglevel`、`tags` 是什么意思？**
A：`dogtaglevel` 是狗牌等级门槛（可选）；`tags` 引用物品标签（见 [17. 物品标签](#十七物品标签-itemtagdictionary)），引用标签会自动展开为该标签包含的全部物品 ID。

---

*本文档依据 `EternalCycle/Classes/*.cs` 与 `EternalCycle/Utils/*.cs` 源码生成，如与代码行为冲突，以代码为准。*
