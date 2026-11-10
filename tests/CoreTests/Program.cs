using System;
using System.Collections.Generic;

namespace CasualtiesCharacters.Tests
{
    public sealed class TestSuite
    {
        readonly List<KeyValuePair<string, Action>> tests = new List<KeyValuePair<string, Action>>();

        public void Add(string name, Action body)
        {
            tests.Add(new KeyValuePair<string, Action>(name, body));
        }

        public int RunAll()
        {
            int failed = 0;
            foreach (KeyValuePair<string, Action> test in tests)
            {
                try { test.Value(); Console.WriteLine("ok    " + test.Key); }
                catch (Exception exception) { failed++; Console.WriteLine("FAIL  " + test.Key + ": " + exception.Message); }
            }
            Console.WriteLine((tests.Count - failed) + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }

    public static class Check
    {
        public static void True(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(message + ": expected " + expected + " got " + actual);
        }
        public static void Near(float expected, float actual, string message)
        {
            if (Math.Abs(expected - actual) > 1e-4f) throw new Exception(message + ": expected " + expected + " got " + actual);
        }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            TestSuite suite = new TestSuite();
            ParserTests.Register(suite);
            RuleTests.Register(suite);
            IdAllocatorTests.Register(suite);
            SkinMatcherTests.Register(suite);
            TraitMathTests.Register(suite);
            CatalogTests.Register(suite);
            return suite.RunAll();
        }
    }
}
