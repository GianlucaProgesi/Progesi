using Microsoft.EntityFrameworkCore;
using Progesi.Infrastructure.EF;
using Progesi.Infrastructure.EF.Repositories;
using ProgesiCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("ProgesiDb")
    ?? throw new InvalidOperationException("Connection string 'ProgesiDb' is not configured.");

builder.Services.AddDbContext<ProgesiDbContext>(options =>
{
  var normalized = ProgesiDbContextFactory.NormalizeConnectionString(connectionString);
  options.UseSqlite(
      normalized,
      sqlite => sqlite.ExecutionStrategy(deps => new Progesi.Infrastructure.EF.Internal.SqliteBusyRetryExecutionStrategy(deps)));
});

builder.Services.AddScoped<IVariableRepository>(sp =>
    new EfVariableRepository(sp.GetRequiredService<ProgesiDbContext>(), ownsContext: false));
builder.Services.AddScoped<IMetadataRepository>(sp =>
    new EfMetadataRepository(sp.GetRequiredService<ProgesiDbContext>(), ownsContext: false));
builder.Services.AddScoped<IProgesiVariableClusterRepository>(sp =>
    new EfClusterRepository(sp.GetRequiredService<ProgesiDbContext>(), ownsContext: false));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
  var db = scope.ServiceProvider.GetRequiredService<ProgesiDbContext>();
  var resetSchema = app.Configuration.GetValue<bool>("Progesi:ResetSchemaOnStartup");
  ProgesiDbContextFactory.EnsureSchema(db, resetSchema);
}

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
