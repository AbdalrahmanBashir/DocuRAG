using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using System.Reflection;
using System.Threading.RateLimiting;
using Api.HealthChecks;
using Api.Swagger;
using Application.contracts;
using Application.Service;
using Domain.Models;
using Infrastructure.Services;
using Infrastructure.Persistence;
using Application.Features.Documents.ProcessDocument;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Response Compression for HTTPS
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Add Memory Cache
builder.Services.AddMemoryCache();

// Add Rate Limiting using the built-in rate limiter middleware in .NET 8
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add OpenTelemetry tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProvider =>
    {
        tracerProvider
            .AddSource("Api")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("Api"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });



builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProcessDocumentCommandHandler).Assembly));


// Add Health Checks with a custom Ollama check
builder.Services.AddHealthChecks()
    .AddCheck<OllamaHealthCheck>("ollama_health");

// Add CORS policy (allow any origin, header, and method)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RAG PDF API",
        Version = "v1",
        Description = "API for processing and searching PDF documents"
    });

    // Include XML comments if available.
    try
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not include XML comments: {ex.Message}");
    }

    // Configure file upload operations in Swagger.
    options.OperationFilter<SwaggerFileOperationFilter>();
});

// Configure Ollama settings from configuration.
builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection("OllamaSettings"));


// Configure HttpClient with Polly policies for IOllamaClient.
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

// Register additional HttpClient for other usages.
builder.Services.AddHttpClient();

// Register MediatR services.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ProcessDocumentCommandHandler).Assembly);
});




// Register application services with proper lifetimes.
builder.Services.AddSingleton<IDocumentRepository>(sp => 
    new FileBackedDocumentRepository(
        sp.GetRequiredService<IConfiguration>(),
        builder.Environment.ContentRootPath));
builder.Services.AddScoped<OllamaEmbeddingService>();
builder.Services.AddScoped<IEmbeddingService>(sp => 
    new CachedEmbeddingService(
        sp.GetRequiredService<OllamaEmbeddingService>(),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<IConfiguration>(),
        builder.Environment.ContentRootPath));
builder.Services.AddScoped<IPdfProcessingService, PdfProcessingService>();
builder.Services.AddScoped<ParallelDocumentProcessor>();
builder.Services.AddHostedService<DocumentCleanupService>();

builder.Services.AddSingleton<TextChunkingService>();


var app = builder.Build();

// Ensure required storage directories exist.
var config = app.Services.GetRequiredService<IConfiguration>();
var contentRoot = app.Environment.ContentRootPath;
var storagePaths = new[]
{
    Path.Combine(contentRoot, config.GetValue<string>("Storage:CachePath") ?? "cache"),
    Path.Combine(contentRoot, config.GetValue<string>("Storage:DataPath") ?? "data")
};

foreach (var path in storagePaths.Where(p => !string.IsNullOrEmpty(p)))
{
    try
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            app.Logger.LogInformation("Created directory: {Path}", path);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error creating directory: {Path}", path);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
        {
            swaggerDoc.Servers = new List<OpenApiServer>
            {
                new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" }
            };
        });
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "RAG PDF API v1");
        c.RoutePrefix = "swagger";
    });
}

// Enable Response Compression.
app.UseResponseCompression();

// Enable Rate Limiting.
app.UseRateLimiter();

// Map Health Checks.
app.MapHealthChecks("/health");

// Enable CORS.
app.UseCors();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
