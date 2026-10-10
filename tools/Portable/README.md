# Portable build and verification

Current v90 exception to the historical v87 policy: selected host SD folders
save guest deltas in place; never clone/switch the directory. Settings/journals
stay local; internal paths are relative, external folder paths absolute. Raw
image/HDD/floppy policy is unchanged. Do not run obsolete v89 real-source write
probes; direct save tests must use disposable external fixtures. Compile
PortableFolderProbe.cs together with DirectFolderCases.cs, then run core/large.
Use -Version v90 and -ArchiveName ZXMAK2-v90-Portable-Direct-SD-Folder-Test.

v88 adds a separate TEST candidate on branch codex/soundcards-v88. Pass
`-Version v88` to PrepareRuntime with the preserved structured v87 baseline;
pass `-ArchiveName ZXMAK2-v88-Portable-SoundCards-Test` to PackageRuntime.
Sound-card tests use tools/SoundCards/RunSoundCards.ps1 and ONLY a private
diagnostics/test-runtime. Current pinned OmniSound is included dynamically
by ExportSources, rather than hardcoding an older submodule commit.
v87 workflow below is retained as historical baseline documentation.

v87 is TEST ONLY until the user explicitly accepts it as the next work base.
Start from c57bf2b (v86), branch codex/portable-v87, not cancelled v83.

1. Build src/ZXVM.sln Release/Any CPU using VS 2022 Build Tools MSBuild, /m:1.
2. PrepareRuntime.ps1 copies the preserved v86 runtime into a NEW destination,
   updates only changed managed components, preserves native binaries/ROM/bank,
   and lays out Lib, Config, Resources, Docs, Data. It rejects an existing target.
3. RunPortable.ps1 verifies independent external saves, merges/conflicts, reset
   and imported config paths, 22 model template smoke checks, real copied runtime
   with Unicode/spaces/# and foreign CWD; native sound and bank via Test.exe.
4. tools/Optimization/RunRegressions.ps1 supports both old flat and new layout.
5. Compare OptimizationProbe fingerprints to an isolated v86 runtime using THE
   SAME input VMZ. Previous archived fingerprints may use a different profile.
6. PackageRuntime.ps1 rejects probes/test data, checks all ZIP SHA256 entries,
   preserves every musical dependency, removes only four root active state files
   for Clean. Test.exe remains in both archives.
7. Commit verified sources, export using tools/Optimization/ExportSources.ps1.
   Include pinned OmniSound source and both verified local Git-history bundles.

Do not mutate published old releases, user D: copies or external source media.
Diagnostics use private local fixtures. Save checkpoint commits/archives before
long checks. Record unperformed manual/hardware checks explicitly.

The developer build remains a flat DLL output for compatibility with old probes;
its EXE config points to Config (copied by a project target). PrepareRuntime is
the reproducible packaging step for the structured runtime. Never just move DLLs
in an old release whose executable lacks probing/bootstrap and portable paths.
