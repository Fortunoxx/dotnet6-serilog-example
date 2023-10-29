using System.Text;
using Serilog;
using Serilog.Events;
// using Microsoft.AspNetCore.Authentication.JwtBearer;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console()
        .ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddAuthentication().AddJwtBearer();
    builder.Services.AddAuthorization();

    var app = builder.Build();
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseSerilogRequestLogging(options =>
    {
        // Customize the message template
        options.MessageTemplate = "HTTP {RequestScheme}://{RequestHost}{RequestPath}?{QueryString} - {uid} - {@RequestBody} - {@ResponseBody}";

        // // Emit debug-level events instead of the defaults
        // options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Debug;

        // Attach additional properties to the request completion event
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("QueryString", httpContext.Request.QueryString);
            // diagnosticContext.Set("RequestBody", await new StreamReader(httpContext.Request.Body).ReadToEndAsync());
            diagnosticContext.Set("CorrelationId", httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId) ? correlationId : Guid.NewGuid());
            // diagnosticContext.Set("ResponseBody", await new StreamReader(httpContext.Response.Body).ReadToEndAsync());

            // // getting the body seems to be quite difficult... also need to obfuscate private data and eliminate long binary data (e.g. documents)
            // Reset the request body stream position to the start so we can read it
            // httpContext.Request.Body.Position = 0;
            // // Leave the body open so the next middleware can read it.
            // var reader = new StreamReader(
            //     httpContext.Request.Body,
            //     encoding: Encoding.UTF8,
            //     detectEncodingFromByteOrderMarks: false);
            // var body = reader.ReadToEndAsync().GetAwaiter().GetResult();

            // if (!string.IsNullOrEmpty(body))
            //     diagnosticContext.Set("RequestBody", body);

            // httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            // var reader = new StreamReader(
            //     httpContext.Response.Body,
            //     encoding: Encoding.UTF8,
            //     detectEncodingFromByteOrderMarks: false);
            // var body = reader.ReadToEndAsync().GetAwaiter().GetResult();
            // if (!string.IsNullOrEmpty(body))
            //     diagnosticContext.Set("ResponseBody", body);

            var uid = httpContext.User?.FindFirst("hcv_uid");
            diagnosticContext.Set("uid", uid?.Value ?? "abc");
        };
    });

    app.MapGet("/", () => "Hello World!").RequireAuthorization();
    app.MapGet("/oops", new Func<string>(() => throw new InvalidOperationException("Oops!")));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}
