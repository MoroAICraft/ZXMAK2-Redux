# Building ZXMAK2-Redux 0.98

Clone this repository or extract the source archive from release 0.98. The
ZX-OmniSound snapshot is included; `git submodule update` is not required.

Use Windows, Visual Studio Build Tools 2022 (or compatible MSBuild) with the
.NET desktop build tools and the .NET Framework 4.6.2 targeting pack.
The emulator uses x86 native libraries even on 64-bit Windows.

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build.ps1
```

The script locates MSBuild through vswhere. You may pass `-MSBuild` explicitly.
Managed build output goes to `artifacts/managed`. This output is not a complete
portable distribution: run the release ZIP for the full Config/Resources/ROM
layout. Do not copy a new DLL into an unrelated installed version.

Native source and notices are included under `src/Libraries` and
`src/ZX-OmniSound/native`; bundled native DLLs are used by the managed build.
Individual native rebuilds use their own toolchains/instructions.
The historical solution can report two missing code-analysis ruleset warnings;
these do not prevent the managed build.

## По-русски

Скачайте исходники 0.98 или клонируйте репозиторий. ZX-OmniSound уже включён:
доступ к дискам разработчика и отдельному репозиторию не требуется.
Нужны Windows, MSBuild/Visual Studio Build Tools с инструментами .NET desktop
и targeting pack .NET Framework 4.6.2. Команда выше собирает управляемую часть
в `artifacts/managed`; готовый переносимый комплект берите из Releases.
Исходники native-библиотек и их лицензии также включены, но их отдельная
пересборка требует соответствующих инструментов. Исторические инструкции
в остальных каталогах описывают прежние этапы, а не текущий порядок сборки.
