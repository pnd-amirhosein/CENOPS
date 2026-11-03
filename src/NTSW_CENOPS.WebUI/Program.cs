using NTSW_CENOPS.Authorization;
using NTSW_CENOPS.Chat;
using NTSW_CENOPS.Application.Chat;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using NTSW_CENOPS.Application;
using NTSW_CENOPS.Components;
using NTSW_CENOPS.Endpoints;
using NTSW_CENOPS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<NTSW_CENOPS.Components.Chat.ChatWorkspaceState>();
builder.Services.AddScoped<NTSW_CENOPS.Components.Chat.ChatInboxState>();
builder.Services.AddScoped<NTSW_CENOPS.Components.Common.Notices.AppNoticeService>();
builder.Services.AddScoped<NTSW_CENOPS.Components.Common.Loading.AppBusyService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// Register after AddAuthorization so dynamic access policies take precedence.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, AccessPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, AccessAuthorizationHandler>();

// Keep chat encryption keys stable between restarts/deployments. Override with Chat:KeyRingPath
// and restrict access to that folder at the OS level; back it up separately from SQL Server.
var keyPath = builder.Configuration["Chat:KeyRingPath"];
if (string.IsNullOrWhiteSpace(keyPath))
    keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NTSW_CENOPS", "chat-keys");
builder.Services.AddDataProtection().SetApplicationName("NTSW_CENOPS")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 32 * 1024);
builder.Services.AddScoped<IChatEventPublisher, SignalRChatEventPublisher>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAuthEndpoints();
app.MapFileEndpoints();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
