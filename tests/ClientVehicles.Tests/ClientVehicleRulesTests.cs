using ClientVehicles.Web.Models;
using ClientVehicles.Web.Models.Input;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClientVehicles.Tests;

/// <summary>
/// Covers the rules the requirements call out: relational integrity, uniqueness among active
/// records, archive/reactivate behaviour, atomic creation and search.
/// </summary>
public class ClientVehicleRulesTests
{
    private static NewClientInput NewClient(string name = "Ana Ribeiro", string phone = "912 345 678") =>
        new() { Client = new ClientInput { Name = name, Phone = phone } };

    private static VehicleInput NewVehicle(string plate = "12-AB-34", string? vin = null, int? mileage = null) =>
        new() { LicensePlate = plate, Brand = "Renault", Model = "Clio", Vin = vin, Mileage = mileage };

    [Fact]
    public async Task Create_client_without_vehicle_saves_the_client()
    {
        using var test = new TestDatabase();

        var result = await test.Clients.CreateAsync(NewClient());

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        var client = await check.Clients.SingleAsync();
        Assert.Equal("Ana Ribeiro", client.Name);
        Assert.Empty(await check.Vehicles.ToListAsync());
    }

    [Fact]
    public async Task Create_client_with_first_vehicle_saves_both_and_links_them()
    {
        using var test = new TestDatabase();

        var input = NewClient();
        input.AddVehicle = true;
        input.Vehicle = NewVehicle("12-ab-34", mileage: 120_000);

        var result = await test.Clients.CreateAsync(input);

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        var client = await check.Clients.Include(c => c.Vehicles).SingleAsync();
        var vehicle = Assert.Single(client.Vehicles);
        Assert.Equal(client.Id, vehicle.ClientId);
        // Plates are stored upper cased, with a punctuation free key used for comparisons.
        Assert.Equal("12-AB-34", vehicle.LicensePlate);
        Assert.Equal("12AB34", vehicle.LicensePlateKey);
    }

    [Fact]
    public async Task Combined_create_is_atomic_when_the_vehicle_is_rejected()
    {
        using var test = new TestDatabase();

        var first = NewClient();
        first.AddVehicle = true;
        first.Vehicle = NewVehicle("12-AB-34");
        Assert.True((await test.Clients.CreateAsync(first)).Succeeded);

        var second = NewClient("Carlos Mendes", "916 000 111");
        second.AddVehicle = true;
        second.Vehicle = NewVehicle("12 ab 34"); // same plate, written differently

        var result = await test.Clients.CreateAsync(second);

        Assert.False(result.Succeeded);
        using var check = test.NewContext();
        // The second client must not exist: no half saved data.
        Assert.Equal(1, await check.Clients.CountAsync());
        Assert.Equal(1, await check.Vehicles.CountAsync());
    }

