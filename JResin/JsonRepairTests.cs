using System.Text.Json;

namespace JResin;

public static class JsonRepairTests
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = new();

    public static void RunAll()
    {
        Console.WriteLine("=== JResin Test Suite ===\n");

        RunExhaustiveTruncationTest();
        RunEdgeCaseTests();

        Console.WriteLine("\n=== Summary ===");
        Console.WriteLine($"Passed: {_passed}");
        Console.WriteLine($"Failed: {_failed}");

        if (_failures.Count > 0)
        {
            Console.WriteLine("\nFailures:");
            foreach (var failure in _failures.Take(20))
            {
                Console.WriteLine($"  {failure}");
            }
            if (_failures.Count > 20)
            {
                Console.WriteLine($"  ... and {_failures.Count - 20} more");
            }
        }
    }

    private static void RunExhaustiveTruncationTest()
    {
        Console.WriteLine("--- Exhaustive Truncation Test ---");

        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData.json");
        if (!File.Exists(testDataPath))
        {
            // Try relative path for development
            testDataPath = "TestData.json";
        }

        if (!File.Exists(testDataPath))
        {
            Console.WriteLine("ERROR: TestData.json not found");
            _failed++;
            return;
        }

        var json = File.ReadAllText(testDataPath);
        Console.WriteLine($"Testing truncation at all {json.Length} positions...");

        var localPassed = 0;
        var localFailed = 0;

        for (var i = 1; i <= json.Length; i++)
        {
            var truncated = json[..i];
            var repaired = Json.Repair(truncated);

            if (IsValidJson(repaired))
            {
                localPassed++;
            }
            else
            {
                localFailed++;
                if (_failures.Count < 100)
                {
                    _failures.Add($"Truncation at position {i}: '{Escape(truncated[^Math.Min(20, truncated.Length)..])}' -> '{Escape(repaired[^Math.Min(30, repaired.Length)..])}' (invalid JSON)");
                }
            }
        }

        Console.WriteLine($"  Positions tested: {json.Length}");
        Console.WriteLine($"  Passed: {localPassed}");
        Console.WriteLine($"  Failed: {localFailed}");

        _passed += localPassed;
        _failed += localFailed;
    }

    private static void RunEdgeCaseTests()
    {
        Console.WriteLine("\n--- Edge Case Tests ---");

        // Truncation before colon (the bug we fixed)
        AssertRepairValid("{\"key\"", "Truncation before colon");
        AssertRepairValid("{\"a\":1,\"b\"", "Truncation before colon after value");
        AssertRepairValid("{\"nested\":{\"inner\"", "Nested truncation before colon");

        // Truncation in middle of string
        AssertRepairValid("{\"key\":\"val", "Truncation mid-string value");
        AssertRepairValid("{\"ke", "Truncation mid-string key");
        AssertRepairValid("{\"key\":\"hello\\", "Truncation after backslash in string");
        AssertRepairValid("{\"key\":\"hello\\u00", "Truncation mid-unicode escape");

        // Truncation in middle of literals
        AssertRepairValid("{\"key\":tru", "Truncation mid-true");
        AssertRepairValid("{\"key\":fal", "Truncation mid-false");
        AssertRepairValid("{\"key\":nul", "Truncation mid-null");

        // Truncation in middle of numbers
        AssertRepairValid("{\"key\":123", "Truncation mid-integer");
        AssertRepairValid("{\"key\":-", "Truncation after negative sign");
        AssertRepairValid("{\"key\":12.", "Truncation after decimal point");
        AssertRepairValid("{\"key\":1.5e", "Truncation mid-exponent");
        AssertRepairValid("{\"key\":1.5e-", "Truncation after exponent sign");

        // Array edge cases
        AssertRepairValid("[", "Single opening bracket");
        AssertRepairValid("[1,2,", "Truncation after comma in array");
        AssertRepairValid("[1,[2,[3,", "Nested array truncation");
        AssertRepairValid("[[[[", "Multiple opening brackets");

        // Object edge cases
        AssertRepairValid("{", "Single opening brace");
        AssertRepairValid("{\"a\":{\"b\":{", "Nested object truncation");
        AssertRepairValid("{\"a\":1,", "Truncation after comma in object");

        // Mixed structures
        AssertRepairValid("{\"arr\":[1,{\"x\":", "Mixed object/array truncation");
        AssertRepairValid("[{\"a\":1},{\"b\":", "Array of objects truncation");

        // Whitespace handling
        AssertRepairValid("{  \"key\"  ", "Truncation with whitespace before colon");
        AssertRepairValid("{\"key\"  :  ", "Truncation with whitespace after colon");

        // Empty and special cases
        AssertRepairValid("", "Empty string");
        AssertRepairValid("   ", "Whitespace only");
        AssertRepairValid("{\"\":\"\"", "Empty key and value");

        // Already valid JSON should remain valid
        AssertRepairValid("{\"key\":\"value\"}", "Already valid object");
        AssertRepairValid("[1,2,3]", "Already valid array");
        AssertRepairValid("{\"a\":{\"b\":[1,2,3]}}", "Already valid nested");

        // Escaped characters
        AssertRepairValid("{\"key\":\"line1\\nline2", "Truncation after newline escape");
        AssertRepairValid("{\"key\":\"tab\\there", "Truncation after tab escape");
        AssertRepairValid("{\"key\":\"quote\\\"in", "Truncation after quote escape");
    }

    private static void AssertRepairValid(string input, string testName)
    {
        var repaired = Json.Repair(input);
        var isValid = string.IsNullOrEmpty(repaired) || IsValidJson(repaired);

        if (isValid)
        {
            Console.WriteLine($"  PASS: {testName}");
            _passed++;
        }
        else
        {
            Console.WriteLine($"  FAIL: {testName}");
            Console.WriteLine($"        Input:    '{Escape(input)}'");
            Console.WriteLine($"        Repaired: '{Escape(repaired)}'");
            _failures.Add($"{testName}: '{Escape(input)}' -> '{Escape(repaired)}'");
            _failed++;
        }
    }

    private static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Escape(string s)
    {
        return s.Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}
