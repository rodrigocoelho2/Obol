using System.Reflection;

namespace Obol.UnitTest.Sdk;

/// <summary>
/// Obol.Sdk keeps its own copies of the WebApi's DTOs (the Website never references the WebApi). These tests make sure
/// every copy has the same properties, with the same types, as the class the WebApi sends.
/// </summary>
public sealed class SdkContractTests
{
    private static readonly Assembly WebApi = typeof(Obol.WebApi.Features.ObolOptions).Assembly;
    private static readonly Assembly Sdk = typeof(Obol.Sdk.ObolClient).Assembly;

    public static TheoryData<string> Contracts()
    {
        var data = new TheoryData<string>();
        foreach (var type in Sdk.GetTypes().Where(static t => t.IsPublic && t.Namespace == "Obol.Sdk.Models").OrderBy(static t => t.Name))
        {
            data.Add(type.Name);
        }

        return data;
    }

    [Fact]
    public void EveryDtoOfTheWebApiHasASdkCopy()
    {
        var sdkNames = Sdk.GetTypes().Where(static t => t.Namespace == "Obol.Sdk.Models").Select(static t => t.Name).ToHashSet();
        var missing = WebApi.GetTypes()
            .Where(IsContract)
            .Select(static t => t.Name)
            .Where(name => !sdkNames.Contains(name))
            .ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void TheSdkCopyMatchesTheWebApi(string name)
    {
        var sdk = Sdk.GetTypes().Single(t => t.Namespace == "Obol.Sdk.Models" && t.Name == name);
        var api = WebApi.GetTypes().Single(t => IsContract(t) && t.Name == name);

        var expected = api.GetProperties().Select(Describe).OrderBy(static p => p).ToList();
        var actual = sdk.GetProperties().Select(Describe).OrderBy(static p => p).ToList();

        Assert.Equal(string.Join(", ", expected), string.Join(", ", actual));
    }

    // What the WebApi sends or receives: the public classes of every Models.DTOs namespace, and the health answer.
    private static bool IsContract(Type type) =>
        type.IsPublic && (type.Namespace?.EndsWith(".Models.DTOs", StringComparison.Ordinal) == true || type.Namespace == "Obol.WebApi.Features.Health");

    private static string Describe(PropertyInfo property)
    {
        var type = property.PropertyType;
        var typeName = type.IsGenericType
            ? $"{type.Name}<{string.Join(",", type.GetGenericArguments().Select(static a => a.Name))}>"
            : type.Name;
        return $"{property.Name}:{typeName}";
    }
}
