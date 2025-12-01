using FluentAssertions;
using main.domain.entity;
using main.domain.vo;
using main.domain.exception;

namespace Access.Tests.Unit.Domain;

public class ClientTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateClient()
    {
        var name = "João Silva";
        var phoneNumber = PhoneNumber.Create("+5511999999999");
        var cpf = Cpf.Create("11144477735");

        var client = Client.Create(name, phoneNumber, cpf);

        client.Should().NotBeNull();
        client.Name.Should().Be(name);
        client.PhoneNumber.Should().Be(phoneNumber);
        client.Cpf.Should().Be(cpf);
        client.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public void RegisterVehicle_WithValidPlate_ShouldAddVehicle()
    {
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        var plate = Plate.Create("ABC1234");

        var vehicle = client.RegisterVehicle(plate, Guid.NewGuid());

        client.Vehicles.Should().HaveCount(1);
        client.Vehicles.First().Plate.Should().Be(plate);
        vehicle.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldThrowException(string invalidName)
    {
        var phoneNumber = PhoneNumber.Create("+5511999999999");
        var cpf = Cpf.Create("11144477735");

        var act = () => Client.Create(invalidName, phoneNumber, cpf);
        act.Should().Throw<DomainException>();
    }
}