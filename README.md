# JResin

JResin is a C# library designed to repair incomplete or truncated JSON data. Whether you're dealing with interrupted network streams, partial responses from APIs, or corrupted files, JResin can help you reconstruct valid JSON structures without losing usable data.

## Why Use JResin?

JSON data often comes from unreliable sources like:

  - Network interruptions during data transfer.
  - Streaming APIs that send partial data over time.
  - Logging systems that may truncate JSON outputs.

JResin ensures that you can process and use the valid parts of JSON even if it’s incomplete. It is, however, a simplified solution and will not fix every possible JSON formatting issue.

## Installation

Just drop the `JResin.cs` file into your project and call the `JResin.Json.Repair` method.

## Usage Example

### Code

```csharp
var json = """
           [   1, 9,8,1, ["hello", "worl
           """;
Console.WriteLine(json);
var repairedJson = JResin.Json.Repair(json);
Console.WriteLine(repairedJson);
```

### Output
```
[   1, 2,3,4, ["hello", "worl            
[1,2,3,4,["hello","worl"]]
```