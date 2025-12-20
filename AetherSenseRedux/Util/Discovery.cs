using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AetherSenseRedux.Util;

public interface Discovery
{
    static HashSet<string> AssemblyBlacklist =
    [
        "HelixToolkit.SharpDX.Core",
    ];

    static IEnumerable<Type> ImplementersOf(Type iintf)
    {
        return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x =>
        {
            string name = x.GetName()?.Name ?? "<anonymous assembly>";
            if (AssemblyBlacklist.Contains(name)) return [];
            try
            {
                return x.GetTypes();
            }
            catch (Exception exc)
            {
                Service.PluginLog.Warning($"Exception while getting types from {name}: {exc.Message}; blacklisting this assembly. Consider blacklisting it by default!");
                AssemblyBlacklist.Add(name);
                return [];
            }
        }).Where(x => iintf.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract);
    }

    static IEnumerable<TYPE> DefaultInstances<TYPE>()
    {
        return ImplementersOf(typeof(TYPE)).Select(x => (TYPE?)Activator.CreateInstance(x)).Where(x => x != null).Select(x => x!);
    }
}
