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
            Console.WriteLine($"Refined: '{sut.Value}'");

            var fn = (FullName)"Ian Jacob Tremblay";
            Console.WriteLine($"'{fn.Value}'");
        }

        {
            var sut = X10.Create(2);
            Console.WriteLine($"X10.Create(2) : {sut.Value}");
        }

        {
            var sut = NonNegative.Create(5);
            Console.WriteLine($"StrictlyPositive sut = 1 : {sut.Value}");
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
                $"NonNullException.Create(new AggregateException(\"foobar\")) : {Environment.NewLine}\t{sut}{Environment.NewLine}\t{sut.Value}");
        }

        {
            var sut = ValidatedPerson.Create(new Person(" James Dean\t", 42));
            Console.WriteLine(sut.Value);
        }

        {
            var sut = (BasketballScore)(76, 41);
            Console.WriteLine(sut.Value);
        }

        {
            try
            {
                var sut = (BasketballScore)(76, -1);
                Console.WriteLine(sut.Value);
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
                Console.WriteLine(sut.Value);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine($"BasketballScore sut = (-2, -1) : {Environment.NewLine}{e.Message}");
            }
        }
    }
}
