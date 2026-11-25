using Microsoft.EntityFrameworkCore;
using DotNetCoreSqlDb.Data;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
var builder = WebApplication.CreateBuilder(args);

// Add database context
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<MyDatabaseContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("MyDbConnection")));
}
else
{
    builder.Services.AddDbContext<MyDatabaseContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING")));
}

// Add services to the container.
builder.Services.AddControllersWithViews();

// Service Bus: one client and one sender for the whole app (thread-safe, reuses the connection)
var serviceBusNamespace = builder.Configuration["ServiceBus:FullyQualifiedNamespace"]
    ?? throw new InvalidOperationException("ServiceBus:FullyQualifiedNamespace is not configured.");
var todoQueueName = builder.Configuration["ServiceBus:QueueName"] ?? "todo-items";

// Shorter retries than the SDK defaults (3 retries x 60s per attempt), so a Service Bus outage
// can't hang a web request for minutes: worst case is about 30 seconds.
builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential(), new ServiceBusClientOptions
{
    RetryOptions = new ServiceBusRetryOptions
    {
        MaxRetries = 2,
        TryTimeout = TimeSpan.FromSeconds(10)
    }
}));
builder.Services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(todoQueueName));

// Add App Service logging
builder.Logging.AddAzureWebAppDiagnostics();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Todos}/{action=Index}/{id?}");

app.Run();
