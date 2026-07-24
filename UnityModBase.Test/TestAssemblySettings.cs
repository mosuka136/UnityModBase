using System.Runtime.CompilerServices;
using System.Reflection;
using UnityModBase.BSpace;
using UnityModBase.HLogSpace;
using UnityModBase.HTranslatorSpace;
using UnityModBase.HUserSpace;

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

            var context = new UserContext(
                "UnityModBase.Test",
                new Translator("UnityModBase.Test", "UnityModBase.Test"))
            {
                Service = service
            };

            typeof(BService)
                .GetProperty("Context", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(null, context);
        }
    }
}
