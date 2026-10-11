# Diverse Biomes

[简体中文](README.zh-CN.md) · 2.4.0 preview · Works with vanilla; GS2 optional

[Download 2.4.0](https://github.com/cmx00/DiverseBiomes/releases/tag/v2.4.0)

Diverse Biomes is a vegetation mod for Dyson Sphere Program. Plant density and size vary with terrain, producing meadows, dense woods and sparse slopes on the same planet. Each planet theme has its own settings.

During construction, only plants that physically overlap buildings are cleared, preserving trees and grass beside your factory.

**Galactic Scale and BetterSolarSystem are not required.** The mod works in vanilla-generated galaxies. If GS2 is installed, additional theme support is enabled automatically.

![Meadow and woodland edge](assets/europa-meadow.jpg)

## Terrain and vegetation

A community is a local combination of plant types, density and sizes. Distribution follows shorelines, elevation, slope and local moisture:

- Shores and moist lowlands have more connected vegetation.
- Meadows, forest edges and dense woodland have different densities, with clearings between trees.
- Steep slopes, ridges and cold areas use sparser or shorter vegetation.
- Rocky, icy and desert worlds share rules for existing surface decoration; they do not gain forests.

The mod uses models already available in the planet's theme. Themes without plants only receive changes to rocks and other decoration. Terrain is unchanged.

## Planet themes

Vanilla Mediterranean, Oceanic Jungle, Red Stone, Prairie, Sakura, Savanna and Pandora Swamp themes have their own rules. With GS2 installed, additional themes such as Gigantic Forest and Red Forest are supported too.

| Theme | Main variation |
| --- | --- |
| Prairie and Savanna | Open grassland alternates with scattered trees and woodland patches |
| Mediterranean and Oceanic Jungle | Shore woods, moist lowlands, clearings and upland vegetation connect across terrain |
| Red Stone | Grass and dry vegetation form patches, with sparser rocky slopes |
| Sakura | Flower valleys, dense woods and open slopes have distinct coverage |
| Pandora Swamp | Wetland plants mixed with alien woodland |
| GS2 forest themes | Separate settings for dense woods, clearings and wooded slopes |

Twelve organic themes are supported, each with eight local community combinations; some themes belong to GS2. Cold areas, wind-exposed terrain, rocky slopes and bare ground share transition rules. Plant density and sizes change gradually between neighboring communities.

## Construction and clearing

Construction normally clears nearby plants. This mod checks each building's actual shape to reduce unnecessary clearing. Foundation placement and manual harvesting still work. Plant sizes are capped to avoid obstructing movement.

## GS2 support

With GS2 installed, some solid-resource clusters also receive small position adjustments to better fit local terrain. This does not add resources or relocate deposits already generated in a save. **Vanilla mode provides vegetation and construction preservation, without ore-position adjustments.**

![Planet surface overview](assets/europa-homeworld.jpg)

*These screenshots also use BetterSolarSystem. Jupiter and the Solar System layout come from that mod; installing Diverse Biomes alone does not generate this system.*

## Installation

Requires **BepInEx and DSPModSave**. DSPModSave stores each save's community settings. Galactic Scale and BetterSolarSystem are optional.

Use either vanilla galaxy generation or a GS2 generator. For manual installation, close the game and put both included files in the same folder:

```text
BepInEx/plugins/DiverseBiomes/DiverseBiomes.dll
BepInEx/plugins/DiverseBiomes/DiverseBiomes.GalacticScale.dll
```

The second file is the GS2 adapter and loads only when GS2 is present.

## Existing saves and compatibility

**Start a new save after installation to get the complete new communities.** Community and plant-size settings are saved with the galaxy. Configuration changes apply to new galaxies; existing galaxies keep their saved settings.

Existing vanilla saves retain their original vegetation distribution. Older LW1–LW3 saves are not converted to the new community sampler. Cleared plants are not restored in existing factories, and undeveloped or regenerated terrain may use base vegetation. Keep the original version if exact historical surface appearance matters.

Back up or move DSPModSave's `.moddsv` companion file together with the save. GS2 saves still need GS2 and their matching `.gs2` files; this mod does not convert them to vanilla galaxies.

Settings remain in `BepInEx/config/cmx.LivingWorlds.cfg` to preserve older configuration. Disable older LivingWorlds, GiantPlants and VegetationProtection installations before switching to this package.

Targeted checks passed for loading without GS2, vanilla theme recognition, settings round-trips, save switching and the optional adapter. Full in-game appearance, construction and save/reload behavior still need validation, so this is a preview release.

See [BUILDING.md](BUILDING.md) to build from source. Reports should include theme, planet radius, seed, whether GS2 is installed and screenshots. Source is under the [MIT License](LICENSE); code provenance and image rights are described in [NOTICE.md](NOTICE.md).
