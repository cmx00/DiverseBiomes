using System;
using BepInEx.Configuration;

namespace DiverseBiomes
{
    internal static class ModConfig
    {
        internal static float GiantMinimumPlanetRadius { get; private set; }
        internal static float MaximumPlayerColliderScale { get; private set; }
        internal static float PlayerCollisionDamageMultiplier { get; private set; }
        internal static bool DisablePlayerVegetationCollision { get; private set; }

        internal static DistributionSettings Bind(ConfigFile config)
        {
            // Existing key names are a data compatibility contract, not a copy
            // of the former configuration migration/implementation.
            string[] sections = { "1 - Super-large / 超大型", "2 - Large / 大型", "3 - Triple peak / 三峰型" };
            string[] keys = { "Peak multiplier minimum / 波峰倍数下限", "Peak multiplier maximum / 波峰倍数上限",
                "Small peak minimum / 小型峰下限", "Small peak maximum / 小型峰上限",
                "Medium peak minimum / 中型峰下限", "Medium peak maximum / 中型峰上限",
                "Large peak minimum / 大型峰下限", "Large peak maximum / 大型峰上限" };
            float[] values = { 1.75f, 2.5f, 1.25f, 1.75f, 1f, 1f, 1.25f, 1.75f, 2.375f, 3f, .5f, 5, 15, 4, 7.5f };
            for (int i = 0; i < 10; i++) {
                int group = i < 2 ? 0 : i < 4 ? 1 : 2;
                int key = i < 4 ? i % 2 : i - 2;
                values[i] = Float(config, sections[group], keys[key], values[i], 1, 7, "Plant size peak. / 植物尺寸波峰。");
            }
            const string giants = "4 - Giant vegetation / 巨型植被";
            values[10] = Float(config, giants, "Planet chance / 星球概率", .5f, 0, 1, "Chance of a giant grove. / 古木群出现概率。");
            values[11] = LegacyConfiguration.Bind(config, giants, "Minimum count / 最少数量", 5,
                new ConfigDescription("Giant grove count range. / 古木数量范围。", new AcceptableValueRange<int>(0, 100))).Value;
            values[12] = LegacyConfiguration.Bind(config, giants, "Maximum count / 最多数量", 15,
                new ConfigDescription("Giant grove count range. / 古木数量范围。", new AcceptableValueRange<int>(0, 100))).Value;
            values[13] = Float(config, giants, "Minimum multiplier / 最小倍数", 4, 1, 15, "Giant size range. / 古木尺寸范围。");
            values[14] = Float(config, giants, "Maximum multiplier / 最大倍数", 7.5f, 1, 15, "Giant size range. / 古木尺寸范围。");
            GiantMinimumPlanetRadius = Float(config, giants, "Minimum planet radius for giants / 巨型植物最低行星半径",
                400, 0, 510, "Set zero to allow giant groves on every radius. / 设零取消古木最低星球半径限制。");
            const string collision = "4 - Player collision / 人物碰撞";
            DisablePlayerVegetationCollision = LegacyConfiguration.Bind(config, collision, "Disable physical collision / 关闭物理碰撞",
                false, "Let Icarus pass through vegetation. / 允许伊卡洛斯穿过植被。").Value;
            MaximumPlayerColliderScale = Float(config, collision, "Maximum collider scale / 碰撞体最大倍数", 1.5f, 1, 7,
                "Limit physical obstructions without shrinking visible plants. / 限制人物碰撞体，不缩小可见植物。");
            PlayerCollisionDamageMultiplier = Float(config, collision, "Vegetation damage multiplier / 植被伤害倍率", .1f, 0, 1,
                "Damage to plants from Icarus collisions. / 伊卡洛斯撞击植物的伤害比例。");
            return new DistributionSettings(values);
        }
        private static float Float(ConfigFile file, string section, string key, float value, float lo, float hi, string explanation)
            => LegacyConfiguration.Bind(file, section, key, value,
                new ConfigDescription(explanation, new AcceptableValueRange<float>(lo, hi))).Value;
    }
}
