using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

public class ScriptsDummy
{
    // from http://www.dotnetperls.com/word-count

    public static int CountWords1(string s)
    {
        MatchCollection collection = Regex.Matches(s, @"[\S]+");
        return collection.Count;
    }

    public static int CountWords2(string s)
    {
        int c = 0;
        for (int i = 1; i < s.Length; i++)
        {
            if (char.IsWhiteSpace(s[i - 1]) == true)
            {
                if (char.IsLetterOrDigit(s[i]) == true ||
                    char.IsPunctuation(s[i]))
                {
                    c++;
                }
            }
        }
        if (s.Length > 2)
        {
            c++;
        }
        return c;
    }

    // from http://www.dotnetperls.com/array-optimization

    const int _max = 100000000;

    public static void ArrayOptimization()
    {
        int[] array = new int[12];
        Method1(array);
        Method2(array);

        var s1 = Stopwatch.StartNew();
        for (int i = 0; i < _max; i++)
        {
            Method1(array);
        }
        s1.Stop();
        var s2 = Stopwatch.StartNew();
        for (int i = 0; i < _max; i++)
        {
            Method2(array);
        }
        s2.Stop();
        Console.WriteLine(((double)(s1.Elapsed.TotalMilliseconds * 1000 * 1000) /
            _max).ToString("0.00 ns"));
        Console.WriteLine(((double)(s2.Elapsed.TotalMilliseconds * 1000 * 1000) /
            _max).ToString("0.00 ns"));
        Console.Read();
    }

    static void Method1(int[] array)
    {
        // Initialize each element in for-loop.
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = 1;
        }
    }

    static void Method2(int[] array)
    {
        // Initialize each element in separate statement with no enclosing loop.
        array[0] = 1;
        array[1] = 1;
        array[2] = 1;
        array[3] = 1;
        array[4] = 1;
        array[5] = 1;
        array[6] = 1;
        array[7] = 1;
        array[8] = 1;
        array[9] = 1;
        array[10] = 1;
        array[11] = 1;
    }

    // from http://www.dotnetperls.com/dictionary

    public static void Dictionary1()
    {
        // Example Dictionary again.
        Dictionary<string, int> d = new Dictionary<string, int>()
        {
            {"cat", 2},
            {"dog", 1},
            {"llama", 0},
            {"iguana", -1}
        };
        // Loop over pairs with foreach.
        foreach (KeyValuePair<string, int> pair in d)
        {
            Console.WriteLine("{0}, {1}",
            pair.Key,
            pair.Value);
        }
        // Use var keyword to enumerate dictionary.
        foreach (var pair in d)
        {
            Console.WriteLine("{0}, {1}",
            pair.Key,
            pair.Value);
        }
    }

    public static void Dictionary2()
    {
        Dictionary<string, int> d = new Dictionary<string, int>()
        {
            {"cat", 2},
            {"dog", 1},
            {"llama", 0},
            {"iguana", -1}
        };
        // Store keys in a List
        List<string> list = new List<string>(d.Keys);
        // Loop through list
        foreach (string k in list)
        {
            Console.WriteLine("{0}, {1}",
                k,
                d[k]);
        }
    }

    // from: http://www.dotnetperls.com/format

    public static void Format1()
    {
        // Declare three variables.
        // ... The values they have are not important.
        string value1 = "Dot Net Perls";
        int value2 = 10000;
        DateTime value3 = new DateTime(2015, 11, 1);
        // Use string.Format method with four arguments.
        // ... The first argument is the formatting string.
        // ... It specifies how the next arguments are formatted.
        string result = string.Format("{0}: {1:0.0} - {2:yyyy}",
            value1,
            value2,
            value3);
        // Write the result.
        Console.WriteLine(result);
    }

    public static void Format2()
    {
        // Format a ratio as a percentage string.
        // ... You must specify the percentage symbol.
        // ... It will multiply the value by 100.
        double ratio = 0.73;
        string result = string.Format("string = {0:0.0%}",
            ratio);
        Console.WriteLine(result);
    }

    public static void Format3()
    {
        // The constant formatting string.
        // ... It specifies the padding.
        // ... A negative number means to left-align.
        // ... A positive number means to right-align.
        const string format = "{0,-10} {1,10}";
        // Construct the strings.
        string line1 = string.Format(format,
            100,
            5);
        string line2 = string.Format(format,
            "Carrot",
            "Giraffe");
        // Write the formatted strings.
        Console.WriteLine(line1);
        Console.WriteLine(line2);
    }

    public static void Format4()
    {
        int value1 = 10995;

        // Write number in hex format.
        Console.WriteLine("{0:x}", value1);
        Console.WriteLine("{0:x8}", value1);

        Console.WriteLine("{0:X}", value1);
        Console.WriteLine("{0:X8}", value1);

        // Convert to hex.
        string hex = value1.ToString("X8");

        // Convert from hex to integer.
        int value2 = int.Parse(hex, NumberStyles.AllowHexSpecifier);
        Console.WriteLine(value1 == value2);
    }
}
