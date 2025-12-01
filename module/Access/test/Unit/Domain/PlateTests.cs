using FluentAssertions;
using main.domain.vo;

namespace Access.Tests.Unit.Domain;

public class PlateTests
{
    [Theory]
    [InlineData("ABC1234")]
    [InlineData("XYZ9876")]
    public void Create_WithValidPlate_ShouldCreatePlate(string validPlate)
    {
        var plate = Plate.Create(validPlate);

        plate.Should().NotBeNull();
        plate.Document.Should().Be(validPlate);
    }

    [Fact]
    public void Equals_WithSamePlate_ShouldReturnTrue()
    {
        var plate1 = Plate.Create("ABC1234");
        var plate2 = Plate.Create("ABC1234");

        plate1.Document.Should().Be(plate2.Document);
    }
}