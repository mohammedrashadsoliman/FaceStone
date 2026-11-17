using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using FaceStone.Web.Data;
using FaceStone.Web.Hubs;
using FaceStone.Web.Services;
// FaceStone Web Application Entry Point
if (args.Contains("--test-storage-queries"))
{
    var options = new DbContextOptionsBuilder<FaceStoneDbContext>()
        .UseSqlServer("Server=db79422.databaseasp.net; Database=db79422; User Id=db79422; Password=Cr7-8=zEDx6@; Encrypt=False; MultipleActiveResultSets=True;")
        .Options;
    using var db = new FaceStoneDbContext(options);

    Console.WriteLine("=== Testing GET /api/storage/operations query ===");
    try
    {
        var query = db.StorageOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.TransactionDate)
            .ThenByDescending(o => o.Id)
            .Take(50);
        var ops = await query.ToListAsync();
        Console.WriteLine($"SUCCESS: Retrieved {ops.Count} operations.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED /api/storage/operations: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }
    }

    Console.WriteLine("\n=== Testing GET /api/storage/compliant-references query ===");
    try
    {
        var reports = await db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" &&
                        r.ComplyStatus == "COMPLY" &&
                        r.InspectionDecision != "REJECTED" &&
                        r.AcceptedQuantity > 0)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .Take(150)
            .ToListAsync();
        Console.WriteLine($"Retrieved {reports.Count} lab reports.");

        var reportIds = reports.Select(r => r.Id).ToList();
        var allocatedGrouped = await db.WarehouseBatches
            .AsNoTracking()
            .Where(b => b.LaboratoryReportId.HasValue && reportIds.Contains(b.LaboratoryReportId.Value) && b.Status != "REJECTED")
            .GroupBy(b => b.LaboratoryReportId!.Value)
            .Select(g => new { LabId = g.Key, Allocated = g.Sum(x => x.InitialQuantity) })
            .ToDictionaryAsync(x => x.LabId, x => x.Allocated);
        Console.WriteLine($"SUCCESS: Allocated grouped count: {allocatedGrouped.Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED /api/storage/compliant-references: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }
    }
    return;
}

// 1. Force Development environment when running locally on developer workstation
if (string.Equals(Environment.MachineName, "DESKTOP-OVKNQTJ", StringComparison.OrdinalIgnoreCase) ||
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
}

// Determine the absolute project root directory containing wwwroot
var currentDir = Directory.GetCurrentDirectory();
var projectRoot = currentDir;

if (!Directory.Exists(Path.Combine(projectRoot, "wwwroot")))
{
    var baseDir = AppContext.BaseDirectory;
    var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
    if (Directory.Exists(Path.Combine(candidate, "wwwroot")))
    {
        projectRoot = candidate;
    }
}

var webRootPath = Path.Combine(projectRoot, "wwwroot");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = projectRoot,
    WebRootPath = webRootPath,
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
});

// Configure default local listening URL (prevents fallback to port 5000)
builder.WebHost.UseUrls("http://localhost:5124");

// 1. Add Database Context with SQL Server (Centralized MonsterASP.NET Database)
const string monsterAspDefaultConn = "Server=db79422.databaseasp.net; Database=db79422; User Id=db79422; Password=Cr7-8=zEDx6@; Encrypt=False; MultipleActiveResultSets=True;";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = monsterAspDefaultConn;
}

// When running locally in Development mode (Visual Studio 2022 on developer workstation),
// if the connection string targets MonsterASP's internal hostname (db79422.databaseasp.net),
// adapt it to the public endpoint (db79422.public.databaseasp.net) required for external connections.
if (builder.Environment.IsDevelopment() || string.Equals(Environment.MachineName, "DESKTOP-OVKNQTJ", StringComparison.OrdinalIgnoreCase))
{
    if (connectionString.Contains("db79422.databaseasp.net", StringComparison.OrdinalIgnoreCase) &&
        !connectionString.Contains("db79422.public.databaseasp.net", StringComparison.OrdinalIgnoreCase))
    {
        connectionString = connectionString.Replace("db79422.databaseasp.net", "db79422.public.databaseasp.net", StringComparison.OrdinalIgnoreCase);
        if (!connectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += ";TrustServerCertificate=True;";
        }
    }
}

builder.Services.AddDbContext<FaceStoneDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(60);
    });
});

// 2. Add API Controllers with JSON configurations
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// 3. Add API Explorer and Swagger / OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FaceStone REST API",
        Version = "v1",
        Description = "Official RESTful Web API for FaceStone (FACESTONE - FULL CONCRETE SOLUTIONS). Supports authentication, user management, and real-time chat with attachments.",
        Contact = new OpenApiContact
        {
            Name = "FaceStone Engineering",
            Email = "support@facestone.com"
        }
    });

    // Configure Swagger Bearer JWT Authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Please enter your JWT Bearer token into the field below.\r\n\r\nExample: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
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

