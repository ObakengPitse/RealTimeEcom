using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton(sp =>
{
    var cs = builder.Configuration["EventHubs:Orders:ConnectionString"];
    var name = builder.Configuration["EventHubs:Orders:Name"];
    return new EventHubProducerClient(cs, name);
});
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
