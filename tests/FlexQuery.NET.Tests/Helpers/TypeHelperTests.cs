using FlexQuery.NET.Helpers;
using Xunit;

namespace FlexQuery.NET.Tests.Helpers;

public class TypeHelperTests
{
    private enum Color { Red, Green, Blue }

    [Theory]
    [InlineData("5", typeof(int), 5)]
    [InlineData("3.14", typeof(decimal), 3.14)]
    [InlineData("true", typeof(bool), true)]
    [InlineData("10.5", typeof(double), 10.5)]
    [InlineData("7", typeof(long), 7L)]
    public void ConvertValue_Primitives_ReturnsConvertedValue(string raw, Type target, object expected)
    {
        var result = TypeHelper.ConvertValue(raw, target);

        result.Should().Be(expected);
        result!.GetType().Should().Be(Nullable.GetUnderlyingType(target) ?? target);
    }

    [Fact]
    public void ConvertValue_String_ReturnsRawString()
    {
        TypeHelper.ConvertValue("hello", typeof(string)).Should().Be("hello");
    }

    [Fact]
    public void ConvertValue_NullableInt_UnwrapsAndConverts()
    {
        TypeHelper.ConvertValue("42", typeof(int?)).Should().Be(42);
    }

    [Fact]
    public void ConvertValue_NullRaw_ReturnsNull()
    {
        TypeHelper.ConvertValue(null, typeof(int)).Should().BeNull();
    }

    [Fact]
    public void ConvertValue_InvalidInt_ReturnsNull()
    {
        TypeHelper.ConvertValue("abc", typeof(int)).Should().BeNull();
    }

    [Fact]
    public void ConvertValue_NumericStringToBool_ReturnsNull()
    {
        // "1" is not a valid bool via Convert.ChangeType.
        TypeHelper.ConvertValue("1", typeof(bool)).Should().BeNull();
    }

    [Fact]
    public void ConvertValue_Enum_CaseInsensitive()
    {
        TypeHelper.ConvertValue("green", typeof(Color)).Should().Be(Color.Green);
        TypeHelper.ConvertValue("RED", typeof(Color)).Should().Be(Color.Red);
    }

    [Fact]
    public void ConvertValue_InvalidEnum_ReturnsNull()
    {
        TypeHelper.ConvertValue("purple", typeof(Color)).Should().BeNull();
    }

    [Fact]
    public void ConvertValue_Guid_IsNotConvertibleViaChangeType()
    {
        // Convert.ChangeType does not support string -> Guid, so conversion degrades to null.
        // See "Future Improvements" in the testing plan for adding first-class Guid support.
        TypeHelper.ConvertValue(Guid.NewGuid().ToString(), typeof(Guid)).Should().BeNull();
    }

    [Fact]
    public void ConvertValue_DateTime_ReturnsDateTime()
    {
        TypeHelper.ConvertValue("2020-01-01", typeof(DateTime)).Should().Be(new DateTime(2020, 1, 1));
    }

    [Fact]
    public void ConvertValue_InvalidDateTime_ReturnsNull()
    {
        TypeHelper.ConvertValue("not-a-date", typeof(DateTime)).Should().BeNull();
    }

    [Fact]
    public void ConvertValue_DateOnly_IsNotConvertibleViaChangeType()
    {
        // Convert.ChangeType does not support string -> DateOnly, so conversion degrades to null.
        // See "Future Improvements" in the testing plan for adding first-class DateOnly support.
        TypeHelper.ConvertValue("2020-01-01", typeof(DateOnly)).Should().BeNull();
    }

    [Fact]
    public void ConvertValue_TimeOnly_IsNotConvertibleViaChangeType()
    {
        // Convert.ChangeType does not support string -> TimeOnly, so conversion degrades to null.
        // See "Future Improvements" in the testing plan for adding first-class TimeOnly support.
        TypeHelper.ConvertValue("10:30:00", typeof(TimeOnly)).Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(decimal), true)]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(long), true)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(Color), false)]
    public void IsNumeric_ReturnsExpected(Type type, bool expected)
    {
        TypeHelper.IsNumeric(type).Should().Be(expected);
    }

    [Fact]
    public void IsNavigationProperty_ReferenceType_ReturnsTrue()
    {
        TypeHelper.IsNavigationProperty(typeof(Customer)).Should().BeTrue();
    }

    [Fact]
    public void IsNavigationProperty_StringAndValueType_ReturnFalse()
    {
        TypeHelper.IsNavigationProperty(typeof(string)).Should().BeFalse();
        TypeHelper.IsNavigationProperty(typeof(int)).Should().BeFalse();
        TypeHelper.IsNavigationProperty(typeof(int?)).Should().BeFalse();
    }

    [Fact]
    public void TryGetCollectionElementType_List_ReturnsElement()
    {
        TypeHelper.TryGetCollectionElementType(typeof(List<Customer>), out var element)
            .Should().BeTrue();
        element.Should().Be(typeof(Customer));
    }

    [Fact]
    public void TryGetCollectionElementType_String_ReturnsFalse()
    {
        TypeHelper.TryGetCollectionElementType(typeof(string), out _).Should().BeFalse();
    }
}
