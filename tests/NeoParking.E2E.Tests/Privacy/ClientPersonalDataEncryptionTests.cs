namespace NeoParking.E2E.Tests.Privacy;

using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-710: nome e telefone não ficam legíveis no banco nem no outbox, e as respostas continuam trazendo os dois.</summary>
[Collection(SecurityCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ClientPersonalDataEncryptionTests
{
    private readonly SecurityFixture _f;

    public ClientPersonalDataEncryptionTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task NameAndPhone_ShouldNotBeLegibleInTheDatabase_AndStillBeReturned()
    {
        var name = "Nome " + Guid.NewGuid().ToString("N")[..12];
        const string phone = "+5511987654321";

        var created = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token,
            new { name, phoneNumber = phone, type = "Individual", document = SecurityFixture.UniqueCpf() });
        var client = await _f.ReadAsync<ClientView>(created);

        client.Name.Should().Be(name);
        client.PhoneNumber.Should().Be(phone);

        var row = await _f.ReadClientRowAsync(client.Id);
        row.Should().NotContain(name).And.NotContain(phone).And.NotContain("987654321");
        (await _f.ReadAccessOutboxPayloadsAsync()).Should().NotContain(name);
        _f.Logs.Text.Should().NotContain(name);

        var read = await _f.ReadAsync<ClientView>(await _f.SendAsync(HttpMethod.Get, $"/clients/{client.Id}", _f.Operator.Token));
        read.Name.Should().Be(name);
        read.PhoneNumber.Should().Be(phone);
    }

    [Fact]
    public async Task UpdatedName_ShouldStayEncrypted_AndBeReturned()
    {
        var created = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", type = "Individual", document = SecurityFixture.UniqueCpf() });
        var client = await _f.ReadAsync<ClientView>(created);
        var name = "Novo " + Guid.NewGuid().ToString("N")[..12];

        var updated = await _f.SendAsync(HttpMethod.Patch, $"/clients/{client.Id}", _f.Operator.Token, new { name });

        (await _f.ReadAsync<ClientView>(updated)).Name.Should().Be(name);
        (await _f.ReadClientRowAsync(client.Id)).Should().NotContain(name);
    }

    private sealed record ClientView(Guid Id, string Name, string PhoneNumber);
}
