using System;
using Dalamud.Bindings.ImGui;


namespace AetherSenseRedux.Pattern
{
    internal class SquarePatternType : IPatternType
    {
        public string Name => "Square";

        public PatternConfig GetDefaultConfiguration()
        {
            return new SquarePatternConfig();
        }

        public PatternConfig DeserializeConfiguration(dynamic source)
        {
            return new SquarePatternConfig()
            {
                Duration = (long)source.Duration,
                Level1 = (double)source.Level1,
                Level2 = (double)source.Level2,
                Duration1 = (long)source.Duration1,
                Duration2 = (long)source.Duration2,
                Offset = (long)source.Offset,
            };
        }

        public IPattern Create(PatternConfig config)
        {
            if (config is SquarePatternConfig spcfg) return new SquarePattern(spcfg);
            throw new ArgumentException("config is not SquarePatternConfig");
        }

        /// <summary>
        /// Draws the configuration interface for square patterns
        /// </summary>
        /// <param name="pattern">A SquarePatternConfig object containing the current configuration for the pattern.</param>
        public void DrawSettings(PatternConfig config)
        {
            if (config is SquarePatternConfig pattern)
            {
                int duration = (int)pattern.Duration;
                if (ImGui.InputInt("Duration (ms)", ref duration))
                {
                    pattern.Duration = (long)duration;
                }
                double level1 = (double)pattern.Level1;
                if (ImGui.InputDouble("Level 1", ref level1))
                {
                    pattern.Level1 = level1;
                }
                int duration1 = (int)pattern.Duration1;
                if (ImGui.InputInt("Level 1 Duration (ms)", ref duration1))
                {
                    pattern.Duration1 = (long)duration1;
                }
                double level2 = (double)pattern.Level2;
                if (ImGui.InputDouble("Level 2", ref level2))
                {
                    pattern.Level2 = level2;
                }
                int duration2 = (int)pattern.Duration2;
                if (ImGui.InputInt("Level 2 Duration (ms)", ref duration2))
                {
                    pattern.Duration2 = (long)duration2;
                }
                int offset = (int)pattern.Offset;
                if (ImGui.InputInt("Offset (ms)", ref offset))
                {
                    pattern.Offset = (long)offset;
                }
            }
            else
            {
                ImGui.Text("Internal error: config is not SquarePatternConfig");
            }
        }
    }

    internal class SquarePattern : IPattern
    {
        public DateTime Expires { get; set; }
        private readonly double level1;
        private readonly double level2;
        private readonly long duration1;
        private readonly long offset;
        private readonly long total_duration;


        public SquarePattern(SquarePatternConfig config)
        {
            level1 = config.Level1;
            level2 = config.Level2;
            duration1 = config.Duration1;
            offset = config.Offset;
            Expires = DateTime.UtcNow + TimeSpan.FromMilliseconds(config.Duration);
            total_duration = duration1 + config.Duration2;
        }

        public double GetIntensityAtTime(DateTime time)
        {
            if (Expires < time)
            {
                throw new PatternExpiredException();
            }
            long patternTime = DateTime.UtcNow.Ticks / 10000 + offset;

            long progress = patternTime % total_duration;

            return (progress < duration1) ? level1 : level2;
        }
        public static PatternConfig GetDefaultConfiguration()
        {
            return new SquarePatternConfig();
        }
    }
    [Serializable]
    public class SquarePatternConfig : PatternConfig
    {
        public override string Type { get; } = "Square";
        public double Level1 { get; set; } = 0;
        public double Level2 { get; set; } = 1;
        public long Duration1 { get; set; } = 200;
        public long Duration2 { get; set; } = 200;
        public long Offset { get; set; } = 0;
    }
}
