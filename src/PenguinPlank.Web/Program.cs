using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PenguinPlank.Web;
using PenguinPlank.Web.Api;
using PenguinPlank.Web.Shell;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddPenguinPlankApiClient(new Uri(builder.HostEnvironment.BaseAddress));

builder.Services.AddPenguinPlankShell();

await builder.Build().RunAsync();
