using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherSenseRedux.Pattern
{
    internal class PatternFactory
    {
        public static IPattern GetPatternFromObject(PatternConfig settings)
        {
            return IPatternType.All[settings.Type].Create(settings);
        }

        public static PatternConfig GetDefaultsFromString(string name)
        {
            return IPatternType.All[name].GetDefaultConfiguration();
        }

        public static PatternConfig GetPatternConfigFromObject(dynamic o)
        {
            return IPatternType.All[(string)o.Type].DeserializeConfiguration(o);
        }
    }
}
