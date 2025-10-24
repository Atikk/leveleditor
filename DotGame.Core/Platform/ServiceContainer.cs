using System;
using System.Collections.Concurrent;

namespace DotGame.Core.Platform
{
    // Very small service container used for application composition in lieu of a full DI framework.
    public static class ServiceContainer
    {
        private static readonly ConcurrentDictionary<Type, object> services = new();

        public static void RegisterSingleton<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            services[typeof(T)] = instance!;
        }

        public static T? Resolve<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var o))
                return (T?)o;
            return null;
        }
    }
}
