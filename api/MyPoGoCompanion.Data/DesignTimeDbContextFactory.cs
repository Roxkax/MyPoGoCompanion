using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyPoGoCompanion.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tool. Adding a migration doesn't connect; <c>dotnet ef database update</c>
/// uses <c>ConnectionStrings__Database</c>, or the local Docker Compose database without it.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Called by the dotnet-ef tool only.")]
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDatabase = "Host=localhost;Database=mypogocompanion;Username=mypogocompanion;Password=localdev";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? LocalDatabase;
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
    }
}
