var builder = WebApplication.CreateBuilder(args);

var draftsApiBaseUrl = builder.Configuration["DraftsApi:BaseUrl"];
if (!Uri.TryCreate(draftsApiBaseUrl, UriKind.Absolute, out var draftsApiUri))
{
    throw new InvalidOperationException(
        "Configure DraftsApi:BaseUrl with an absolute URL to the LegalDraftAI API.");
}

builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(3);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpClient("DraftsApi", client =>
{
    client.BaseAddress = draftsApiUri;
    client.Timeout = TimeSpan.FromMinutes(4);
});
builder.Services.AddHttpClient("EmployeeAuth", client =>
{
    client.BaseAddress = new Uri("https://employeesapi.runasp.net/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseSession();
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isPublicPage = path == "/" ||
        path.Equals("/Index", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/Error", StringComparison.OrdinalIgnoreCase);

    if (!isPublicPage)
    {
        await context.Session.LoadAsync(context.RequestAborted);
        if (string.IsNullOrWhiteSpace(context.Session.GetString("Account.DisplayName")))
        {
            context.Response.Redirect("/");
            return;
        }
    }

    await next();
});
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
