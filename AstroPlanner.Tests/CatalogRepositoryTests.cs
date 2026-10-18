using AstroPlanner.Data.Repositories;
using FluentAssertions;

namespace AstroPlanner.Tests;

public sealed class CatalogRepositoryTests
{
    [Fact]
    public async Task SeedCatalog_Contains110Objects()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "AstroPlanner.Data", "SeedData", "messier_objects.json"));
        var repository = new JsonCelestialRepository(path);

        var objects = await repository.GetAllAsync();

        objects.Should().HaveCount(110);
        objects.Select(item => item.Id).Should().OnlyHaveUniqueItems();
    }
}
