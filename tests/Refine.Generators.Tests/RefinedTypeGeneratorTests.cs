using Refine.Generators.Tests.TestTypes;

namespace Refine.Generators.Tests;


//[RefinedType(typeof(SomeValue<int>))]
public partial class StrictlyPositiveInt
{
    private static bool TryValidate(SomeValue<int> some)
    {
        return some.Value > 0;
    }
}


public class RefinedTypeGeneratorTests
{
    [Fact]
    public void can_target_type_in_different_namespace_from_decorated_type()
    {
        //var sut = StrictlyPositiveInt.Create(new SomeValue<int>(1));
        //Assert.NotNull(sut);
        //Assert.Equal(1, sut.Value.Value);
    }
}