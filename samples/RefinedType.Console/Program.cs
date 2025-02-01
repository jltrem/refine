using System.Reflection;
using Refine;

namespace Sample;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(StringWrapper.Create("howdy").Value);

        {
            string raw = "\tJames T. Kirk ";
            Console.WriteLine($"Raw:     '{raw}'");
            var sut = FullName.Create(raw);
            Console.WriteLine($"Refined: '{sut}'");

            var fn = (FullName)"\tJohn Jacob Jingleheimer Schmidt\n";
            Console.WriteLine($"'{fn}'");
        }

        {
            var sut = X10.Create(2);
            Console.WriteLine($"X10.Create(2) : {sut}");
        }

        {
            var sut = NonNegative.Create(5);
            Console.WriteLine($"StrictlyPositive sut = 1 : {sut}");
        }

        {
            if (StrictlyPositive.TryCreate(0, out var sut))
                Console.WriteLine($"StrictlyPositive.TryCreate(0, out var sut) : {sut!.Value}");
            else
                Console.WriteLine("StrictlyPositive.TryCreate(0, out var sut) : false");
        }

        {
            try
            {
                Exception? nullException = null;
                NonNullException sut = (NonNullException)nullException;
            }
            catch (ArgumentException e)
            {
                Console.WriteLine($"NonNullException = null : {e.Message}");
            }
        }

        {
            var sut = NonNullException.Create(new AggregateException("foobar"));
            Console.WriteLine(
                $"NonNullException.Create(new AggregateException(\"foobar\")) : {Environment.NewLine}\t{sut}{Environment.NewLine}\t{sut}");
        }

        {
            var sut = ValidatedPerson.Create(new Person(" James Dean\t", 42));
            Console.WriteLine(sut);
        }

        {
            var sut = (BasketballScore)(76, 41);
            Console.WriteLine(sut);
        }

        {
            try
            {
                var sut = (BasketballScore)(76, -1);
                Console.WriteLine(sut);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine($"BasketballScore sut = (76, -1) : {Environment.NewLine}{e.Message}");
            }
        }

        {
            try
            {
                var sut = (BasketballScore)(-2, -1);
                Console.WriteLine(sut);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine($"BasketballScore sut = (-2, -1) : {Environment.NewLine}{e.Message}");
            }
        }
    }
}
