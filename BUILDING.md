# Building
Building the plugin solution requires a common library called [CommonGround](https://github.com/brianide/CommonGround).

The library is available as a content-only nuget package; to prepare your build environment after a fresh clone, you can run the following commands in bash or PowerShell:
```bash
nuget restore
cp -r packages/CommonGround.Sources.0.1.0/content/CommonGround BloodTithe/
```
After that, the solution should build normally from within Visual Studio.

## But Why?
A proper binary nuget package would, as far as I know, require distributing an additional library DLL for CommonGround alongside the plugin itself, which I'd like to avoid. If you know of a better way of achieving this that doesn't involve a hacky content-only nuget package, I would be delighted to merge a PR from you.
