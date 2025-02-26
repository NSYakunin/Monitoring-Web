using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
//using Monitoring.Infrastructure.Services.Fake;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});

builder.Services.AddMemoryCache();

 builder.Services.AddScoped<IWorkItemService, WorkItemService>();
//builder.Services.AddScoped<IWorkItemService, FakeWorkItemService>();
 builder.Services.AddScoped<ILoginService, LoginService>();
//builder.Services.AddScoped<ILoginService, FakeLoginService>();

builder.Services.AddScoped<IUserSettingsService, UserSettingsService>(); 

builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<IWorkRequestService, WorkRequestService>();

//автоматически загружается при вызове 
//builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Если не в Dev-режиме, включаем обработку ошибок
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// для https
//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

app.Run();