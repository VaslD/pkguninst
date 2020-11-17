# macOS Package (`*.pkg`) Uninstaller

A wrapper for macOS system utility `pkgutil`. *Package Uninstaller* can extract registered changes to your system by any package then reverse these changes, effectively uninstalls the package.

*Package Uninstaller* deletes files atomically. If any delete operation fails mid-way, "deleted files" are restored. *Package Uninstaller* also attempts to delete empty folders left by uninstallation.

## Usage

### `pkguninst interactive`

Run *Package Uninstaller* in keyboard interactive mode. Allowing you to see a list of Volumes and installed package before choosing packages to uninstall.

`--force` switch toggles protection of Apple-provided packages that may be required by macOS.

### `pkguninst list`

Show all packages installed on a specific Volume. Default to `/`.

Supply with `--volume <PATH>` to change Volume.

### `pkguninst remove`

Remove one or more packages matching a list of package IDs or a RegEx filter. Run `pkguninst help remove` for detailed syntax.

`--force` switch toggles protection of Apple-provided packages that may be required by macOS.

`--quiet` switch toggles requirement of confirmation before deleting files.