    [Fact]
    public async Task Two_active_vehicles_cannot_share_a_plate()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);

        Assert.True((await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"))).Succeeded);
        var result = await test.Vehicles.CreateAsync(client, NewVehicle("58qr72"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Key == "LicensePlate");
        using var check = test.NewContext();
        Assert.Equal(1, await check.Vehicles.CountAsync());
    }

    [Fact]
    public async Task Two_active_vehicles_cannot_share_a_vin()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);

        Assert.True((await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72", vin: "WVWZZZ1KZAW223417"))).Succeeded);
        var result = await test.Vehicles.CreateAsync(client, NewVehicle("74-LM-08", vin: "wvwzzz1kzaw223417"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Key == "Vin");
    }

    [Fact]
    public async Task Empty_vin_is_not_treated_as_a_duplicate()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);

        Assert.True((await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"))).Succeeded);
        var result = await test.Vehicles.CreateAsync(client, NewVehicle("74-LM-08"));

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        Assert.Equal(2, await check.Vehicles.CountAsync());
        Assert.All(await check.Vehicles.ToListAsync(), v => Assert.Null(v.VinKey));
    }

    [Fact]
    public async Task Negative_mileage_is_rejected()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);

        var result = await test.Vehicles.CreateAsync(client, NewVehicle(mileage: -1));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Key == "Mileage");
    }

    [Fact]
    public async Task Database_rejects_negative_mileage_even_when_the_service_is_bypassed()
    {
        using var test = new TestDatabase();
        var clientId = await CreateClientAsync(test);

        var ex = await Assert.ThrowsAsync<SqliteException>(async () =>
            await test.Db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Vehicles (ClientId, LicensePlate, LicensePlateKey, Brand, Model, Mileage,
                                      IsArchived, ArchivedWithClient, CreatedAtUtc, UpdatedAtUtc)
                VALUES ({0}, '00-AA-00', '00AA00', 'Fiat', '500', -5, 0, 0, '2026-01-01', '2026-01-01')
                """.Replace("{0}", clientId.ToString())));

        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public async Task Database_rejects_a_duplicate_active_plate_even_when_the_service_is_bypassed()
    {
        using var test = new TestDatabase();
        var clientId = await CreateClientAsync(test);
        Assert.True((await test.Vehicles.CreateAsync(clientId, NewVehicle("58-QR-72"))).Succeeded);

        var ex = await Assert.ThrowsAsync<SqliteException>(async () =>
            await test.Db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Vehicles (ClientId, LicensePlate, LicensePlateKey, Brand, Model,
                                      IsArchived, ArchivedWithClient, CreatedAtUtc, UpdatedAtUtc)
                VALUES ({0}, '58-QR-72', '58QR72', 'Fiat', '500', 0, 0, '2026-01-01', '2026-01-01')
                """.Replace("{0}", clientId.ToString())));

        Assert.Contains("UNIQUE constraint failed", ex.Message);
    }

    [Fact]
    public async Task An_archived_vehicle_releases_its_plate()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        var created = await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"));
        Assert.True((await test.Vehicles.ArchiveAsync(created.EntityId)).Succeeded);

        var result = await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"));

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        // The old record is still there, only archived.
        Assert.Equal(2, await check.Vehicles.CountAsync());
        Assert.Equal(1, await check.Vehicles.CountAsync(v => v.IsArchived));
    }

    [Fact]
    public async Task Archiving_a_client_archives_their_vehicles_and_deletes_nothing()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"));
        await test.Vehicles.CreateAsync(client, NewVehicle("74-LM-08"));

        var result = await test.Clients.ArchiveAsync(client);

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        Assert.True((await check.Clients.SingleAsync()).IsArchived);
        var vehicles = await check.Vehicles.ToListAsync();
        Assert.Equal(2, vehicles.Count);
        Assert.All(vehicles, v =>
        {
            Assert.True(v.IsArchived);
            Assert.True(v.ArchivedWithClient);
            Assert.Equal(client, v.ClientId);
        });
    }

    [Fact]
    public async Task Reactivating_a_client_restores_only_the_vehicles_archived_with_them()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        var keep = await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"));
        var soldEarlier = await test.Vehicles.CreateAsync(client, NewVehicle("74-LM-08"));

        // This one was archived on its own before the client was archived.
        await test.Vehicles.ArchiveAsync(soldEarlier.EntityId);
        await test.Clients.ArchiveAsync(client);

        var result = await test.Clients.ReactivateAsync(client);

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        Assert.False((await check.Clients.SingleAsync()).IsArchived);
        Assert.False((await check.Vehicles.SingleAsync(v => v.Id == keep.EntityId)).IsArchived);
        Assert.True((await check.Vehicles.SingleAsync(v => v.Id == soldEarlier.EntityId)).IsArchived);
    }

    [Fact]
    public async Task Reactivating_a_client_keeps_a_vehicle_archived_when_its_plate_was_taken()
    {
        using var test = new TestDatabase();
        var first = await CreateClientAsync(test);
        var vehicle = await test.Vehicles.CreateAsync(first, NewVehicle("58-QR-72"));
        await test.Clients.ArchiveAsync(first);

        // The plate is free again, so another client registers it.
        var second = await CreateClientAsync(test, "Carlos Mendes", "916 000 111");
        Assert.True((await test.Vehicles.CreateAsync(second, NewVehicle("58-QR-72"))).Succeeded);

        var result = await test.Clients.ReactivateAsync(first);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Warnings, w => w.Contains("58-QR-72"));
        using var check = test.NewContext();
        Assert.False((await check.Clients.SingleAsync(c => c.Id == first)).IsArchived);
        Assert.True((await check.Vehicles.SingleAsync(v => v.Id == vehicle.EntityId)).IsArchived);
    }

    [Fact]
    public async Task A_vehicle_cannot_be_reactivated_while_its_client_is_archived()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        var vehicle = await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"));
        await test.Clients.ArchiveAsync(client);

        var result = await test.Vehicles.ReactivateAsync(vehicle.EntityId);

        Assert.False(result.Succeeded);
        using var check = test.NewContext();
        Assert.True((await check.Vehicles.SingleAsync()).IsArchived);
    }

    [Fact]
    public async Task A_vehicle_cannot_be_added_to_an_archived_client()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        await test.Clients.ArchiveAsync(client);

        var result = await test.Vehicles.CreateAsync(client, NewVehicle());

        Assert.False(result.Succeeded);
        using var check = test.NewContext();
        Assert.Empty(await check.Vehicles.ToListAsync());
    }

    [Fact]
    public async Task Editing_a_client_keeps_the_vehicle_associations()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"));

        var result = await test.Clients.UpdateAsync(client, new ClientInput
        {
            Name = "Ana Ribeiro Silva",
            Phone = "+351 912 345 678",
            Address = "Rua das Flores 24",
            TaxNumber = "214558930"
        });

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        var saved = await check.Clients.Include(c => c.Vehicles).SingleAsync();
        Assert.Equal("Ana Ribeiro Silva", saved.Name);
        Assert.Equal("351912345678", saved.PhoneKey);
        var vehicle = Assert.Single(saved.Vehicles);
        Assert.Equal(saved.Id, vehicle.ClientId);
    }

    [Fact]
    public async Task Editing_a_vehicle_updates_the_same_row()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        var created = await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72", mileage: 100_000));

        var result = await test.Vehicles.UpdateAsync(created.EntityId, NewVehicle("58-QR-72", mileage: 101_500));

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        var vehicle = Assert.Single(await check.Vehicles.ToListAsync());
        Assert.Equal(created.EntityId, vehicle.Id);
        Assert.Equal(101_500, vehicle.Mileage);
        Assert.Equal(client, vehicle.ClientId);
    }

    [Fact]
    public async Task Editing_a_vehicle_onto_another_active_plate_is_rejected()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        await test.Vehicles.CreateAsync(client, NewVehicle("58-QR-72"));
        var second = await test.Vehicles.CreateAsync(client, NewVehicle("74-LM-08"));

        var result = await test.Vehicles.UpdateAsync(second.EntityId, NewVehicle("58-QR-72"));

        Assert.False(result.Succeeded);
        using var check = test.NewContext();
        Assert.Equal("74-LM-08", (await check.Vehicles.SingleAsync(v => v.Id == second.EntityId)).LicensePlate);
    }

    [Theory]
    [InlineData("12-AB-34")]
    [InlineData("12ab34")]
    [InlineData("912 345 678")]
    [InlineData("912345678")]
    [InlineData("ana")]
    [InlineData("RIBEIRO")]
    [InlineData("WVWZZZ1KZAW223417")]
    public async Task Search_finds_the_client_by_name_phone_plate_or_vin(string term)
    {
        using var test = new TestDatabase();
        var input = NewClient();
        input.AddVehicle = true;
        input.Vehicle = NewVehicle("12-AB-34", vin: "WVWZZZ1KZAW223417");
        await test.Clients.CreateAsync(input);

        var rows = await test.Clients.SearchAsync(term, ArchiveFilter.Active);

        var row = Assert.Single(rows);
        Assert.Equal("Ana Ribeiro", row.Name);
    }

    [Fact]
    public async Task Search_ignores_accents_in_the_client_name()
    {
        using var test = new TestDatabase();
        await test.Clients.CreateAsync(NewClient("João Gonçalves", "917 884 120"));

        var rows = await test.Clients.SearchAsync("joao goncalves", ArchiveFilter.Active);

        Assert.Single(rows);
    }

    [Fact]
    public async Task Search_respects_the_archived_filter()
    {
        using var test = new TestDatabase();
        var client = await CreateClientAsync(test);
        await test.Clients.ArchiveAsync(client);

        Assert.Empty(await test.Clients.SearchAsync(null, ArchiveFilter.Active));
        Assert.Single(await test.Clients.SearchAsync(null, ArchiveFilter.Archived));
        Assert.Single(await test.Clients.SearchAsync(null, ArchiveFilter.All));
    }

    [Fact]
    public async Task Client_text_is_trimmed_and_optional_fields_become_null()
    {
        using var test = new TestDatabase();

        var result = await test.Clients.CreateAsync(new NewClientInput
        {
            Client = new ClientInput
            {
                Name = "  Sofia   Marques  ",
                Phone = " 933 118 602 ",
                Address = "   ",
                TaxNumber = ""
            }
        });

        Assert.True(result.Succeeded);
        using var check = test.NewContext();
        var client = await check.Clients.SingleAsync();
        Assert.Equal("Sofia Marques", client.Name);
        Assert.Equal("933 118 602", client.Phone);
        Assert.Null(client.Address);
        Assert.Null(client.TaxNumber);
    }

    private static async Task<int> CreateClientAsync(TestDatabase test, string name = "Ana Ribeiro", string phone = "912 345 678")
    {
        var result = await test.Clients.CreateAsync(NewClient(name, phone));
        Assert.True(result.Succeeded);
        return result.EntityId;
    }
}
