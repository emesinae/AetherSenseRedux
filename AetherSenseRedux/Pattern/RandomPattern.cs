using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;

namespace AetherSenseRedux.Pattern
{
    internal class RandomPatternType : IPatternType
    {
        public string Name => "Random";

        public PatternConfig GetDefaultConfiguration()
        {
            return new RandomPatternConfig();
        }

        public PatternConfig DeserializeConfiguration(dynamic source)
        {
            return new RandomPatternConfig()
            {
                Duration = (long)source.Duration,
                Minimum = (double)source.Minimum,
                Maximum = (double)source.Maximum,
            };
        }

        public IPattern Create(PatternConfig config)
        {
            if (config is RandomPatternConfig rpcfg) return new RandomPattern(rpcfg);
            throw new ArgumentException("config is not RandomPatternConfig");
        }

        /// <summary>
        /// Draws the configuration interface for random patterns
        /// </summary>
        /// <param name="pattern">A RandomPatternConfig object containing the current configuration for the pattern.</param>
        public void DrawSettings(PatternConfig config)
        {
            if (config is RandomPatternConfig pattern)
            {
                int duration = (int)pattern.Duration;
                if (ImGui.InputInt("Duration (ms)", ref duration))
                {
                    pattern.Duration = (long)duration;
                }
                double min = (double)pattern.Minimum;
                if (ImGui.InputDouble("Minimum", ref min))
                {
                    pattern.Minimum = min;
                }
                double max = (double)pattern.Maximum;
                if (ImGui.InputDouble("Maximum", ref max))
                {
                    pattern.Maximum = max;
                }
            }
            else
            {
                ImGui.Text("Internal error: config is not RandomPatternConfig");
            }
        }
    }

    internal class RandomPattern : IPattern
    {
        public DateTime Expires { get; set; }
        private readonly Random rand = new();
        private readonly double min;
        private readonly double max;

        public RandomPattern(RandomPatternConfig config)
        {
            Expires = DateTime.UtcNow + TimeSpan.FromMilliseconds(config.Duration);
            min = config.Minimum;
            max = config.Maximum;
        }

        public double GetIntensityAtTime(DateTime time)
        {
            if (Expires < time)
            {
                throw new PatternExpiredException();
            }
            return Scale(rand.NextDouble(), min, max);
        }
        private static double Scale(double value, double min, double max)
        {
            return value * (max - min) + min;
        }

        public static PatternConfig GetDefaultConfiguration()
        {
            return new RandomPatternConfig();
        }
    }
    [Serializable]
    public class RandomPatternConfig : PatternConfig
    {
        public override string Type { get; } = "Random";
        public double Minimum { get; set; } = 0;
        public double Maximum { get; set; } = 1;
    }
}
