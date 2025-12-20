using Dalamud.Bindings.ImGui;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherSenseRedux.Pattern
{
    internal class SawPatternType : IPatternType
    {
        public string Name => "Saw";

        public PatternConfig GetDefaultConfiguration()
        {
            return new SawPatternConfig();
        }

        public PatternConfig DeserializeConfiguration(dynamic source)
        {
            return new SawPatternConfig()
            {
                Duration = (long)source.Duration,
                Start = (double)source.Start,
                End = (double)source.End,
                Duration1 = (long)source.Duration1,
            };
        }

        public IPattern Create(PatternConfig config)
        {
            if (config is SawPatternConfig spcfg) return new SawPattern(spcfg);
            throw new ArgumentException("config is not SawPatternConfig");
        }

        /// <summary>
        /// Draws the configuration interface for saw patterns
        /// </summary>
        /// <param name="pattern">A SawPatternConfig object containing the current configuration for the pattern.</param>
        public void DrawSettings(PatternConfig config)
        {
            if (config is SawPatternConfig pattern)
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
                int duration1 = (int)pattern.Duration1;
                if (ImGui.InputInt("Saw Duration (ms)", ref duration1))
                {
                    pattern.Duration1 = (long)duration1;
                }
            }
            else
            {
                ImGui.Text("Internal error: config is not SawPatternConfig");
            }
        }
    }

    internal class SawPattern : IPattern
    {
        public DateTime Expires { get; set; }
        private readonly double startLevel;
        private readonly double endLevel;
        private readonly long duration;
        private readonly long duration1;


        public SawPattern(SawPatternConfig config)
        {
            startLevel = config.Start;
            endLevel = config.End;
            this.duration = config.Duration;
            this.duration1 = config.Duration1;
            Expires = DateTime.UtcNow + TimeSpan.FromMilliseconds(duration);
        }

        public double GetIntensityAtTime(DateTime time)
        {
            if (Expires < time)
            {
                throw new PatternExpiredException();
            }
            double progress = 1.0 - ((Expires.Ticks - time.Ticks) / ((double)duration1 * 10000) % 1.0); // we only want the floating point remainder here
            return (endLevel - startLevel) * progress + startLevel;
        }

        public static PatternConfig GetDefaultConfiguration()
        {
            return new SawPatternConfig();
        }
    }
    [Serializable]
    public class SawPatternConfig : PatternConfig
    {
        public override string Type { get; } = "Saw";
        public double Start { get; set; } = 0;
        public double End { get; set; } = 1;
        public long Duration1 { get; set; } = 500;
    }
}
