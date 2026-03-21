using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SmartNest.Client;
using SmartNest.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddSingleton<AuthenticationService>();
builder.Services.AddScoped<SensorService>();
builder.Services.AddScoped<ChickService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<VideoService>();

var host = builder.Build();

var auth = host.Services.GetRequiredService<AuthenticationService>();
await auth.InitializeAsync(
    host.Services.GetRequiredService<ILocalStorageService>(),
    host.Services.GetRequiredService<HttpClient>()
);

await host.RunAsync();
