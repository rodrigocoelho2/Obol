using Obol.WebApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddObol(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseObol();
await app.RunAsync();
