using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace TheShop.Application.Tests;

internal static class AssemblySetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Apply production defaults before any test constructs a validator.
        new ServiceCollection().AddApplication();
    }
}
