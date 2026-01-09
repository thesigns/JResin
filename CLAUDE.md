# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the project
dotnet build JResin.sln

# Run the project (executes Program.cs example)
dotnet run --project JResin/JResin.csproj

# Build in release mode
dotnet build JResin.sln -c Release
```

## Project Overview

JResin is a C# library for repairing incomplete or truncated JSON data. It reconstructs valid JSON from partial input that may result from interrupted network streams, partial API responses, or corrupted files.

**Target Framework:** .NET 9.0

## Architecture

The library consists of a single static class `JResin.Json` with one public method:

- `JResin.Json.Repair(string json)` - Takes potentially malformed JSON and returns a repaired version

The repair logic uses a recursive descent parser with local nested functions. Key repair strategies:
- Auto-closes unclosed strings, arrays, and objects
- Inserts missing commas between values
- Inserts missing colons in key-value pairs
- Substitutes `null` for missing or incomplete values
- Uses a stack to track nested structure types (`JsonStructureType.Object` or `JsonStructureType.Array`)

## Usage

The library is designed as a single-file drop-in. Copy `JResin/JResin.cs` into any project and call:

```csharp
string repaired = JResin.Json.Repair(incompleteJson);
```
