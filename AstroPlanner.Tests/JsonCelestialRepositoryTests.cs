using AstroPlanner.Data.Repositories;
using FluentAssertions;
using Xunit;

namespace AstroPlanner.Tests;

public class JsonCelestialRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsAll110MessierObjects()
    {
        var repo = new JsonCelestialRepository();

        var objetos = await repo.GetAllAsync();

        objetos.Should().HaveCount(110);
        objetos.Select(o => o.Id).Should().OnlyHaveUniqueItems();
        objetos.Should().Contain(o => o.Id == "M1");
        objetos.Should().Contain(o => o.Id == "M110");
    }

    [Fact]
    public async Task GetAllAsync_ParsesTypeAndConstellation()
    {
        var repo = new JsonCelestialRepository();

        var objetos = await repo.GetAllAsync();

        var m42 = objetos.Single(o => o.Id == "M42");
        m42.Constellation.Should().Be("Orion");
        m42.Type.Should().Be(Core.Models.CelestialObjectType.DiffuseNebula);
        m42.Coordinates.RightAscensionHours.Should().BeApproximately(5.59, 1e-6);

        // M40 y M73 son casos raros catalogados como Unknown.
        objetos.Single(o => o.Id == "M40").Type.Should().Be(Core.Models.CelestialObjectType.Unknown);
        objetos.Single(o => o.Id == "M73").Type.Should().Be(Core.Models.CelestialObjectType.Unknown);
    }

    [Fact]
    public async Task GetAllAsync_CoordinatesAreValid()
    {
        var repo = new JsonCelestialRepository();

        var objetos = await repo.GetAllAsync();

        foreach (var o in objetos)
        {
            o.Coordinates.RightAscensionHours.Should().BeInRange(0, 24);
            o.Coordinates.DeclinationDegrees.Should().BeInRange(-90, 90);
        }
    }

    [Fact]
    public async Task GetAllAsync_MissingFile_Throws()
    {
        var repo = new JsonCelestialRepository("no/existe/messier_objects.json");

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => repo.GetAllAsync());
        ex.Should().BeAssignableTo<IOException>();
    }
}
