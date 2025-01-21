using System;

namespace Refine;

[Flags]
public enum MethodOptions : ushort
{
    None = 0,

    /// <summary>
    /// Implements ToString() as pass-through to Value.
    /// </summary>
    ToString = 1,

    /// <summary>
    /// Implements Equals(object?) and GetHashCode() as pass-through to Value.
    /// </summary>
    Equals = 1 << 1,

    /// <summary>
    /// Implements IEquatable&lt;&gt; as pass-through to Value if the TargetType implements it.
    /// </summary>
    Equatable = 1 << 2,

    /// <summary>
    /// Implements operators as pass-through to Value if the TargetType implements them: == and != 
    /// </summary>
    EqualityOperators = 1 << 3,

    /// <summary>
    /// Implements IComparable and IComparable&lt;&gt; as pass-through to Value if the TargetType implements them.
    /// </summary>
    Comparable = 1 << 4,

    /// <summary>
    /// Implements operators as pass-through to Value if the TargetType implements them: &lt; , &gt; , &lt;= and &gt;=
    /// </summary>
    ComparisonOperators = 1 << 5,

    /// <summary>
    /// Implements explicit cast operators:
    /// - static explicit operator TTarget(TRefined wrapper)
    /// - static explicit operator TRefined(TTarget wrapper)
    /// </summary>
    ExplicitConversion = 1 << 6,

    /// <summary>
    /// Implements implicit cast operators:
    /// - static implicit operator TTarget(TRefined wrapper)
    /// - static implicit operator TRefined(TTarget wrapper)
    /// </summary>
    ImplicitConversion = 1 << 7,

    Default = ToString | Equals | Equatable | EqualityOperators | Comparable | ComparisonOperators
}

[AttributeUsage(AttributeTargets.Class)]
public class RefinedTypeAttribute : Attribute
{
    public RefinedTypeAttribute(Type targetType, MethodOptions methodOptions = MethodOptions.Default)
    {
        TargetType = targetType;
        MethodOptions = methodOptions;
    }

    public Type TargetType { get; }
    public MethodOptions MethodOptions { get; }
}
