using FlightDelayBlazor.Components;
using FlightDelayBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HTTP client for API calls
builder.Services.AddHttpClient<IFlightDelayApiService,FlightDelayApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5107"); // Your API server
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register the API service

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
