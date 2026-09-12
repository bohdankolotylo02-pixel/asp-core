using ClientVehicles.Web.Common;
using ClientVehicles.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ClientVehicles.Web.Data;

/// <summary>
/// Demo data so the app can be tested immediately after cloning. It only runs when the Clients
/// table is empty, so it never overwrites data that has already been entered.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Clients.AnyAsync(ct))
        {
            return;
        }

        // Fixed dates keep the demo data stable between runs.
        var created = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var archivedOn = new DateTime(2026, 6, 30, 17, 30, 0, DateTimeKind.Utc);

        var clients = new List<Client>
        {
            NewClient("Ana Ribeiro", "+351 912 445 018", "Rua das Flores 24, 4050-262 Porto", "214558930", created,
                NewVehicle("12-AB-34", "Renault", "Clio", "VF1RJA00567812345", 84_200, created),
                NewVehicle("AA-19-PL", "Peugeot", "208", null, 31_450, created)),

            NewClient("Carlos Mendes", "916 330 274", "Avenida da Liberdade 118, 1250-146 Lisboa", "198472115", created,
                NewVehicle("58-QR-72", "Volkswagen", "Golf", "WVWZZZ1KZAW223417", 162_980, created)),

            NewClient("Sofia Marques", "+351 933 118 602", "Travessa do Sol 7, 3000-112 Coimbra", null, created,
                NewVehicle("BC-44-XN", "Toyota", "Corolla", "SB1KZ3JE70F118722", 47_300, created),
                NewVehicle("21-TG-90", "Toyota", "Yaris", null, 112_640, created),
                NewVehicle("74-LM-08", "Ford", "Transit", "WF0XXXTTGXKM18823", 208_115, created)),

            NewClient("Joao Pereira", "912 007 553", "Rua Camilo Castelo Branco 55, 4700-210 Braga", "247119083", created,
                NewVehicle("QT-52-16", "Opel", "Astra", null, 96_700, created)),

            NewClient("Transportes Silva, Lda.", "+351 253 601 944", "Zona Industrial, Lote 14, 4705-025 Braga", "503887214", created,
                NewVehicle("31-VD-67", "Mercedes-Benz", "Sprinter", "WDB9066571S334218", 274_500, created),
                NewVehicle("AB-77-QS", "Iveco", "Daily", "ZCFC135A005612774", 189_220, created)),

            NewClient("Marta Antunes", "961 552 310", null, null, created,
                NewVehicle("09-HJ-41", "Fiat", "500", "ZFA3120000J118254", 62_150, created)),

            NewClient("Rui Goncalves", "+351 917 884 120", "Rua do Carmo 3, 8000-311 Faro", "176440289", created,
                NewVehicle("KP-38-24", "Seat", "Ibiza", null, 78_940, created)),

            NewClient("Helena Costa", "914 726 005", "Praceta das Acacias 12, 2750-642 Cascais", "231905764", created,
                NewVehicle("55-BN-19", "Citroen", "C3", "VF7SC8HR0BW512338", 54_060, created))
        };

        // One client archived together with their vehicle, to exercise the archived views and the
        // rule that reactivating a client also brings back the vehicles archived with them.
        var archivedClient = NewClient("Bruno Faria", "938 210 447", "Rua Nova 9, 4400-100 Vila Nova de Gaia", null, created,
            NewVehicle("MN-61-33", "Nissan", "Qashqai", "SJNFAAJ11U1012994", 143_770, created));
        archivedClient.IsArchived = true;
        archivedClient.ArchivedAtUtc = archivedOn;
        archivedClient.UpdatedAtUtc = archivedOn;
        foreach (var vehicle in archivedClient.Vehicles)
        {
            vehicle.IsArchived = true;
            vehicle.ArchivedWithClient = true;
            vehicle.ArchivedAtUtc = archivedOn;
            vehicle.UpdatedAtUtc = archivedOn;
        }

        clients.Add(archivedClient);

        // One vehicle archived on its own, while its client stays active (car sold, client kept).
        var soldCar = clients[0].Vehicles[1];
        soldCar.IsArchived = true;
        soldCar.ArchivedWithClient = false;
        soldCar.ArchivedAtUtc = archivedOn;
        soldCar.UpdatedAtUtc = archivedOn;

        db.Clients.AddRange(clients);
        await db.SaveChangesAsync(ct);
    }

    private static Client NewClient(
        string name,
        string phone,
        string? address,
        string? taxNumber,
        DateTime created,
        params Vehicle[] vehicles)
    {
        var client = new Client
        {
            Name = name,
            NameKey = TextKeys.SearchKey(name),
            Phone = phone,
            PhoneKey = TextKeys.DigitsKey(phone),
            Address = address,
            TaxNumber = taxNumber,
            CreatedAtUtc = created,
            UpdatedAtUtc = created
        };

        client.Vehicles.AddRange(vehicles);
        return client;
    }

    private static Vehicle NewVehicle(string plate, string brand, string model, string? vin, int? mileage, DateTime created) =>
        new()
        {
            LicensePlate = TextKeys.UpperDisplay(plate),
            LicensePlateKey = TextKeys.AlphaNumericKey(plate),
            Brand = brand,
            Model = model,
            Vin = vin is null ? null : TextKeys.UpperDisplay(vin),
            VinKey = vin is null ? null : TextKeys.AlphaNumericKey(vin),
            Mileage = mileage,
            CreatedAtUtc = created,
            UpdatedAtUtc = created
        };
}
