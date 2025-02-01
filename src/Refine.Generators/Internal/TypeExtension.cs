using Microsoft.CodeAnalysis;

namespace Refine.Generators.Internal;

internal static class TypeExtension
{
    public static bool ImplementsInterface<TInterface>(this Type type) where TInterface : class =>
        type.ImplementsInterface(typeof(TInterface));

    public static bool ImplementsGenericOfType(this Type type, Type openGenericInterface)
    {
        if (!openGenericInterface.IsInterface)
            throw new ArgumentException("The provided type must be an interface.", nameof(openGenericInterface));

        if (!openGenericInterface.IsGenericTypeDefinition)
            throw new ArgumentException("The provided type must be an open generic interface.", nameof(openGenericInterface));
            
        return ImplementsInterface(type, openGenericInterface.MakeGenericType(type));
    }

    public static bool ImplementsInterface(this Type type, Type interfaceType)
    {
        if (!interfaceType.IsInterface)
            throw new ArgumentException("The provided type must be an interface.", nameof(interfaceType));
        
        if (interfaceType.IsGenericTypeDefinition)
        {
            return type.GetInterfaces()
                .Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == interfaceType);
        }
        return type.GetInterfaces().Contains(interfaceType);
    }
    
    public static bool ImplementsInterface<TInterface>(this ITypeSymbol typeSymbol, Compilation compilation) where TInterface : class
    {
        // Get the symbol of the interface type
        var interfaceSymbol = compilation.GetTypeByMetadataName(typeof(TInterface).FullName);
        if (interfaceSymbol == null)
            throw new ArgumentException($"The provided interface '{typeof(TInterface).FullName}' could not be found in the compilation.");

        return typeSymbol.ImplementsInterface(interfaceSymbol);
    }

    public static bool ImplementsGenericOfType(this ITypeSymbol typeSymbol, Type openGenericInterface, Compilation compilation)
    {
        
        if (!openGenericInterface.IsInterface)
            throw new ArgumentException("The provided type must be an interface.", nameof(openGenericInterface));

        if (!openGenericInterface.IsGenericTypeDefinition)
            throw new ArgumentException("The provided type must be an open generic interface.", nameof(openGenericInterface));


        // Resolve the Roslyn symbol for the open generic interface
        var openGenericInterfaceSymbol = compilation.GetTypeByMetadataName(openGenericInterface.FullName);
        if (openGenericInterfaceSymbol is not INamedTypeSymbol namedOpenGenericInterfaceSymbol)
        {
            throw new ArgumentException($"Could not resolve the type '{openGenericInterface.FullName}' in the current compilation.");
        }
        
        return typeSymbol.AllInterfaces.Any(x =>
            x.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(x.ConstructedFrom, namedOpenGenericInterfaceSymbol));
    }

    public static bool ImplementsInterface(this ITypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol)
    {
        if (interfaceSymbol.TypeKind != TypeKind.Interface)
            throw new ArgumentException("The provided type must be an interface.", nameof(interfaceSymbol));

        // Handle generic interface definitions
        if (interfaceSymbol.IsGenericType)
        {
            return typeSymbol.AllInterfaces.Any(x =>
                x.IsGenericType && SymbolEqualityComparer.Default.Equals(x.ConstructedFrom, interfaceSymbol));
        }

        // Handle non-generic interface types
        return typeSymbol.AllInterfaces.Contains(interfaceSymbol, SymbolEqualityComparer.Default);
    }
}