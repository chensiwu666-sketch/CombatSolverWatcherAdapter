# 构建说明

## 需要什么

| 依赖 | 版本 / 说明 |
|---|---|
| .NET SDK | 9.0 |
| 游戏 | Slay the Spire 2 0.111.0（需要 `data_sts2_windows_x86_64/sts2.dll` 与 `0Harmony.dll`） |
| Combat Solver | 0.50.1（编译期需要一个**成员已公开化**的 `CombatSolver.dll` 副本） |
| 观者（Watcher） | 0.10.8（需要工坊目录里的 `Watcher.dll`） |

本仓库**不附带**任何游戏、Combat Solver 或观者 mod 的二进制文件。

## 公开化 CombatSolver.dll

适配层要调用 Combat Solver 的内部镜像注册表。作者手册（`docs/third-party/README.md`）写明的做法是：
用一个 publicizer 把程序集里的类型与成员改成 `public`，**保持程序集标识不变**，编译期引用这份副本，
运行期由游戏加载工坊里的原始 DLL；再配合 `[assembly: IgnoresAccessChecksTo("CombatSolver")]`
（见 `AssemblyAttributes.cs`）绕过运行时访问检查。

自己生成一份即可：任何能把 IL 中 `private/internal` 改成 `public` 且不重建程序集标识的工具都行
（Mono.Cecil 写几十行即可；Krafs.Publicizer 之类的现成包也可以）。

## 配置路径

复制示例文件，按自己机器改：

```sh
cp local.props.example local.props
```

```xml
<Project>
  <PropertyGroup>
    <Sts2Dir>D:/SteamLibrary/steamapps/common/Slay the Spire 2</Sts2Dir>
    <WatcherModDir>D:/SteamLibrary/steamapps/workshop/content/2868840/3747526116</WatcherModDir>
    <PublicizedSolverDll>D:/work/refs/CombatSolver.dll</PublicizedSolverDll>
  </PropertyGroup>
</Project>
```

`local.props` 已被 git 忽略。

## 构建与部署

```sh
dotnet build -c Release
cp bin/Release/WatcherAdapter.dll "<游戏目录>/mods/WatcherAdapter/"
cp mods/WatcherAdapter/WatcherAdapter.json "<游戏目录>/mods/WatcherAdapter/"
```

或者用 `tools/package.sh` 一键构建两个 mod 并打成 zip（`dist/`）。

## 验证

启动游戏后读 `<APPDATA>/SlayTheSpire2/logs/WatcherAdapter.log`，正常应包含：

```
CombatSolver assembly: 0.50.1.0 ...
self-check: 75 Watcher card mirror(s) present in the registry
```

换求解器版本时适配层会主动拒绝注册并在日志写明原因（`DISABLED: ...`），这是有意为之。
