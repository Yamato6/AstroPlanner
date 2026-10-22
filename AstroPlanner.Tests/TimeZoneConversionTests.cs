using AstroPlanner.Core.Algorithms;
using FluentAssertions;

namespace AstroPlanner.Tests;

public sealed class TimeZoneConversionTests
{
    [Fact]
    public void ToUtc_MexicoCity_AppliesFixedOffsetWithoutDaylightSaving()
    {
        // México eliminó el horario de verano en 2022: CDMX es UTC-6 todo el año.
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");

        var utc = TimeZoneConversion.ToUtc(new DateTime(2026, 10, 22, 20, 0, 0), zone);

        utc.Should().Be(new DateTime(2026, 10, 23, 2, 0, 0));
        utc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ToUtc_InvalidTimeDuringSpringForward_IsShiftedOneHour()
    {
        // En Los Ángeles, el 8/3/2026 a las 02:00 el reloj salta a las 03:00:
        // las 02:30 no existen y se tratan como 03:30 PDT (UTC-7).
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        var utc = TimeZoneConversion.ToUtc(new DateTime(2026, 3, 8, 2, 30, 0), zone);

        utc.Should().Be(new DateTime(2026, 3, 8, 10, 30, 0));
    }

    [Fact]
    public void ToUtc_IgnoresKindOfInput()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");
        var wallClock = new DateTime(2026, 1, 15, 22, 0, 0);

        var fromLocalKind = TimeZoneConversion.ToUtc(DateTime.SpecifyKind(wallClock, DateTimeKind.Local), zone);
        var fromUtcKind = TimeZoneConversion.ToUtc(DateTime.SpecifyKind(wallClock, DateTimeKind.Utc), zone);

        fromLocalKind.Should().Be(new DateTime(2026, 1, 16, 4, 0, 0));
        fromUtcKind.Should().Be(fromLocalKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Zona que no existe")]
    public void FindOrLocal_UnknownId_ReturnsLocalZone(string? id)
    {
        TimeZoneConversion.FindOrLocal(id).Should().Be(TimeZoneInfo.Local);
    }

    [Fact]
    public void FindOrLocal_KnownId_ReturnsThatZone()
    {
        TimeZoneConversion.FindOrLocal("Pacific Standard Time").Id.Should().Be("Pacific Standard Time");
    }
}
