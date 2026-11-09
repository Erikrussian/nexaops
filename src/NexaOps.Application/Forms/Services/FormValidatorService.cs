using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Forms.Services;

public interface IFormValidatorService
{
    void ValidateSubmissionData(List<FormFieldDto> schemaFields, Dictionary<string, object?> data);
}

public class FormValidatorService : IFormValidatorService
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);
    private const int MaxPatternLength = 500;
    private const int MaxRegexInputLength = 10000;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(250));

    public void ValidateSubmissionData(List<FormFieldDto> schemaFields, Dictionary<string, object?> data)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        ValidateSubmissionDataInternal(schemaFields ?? new(), data ?? new(), errors, string.Empty);

        if (errors.Count > 0)
        {
            var resultErrors = errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
            throw new ValidationException(resultErrors);
        }
    }

    private void ValidateSubmissionDataInternal(
        List<FormFieldDto> schemaFields,
        IDictionary<string, object?> data,
        Dictionary<string, List<string>> errors,
        string keyPrefix)
    {
        // 1. Determine allowed keys at this level
        var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in schemaFields)
        {
            if (!string.IsNullOrWhiteSpace(field.Name))
            {
                allowedKeys.Add(field.Name.Trim());
            }
            if (!string.IsNullOrWhiteSpace(field.Id))
            {
                allowedKeys.Add(field.Id.Trim());
            }
        }

        // 2. Reject unexpected payload properties (Closed Schema)
        foreach (var key in data.Keys)
        {
            if (!allowedKeys.Contains(key))
            {
                var fullKeyPath = string.IsNullOrEmpty(keyPrefix) ? key : $"{keyPrefix}.{key}";
                AddError(errors, fullKeyPath, $"Trường không xác định '{fullKeyPath}' không được định nghĩa trong cấu hình biểu mẫu.");
            }
        }

        // Normalize keys for case-insensitive lookup
        var dataLookup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in data)
        {
            dataLookup[kvp.Key] = kvp.Value;
        }

        // 3. Validate each declared field
        foreach (var field in schemaFields)
        {
            var fieldIdentifier = string.IsNullOrWhiteSpace(field.Name) ? field.Id : field.Name;
            var fullFieldKey = string.IsNullOrEmpty(keyPrefix) ? fieldIdentifier : $"{keyPrefix}.{fieldIdentifier}";
            var label = string.IsNullOrWhiteSpace(field.Label) ? fieldIdentifier : field.Label;

            var hasValue = dataLookup.TryGetValue(fieldIdentifier, out var rawVal);
            if (!hasValue && !string.IsNullOrWhiteSpace(field.Id))
            {
                hasValue = dataLookup.TryGetValue(field.Id, out rawVal);
            }

            var (isEmpty, parsedVal) = EvaluateValue(rawVal);

            // Required Check
            if (field.Required && isEmpty)
            {
                AddError(errors, fullFieldKey, $"{label} is required.");
                continue;
            }

            if (isEmpty)
            {
                continue; // Non-required empty/null field is valid
            }

            // Check if field has nested sub-fields
            if (field.Fields != null && field.Fields.Count > 0)
            {
                ValidateNestedStructure(field, parsedVal, errors, fullFieldKey, label);
                continue;
            }

            // Leaf field type & rule validation
            ValidateFieldType(field, parsedVal, errors, fullFieldKey, label);
        }
    }

    private void ValidateNestedStructure(
        FormFieldDto field,
        object? parsedVal,
        Dictionary<string, List<string>> errors,
        string fullFieldKey,
        string label)
    {
        if (parsedVal is IDictionary<string, object?> nestedDict)
        {
            ValidateSubmissionDataInternal(field.Fields!, nestedDict, errors, fullFieldKey);
        }
        else if (parsedVal is JsonElement { ValueKind: JsonValueKind.Object } objElement)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in objElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value;
            }
            ValidateSubmissionDataInternal(field.Fields!, dict, errors, fullFieldKey);
        }
        else if (parsedVal is JsonElement { ValueKind: JsonValueKind.Array } arrayElement)
        {
            int index = 0;
            foreach (var item in arrayElement.EnumerateArray())
            {
                var itemKey = $"{fullFieldKey}[{index}]";
                if (item.ValueKind == JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in item.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value;
                    }
                    ValidateSubmissionDataInternal(field.Fields!, dict, errors, itemKey);
                }
                else
                {
                    AddError(errors, itemKey, $"{label} item must be an object conforming to schema.");
                }
                index++;
            }
        }
        else if (parsedVal is IEnumerable<IDictionary<string, object?>> list)
        {
            int index = 0;
            foreach (var item in list)
            {
                var itemKey = $"{fullFieldKey}[{index}]";
                ValidateSubmissionDataInternal(field.Fields!, item, errors, itemKey);
                index++;
            }
        }
        else if (parsedVal is IEnumerable<object> objList && parsedVal is not string)
        {
            int index = 0;
            foreach (var item in objList)
            {
                var itemKey = $"{fullFieldKey}[{index}]";
                if (item is IDictionary<string, object?> itemDict)
                {
                    ValidateSubmissionDataInternal(field.Fields!, itemDict, errors, itemKey);
                }
                else if (item is JsonElement { ValueKind: JsonValueKind.Object } itemEl)
                {
                    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in itemEl.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value;
                    }
                    ValidateSubmissionDataInternal(field.Fields!, dict, errors, itemKey);
                }
                else
                {
                    AddError(errors, itemKey, $"{label} item must be an object conforming to schema.");
                }
                index++;
            }
        }
        else if (parsedVal is string jsonStr && jsonStr.TrimStart().StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value.Clone();
                    }
                    ValidateSubmissionDataInternal(field.Fields!, dict, errors, fullFieldKey);
                    return;
                }
            }
            catch
            {
                AddError(errors, fullFieldKey, $"{label} contains malformed JSON object.");
                return;
            }
        }
        else if (parsedVal is string jsonArrStr && jsonArrStr.TrimStart().StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArrStr);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var itemKey = $"{fullFieldKey}[{index}]";
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            foreach (var prop in item.EnumerateObject())
                            {
                                dict[prop.Name] = prop.Value.Clone();
                            }
                            ValidateSubmissionDataInternal(field.Fields!, dict, errors, itemKey);
                        }
                        else
                        {
                            AddError(errors, itemKey, $"{label} item must be an object conforming to schema.");
                        }
                        index++;
                    }
                    return;
                }
            }
            catch
            {
                AddError(errors, fullFieldKey, $"{label} contains malformed JSON array.");
                return;
            }
        }
        else
        {
            AddError(errors, fullFieldKey, $"{label} must be a valid object or array conforming to schema.");
        }
    }

    private static void ValidateFieldType(
        FormFieldDto field,
        object? parsedVal,
        Dictionary<string, List<string>> errors,
        string fullFieldKey,
        string label)
    {
        switch (field.Type)
        {
            case FormFieldType.Text:
            case FormFieldType.Textarea:
                if (parsedVal is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array } ||
                    (parsedVal is IEnumerable && parsedVal is not string))
                {
                    AddError(errors, fullFieldKey, $"{label} must be a valid text string.");
                }
                else
                {
                    var strVal = parsedVal?.ToString() ?? string.Empty;
                    if (field.Min.HasValue && strVal.Length < field.Min.Value)
                    {
                        AddError(errors, fullFieldKey, $"{label} must be at least {field.Min.Value} characters.");
                    }
                    if (field.Max.HasValue && strVal.Length > field.Max.Value)
                    {
                        AddError(errors, fullFieldKey, $"{label} cannot exceed {field.Max.Value} characters.");
                    }
                    if (!string.IsNullOrWhiteSpace(field.Pattern))
                    {
                        if (!SafeIsMatch(strVal, field.Pattern, out var regexError))
                        {
                            AddError(errors, fullFieldKey, regexError != null ? $"{label}: {regexError}" : $"{label} format is invalid.");
                        }
                    }
                }
                break;

            case FormFieldType.Number:
                if (parsedVal is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array } ||
                    !TryConvertToDouble(parsedVal, out var numVal))
                {
                    AddError(errors, fullFieldKey, $"{label} must be a valid number.");
                }
                else
                {
                    if (field.Min.HasValue && numVal < field.Min.Value)
                    {
                        AddError(errors, fullFieldKey, $"{label} must be at least {field.Min.Value}.");
                    }
                    if (field.Max.HasValue && numVal > field.Max.Value)
                    {
                        AddError(errors, fullFieldKey, $"{label} cannot exceed {field.Max.Value}.");
                    }
                }
                break;

            case FormFieldType.Email:
                if (parsedVal is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array })
                {
                    AddError(errors, fullFieldKey, $"{label} must be a valid email address.");
                }
                else
                {
                    var emailStr = parsedVal?.ToString()?.Trim() ?? string.Empty;
                    if (!EmailRegex.IsMatch(emailStr))
                    {
                        AddError(errors, fullFieldKey, $"{label} must be a valid email address.");
                    }
                }
                break;

            case FormFieldType.Date:
                if (parsedVal is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array } ||
                    !TryConvertToDateTime(parsedVal, out _))
                {
                    AddError(errors, fullFieldKey, $"{label} must be a valid date.");
                }
                break;

            case FormFieldType.Select:
                if (parsedVal is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array })
                {
                    AddError(errors, fullFieldKey, $"{label} must be a valid option string.");
                }
                else
                {
                    var selectedStr = parsedVal?.ToString()?.Trim();
                    if (field.Options != null && field.Options.Count > 0)
                    {
                        if (string.IsNullOrEmpty(selectedStr) || !field.Options.Any(o => string.Equals(o, selectedStr, StringComparison.OrdinalIgnoreCase)))
                        {
                            AddError(errors, fullFieldKey, $"{label} must be one of: {string.Join(", ", field.Options)}.");
                        }
                    }
                }
                break;

            case FormFieldType.Checkbox:
                if (parsedVal is bool)
                {
                    // valid boolean
                }
                else if (parsedVal is JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False })
                {
                    // valid boolean
                }
                else if (bool.TryParse(parsedVal?.ToString(), out _))
                {
                    // valid boolean string
                }
                else if (parsedVal is JsonElement { ValueKind: JsonValueKind.Array } arrayElement)
                {
                    var selectedItems = new List<string>();
                    bool hasTypeError = false;
                    foreach (var item in arrayElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String && item.ValueKind != JsonValueKind.Number)
                        {
                            AddError(errors, fullFieldKey, $"{label} array contains invalid item types.");
                            hasTypeError = true;
                            break;
                        }
                        var s = item.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            selectedItems.Add(s);
                        }
                    }
                    if (!hasTypeError && field.Options != null && field.Options.Count > 0)
                    {
                        foreach (var item in selectedItems)
                        {
                            if (!field.Options.Any(o => string.Equals(o, item, StringComparison.OrdinalIgnoreCase)))
                            {
                                AddError(errors, fullFieldKey, $"{label} contains invalid option '{item}'. Allowed options: {string.Join(", ", field.Options)}.");
                            }
                        }
                    }
                }
                else if (parsedVal is IEnumerable enumerable && parsedVal is not string)
                {
                    var selectedItems = new List<string>();
                    foreach (var item in enumerable)
                    {
                        if (item != null) selectedItems.Add(item.ToString()!);
                    }
                    if (field.Options != null && field.Options.Count > 0)
                    {
                        foreach (var item in selectedItems)
                        {
                            if (!field.Options.Any(o => string.Equals(o, item, StringComparison.OrdinalIgnoreCase)))
                            {
                                AddError(errors, fullFieldKey, $"{label} contains invalid option '{item}'. Allowed options: {string.Join(", ", field.Options)}.");
                            }
                        }
                    }
                }
                else
                {
                    AddError(errors, fullFieldKey, $"{label} must be a boolean or an array of selected options.");
                }
                break;
        }
    }

    private static (bool IsEmpty, object? Value) EvaluateValue(object? rawVal)
    {
        if (rawVal == null)
            return (true, null);

        if (rawVal is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return (true, null);
                case JsonValueKind.String:
                    var s = element.GetString();
                    return (string.IsNullOrWhiteSpace(s), s);
                case JsonValueKind.Number:
                    return (false, element.GetDouble());
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return (false, element.GetBoolean());
                case JsonValueKind.Array:
                    return (element.GetArrayLength() == 0, element);
                case JsonValueKind.Object:
                    return (false, element);
                default:
                    return (false, element.ToString());
            }
        }

        if (rawVal is string str)
        {
            return (string.IsNullOrWhiteSpace(str), str);
        }

        return (false, rawVal);
    }

    private static bool TryConvertToDouble(object? val, out double result)
    {
        result = 0;
        if (val == null || val is bool) return false;

        if (val is double d)
        {
            result = d;
            return true;
        }

        if (val is int i)
        {
            result = i;
            return true;
        }

        if (val is long l)
        {
            result = l;
            return true;
        }

        if (val is decimal m)
        {
            result = (double)m;
            return true;
        }

        if (val is float f)
        {
            result = f;
            return true;
        }

        if (val is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number)
            {
                return el.TryGetDouble(out result);
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                return double.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            }
            return false;
        }

        return double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryConvertToDateTime(object? val, out DateTime result)
    {
        result = default;
        if (val == null || val is bool) return false;

        if (val is DateTime dt)
        {
            result = dt;
            return true;
        }

        if (val is DateTimeOffset dto)
        {
            result = dto.UtcDateTime;
            return true;
        }

        if (val is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                return el.TryGetDateTime(out result) || DateTime.TryParse(el.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result);
            }
            return false;
        }

        return DateTime.TryParse(val.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result);
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = new List<string>();
            errors[key] = list;
        }
        list.Add(message);
    }

    private static bool SafeIsMatch(string input, string pattern, out string? errorMessage)
    {
        errorMessage = null;

        if (pattern.Length > MaxPatternLength)
        {
            errorMessage = "Validation pattern is too long.";
            return false;
        }

        if (input.Length > MaxRegexInputLength)
        {
            errorMessage = "Input value exceeds maximum allowed length for pattern matching.";
            return false;
        }

        try
        {
            // 1. Attempt non-backtracking engine first for linear time complexity guarantees
            try
            {
                return Regex.IsMatch(
                    input,
                    pattern,
                    RegexOptions.NonBacktracking | RegexOptions.CultureInvariant,
                    RegexMatchTimeout);
            }
            catch (ArgumentException)
            {
                // Pattern uses constructs unsupported by NonBacktracking (e.g. lookarounds/backreferences);
                // fallback to standard engine with strict timeout
                return Regex.IsMatch(
                    input,
                    pattern,
                    RegexOptions.CultureInvariant,
                    RegexMatchTimeout);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            errorMessage = "Pattern evaluation timed out.";
            return false;
        }
        catch (ArgumentException)
        {
            errorMessage = "Validation pattern is invalid.";
            return false;
        }
    }
}
