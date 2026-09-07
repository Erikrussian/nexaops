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
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public void ValidateSubmissionData(List<FormFieldDto> schemaFields, Dictionary<string, object?> data)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Normalize keys for case-insensitive lookup
        var dataLookup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in data)
        {
            dataLookup[kvp.Key] = kvp.Value;
        }

        foreach (var field in schemaFields)
        {
            var fieldName = string.IsNullOrWhiteSpace(field.Name) ? field.Id : field.Name;
            var label = string.IsNullOrWhiteSpace(field.Label) ? fieldName : field.Label;

            var hasValue = dataLookup.TryGetValue(fieldName, out var rawVal);
            if (!hasValue && !string.IsNullOrWhiteSpace(field.Id))
            {
                hasValue = dataLookup.TryGetValue(field.Id, out rawVal);
            }

            var (isEmpty, parsedVal) = EvaluateValue(rawVal);

            // 1. Required Check
            if (field.Required && isEmpty)
            {
                AddError(errors, fieldName, $"{label} is required.");
                continue;
            }

            if (isEmpty)
            {
                continue; // Non-required empty field is valid
            }

            // 2. Type & Rule Validation
            switch (field.Type)
            {
                case FormFieldType.Text:
                case FormFieldType.Textarea:
                    var strVal = parsedVal?.ToString() ?? string.Empty;
                    if (field.Min.HasValue && strVal.Length < field.Min.Value)
                    {
                        AddError(errors, fieldName, $"{label} must be at least {field.Min.Value} characters.");
                    }
                    if (field.Max.HasValue && strVal.Length > field.Max.Value)
                    {
                        AddError(errors, fieldName, $"{label} cannot exceed {field.Max.Value} characters.");
                    }
                    if (!string.IsNullOrWhiteSpace(field.Pattern))
                    {
                        if (!Regex.IsMatch(strVal, field.Pattern))
                        {
                            AddError(errors, fieldName, $"{label} format is invalid.");
                        }
                    }
                    break;

                case FormFieldType.Number:
                    if (!TryConvertToDouble(parsedVal, out var numVal))
                    {
                        AddError(errors, fieldName, $"{label} must be a valid number.");
                    }
                    else
                    {
                        if (field.Min.HasValue && numVal < field.Min.Value)
                        {
                            AddError(errors, fieldName, $"{label} must be at least {field.Min.Value}.");
                        }
                        if (field.Max.HasValue && numVal > field.Max.Value)
                        {
                            AddError(errors, fieldName, $"{label} cannot exceed {field.Max.Value}.");
                        }
                    }
                    break;

                case FormFieldType.Email:
                    var emailStr = parsedVal?.ToString()?.Trim() ?? string.Empty;
                    if (!EmailRegex.IsMatch(emailStr))
                    {
                        AddError(errors, fieldName, $"{label} must be a valid email address.");
                    }
                    break;

                case FormFieldType.Date:
                    if (!TryConvertToDateTime(parsedVal, out _))
                    {
                        AddError(errors, fieldName, $"{label} must be a valid date.");
                    }
                    break;

                case FormFieldType.Select:
                    var selectedStr = parsedVal?.ToString()?.Trim();
                    if (field.Options != null && field.Options.Count > 0)
                    {
                        if (string.IsNullOrEmpty(selectedStr) || !field.Options.Any(o => string.Equals(o, selectedStr, StringComparison.OrdinalIgnoreCase)))
                        {
                            AddError(errors, fieldName, $"{label} must be one of: {string.Join(", ", field.Options)}.");
                        }
                    }
                    break;

                case FormFieldType.Checkbox:
                    // Checkbox can be boolean or selected values array
                    if (parsedVal is bool)
                    {
                        // valid boolean
                    }
                    else if (bool.TryParse(parsedVal?.ToString(), out _))
                    {
                        // valid boolean string
                    }
                    else if (parsedVal is IEnumerable<object> || parsedVal is JsonElement { ValueKind: JsonValueKind.Array })
                    {
                        // Valid multiple choice
                    }
                    break;
            }
        }

        if (errors.Count > 0)
        {
            var resultErrors = errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
            throw new ValidationException(resultErrors);
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
        if (val == null) return false;

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

        if (val is JsonElement el && el.ValueKind == JsonValueKind.Number)
        {
            return el.TryGetDouble(out result);
        }

        return double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryConvertToDateTime(object? val, out DateTime result)
    {
        result = default;
        if (val == null) return false;

        if (val is DateTime dt)
        {
            result = dt;
            return true;
        }

        if (val is JsonElement el && el.ValueKind == JsonValueKind.String)
        {
            return el.TryGetDateTime(out result);
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
}
