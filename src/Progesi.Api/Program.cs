using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Progesi.Api.Auth;
using Progesi.Api.Infrastructure;
using Progesi.Api.Projects;
using Progesi.Infrastructure.EF;
using Progesi.Infrastructure.EF.Repositories;
using ProgesiCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
  options.Filters.Add<ProjectNotFoundExceptionFilter>();
});
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

var useTestAuth = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Progesi:UseTestAuthentication");
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IProjectRegistry, JsonFileProjectRegistry>();
builder.Services.AddScoped<IProjectProvisioningService, ProjectProvisioningService>();
builder.Services.AddScoped<IProjectContext, ProjectContext>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IProjectContext>().DbContext);

builder.Services.AddScoped<IVariableRepository>(sp =>
    new EfVariableRepository(sp.GetRequiredService<ProgesiDbContext>(), ownsContext: false));
builder.Services.AddScoped<IMetadataRepository>(sp =>
    new EfMetadataRepository(sp.GetRequiredService<ProgesiDbContext>(), ownsContext: false));
builder.Services.AddScoped<IProgesiVariableClusterRepository>(sp =>
    new EfClusterRepository(sp.GetRequiredService<ProgesiDbContext>(), ownsContext: false));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
  var registry = scope.ServiceProvider.GetRequiredService<IProjectRegistry>();
  registry.EnsureDefaultProject();

  if (app.Configuration.GetValue<bool>("Progesi:ResetSchemaOnStartup"))
  {
    var defaultProjectId = app.Configuration["Progesi:DefaultProjectId"] ?? "default";
    var defaultEntry = registry.GetById(defaultProjectId)
        ?? throw new InvalidOperationException($"Default project '{defaultProjectId}' is not registered.");

    var options = ProgesiDbContextOptionsBuilder.Build(defaultEntry.ConnectionString, app.Configuration);
    using var db = new ProgesiDbContext(options);
    ProgesiDbContextFactory.EnsureSchema(db, resetSchema: true);
  }
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
