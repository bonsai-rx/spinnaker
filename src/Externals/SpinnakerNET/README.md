# SpinnakerNET reference assembly

`ref/SpinnakerNET_v140.dll` is a reference assembly: it carries only the public
API metadata of the FLIR Spinnaker SDK managed assembly, with all method bodies
removed. It exists solely so this package and downstream packages can compile
against the Spinnaker API. It is never deployed or loaded at runtime; the actual
implementation is bound at runtime to the Spinnaker SDK installed on the machine.

## Regenerating

The reference is generated from the managed assembly of an installed Spinnaker
SDK using [Refasmer](https://github.com/JetBrains/Refasmer):

```
dotnet tool install --global JetBrains.Refasmer.CliTool
refasmer --omit-non-api-members true --output ref/SpinnakerNET_v140.dll <SDK>/bin64/vs2015/SpinnakerNET_v140.dll
```

Regenerate only when changing the pinned SDK API version. The current reference
was generated from SDK version 4.2.0.83.
