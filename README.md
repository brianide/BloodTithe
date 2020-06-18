# Blood Tithe
A TShock plugin that provides a means to mitigate Demon Altar corruption.

## Details
The purpose of this plugin is to make the spread of corruption resulting from Demon Altars more manageable. With the plugin active, Demon Altars will consume Life Crystals thrown onto them; after being fed three of them, they'll shatter as if they were hammered. If an altar so destroyed releases any corruption into the world, a fairy will appear to take you to it so that you can root it out. [See this video for a demonstration](https://youtu.be/_4b34-RtKtU).

The plugin can also be configured to simply cancel the corruption from Demon Altars in a more straightforward fashion.

## Configuration
Run the TShock executable after installing the plugin to generate a default configuration file. The file will be located in the `/tshock` subfolder, called `BloodTithe.json`.

| Parameter | Description | Default |
| --- | --- | --- |
| `Item` | The ID of the item that altars will accept. | `29` (Life Crystal) |
| `ItemsRequired` | The number of items that must be fed to the altar for the effect to trigger. | `3` |
| `PreventCorruption` | If `true`, corruption from altar-smashing will be prevented altogether. | `false` | 
| `SpawnWarpFairy` | If `true`, a fairy guide will be spawned to take the player to the corrupted tile. | `true` |

Generally, you'll want either `PreventCorruption` or `SpawnWarpFairy` enabled; there's not much sense in having neither or both active.

## Contributing
Feel free to open an issue if you run into any bugs, or just have a suggested improvement or feature request.