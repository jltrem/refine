using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Refine.Generators
{
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

        private static WrapperTypeInfo? GetWrapperType(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            // Ensure the class has the `partial` modifier.
            if (!classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                return null;
            
            var expectedAttributeSymbol =
                context.SemanticModel.Compilation.GetTypeByMetadataName("Refine.RefinedTypeAttribute");

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
                        var typeExpression = attribute.ArgumentList.Arguments[0].Expression;

                        if (typeExpression is TypeOfExpressionSyntax typeofExpression)
                        {
                            // Resolve the type inside typeof(...)
                            var typeSymbol = context.SemanticModel.GetTypeInfo(typeofExpression.Type).Type;
                            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as ITypeSymbol;
                            var methods = classSymbol?.GetMembers().OfType<IMethodSymbol>().ToList();

                            if (typeSymbol != null && methods != null)
                                return new WrapperTypeInfo
                                {
                                    ClassName = classDeclaration.Identifier.Text,
                                    Namespace = GetNamespace(classDeclaration),
                                    TargetType = typeSymbol,
                                    ImplementsEquatable = ImplementsEquatable(typeSymbol),
                                    HasTryValidate = HasTryValidate(methods, typeSymbol),
                                    HasValidate = HasValidate(methods, typeSymbol),
                                    HasTransform = HasTransform(methods, typeSymbol)
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
                //var targetTypeSymbol = (INamedTypeSymbol)wrapper.TargetType;
                //var isRecord = targetTypeSymbol.IsRecord;

                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "GEN002",
                        "dump",
                        $"{wrapper}",
                        "SourceGenerator",
                        DiagnosticSeverity.Warning,
                        true),
                    Location.None));
                var sourceCode = GenerateWrapperSource(wrapper);

                context.AddSource($"{wrapper.ClassName}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            }
        }

        private static string GenerateWrapperSource(WrapperTypeInfo info)
        {
            var targetType = info.TargetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (string.IsNullOrEmpty(targetType))
                throw new InvalidOperationException("Target type cannot be resolved.");

            var equalsLogic = info.ImplementsEquatable
                ? "Value.Equals(other.Value)"
                : $"EqualityComparer<{targetType}>.Default.Equals(Value, other.Value)";

            var validateCall = info.HasValidate || info.HasTryValidate
                ? "Validate(value);"
                : "";

            var transformCall = info.HasTransform
                ? "value = Transform(value);"
                : "";

            var validateMethod = "";
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

            return @$"
// <auto-generated/>
#nullable enable

namespace {info.Namespace}
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
        public static implicit operator {targetType}({info.ClassName} wrapper) => wrapper.Value;
        public static implicit operator {info.ClassName}({targetType} value) => Create(value);
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

    internal class WrapperTypeInfo
    {
        public string ClassName { get; set; } = null!;
        public string Namespace { get; set; } = null!;
        public ITypeSymbol TargetType { get; set; } = null!;
        public bool ImplementsEquatable { get; set; }
        public bool HasTryValidate { get; set; }
        public bool HasValidate { get; set; }
        public bool HasTransform { get; set; }
    }
}