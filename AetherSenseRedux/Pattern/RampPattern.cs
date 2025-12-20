using Dalamud.Bindings.ImGui;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherSenseRedux.Pattern
{
    internal class RampPatternType : IPatternType
    {
        public string Name => "Ramp";

        public PatternConfig GetDefaultConfiguration()
        {
            return new RampPatternConfig();
        }

        public PatternConfig DeserializeConfiguration(dynamic source)
        {
            return new RampPatternConfig()
            {
                Duration = (long)source.Duration,
                Start = (double)source.Start,
                End = (double)source.End,
            };
        }

        public IPattern Create(PatternConfig config)
        {
            if (config is RampPatternConfig rpcfg) return new RampPattern(rpcfg);
            throw new ArgumentException("config is not RampPatternConfig");
        }

        /// <summary>
        /// Draws the configuration interface for ramp patterns
        /// </summary>
        /// <param name="pattern">A RampPatternConfig object containing the current configuration for the pattern.</param>
        public void DrawSettings(PatternConfig config)
        {
            if (config is RampPatternConfig pattern)
            {
                int duration = (int)pattern.Duration;
                if (ImGui.InputInt("Duration (ms)", ref duration))
                {
                    pattern.Duration = (long)duration;
                }
                double start = (double)pattern.Start;
                if (ImGui.InputDouble("Start", ref start))
                {
                    pattern.Start = start;
                }
                double end = (double)pattern.End;
                if (ImGui.InputDouble("End", ref end))
                {
                    pattern.End = end;
                }
            }
            else
            {
                ImGui.Text("Internal error: config is not RampPatternConfig");
            }
        }
    }

    internal class RampPattern : IPattern
    {
        public DateTime Expires { get; set; }
        private readonly double startLevel;
        private readonly double endLevel;
        private readonly long duration;


        public RampPattern(RampPatternConfig config)
        {
            startLevel = config.Start;
            endLevel = config.End;
            this.duration = config.Duration;
            Expires = DateTime.UtcNow + TimeSpan.FromMilliseconds(duration);
        }

        public double GetIntensityAtTime(DateTime time)
        {
            if (Expires < time)
            {
                throw new PatternExpiredException();
            }
            double progress = 1.0 - ((Expires.Ticks - time.Ticks) / ((double)duration * 10000));
            return (endLevel - startLevel) * progress + startLevel;
        }

        public static PatternConfig GetDefaultConfiguration()
        {
            return new RampPatternConfig();
        }
    }
    [Serializable]
    public class RampPatternConfig : PatternConfig
    {
        public override string Type { get; } = "Ramp";
        public double Start { get; set; } = 0;
        public double End { get; set; } = 1;
    }
}
