// Usage example

var json = """
           [   1, 9,8,1, ["hello", "worl
           """;
Console.WriteLine(json);
var repairedJson = JResin.Json.Repair(json);
Console.WriteLine(repairedJson);