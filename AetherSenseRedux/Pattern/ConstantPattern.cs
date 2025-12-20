
using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace AetherSenseRedux.Pattern
{
    internal class ConstantPatternType : IPatternType
    {
        public string Name => "Constant";

        public PatternConfig GetDefaultConfiguration()
        {
            return new ConstantPatternConfig();
        }

        public PatternConfig DeserializeConfiguration(dynamic source)
        {
            return new ConstantPatternConfig()
            {
                Duration = (long)source.Duration,
                Level = (double)source.Level,
            };
        }

        public IPattern Create(PatternConfig config)
        {
            if (config is ConstantPatternConfig ccfg) return new ConstantPattern(ccfg);
            throw new ArgumentException("config isn't ConstantPatternConfig");
        }

        /// <summary>
        /// Draws the configuration interface for constant patterns
        /// </summary>
        /// <param name="pattern">A ConstantPatternConfig object containing the current configuration for the pattern.</param>
        public void DrawSettings(PatternConfig config)
        {
            if (config is ConstantPatternConfig ccfg)
            {
                long duration = ccfg.Duration;
                if (ImGui.InputLong("Duration (ms)", ref duration))
                {
                    ccfg.Duration = duration;
                }

                double level = ccfg.Level;
                if (ImGui.InputDouble("Level", ref level))
                {
                    ccfg.Level = level;
                }
            }
            else
            {
                ImGui.Text("Internal error: config is not ConstantPatternConfig");
            }
        }
    }

    internal class ConstantPattern : IPattern
    {
        public DateTime Expires { get; set; }
        private readonly double level;

        public ConstantPattern(ConstantPatternConfig config)
        {
            level = config.Level;
            Expires = DateTime.UtcNow + TimeSpan.FromMilliseconds(config.Duration);
        }

        public double GetIntensityAtTime(DateTime time)
        {
            if (Expires < time)
            {
                throw new PatternExpiredException();
            }
            return level;
        }
        public static PatternConfig GetDefaultConfiguration()
        {
            return new ConstantPatternConfig();
        }
    }
    [Serializable]
    public class ConstantPatternConfig : PatternConfig
    {
        public override string Type { get; } = "Constant";
        public double Level { get; set; } = 1;
    }
}
