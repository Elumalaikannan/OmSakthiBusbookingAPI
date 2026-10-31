using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BusBooking.Api;

public sealed class JsonDatabaseStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonDatabaseStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("JsonDatabase:FilePath must be configured.");

        _filePath = Path.GetFullPath(filePath);
    }

    public async Task LoadAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return;

        await using var stream = File.OpenRead(_filePath);
        var document = await JsonSerializer.DeserializeAsync<JsonDatabaseDocument>(
            stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException($"The JSON database at '{_filePath}' is empty or invalid.");

        if (document.Version != 1)
            throw new InvalidDataException($"The JSON database at '{_filePath}' uses unsupported version {document.Version}.");

        db.Users.AddRange(document.Users.Select(user => new AppUser
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Role = user.Role,
            CreatedAtUtc = user.CreatedAtUtc
        }));
        db.Buses.AddRange(document.Buses.Select(bus => new Bus
        {
            Id = bus.Id,
            Name = bus.Name,
            RegistrationNumber = bus.RegistrationNumber,
            Capacity = bus.Capacity
        }));
        db.Trips.AddRange(document.Trips.Select(trip => new Trip
        {
            Id = trip.Id,
            BusId = trip.BusId,
            Origin = trip.Origin,
            Destination = trip.Destination,
            DepartureUtc = trip.DepartureUtc,
            ArrivalUtc = trip.ArrivalUtc,
            Price = trip.Price,
            Status = trip.Status
        }));
        db.Bookings.AddRange(document.Bookings.Select(booking => new Booking
        {
            Id = booking.Id,
            UserId = booking.UserId,
            TripId = booking.TripId,
            SeatNumber = booking.SeatNumber,
            PassengerName = booking.PassengerName,
            PassengerAge = booking.PassengerAge,
            PassengerGender = booking.PassengerGender,
            PassengerMobileNumber = booking.PassengerMobileNumber,
            AdhiparasakthiCircleName = booking.AdhiparasakthiCircleName,
            BookedAtUtc = booking.BookedAtUtc,
            Status = booking.Status
        }));
        db.PasswordResetTokens.AddRange(document.PasswordResetTokens.Select(token => new PasswordResetToken
        {
            Id = token.Id,
            UserId = token.UserId,
            TokenHash = token.TokenHash,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UsedAtUtc = token.UsedAtUtc
        }));
    }

    public async Task SaveAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var document = new JsonDatabaseDocument
        {
            Users = await db.Users.AsNoTracking().Select(user => new JsonUserRecord
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                Role = user.Role,
                CreatedAtUtc = user.CreatedAtUtc
            }).ToListAsync(cancellationToken),
            Buses = await db.Buses.AsNoTracking().Select(bus => new JsonBusRecord
            {
                Id = bus.Id,
                Name = bus.Name,
                RegistrationNumber = bus.RegistrationNumber,
                Capacity = bus.Capacity
            }).ToListAsync(cancellationToken),
            Trips = await db.Trips.AsNoTracking().Select(trip => new JsonTripRecord
            {
                Id = trip.Id,
                BusId = trip.BusId,
                Origin = trip.Origin,
                Destination = trip.Destination,
                DepartureUtc = trip.DepartureUtc,
                ArrivalUtc = trip.ArrivalUtc,
                Price = trip.Price,
                Status = trip.Status
            }).ToListAsync(cancellationToken),
            Bookings = await db.Bookings.AsNoTracking().Select(booking => new JsonBookingRecord
            {
                Id = booking.Id,
                UserId = booking.UserId,
                TripId = booking.TripId,
                SeatNumber = booking.SeatNumber,
                PassengerName = booking.PassengerName,
                PassengerAge = booking.PassengerAge,
                PassengerGender = booking.PassengerGender,
                PassengerMobileNumber = booking.PassengerMobileNumber,
                AdhiparasakthiCircleName = booking.AdhiparasakthiCircleName,
                BookedAtUtc = booking.BookedAtUtc,
                Status = booking.Status
            }).ToListAsync(cancellationToken),
            PasswordResetTokens = await db.PasswordResetTokens.AsNoTracking()
                .Select(token => new JsonPasswordResetTokenRecord
                {
                    Id = token.Id,
                    UserId = token.UserId,
                    TokenHash = token.TokenHash,
                    ExpiresAtUtc = token.ExpiresAtUtc,
                    UsedAtUtc = token.UsedAtUtc
                }).ToListAsync(cancellationToken)
        };

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The JSON database path must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        await using (var stream = new FileStream(
            temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}

internal sealed class JsonDatabaseDocument
{
    public int Version { get; set; } = 1;
    public List<JsonUserRecord> Users { get; set; } = [];
    public List<JsonBusRecord> Buses { get; set; } = [];
    public List<JsonTripRecord> Trips { get; set; } = [];
    public List<JsonBookingRecord> Bookings { get; set; } = [];
    public List<JsonPasswordResetTokenRecord> PasswordResetTokens { get; set; } = [];
}

internal sealed class JsonUserRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTime CreatedAtUtc { get; set; }
}

internal sealed class JsonBusRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
}

internal sealed class JsonTripRecord
{
    public int Id { get; set; }
    public int BusId { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureUtc { get; set; }
    public DateTime ArrivalUtc { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Scheduled";
}

internal sealed class JsonBookingRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TripId { get; set; }
    public int SeatNumber { get; set; }
    public string PassengerName { get; set; } = string.Empty;
    public int PassengerAge { get; set; }
    public string PassengerGender { get; set; } = string.Empty;
    public string PassengerMobileNumber { get; set; } = string.Empty;
    public string AdhiparasakthiCircleName { get; set; } = string.Empty;
    public DateTime BookedAtUtc { get; set; }
    public string Status { get; set; } = "Confirmed";
}

internal sealed class JsonPasswordResetTokenRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}
