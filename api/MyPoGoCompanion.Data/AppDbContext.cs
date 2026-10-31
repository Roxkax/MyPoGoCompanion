using Microsoft.EntityFrameworkCore;

namespace MyPoGoCompanion.Data;

/// <summary>
/// The app's PostgreSQL database. The schema is built only by the migrations in <c>Migrations/</c>
/// (applied by the deploy pipelines, and once per run by the integration tests).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
