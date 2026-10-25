using Obol.Website;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddObolWebsite(builder.Configuration);

var app = builder.Build();
app.UseObolWebsite();
await app.RunAsync();
