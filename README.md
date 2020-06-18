# Blood Tithe
A TShock plugin that provides a means to mitigate Demon Altar corruption.

## Details
The purpose of this plugin is to make the spread of corruption resulting from Demon Altars more manageable. With the plugin active, placing three Life Crystals near a Demon Altar before breaking it will cause a fairy to appear once it's shattered. Touching this fairy will cause it to teleport the player to an area near the new corruption, and then guide them directly to the affected tile. [See this video for a demonstration of this effect](https://youtu.be/IAquudGoUqc).

The plugin can also be configured to cancel the corruption from Demon Altars in a more straightforward fashion.

## Configuration
Run the TShock executable after installing the plugin to generate a default configuration file. The file will be located in the `/tshock` subfolder, called `BloodTithe.json`.

| Parameter | Description | Default |
| --- | --- | --- |
| `Item` | The ID of the item that altars will accept to trigger the effect. | `29` (Life Crystal) |
| `ItemsRequired` | The number of items that must be available for consumption for the effect to trigger. | `3` |
| `PreventCorruption` | If `true`, corruption from altar-smashing will be prevented altogether. | `false` | 
| `SpawnWarpFairy` | If `true`, a fairy guide will be spawned to take the player to the corrupted tile. | `true` |

Generally, you'll want either `PreventCorruption` or `SpawnWarpFairy` enabled; there's not much sense in having neither or both at the same time.

## Contributing
Feel free to open an issue if you run into any bugs, or just have a suggested improvement or feature request.