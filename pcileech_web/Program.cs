using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("hosting.json", optional: true);
// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

Directory.CreateDirectory("mem");

void LoadSettings()
{
    
}
app.Configuration.GetReloadToken().RegisterChangeCallback(o => { LoadSettings(); }, null);
LoadSettings();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles(new StaticFileOptions() {  
    FileProvider=new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mem")),
    RequestPath="/mem",
    ServeUnknownFileTypes = true 
});

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.MapControllers();

app.Run();