using AstroPlanner.Data.Repositories;
using FluentAssertions;

namespace AstroPlanner.Tests;

public sealed class CityRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsMexicanCitiesWithValidCoordinates()
    {
        var repository = new JsonCityRepository();

        var cities = await repository.GetAllAsync();

        cities.Should().HaveCount(11);
        cities.Should().OnlyContain(city => city.Country == "México");
        cities.Should().OnlyContain(city => city.LatitudeDegrees >= -90 && city.LatitudeDegrees <= 90);
        cities.Should().OnlyContain(city => city.LongitudeDegrees >= -180 && city.LongitudeDegrees <= 180);
        cities.Should().OnlyContain(city => city.ElevationMeters >= 0);
        cities.Should().OnlyContain(city => !string.IsNullOrWhiteSpace(city.TimeZoneId));
    }

    [Fact]
    public async Task GetAllAsync_ContainsMexicoCityAndCancunTimeZones()
    {
        var repository = new JsonCityRepository();

        var cities = await repository.GetAllAsync();

        cities.Single(city => city.Id == "mexico-city").TimeZoneId
            .Should().Be("Central Standard Time (Mexico)");
        cities.Single(city => city.Id == "cancun").TimeZoneId
            .Should().Be("Eastern Standard Time (Mexico)");
    }

    [Fact]
    public async Task GetAllAsync_MissingFile_ThrowsIOException()
    {
        var repository = new JsonCityRepository("no existe/cities.json");

        await Assert.ThrowsAnyAsync<IOException>(() => repository.GetAllAsync());
    }
}
