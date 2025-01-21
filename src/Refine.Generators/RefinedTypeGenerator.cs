using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Refine.Generators.Internal;

namespace Refine.Generators;

[Generator]
public class RefinedTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Look for class/record candidates with RefinedTypeAttribute
        var targets = context.SyntaxProvider
            .CreateSyntaxProvider(
                (syntaxNode, _) => IsCandidateForGeneration(syntaxNode),
                (ctx, _) => GetWrapperType(ctx))
            .Where(item => item != null);

        var compilationAndWrappers = context.CompilationProvider.Combine(targets.Collect());

        context.RegisterSourceOutput(compilationAndWrappers, (ctx, source) =>
        {
            // Emit a diagnostic if no targets were found
            if (!source.Right.Any())
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "GEN001",
                        "No Targets Found",
                        "The source generator could not find any valid targets to process.",
                        "SourceGenerator",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    Location.None));

                return;
            }

            GenerateWrapperCode(ctx, source.Right!);
        });
    }

    private static bool IsCandidateForGeneration(SyntaxNode syntaxNode)
    {
        return syntaxNode is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0;
    }

    private static MethodOptions GetMethodOptions(SemanticModel semanticModel, AttributeSyntax attribute)
    {
        if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count < 2)
        {
            return MethodOptions.Default;
        }
        
        var methodOptionsArgument = attribute.ArgumentList.Arguments[1];

        var constantValue = semanticModel.GetConstantValue(methodOptionsArgument.Expression);
        if (constantValue.HasValue)
        {
            if (constantValue.Value is ushort ushortValue)
            {
                return (MethodOptions)ushortValue;
            }
            else if (constantValue.Value is int intValue)
            {
                return (MethodOptions)intValue;
            }
        }

        // Fallback: Use SymbolInfo to resolve enum members manually
        var symbolInfo = semanticModel.GetSymbolInfo(methodOptionsArgument.Expression);
        if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
        {
            // Ensure the field belongs to the MethodOptions enum
            if (fieldSymbol.ContainingType?.Name == nameof(MethodOptions) &&
                fieldSymbol.ContainingType.TypeKind == TypeKind.Enum)
            {
                // Resolve the enum value manually by matching the field name
                if (Enum.TryParse(fieldSymbol.Name, out MethodOptions resolvedValue))
                {
                    return resolvedValue;
                }
            }
        }
            
        throw new InvalidOperationException("Could not resolve MethodOptions");
    }
        
    private static readonly Dictionary<string, string> TypeAliases = new()
    {
        { "string", "System.String" },
        { "int", "System.Int32" },
        { "bool", "System.Boolean" },
        { "byte", "System.Byte" },
        { "sbyte", "System.SByte" },
        { "short", "System.Int16" },
        { "ushort", "System.UInt16" },
        { "uint", "System.UInt32" },
        { "long", "System.Int64" },
        { "ulong", "System.UInt64" },
        { "float", "System.Single" },
        { "double", "System.Double" },
        { "decimal", "System.Decimal" },
        { "char", "System.Char" },
        { "object", "System.Object" }
    };

    public static Type? GetRuntimeType(ISymbol targetTypeSymbol)
    {
        // Generate the fully qualified name
        string fullyQualifiedTypeName = targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Remove the global:: prefix if it exists
        if (fullyQualifiedTypeName.StartsWith("global::"))
        {
            fullyQualifiedTypeName = fullyQualifiedTypeName.Substring(8); // Remove "global::"
        }
        
        // Handle aliases like "string" -> "System.String"
        if (TypeAliases.TryGetValue(fullyQualifiedTypeName, out var aliasType))
        {
            fullyQualifiedTypeName = aliasType;
        }
    
        // First: Try to resolve using Type.GetType
        Type? targetType = Type.GetType(fullyQualifiedTypeName);
        if (targetType != null)
        {
            return targetType; 
        }
        
        // Second: Scan all loaded assemblies for the type
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            targetType = assembly.GetType(fullyQualifiedTypeName);
            if (targetType != null)
            {
                return targetType;
            }
        }


        return null;
    }
        
    public enum ComparisonOperators
    {
        op_Equality,
        op_Inequality,
        op_LessThan,
        op_GreaterThan,
        op_LessThanOrEqual,
        op_GreaterThanOrEqual
    };
        
    private static readonly ComparisonOperators[] _operators = 
        Enum.GetValues(typeof(ComparisonOperators))
            .Cast<ComparisonOperators>()
            .ToArray();
    
    public static bool IsPrimitiveType(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType switch
        {
            SpecialType.System_Boolean => true,    // bool
            SpecialType.System_Byte => true,       // byte
            SpecialType.System_SByte => true,      // sbyte
            SpecialType.System_Int16 => true,      // short
            SpecialType.System_UInt16 => true,     // ushort
            SpecialType.System_Int32 => true,      // int
            SpecialType.System_UInt32 => true,     // uint
            SpecialType.System_Int64 => true,      // long
            SpecialType.System_UInt64 => true,     // ulong
            SpecialType.System_Single => true,     // float
            SpecialType.System_Double => true,     // double
            SpecialType.System_Char => true,       // char
            SpecialType.System_String => true,     // string
            SpecialType.System_Decimal => true,    // decimal
            _ => false 
        };
    }
        
    public static Dictionary<ComparisonOperators, bool> DetermineOperators(ITypeSymbol targetTypeSymbol)
    {

        
        if (IsPrimitiveType(targetTypeSymbol))
        {
            return _operators.ToDictionary(op => op, op => true);
        }

        
        var methods = targetTypeSymbol
            .GetMembers() // Get all members of the type
            .OfType<IMethodSymbol>() // Filter only methods
            .Where(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public);

        return _operators
            .ToDictionary(op => op, op => methods.Any(method => method.Name == op.ToString()));
    }
    
    private static WrapperTypeInfo? GetWrapperType(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Ensure the class has the `partial` modifier.
        if (!classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;
            
        var expectedAttributeSymbol =
            context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(RefinedTypeAttribute).FullName!);

        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!(context.SemanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol attributeSymbol))
                    continue;

                if (attribute.ArgumentList == null)
                    continue;

                if (SymbolEqualityComparer.Default.Equals(attributeSymbol.ContainingType, expectedAttributeSymbol))
                {
                    if (attribute.ArgumentList?.Arguments == null) continue;
                    ExpressionSyntax typeExpression = attribute.ArgumentList.Arguments[0].Expression;
                    MethodOptions options = GetMethodOptions(context.SemanticModel, attribute);

                    
                    if (typeExpression is TypeOfExpressionSyntax typeofExpression)
                    {
                        // Resolve the type inside typeof(...)
                        ITypeSymbol? targetTypeSymbol = context.SemanticModel.GetTypeInfo(typeofExpression.Type).Type;
                        if (targetTypeSymbol == null) continue;
                            
                        ITypeSymbol? classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as ITypeSymbol;
                        if (classSymbol == null) continue;
                            
                        List<IMethodSymbol> methods = classSymbol.GetMembers().OfType<IMethodSymbol>().ToList();
                        string classNamespace = GetNamespace(classDeclaration);
                        string targetNamespace = targetTypeSymbol.ContainingNamespace.ToDisplayString();

                        string fullyQualifiedTypeName = targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        //Type? targetType = Type.GetType(fullyQualifiedTypeName);
                        

                        //Type? targetType = GetRuntimeType(targetTypeSymbol);
                        //if (targetType == null) 
                        //    throw new InvalidOperationException($"{fullyQualifiedTypeName} - {targetType?.Name}");

                        var compilation = context.SemanticModel.Compilation;

                        // Get the symbol of the interface type
                        var interfaceSymbol = compilation.GetTypeByMetadataName(typeof(IComparable<>)?.FullName);
                        if (interfaceSymbol == null)
                            throw new ArgumentException($"The provided interface '{typeof(IComparable<>)?.FullName}' could not be found in the compilation.");
                        
                        return new WrapperTypeInfo
                        {
                            ClassName = classDeclaration.Identifier.Text,
                            ClassNamespace = classNamespace,
                            Options = options,
   
                            HasTryValidate = HasTryValidate(methods, targetTypeSymbol),
                            HasValidate = HasValidate(methods, targetTypeSymbol),
                            HasTransform = HasTransform(methods, targetTypeSymbol),

                            TargetTypeSymbol = targetTypeSymbol,
                            TargetNamespace = targetNamespace,
                            TargetHasOperators = DetermineOperators(targetTypeSymbol),
                            TargetHasComparable = targetTypeSymbol.ImplementsInterface<IComparable>(compilation),
                            TargetHasComparableT = targetTypeSymbol.ImplementsGenericOfType(typeof(IComparable<>), compilation),
                            TargetHasEquatableT = targetTypeSymbol.ImplementsGenericOfType(typeof(IEquatable<>), compilation),
                        };
                    }
                }
            }
        }

        return null;
    }

    private static bool ImplementsEquatable(ITypeSymbol typeSymbol)
    {
        return typeSymbol
            .AllInterfaces
            .Any(namedType =>
            {
                return namedType.Name == "IEquatable" &&
                       namedType.TypeArguments.Any(t => t.Equals(typeSymbol, SymbolEqualityComparer.Default));
            });
    }


    private static bool HasMethod(IEnumerable<IMethodSymbol> methods, string methodName, ITypeSymbol parameterType,
        SpecialType? specialReturnType = SpecialType.None, ITypeSymbol? specificReturnType = null)
    {
        return methods.Any(method => method.Name == methodName &&
                                     method.Parameters.Length == 1 &&
                                     SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, parameterType) &&
                                     (
                                         method.ReturnType.SpecialType == specialReturnType ||
                                         method.ReturnType.Equals(specificReturnType, SymbolEqualityComparer.Default)
                                     )
        );
    }

    private static bool HasTryValidate(IEnumerable<IMethodSymbol> methods, ITypeSymbol valueSymbol)
    {
        return HasMethod(methods, "TryValidate", valueSymbol, SpecialType.System_Boolean);
    }

    private static bool HasValidate(IEnumerable<IMethodSymbol> methods, ITypeSymbol valueSymbol)
    {
        return HasMethod(methods, "Validate", valueSymbol, SpecialType.System_Void);
    }

    private static bool HasTransform(IEnumerable<IMethodSymbol> methods, ITypeSymbol valueSymbol)
    {
        return HasMethod(methods, "Transform", valueSymbol, specificReturnType: valueSymbol);
    }

    private static void GenerateWrapperCode(SourceProductionContext context, ImmutableArray<WrapperTypeInfo> wrappers)
    {
        foreach (var wrapper in wrappers)
        {
            var sourceCode = GenerateWrapperSource(wrapper);

            context.AddSource($"{wrapper.ClassName}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }
    }

    private static string GenerateWrapperSource(WrapperTypeInfo info)
    {
        string targetType = info.TargetTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (string.IsNullOrEmpty(targetType))
            throw new InvalidOperationException("Target type cannot be resolved.");

        string usingTargetNamespace =
            info.TargetNamespace != info.ClassNamespace && !string.IsNullOrEmpty(info.TargetNamespace)
                ? $"using {info.TargetNamespace};"
                : "";
            
        string equalsLogic = info.TargetHasEquatableT
            ? "Value.Equals(other.Value)"
            : $"EqualityComparer<{targetType}>.Default.Equals(Value, other.Value)";

        string validateCall = info.HasValidate || info.HasTryValidate
            ? "Validate(value);"
            : "";

        string transformCall = info.HasTransform
            ? "value = Transform(value);"
            : "";

        string validateMethod = "";
        if (info is { HasTryValidate: true, HasValidate: false })
            validateMethod = $@"
       private static void Validate({targetType} value)
       {{
           if (!TryValidate(value))
               throw new ArgumentException(""Validation failed for the provided value."");
       }}
";

        var tryCreateMethod = info.HasTryValidate
            ? $@"
        public static bool TryCreate({targetType} value, out {info.ClassName}? newValue)
        {{   
            {transformCall}
            if (TryValidate(value))
            {{
                newValue = new {info.ClassName}(value);
                return true;
            }}
            newValue = null;
            return false;
        }}
    "
            : "";

        var infoComment = $@"
/*
{info}

operators:
{info.TargetHasOperators.Aggregate(new StringBuilder(), (sb, kv) => sb.AppendLine($" - {kv.Key}: {kv.Value}"), sb => sb.ToString())}
*/
";

        return @$"
// <auto-generated/>
#nullable enable
{infoComment}
{usingTargetNamespace}
namespace {info.ClassNamespace}
{{
    public partial class {info.ClassName}
    {{
        public {targetType} Value {{ get; private init; }}
        private {info.ClassName}({targetType} value) => Value = value;
        
        public static {info.ClassName} Create({targetType} value)
        {{
            {transformCall}
            {validateCall}
            return new {info.ClassName}(value);
        }}
        {validateMethod}
        {tryCreateMethod}
        
        public override bool Equals(object? obj) => obj is {info.ClassName} other && {equalsLogic};
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==({info.ClassName}? wrapper, {targetType}? value) => wrapper?.Value.Equals(value) ?? value is null;
        public static bool operator !=({info.ClassName}? wrapper, {targetType}? value) => !(wrapper == value);
        public static bool operator ==({targetType}? value, {info.ClassName}? wrapper) => wrapper == value;
        public static bool operator !=({targetType}? value, {info.ClassName}? wrapper) => !(wrapper == value);
        public static explicit operator {targetType}({info.ClassName} wrapper) => wrapper.Value;
        public static explicit operator {info.ClassName}({targetType} value) => Create(value);
    }}
}}
";
    }

    private static string GetNamespace(SyntaxNode node)
    {
        var namespaceDeclaration = node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        if (namespaceDeclaration == null)
            return "GlobalNamespace";

        var namespaceParts = new Stack<string>();

        while (namespaceDeclaration != null)
        {
            namespaceParts.Push(namespaceDeclaration.Name.ToString());
            namespaceDeclaration = namespaceDeclaration.Parent as BaseNamespaceDeclarationSyntax;
        }

        return string.Join(".", namespaceParts);
    }
}

internal record WrapperTypeInfo
{
    public  string ClassName { get; set; } = null!;
    public  string ClassNamespace { get; set; } = null!;
    public  ITypeSymbol TargetTypeSymbol { get; set; } = null!;
    public  string TargetNamespace { get; set; } = null!;
    public  bool HasTryValidate { get; set; }
    public  bool HasValidate { get; set; }
    public  bool HasTransform { get; set; }
    public  MethodOptions Options { get; set; } 
    public  Dictionary<RefinedTypeGenerator.ComparisonOperators, bool> TargetHasOperators { get; set; } = null!;
    public  bool TargetHasComparable { get; set; }
    public  bool TargetHasComparableT { get; set; }
    public  bool TargetHasEquatableT { get; set; }
}