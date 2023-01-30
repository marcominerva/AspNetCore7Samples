using System.Diagnostics;
using System.Net.Mime;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.WebUtilities;

var builder = WebApplication.CreateBuilder(args);

// Enable HTTP/3 support.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(settings =>
    {
        settings.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter("Default", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromSeconds(10),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window))
        {
            context.HttpContext.Response.Headers.RetryAfter = window.TotalSeconds.ToString();
        }

        return ValueTask.CompletedTask;
    };
});

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("People", builder =>
    {
        builder.Tag("People").Cache();
    });
});

// Enable Problem Details for Exceptions.
builder.Services.AddProblemDetails();

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    // Error handling
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        AllowStatusCode404Response = true,
        ExceptionHandler = async (HttpContext context) =>
        {
            var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
            var error = exceptionHandlerFeature?.Error;

            if (context.RequestServices.GetService<IProblemDetailsService>() is { } problemDetailsService)
            {
                // Write as JSON problem details
                await problemDetailsService.WriteAsync(new()
                {
                    HttpContext = context,
                    AdditionalMetadata = exceptionHandlerFeature?.Endpoint?.Metadata,
                    ProblemDetails =
                    {
                        Status = context.Response.StatusCode,
                        Title = error?.GetType().FullName ?? "an error occurred while processing your request",
                        Detail = error?.Message
                    }
                });
            }
            else
            {
                context.Response.ContentType = MediaTypeNames.Text.Plain;
                var message = ReasonPhrases.GetReasonPhrase(context.Response.StatusCode) switch
                {
                    { Length: > 0 } reasonPhrase => reasonPhrase,
                    _ => "An error occurred"
                };
                await context.Response.WriteAsync(message + "\r\n");
                await context.Response.WriteAsync($"Request ID: {Activity.Current?.Id ?? context.TraceIdentifier}");
            }
        }
    });
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseRateLimiter();
app.UseOutputCache();

app.UseAuthorization();

app.MapControllers();

app.Run();
