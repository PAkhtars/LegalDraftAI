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
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpClient("DraftsApi", client => client.BaseAddress = draftsApiUri);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
