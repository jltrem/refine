using System.Collections;
using Refine;

namespace Sample;

[RefinedType(typeof(string), MethodOptions.ToString | MethodOptions.ComparisonOperators | MethodOptions.ExplicitConversion)]
public partial class FullName
{
    private static string Transform(string value) =>
        value?.Trim() ?? "";
        
    private static bool TryValidate(string value) =>
        !string.IsNullOrEmpty(value);
}


[RefinedType(typeof(string))]
public partial class StringWrapper;

[RefinedType(typeof(int))]
public partial class X10
{
    private static int Transform(int value) =>
        value * 10;
}

[RefinedType(typeof(int))]
public partial class NonNegative
{
    private static void Validate(int value)
    {
        if (value < 0) throw new ArgumentException("Value cannot be negative.");
    }
}

[RefinedType(typeof(int))]
public partial class StrictlyPositive
{
    private static bool TryValidate(int value)
    {
        return value > 0;
    }
}

[RefinedType(typeof(Exception))]
public partial class NonNullException
{
    private static Exception Transform(Exception value) =>
        value != null ? new ApplicationException(value?.Message) : null;

    private static void Validate(Exception value)
    {
        ArgumentNullException.ThrowIfNull(value);
    }
}

public record Person(string FullName, int Age);

[RefinedType(typeof(Person))]
public partial class ValidatedPerson
{
    private static Person Transform(Person value) =>
        value with { FullName = value.FullName.Trim() };

    private static void Validate(Person value)
    {
        if (string.IsNullOrWhiteSpace(value.FullName))
            throw new ArgumentOutOfRangeException(nameof(value.FullName), "Name must be specified.");

        if (value.Age < 0)
            throw new ArgumentOutOfRangeException(nameof(value.Age), "Age must be non-negative.");
    }
}

[RefinedType(typeof((int Home, int Away)))]
public partial class BasketballScore
{
    private static void Validate((int Home, int Away) val)
    {
        string?[] validations =
        [
            Validate(nameof(val.Home), val.Home),
            Validate(nameof(val.Away), val.Away)
        ];

        var errors = validations.Where(e => e != null).ToArray();
        if (errors.Length <= 0) return;

        var fail = string.Join(Environment.NewLine, errors);
        throw new ArgumentException(fail);
    }

    private static string? Validate(string team, int score) =>
        score < 0 ? $"score must be non-negative: ({team} = {score})" : null;
}