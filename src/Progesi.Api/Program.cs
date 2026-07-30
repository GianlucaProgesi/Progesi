using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Progesi.Api.Auth;
using Progesi.Infrastructure.EF;
using Progesi.Infrastructure.EF.Repositories;
using ProgesiCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
  options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT"
  });

  options.AddSecurityRequirement(new OpenApiSecurityRequirement
  {
    {
      new OpenApiSecurityScheme
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.SecurityScheme,
          Id = "Bearer"
        }
      },
      Array.Empty<string>()
    }
  });
});

var useTestAuth = builder.Configuration.GetValue<bool>("Progesi:UseTestAuthentication");
if (useTestAuth)
{
  // Integration tests register TestAuthHandler via WebApplicationFactory.ConfigureTestServices.
  builder.Services.AddAuthentication();
}
else
{
  builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}

builder.Services.AddAuthorization(options =>
{
  options.AddPolicy(AuthPolicies.Reader, policy =>
      policy.RequireRole(AuthRoles.Reader, AuthRoles.Writer));
  options.AddPolicy(AuthPolicies.Writer, policy =>
      policy.RequireRole(AuthRoles.Writer));
});

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
