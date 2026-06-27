// Unit/Domain/ClientTests.cs
namespace NeoParking.Access.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;

public class ClientTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateClient()
    {
        var client = Client.Create("João Silva", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));

        client.Should().NotBeNull();
        client.Name.Should().Be("João Silva");
        client.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public void RegisterVehicle_WithValidPlate_ShouldAddVehicle()
    {
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));

        var vehicle = client.RegisterVehicle(Plate.Create("ABC1234")); // sem clientId

        client.Vehicles.Should().HaveCount(1);
        vehicle.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldThrowDomainException(string invalidName)
    {
        var act = () => Client.Create(invalidName, PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        act.Should().Throw<DomainException>();
    }
}