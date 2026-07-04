using System.Runtime.CompilerServices;
using System.Reflection;
using UnityModBase.HLogSpace;
using UnityModBase.HUserSpace;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace UnityModBase.Test
{
    public static class TestAssemblySettings
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            var service = new UserService("UnityModBase.Test");
            typeof(UserService)
                .GetProperty(nameof(UserService.LogDatabase), BindingFlags.Instance | BindingFlags.Public)
                .SetValue(service, new LogDatabase(null));

            var context = new UserContext("UnityModBase.Test", "UnityModBase.Test")
            {
                Service = service
            };

            typeof(global::UnityModBase.UnityModBase).Assembly
                .GetType("UnityModBase.BSpace.BService")
                .GetProperty("Context", BindingFlags.Static | BindingFlags.Public)
                .SetValue(null, context);

            AppDomain.CurrentDomain.ProcessExit += (_, _) => global::UnityModBase.UnityModBase.Dispose();
        }
    }
}
