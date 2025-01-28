using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// JResin v1.0

namespace JResin
{
    public static class Json
    {
        /// <summary>
        /// Indicates the type of JSON structure currently open (object or array).
        /// Used by the parser to keep track of nested structures.
        /// </summary>
        private enum JsonStructureType
        {
            Object,
            Array,
        }

        /// <summary>
        /// Attempts to parse and “repair” a potentially incomplete or malformed JSON string, for example:
        /// - Removing leading/trailing whitespace.
        /// - Handling object ("{...}") and array ("[...]") structures.
        /// - Automatically inserting missing commas, braces, or brackets.
        /// - Forcibly closing strings if they are left open.
        /// - Substituting missing values with null when needed.
        /// 
        /// This approach is particularly useful for streaming JSON responses (e.g., from ChatGPT) that
        /// may arrive in segments or terminate abruptly. It is, however, a simplified solution and
        /// will not fix every possible JSON formatting issue.
        /// </summary>
        /// <param name="json">The input JSON string, possibly partial or malformed.</param>
        /// <returns>A “repaired” JSON string to the best of this parser's ability.</returns>
        public static string Repair(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            // Trim leading and trailing whitespace
            json = json.Trim(' ', '\t', '\n', '\r');

            var stack = new Stack<JsonStructureType>();
            var result = new StringBuilder();
            var index = 0;

            // Attempt to parse the top-level structure based on the first character
            if (index < json.Length)
            {
                switch (json[index])
                {
                    case '{':
                        if (!ConsumeObject())
                        {
                            // Parsing could not fully handle an object start
                        }
                        break;
                    case '[':
                        if (!ConsumeArray())
                        {
                            // Parsing could not fully handle an array start
                        }
                        break;
                }
            }

            // If there are still unclosed objects/arrays, close them forcibly
            while (stack.Count > 0)
            {
                var top = stack.Pop();
                result.Append(top == JsonStructureType.Array ? ']' : '}');
            }

            return result.ToString();


            // --------------------- Local (Nested) Methods ----------------------

            // Consumes any sequence of whitespace characters (spaces, tabs, newlines, etc.)
            // and advances the current index. Returns true if there are still characters left
            // to parse after skipping whitespace.
            bool ConsumeWhitespace()
            {
                while (index < json.Length)
                {
                    if (json[index] is ' ' or '\t' or '\n' or '\r')
                    {
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
                return (index < json.Length);
            }

            // Attempts to consume a comma character ',' if it exists. If the next character
            // is not a comma but is instead a closing bracket/brace or end of string, this method
            // will accept it as valid. If it seems a comma is missing (e.g., there is another value
            // following without a comma), it inserts one into the result.
            // 
            // Returns true to continue parsing.
            bool ConsumeComma()
            {
                ConsumeWhitespace();

                if (index >= json.Length)
                {
                    // No more input. No comma needed at the very end.
                    return true;
                }

                if (json[index] == ',')
                {
                    // The comma is present in the input
                    result.Append(',');
                    index++;
                    return true;
                }

                // If the next character indicates a close bracket/brace, a comma may not be necessary
                if (json[index] == ']' || json[index] == '}')
                {
                    return true;
                }

                // Otherwise, we assume a missing comma. We insert one as part of the "repair".
                result.Append(',');
                return true;
            }

            // Consumes a JSON string token, including the surrounding double quotes
            // and any escape sequences within. If successful, the exact substring
            // (including quotes) is appended to the result. Returns false if any
            // errors occur (e.g., malformed escape sequences).
            bool ConsumeString()
            {
                if (index >= json.Length || json[index] != '"')
                    return false;

                var startIndex = index;
                index++; // Skip the initial double quote

                while (index < json.Length)
                {
                    var c = json[index];

                    // Found the closing quote
                    if (c == '"')
                    {
                        index++;
                        // Append the entire string from the starting quote up to the closing quote
                        result.Append(json[startIndex..index]);
                        return true;
                    }

                    // If a backslash is found, handle the escape sequence
                    if (c == '\\')
                    {
                        index++;
                        if (index >= json.Length)
                            // If the string ends right after a backslash, we might choose to forcibly close it
                            break; // We'll jump to "incomplete" logic below

                        var escapeChar = json[index];
                        switch (escapeChar)
                        {
                            case '"':
                            case '\\':
                            case '/':
                            case 'b':
                            case 'f':
                            case 'n':
                            case 'r':
                            case 't':
                                // Valid single-character escapes
                                break;
                            case 'u':
                                // Unicode escape sequence: \uXXXX
                                if (index + 4 >= json.Length)
                                {
                                    // Not enough room for 4 hex digits → incomplete
                                    index = json.Length;
                                    break;
                                }

                                string hex = json.Substring(index + 1, 4);
                                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                                {
                                    // Malformed hex
                                    index = json.Length;
                                    break;
                                }
                                index += 4;
                                break;
                            default:
                                // Invalid escape sequence → 
                                // we could break or forcibly try to keep going
                                index = json.Length;
                                break;
                        }
                        index++;
                    }
                    else
                    {
                        // For a valid JSON string, characters below ASCII code 32 must be escaped
                        if (c < 32) 
                        {
                            // We can treat this as an error or forcibly continue
                            index = json.Length;
                            break;
                        }
                        
                        index++;
                    }
                }

                // If we got here, it means the string was never closed (or had a broken escape),
                // so we forcibly "close" it with a quote.
                // We include everything from the original starting point to the current `index`.
                // Then add a double quote to mimic a closed string.

                // Append everything up to current position (which might be end of JSON)
                result.Append(json[startIndex..index]);

                // Now force a closing quote
                result.Append('"');

                return true;
            }

            // Consumes a JSON number using a regular expression to match
            // the valid format: optional '-', digits, optional fraction, optional exponent.
            // If successful, the matched substring is appended to the result.
            // Additionally handles incomplete numbers by replacing them with null.
            bool ConsumeNumber()
            {
                string substring = json[index..];
                var regex = new Regex(@"^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?");
                var match = regex.Match(substring);
                if (!match.Success)
                    return false;

                // Check if the match consumes the entire remaining input
                bool isPotentiallyIncomplete = match.Length == substring.Length && index + match.Length < json.Length;

                if (isPotentiallyIncomplete)
                {
                    // Log or handle the incomplete number scenario
                    // For simplicity, we'll replace it with null
                    result.Append("null");
                    index += match.Length;
                }
                else
                {
                    // Append the matched number to the result
                    result.Append(match.Value);
                    index += match.Value.Length;
                }

                return true;
            }

            // Consumes a JSON object, which starts with '{', ends with '}',
            // and contains zero or more pairs in the form "key": value.
            // Missing braces or commas will be inserted if needed, as part of the "repair" strategy.
            bool ConsumeObject()
            {
                // Must have an opening brace
                if (index >= json.Length || json[index] != '{')
                    return false;

                // Append the '{' and push the object type onto the stack
                result.Append('{');
                index++;
                stack.Push(JsonStructureType.Object);

                while (index < json.Length)
                {
                    // If the next character is '}', the object ends
                    if (json[index] == '}')
                    {
                        result.Append('}');
                        index++;
                        stack.Pop();
                        return true;
                    }

                    // Skip any whitespace
                    if (!ConsumeWhitespace())
                        break;

                    // According to standard JSON, a key must be a string
                    if (!ConsumeString())
                    {
                        // If we wanted to be extra lenient, we could insert quotes here,
                        // but let's keep it strict for now.
                        return false;
                    }

                    // After the key, we expect a colon
                    if (!ConsumeWhitespace()) return false;
                    if (index < json.Length && json[index] == ':')
                    {
                        result.Append(':');
                        index++;
                    }
                    else
                    {
                        // Missing colon - insert it automatically
                        result.Append(':');
                    }

                    // Consume the value after the colon
                    if (!ConsumeValue())
                    {
                        // Check if next token is a comma or '}' or end of input
                        // If so, we assume the user omitted the value entirely.
                        if (index >= json.Length || json[index] == '}' || json[index] == ',')
                        {
                            result.Append("null");
                        }
                        else
                        {
                            // Some other unexpected character => real error or attempt further repair
                            return false;
                        }
                    }

                    // After the value, there may be a comma if there is another key/value pair,
                    // or a '}' if the object is closing
                    if (!ConsumeComma())
                        return false;

                    // Skip whitespace before the next key or closing brace
                    ConsumeWhitespace();
                }

                // If we reach here, it means the object was never properly closed.
                // We'll pop the object from the stack and insert a '}' to close it.
                stack.Pop();
                result.Append('}');
                return true;
            }

            // Consumes a JSON array, which starts with '[', ends with ']',
            // and contains zero or more values separated by commas.
            // Missing brackets or commas will be inserted if needed, as part of the "repair" strategy.
            bool ConsumeArray()
            {
                if (index >= json.Length || json[index] != '[')
                    return false;

                result.Append('[');
                index++;
                stack.Push(JsonStructureType.Array);

                while (index < json.Length)
                {
                    // Check for the closing bracket
                    if (json[index] == ']')
                    {
                        result.Append(']');
                        index++;
                        stack.Pop();
                        return true;
                    }

                    // Otherwise, parse the next value in the array
                    if (!ConsumeValue())
                        return false;

                    // Arrays expect commas or a closing bracket
                    if (!ConsumeComma())
                        return false;

                    // Skip whitespace before next value or closing bracket
                    ConsumeWhitespace();
                }

                // If the input ends without a closing bracket, add it to the result
                stack.Pop();
                result.Append(']');
                return true;
            }

            // Checks if the next 4 characters match "true". If so, appends "true" to the result
            // and advances the index.
            bool ConsumeTrue()
            {
                if (json.Length - index >= 4 && json[index..(index + 4)] == "true")
                {
                    result.Append("true");
                    index += 4;
                    return true;
                }
                return false;
            }

            // Checks if the next 5 characters match "false". If so, appends "false" to the result
            // and advances the index.
            bool ConsumeFalse()
            {
                if (json.Length - index >= 5 && json[index..(index + 5)] == "false")
                {
                    result.Append("false");
                    index += 5;
                    return true;
                }
                return false;
            }

            // Checks if the next 4 characters match "null". If so, appends "null" to the result
            // and advances the index.
            bool ConsumeNull()
            {
                if (json.Length - index >= 4 && json[index..(index + 4)] == "null")
                {
                    result.Append("null");
                    index += 4;
                    return true;
                }
                return false;
            }

            // Attempts to parse a single JSON value (string, number, object, array, true, false, or null)
            // and append it to the result, potentially with minor corrections.
            bool ConsumeValue()
            {
                ConsumeWhitespace();
                if (index >= json.Length) return false;

                var c = json[index];

                if (c == '"')
                {
                    if (!ConsumeString()) return false;
                }
                else if (c is '-' or '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9')
                {
                    if (!ConsumeNumber()) return false;
                }
                else if (c == '{')
                {
                    if (!ConsumeObject()) return false;
                }
                else if (c == '[')
                {
                    if (!ConsumeArray()) return false;
                }
                else if (c == 't')
                {
                    if (!ConsumeTrue()) return false;
                }
                else if (c == 'f')
                {
                    if (!ConsumeFalse()) return false;
                }
                else if (c == 'n')
                {
                    if (!ConsumeNull()) return false;
                }
                else
                {
                    // Unrecognized token
                    return false;
                }
                ConsumeWhitespace();
                return true;
            }
        }
    }
}
