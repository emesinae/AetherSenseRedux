using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherSenseRedux.Pattern
{
    internal interface IPatternType
    {
        public static readonly Dictionary<string, IPatternType> All = Util.Discovery.DefaultInstances<IPatternType>()
            .ToDictionary(x => x.Name);

        public abstract string Name { get; }
        public PatternConfig GetDefaultConfiguration();
        public PatternConfig DeserializeConfiguration(dynamic source);
        public IPattern Create(PatternConfig config);
        public void DrawSettings(PatternConfig config);
    }

    internal interface IPattern
    {
        DateTime Expires { get; set; }
        double GetIntensityAtTime(DateTime currTime);

        static PatternConfig GetDefaultConfiguration()
        {
            throw new NotImplementedException();
        }

    }
    [Serializable]
    public abstract class PatternConfig
    {
        public abstract string Type { get; }
        public long Duration { get; set; } = 1000;
    }
}