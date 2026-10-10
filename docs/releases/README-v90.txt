ZXMAK2 v90 - Portable Direct SD Folder TEST

Based on v89 with its long-path, Refresh, save-progress and audio fixes.
Mounted host SD folders now save guest changes DIRECTLY to the selected folder.
No full folder copy is created and the selected path is never switched to one.
Only changed/new/deleted guest entries are committed; independent PC edits remain.
Same-file PC/guest conflicts stop saving, retaining the session; no silent overwrite.

Refresh: PC-only changes reread without a question. Guest changes offer Save,
Without saving or Cancel. Successful reread remounts the same current folder and
cold-starts the emulated machine, not the entire ZXMAK2 application.
Exit/Eject Save writes to the same selected folder; No discards, Cancel keeps it.
Missing/unavailable startup folder leaves an empty SD slot, no question/copy.

Portable emulator settings/logs/cache/session/staging remain inside this folder.
Internal references stay EXE-relative; external SD folder paths stay absolute.
On another PC, choose a new folder yourself if the old path is unavailable.
External SD images/HDD/floppy retain their v89 local-copy policy: this change
is specifically for explicitly mounted host folders. Export IMG is explicit.

Temporary rollback data includes only affected files, not the whole collection.
Old copies from earlier releases are not deleted or selected automatically.
Long paths need .NET 4.6.2+, 4.8 recommended. No Windows registry changes.
Old v89 and user D: runtime/original music remain untouched during development.
See V90_RELEASE_REPORT.md. TEST until explicit user acceptance.