// 4. Configure JWT Bearer Authentication & SignalR WebSocket Token Extraction
var jwtKey = builder.Configuration["Jwt:Key"] 
    ?? "FaceStone_Secret_Key_For_Jwt_Authentication_2026_Concrete_Solutions_SuperSecureKey!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FaceStoneApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FaceStoneClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Enable JWT authentication for SignalR WebSockets and Secure Attachments via query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"].FirstOrDefault()
                              ?? context.Request.Query["token"].FirstOrDefault();
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && 
                (path.StartsWithSegments("/chatHub", StringComparison.OrdinalIgnoreCase) || 
                 path.StartsWithSegments("/api/chat/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/control-monitoring/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/attendance/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/transportation/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/storage/attachments", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWithSegments("/api/laboratory/attachments", StringComparison.OrdinalIgnoreCase)))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TransportationAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasTransportationAccess(role);
        }));

    options.AddPolicy("StorageAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasStorageAccess(role);
        }));

    options.AddPolicy("ManagementAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasManagementAccess(role);
        }));

    options.AddPolicy("FinanceAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasFinanceAccess(role);
        }));

    options.AddPolicy("ControlMonitoringAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasControlMonitoringAccess(role);
        }));

    options.AddPolicy("AttendanceAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasAttendanceAccess(role);
        }));

    options.AddPolicy("LaboratoryAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasLaboratoryAccess(role);
        }));

    options.AddPolicy("PhysicalLabAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasPhysicalLabAccess(role);
        }));

    options.AddPolicy("ChemicalLabAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasChemicalLabAccess(role);
        }));

    options.AddPolicy("ProductionAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.HasProductionAccess(role);
        }));

    options.AddPolicy("ProductionManagerAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return FaceStone.Web.Models.AppRoles.IsProductionManager(role);
        }));
});

// 5. Add SignalR for Real-Time WebSockets
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
});

// 6. Configure CORS Policy (Localhost & MonsterASP Production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                // Allow localhost for dev (any port)
                if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Allow production MonsterASP domain
                if (uri.Host.EndsWith("runasp.net", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith("facestone-eg.com", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// 7. Dependency Injection Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IProductionShiftService, ProductionShiftService>();
builder.Services.AddScoped<IWarehouseInventoryService, WarehouseInventoryService>();

var app = builder.Build();

// 8. Configure HTTP Request Pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FaceStone API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "FaceStone API Documentation";
    });
}

// Ensure database schema is ready and seeded with initial demo accounts
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("[FaceStone] Hosting Environment: {Env}", app.Environment.EnvironmentName);
startupLogger.LogInformation("[FaceStone] ContentRootPath: {ContentRoot}", app.Environment.ContentRootPath);
startupLogger.LogInformation("[FaceStone] WebRootPath: {WebRoot} (Exists: {Exists})", 
    app.Environment.WebRootPath, 
    Directory.Exists(app.Environment.WebRootPath));
await DbInitializer.InitializeAsync(app.Services, startupLogger);

// Ensure chat uploads directory exists on host
var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "uploads", "chat");
if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

app.UseCors("AllowAll");

var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";

// Serve default files (index.html) and static uploads/assets
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, max-age=0";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "-1";
        ctx.Context.Response.Headers.Remove("ETag");
        ctx.Context.Response.Headers.Remove("Last-Modified");
    }
});

// Timing and performance monitoring middleware for API and SignalR endpoints
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/chatHub", StringComparison.OrdinalIgnoreCase))
    {
        var method = context.Request.Method;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("FaceStone.ApiTiming");
        
        logger.LogInformation("[API] {Method} {Path} START", method, path);
        
        await next();
        
        sw.Stop();
        logger.LogInformation("[API] {Method} {Path} END duration={Duration}ms status={StatusCode}",
            method, path, sw.ElapsedMilliseconds, context.Response.StatusCode);
    }
    else
    {
        await next();
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoint for deployment verification
app.MapGet("/health", (IWebHostEnvironment env) => Results.Ok(new
{
    status = "Healthy",
    service = "FaceStone Real-Time Messaging API",
    environment = env.EnvironmentName,
    timestamp = DateTime.UtcNow
}));

// Route /docs to Swagger UI documentation
app.MapGet("/docs", () => Results.Redirect("/swagger"));

// Map SignalR Real-Time Hub
app.MapHub<ChatHub>("/chatHub");

// Map RESTful API Controllers
app.MapControllers();

// SPA Fallback for client-side routing (/departments, /finance, /transportation, /storage)
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, max-age=0";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "-1";
        ctx.Context.Response.Headers.Remove("ETag");
        ctx.Context.Response.Headers.Remove("Last-Modified");
    }
});

// Automatically open the default browser when running locally
app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = "http://localhost:5124";
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("==================================================================");
    logger.LogInformation("[FaceStone] Web Application successfully started at: {Url}", url);
    var dbHost = "db79422.databaseasp.net";
    try { dbHost = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).DataSource; } catch { }
    logger.LogInformation("[FaceStone] Centralized Database: MonsterASP.NET SQL Server ({Host} -> db79422)", dbHost);
    logger.LogInformation("[FaceStone] Hosting Environment: {Env}", app.Environment.EnvironmentName);
    logger.LogInformation("==================================================================");

    if (!args.Contains("--no-launch-browser") && Environment.GetEnvironmentVariable("NO_LAUNCH_BROWSER") != "1" &&
        (app.Environment.IsDevelopment() || string.Equals(Environment.MachineName, "DESKTOP-OVKNQTJ", StringComparison.OrdinalIgnoreCase)))
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            logger.LogInformation("[FaceStone] Browser launched automatically to {Url}", url);
        }
        catch (Exception ex)
        {
            logger.LogWarning("[FaceStone] Auto-launch browser notice: {Message}. Please open {Url} manually.", ex.Message, url);
        }
    }
});

app.Run();
// FaceStone Portal - Storage & Finance ready.
