namespace Refine;


[AttributeUsage(AttributeTargets.Class)]
public class RefinedTypeAttribute : Attribute
{
    public RefinedTypeAttribute(Type targetType)
    {
        TargetType = targetType;
    }

    public Type TargetType { get; }
}
