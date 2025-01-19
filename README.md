# Refine
Refine is a source generator to help you design data types. 

- [avoid](https://refactoring.guru/smells/primitive-obsession) [primitive](https://wiki.c2.com/?PrimitiveObsession) [obsession](https://blog.ploeh.dk/2015/01/19/from-primitive-obsession-to-domain-modelling/)
- "make illegal states unrepresentable" - [Yaron Minsky](https://youtu.be/-J8YyfrSwTk?si=3OBX5ANRFyi6TRGs)

# Quick Start

### 1. Add the NuGet

```bash
dotnet add package Refine 
dotnet add package Refine.Generators
```

### 2. Decorate Your Class

Add the `RefinedTypeAttribute` to a class and mark it as `partial`.

```csharp
using Refine;

[RefinedType(typeof(string))]
public partial class FullName;
```

### 3. Transform and/or Validate

```csharp
using Refine;

[RefinedType(typeof(string))]
public partial class FullName
{
    private static string Transform(string value) =>
        value?.Trim() ?? "";
        
    private static bool TryValidate(string value) =>
        !string.IsNullOrEmpty(value);
}
```

### 4. Instantiate

```csharp
string raw = "\tJames T. Kirk ";
Console.WriteLine($"Raw:     '{raw}'");

var refined = FullName.Create(raw);
Console.WriteLine($"Refined: '{refined.Value}'");
```
```
Raw:     '      James T. Kirk '
Refined: 'James T. Kirk'
```

### 5. Invalid States Are Unrepresentable

```csharp
var bad = FullName.Create(Environment.NewLine);
```
throws `ArgumentException: Validation failed for the provided value.`

# Deeper Dive

1. Look at the [samples](./samples)
2. Inspect the generated code
   - add `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` to your csproj
   - generated code will be under `obj/Debug/net8.0/generated/`
3. More coming...